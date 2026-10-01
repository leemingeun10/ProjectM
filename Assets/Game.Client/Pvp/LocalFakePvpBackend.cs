using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Sim.Ai;
using Game.Sim.Battle;
using Game.Sim.Pvp;
using Game.Sim.Technique;
using UnityEngine;

namespace Game.Client.Pvp
{
    // Stand-in for a PvP server: "other players" are a fixed pool of seeded AI techniques and the
    // record lives in a local file. Game.sim owns the record rules and schema (PvpRules,
    // PvpJsonSerializer); this side only picks opponents and decides where the file lives.
    public class LocalFakePvpBackend : IPvpBackend
    {
        private const string FileName = "pvp_record.json";

        // Content constants like StageCatalog's seeds (and distinct from them): changing one
        // changes that fake opponent for every player. Difficulty is spread 3/4/3.
        private static readonly (AiDifficulty difficulty, ulong seed)[] PoolDefinitions =
        {
            (AiDifficulty.Easy, 0x3C6EF372FE94F82BUL),
            (AiDifficulty.Easy, 0xA54FF53A5F1D36F1UL),
            (AiDifficulty.Easy, 0x510E527FADE682D1UL),
            (AiDifficulty.Normal, 0x9B05688C2B3E6C1FUL),
            (AiDifficulty.Normal, 0x1F83D9ABFB41BD6BUL),
            (AiDifficulty.Normal, 0x5BE0CD19137E2179UL),
            (AiDifficulty.Normal, 0xCBBB9D5DC1059ED8UL),
            (AiDifficulty.Hard, 0x629A292A367CD507UL),
            (AiDifficulty.Hard, 0x9159015A3070DD17UL),
            (AiDifficulty.Hard, 0x152FECD8F70E5939UL),
        };

        private readonly IReadOnlyList<TechniqueStep>[] pool;
        private int lastPickedIndex = -1;

        public LocalFakePvpBackend()
        {
            pool = new IReadOnlyList<TechniqueStep>[PoolDefinitions.Length];
            for (int i = 0; i < PoolDefinitions.Length; i++)
                pool[i] = AiTechniqueGenerator.Generate(PoolDefinitions[i].difficulty, PoolDefinitions[i].seed).AsReadOnly();
        }

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        // Never the same opponent twice in a row: draw from the other N-1 entries and skip over
        // the last pick. Non-deterministic on purpose; matchmaking is outside the simulation.
        public IReadOnlyList<TechniqueStep> FetchRandomOpponent()
        {
            int index = Random.Range(0, lastPickedIndex < 0 ? pool.Length : pool.Length - 1);
            if (lastPickedIndex >= 0 && index >= lastPickedIndex)
                index++;
            lastPickedIndex = index;

            (AiDifficulty difficulty, ulong seed) = PoolDefinitions[index];
            Debug.Log($"[Pvp] 가짜 상대 #{index + 1} ({difficulty}, seed {seed:X16})");
            return pool[index];
        }

        public void ReportResult(BattleOutcome outcome)
        {
            PvpRecord current = GetRecord();
            PvpRecord updated;
            switch (outcome)
            {
                case BattleOutcome.WinA: updated = PvpRules.RecordWin(current); break;
                case BattleOutcome.WinB: updated = PvpRules.RecordLoss(current); break;
                default: updated = PvpRules.RecordDraw(current); break;
            }

            File.WriteAllText(FilePath, PvpJsonSerializer.Serialize(updated), new UTF8Encoding(false));
            Debug.Log($"[Pvp] 전적 저장 ({outcome}): {updated.Wins}승 {updated.Losses}패 {updated.Draws}무 → {FilePath}");
        }

        // Missing file = no games yet. A corrupt/unsupported file is reported and treated as fresh,
        // same recovery policy as ProgressStore; the next ReportResult overwrites it.
        public PvpRecord GetRecord()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return default;

            try
            {
                (_, PvpRecord record) = PvpJsonSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8));
                return record;
            }
            catch (PvpJsonException e)
            {
                Debug.LogError($"[Pvp] 전적 파일을 읽을 수 없어 0승 0패 0무로 시작합니다 ({path}): {e.Message}");
                return default;
            }
        }
    }
}
