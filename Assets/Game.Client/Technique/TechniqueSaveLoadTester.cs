using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Technique
{
    // Temporary debug component: verifies TechniqueJsonSerializer save/load end to end before
    // the real 5-slot UI exists. Safe to delete once that UI replaces it.
    public class TechniqueSaveLoadTester : MonoBehaviour
    {
        private const string FileName = "technique_test.json";

        [SerializeField] private TechniqueSequenceController sequenceController;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;

        private string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private void OnEnable()
        {
            saveButton.onClick.AddListener(Save);
            loadButton.onClick.AddListener(Load);
        }

        private void OnDisable()
        {
            saveButton.onClick.RemoveListener(Save);
            loadButton.onClick.RemoveListener(Load);
        }

        private void Save()
        {
            TechniqueBuilder builder = sequenceController.Builder;
            string json = TechniqueJsonSerializer.Serialize(builder);
            File.WriteAllText(FilePath, json, new UTF8Encoding(false));
            Debug.Log($"[TechniqueSave] {builder.TurnCount}개 스텝 저장 완료: {FilePath}");
        }

        private void Load()
        {
            if (!File.Exists(FilePath))
            {
                Debug.LogWarning($"[TechniqueSave] 저장 파일이 없습니다: {FilePath}");
                return;
            }

            int version;
            List<TechniqueStep> steps;
            try
            {
                (version, steps) = TechniqueJsonSerializer.Deserialize(File.ReadAllText(FilePath, Encoding.UTF8));
            }
            catch (TechniqueJsonException e)
            {
                Debug.LogError($"[TechniqueSave] 불러오기 실패: {e.Message}");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[TechniqueSave] 불러오기 완료 (version {version}, 스텝 {steps.Count}개): {FilePath}");
            foreach (TechniqueStep step in steps)
                sb.AppendLine($"턴 {step.TurnIndex + 1}: Stance={step.Stance} Counter={step.IsCounter} {FormatStroke(step.Stroke)} Points={step.RawPoints.Count}");

            Debug.Log(sb.ToString());
        }

        private static string FormatStroke(StrokeResult stroke)
            => stroke.Type == StrokeType.Slash
                ? $"Slash Length={stroke.Length}"
                : $"Thrust Area={stroke.Area}";
    }
}
