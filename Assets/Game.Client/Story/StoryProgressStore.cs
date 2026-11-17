using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Client.Story
{
    // File I/O for the story save (storymap.json), separate from progress.json so the 수련 record
    // and the 비무 unlock are never touched by the story.
    public static class StoryProgressStore
    {
        private const string FileName = "storymap.json";

        // When set, the save is read from and written to this path instead (editor checks).
        public static string PathOverride;

        public static string FilePath => string.IsNullOrEmpty(PathOverride)
            ? Path.Combine(Application.persistentDataPath, FileName)
            : PathOverride;

        // Missing file = fresh save. A corrupt/unsupported file is reported and treated as fresh;
        // it stays on disk untouched until the next Save overwrites it.
        public static StoryProgress Load(int stageCount)
        {
            string path = FilePath;
            if (!File.Exists(path))
                return default;

            try
            {
                return StoryProgressJson.Deserialize(File.ReadAllText(path, Encoding.UTF8), stageCount);
            }
            catch (StoryProgressException e)
            {
                Debug.LogError($"[Story] 스토리 진행 파일을 읽을 수 없어 처음부터 시작합니다 ({path}): {e.Message}");
                return default;
            }
        }

        public static void Save(StoryProgress progress)
        {
            File.WriteAllText(FilePath, StoryProgressJson.Serialize(progress), new UTF8Encoding(false));
            Debug.Log($"[Story] 저장 완료 (클리어 {progress.Cleared}, 플래그 {progress.Flag}): {FilePath}");
        }
    }
}
