using System.IO;
using System.Text;
using Game.Sim.Progress;
using UnityEngine;

namespace Game.Client.Progress
{
    // File I/O for the PvE progress save. Game.sim owns the schema (ProgressJsonSerializer);
    // this side only decides where the file lives and how to recover from a bad one.
    public static class ProgressStore
    {
        private const string FileName = "progress.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        // Missing file = fresh save. A corrupt/unsupported file is reported and treated as fresh;
        // it is left on disk untouched until the next Save overwrites it.
        public static ProgressData Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return default;

            try
            {
                (_, ProgressData progress) = ProgressJsonSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8));
                return progress;
            }
            catch (ProgressJsonException e)
            {
                Debug.LogError($"[Progress] 진행도 파일을 읽을 수 없어 처음부터 시작합니다 ({path}): {e.Message}");
                return default;
            }
        }

        public static void Save(ProgressData progress)
        {
            File.WriteAllText(FilePath, ProgressJsonSerializer.Serialize(progress), new UTF8Encoding(false));
            Debug.Log($"[Progress] 저장 완료 (최고 클리어 스테이지 {progress.HighestClearedStage}): {FilePath}");
        }
    }
}
