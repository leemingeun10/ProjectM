using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Sim.Technique;
using UnityEngine;

namespace Game.Client.Technique
{
    public enum TechniqueSlotState
    {
        Empty,
        Incomplete,
        Complete,
        Unreadable,
    }

    public readonly struct TechniqueSlotStatus
    {
        public readonly TechniqueSlotState State;
        public readonly int TurnCount;

        public TechniqueSlotStatus(TechniqueSlotState state, int turnCount)
        {
            State = state;
            TurnCount = turnCount;
        }

        public string Describe()
        {
            switch (State)
            {
                case TechniqueSlotState.Empty: return "비어있음";
                case TechniqueSlotState.Incomplete: return $"미완성 {TurnCount}/{TechniqueBuilder.MaxTurns}";
                case TechniqueSlotState.Complete: return "완성";
                default: return "손상됨";
            }
        }
    }

    // File I/O for the player's technique slots (technique_slot_1.json ~ technique_slot_5.json).
    // Game.sim owns the schema (TechniqueJsonSerializer); this side only decides where each slot
    // lives. Slot numbers are 1-based to match the UI.
    public static class TechniqueSlotStore
    {
        public const int SlotCount = 5;

        // Pre-slot single save. Moved into slot 1 once, then renamed so it can never be
        // migrated again (e.g. after the player deletes slot 1) but is still kept as a backup.
        private const string LegacyFileName = "technique_A.json";
        private const string LegacyBackupSuffix = ".migrated";

        private static bool migrationChecked;

        public static string GetFilePath(int slot)
            => Path.Combine(Application.persistentDataPath, $"technique_slot_{slot}.json");

        public static bool IsValidSlot(int slot) => slot >= 1 && slot <= SlotCount;

        // Reads the file to count turns; a missing file is Empty, a bad one Unreadable.
        public static TechniqueSlotStatus GetStatus(int slot)
        {
            EnsureMigrated();
            if (!File.Exists(GetFilePath(slot)))
                return new TechniqueSlotStatus(TechniqueSlotState.Empty, 0);

            if (!TryLoad(slot, out List<TechniqueStep> steps))
                return new TechniqueSlotStatus(TechniqueSlotState.Unreadable, 0);

            TechniqueSlotState state = steps.Count >= TechniqueBuilder.MaxTurns
                ? TechniqueSlotState.Complete
                : TechniqueSlotState.Incomplete;
            return new TechniqueSlotStatus(state, steps.Count);
        }

        // Returns false (with a log) when the slot is missing or unreadable.
        public static bool TryLoad(int slot, out List<TechniqueStep> steps)
        {
            EnsureMigrated();
            steps = null;
            string path = GetFilePath(slot);
            if (!File.Exists(path))
                return false;

            try
            {
                (_, steps) = TechniqueJsonSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8));
                return true;
            }
            catch (TechniqueJsonException e)
            {
                Debug.LogError($"[TechniqueSlot] 슬롯 {slot} 불러오기 실패 ({path}): {e.Message}");
                return false;
            }
        }

        public static void Save(int slot, TechniqueBuilder builder)
        {
            EnsureMigrated();
            string path = GetFilePath(slot);
            File.WriteAllText(path, TechniqueJsonSerializer.Serialize(builder), new UTF8Encoding(false));
            Debug.Log($"[TechniqueSlot] 슬롯 {slot}: {builder.TurnCount}개 스텝 저장 완료: {path}");
        }

        public static void Delete(int slot)
        {
            EnsureMigrated();
            string path = GetFilePath(slot);
            if (!File.Exists(path))
                return;

            File.Delete(path);
            Debug.Log($"[TechniqueSlot] 슬롯 {slot} 삭제됨: {path}");
        }

        private static void EnsureMigrated()
        {
            if (migrationChecked)
                return;
            migrationChecked = true;

            string legacyPath = Path.Combine(Application.persistentDataPath, LegacyFileName);
            if (!File.Exists(legacyPath))
                return;

            string slot1Path = GetFilePath(1);
            string backupPath = legacyPath + LegacyBackupSuffix;
            try
            {
                if (File.Exists(slot1Path))
                    Debug.LogWarning($"[TechniqueSlot] 슬롯 1이 이미 있어 {LegacyFileName}을 옮기지 않고 백업만 합니다: {backupPath}");
                else
                    File.Copy(legacyPath, slot1Path);

                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                File.Move(legacyPath, backupPath);
                Debug.Log($"[TechniqueSlot] {LegacyFileName} → 슬롯 1 마이그레이션 완료 (원본 백업: {backupPath})");
            }
            catch (IOException e)
            {
                // Left in place; the next launch retries.
                Debug.LogError($"[TechniqueSlot] {LegacyFileName} 마이그레이션 실패: {e.Message}");
            }
        }
    }
}
