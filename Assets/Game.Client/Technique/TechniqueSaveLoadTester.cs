using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Client.Battle;
using Game.Sim.Battle;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Technique
{
    // Temporary debug component: verifies TechniqueJsonSerializer save/load and a BattleSimulator
    // A-vs-B run end to end before the real 5-slot UI exists. Safe to delete once that UI replaces it.
    public class TechniqueSaveLoadTester : MonoBehaviour
    {
        private const string FileNameA = "technique_A.json";
        private const string FileNameB = "technique_B.json";

        [SerializeField] private TechniqueSequenceController sequenceController;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Toggle slotAToggle;
        [SerializeField] private Toggle slotBToggle;
        [SerializeField] private Button battleButton;
        [SerializeField] private BattleReplayController replayController;

        private static string FilePathA => Path.Combine(Application.persistentDataPath, FileNameA);
        private static string FilePathB => Path.Combine(Application.persistentDataPath, FileNameB);

        private string SelectedSlotName => slotBToggle.isOn ? "B" : "A";
        private string SelectedFilePath => slotBToggle.isOn ? FilePathB : FilePathA;

        private void OnEnable()
        {
            saveButton.onClick.AddListener(Save);
            loadButton.onClick.AddListener(Load);
            battleButton.onClick.AddListener(Battle);
        }

        private void OnDisable()
        {
            saveButton.onClick.RemoveListener(Save);
            loadButton.onClick.RemoveListener(Load);
            battleButton.onClick.RemoveListener(Battle);
        }

        private void Save()
        {
            TechniqueBuilder builder = sequenceController.Builder;
            string json = TechniqueJsonSerializer.Serialize(builder);
            File.WriteAllText(SelectedFilePath, json, new UTF8Encoding(false));
            Debug.Log($"[TechniqueSave] 슬롯 {SelectedSlotName}: {builder.TurnCount}개 스텝 저장 완료: {SelectedFilePath}");

            // Start the next slot's sequence from a clean slate.
            sequenceController.ResetSequence();
        }

        private void Load()
        {
            string path = SelectedFilePath;
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[TechniqueSave] 슬롯 {SelectedSlotName} 저장 파일이 없습니다: {path}");
                return;
            }

            if (!TryReadSteps(path, out int version, out List<TechniqueStep> steps))
                return;

            var sb = new StringBuilder();
            sb.AppendLine($"[TechniqueSave] 슬롯 {SelectedSlotName} 불러오기 완료 (version {version}, 스텝 {steps.Count}개): {path}");
            foreach (TechniqueStep step in steps)
                sb.AppendLine($"턴 {step.TurnIndex + 1}: Stance={step.Stance} Counter={step.IsCounter} {FormatStroke(step.Stroke)} Points={step.RawPoints.Count}");

            Debug.Log(sb.ToString());
        }

        private void Battle()
        {
            if (!File.Exists(FilePathA) || !File.Exists(FilePathB))
            {
                Debug.LogWarning("[Battle] 먼저 A/B를 각각 그려서 저장하세요.");
                return;
            }

            if (!TryReadSteps(FilePathA, out _, out List<TechniqueStep> stepsA)
                || !TryReadSteps(FilePathB, out _, out List<TechniqueStep> stepsB))
                return;

            // BattleSimulator requires full sequences; report it here instead of letting it throw.
            if (stepsA.Count != BattleSimulator.TurnsPerBattle || stepsB.Count != BattleSimulator.TurnsPerBattle)
            {
                Debug.LogWarning($"[Battle] A/B 모두 {BattleSimulator.TurnsPerBattle}턴을 채워서 저장해야 합니다. (A: {stepsA.Count}턴, B: {stepsB.Count}턴)");
                return;
            }

            BattleResult result = BattleSimulator.Simulate(stepsA, stepsB);

            var sb = new StringBuilder();
            sb.AppendLine("[Battle] 대전 결과");
            foreach (TurnEvent e in result.Events)
                sb.AppendLine($"턴 {e.TurnIndex + 1}: A({e.TypeA}) vs B({e.TypeB}) - 판정:{e.Reason} - A데미지 {e.DamageToA} B데미지 {e.DamageToB} - HP A:{e.HpAAfter} B:{e.HpBAfter}");
            sb.AppendLine($"최종 결과: {result.Outcome} - 최종 HP A:{result.FinalHpA} B:{result.FinalHpB}");

            Debug.Log(sb.ToString());

            replayController.StartReplay(result);
        }

        private static bool TryReadSteps(string path, out int version, out List<TechniqueStep> steps)
        {
            try
            {
                (version, steps) = TechniqueJsonSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8));
                return true;
            }
            catch (TechniqueJsonException e)
            {
                Debug.LogError($"[TechniqueSave] 불러오기 실패 ({path}): {e.Message}");
                version = 0;
                steps = null;
                return false;
            }
        }

        private static string FormatStroke(StrokeResult stroke)
            => stroke.Type == StrokeType.Slash
                ? $"Slash Length={stroke.Length}"
                : $"Thrust Area={stroke.Area}";
    }
}
