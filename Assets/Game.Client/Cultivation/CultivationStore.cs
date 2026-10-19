using System;
using System.IO;
using System.Text;
using Game.Sim.Cultivation;
using UnityEngine;

namespace Game.Client.Cultivation
{
    // File I/O for the cultivation (단련) save, plus the one place that reads the clock.
    // Game.sim owns the schema (CultivationJsonSerializer) and the rules; this side decides where
    // the file lives, how to recover from a bad one and what "today" is.
    public static class CultivationStore
    {
        private const string FileName = "cultivation.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        // Today in the device's local time zone as a YYYYMMDD integer, so the daily training
        // resets at the player's own midnight.
        public static int Today()
        {
            DateTime now = DateTime.Now;
            return now.Year * 10000 + now.Month * 100 + now.Day;
        }

        // Missing file = fresh save. A corrupt/unsupported file is reported and treated as fresh;
        // it is left on disk untouched until the next Save overwrites it.
        public static CultivationStats Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return default;

            try
            {
                (_, CultivationStats stats) = CultivationJsonSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8));
                return stats;
            }
            catch (CultivationJsonException e)
            {
                Debug.LogError($"[Cultivation] 단련 파일을 읽을 수 없어 처음부터 시작합니다 ({path}): {e.Message}");
                return default;
            }
        }

        public static void Save(CultivationStats stats)
        {
            File.WriteAllText(FilePath, CultivationJsonSerializer.Serialize(stats), new UTF8Encoding(false));
            Debug.Log($"[Cultivation] 저장 완료 (외공 {stats.ExternalPower}, 내공 {stats.InternalPower}, 마지막 수련 {stats.LastTrainingDate}): {FilePath}");
        }
    }
}
