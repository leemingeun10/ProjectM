using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Technique
{
    // Saves the sequence being drawn into the player's technique slot A, and loads it back for
    // battles. Single slot until the 5-slot selection UI replaces it; the file name is kept from
    // the old A/B tester so existing saves still load.
    public class PlayerTechniqueSlot : MonoBehaviour
    {
        private const string FileName = "technique_A.json";

        [SerializeField] private TechniqueSequenceController sequenceController;
        [SerializeField] private Button saveButton;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private void OnEnable()
        {
            saveButton.onClick.AddListener(Save);
        }

        private void OnDisable()
        {
            saveButton.onClick.RemoveListener(Save);
        }

        private void Save()
        {
            TechniqueBuilder builder = sequenceController.Builder;
            File.WriteAllText(FilePath, TechniqueJsonSerializer.Serialize(builder), new UTF8Encoding(false));
            Debug.Log($"[TechniqueSave] 슬롯 A: {builder.TurnCount}개 스텝 저장 완료: {FilePath}");

            sequenceController.ResetSequence();
        }

        // Returns false (with a log) when slot A is missing or unreadable.
        public static bool TryLoad(out List<TechniqueStep> steps)
        {
            steps = null;
            string path = FilePath;
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[TechniqueSave] 슬롯 A 저장 파일이 없습니다. 먼저 무공을 그려서 저장하세요: {path}");
                return false;
            }

            try
            {
                (_, steps) = TechniqueJsonSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8));
                return true;
            }
            catch (TechniqueJsonException e)
            {
                Debug.LogError($"[TechniqueSave] 슬롯 A 불러오기 실패 ({path}): {e.Message}");
                return false;
            }
        }
    }
}
