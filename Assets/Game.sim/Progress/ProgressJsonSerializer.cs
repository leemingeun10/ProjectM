using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Sim.Common;

namespace Game.Sim.Progress
{
    // Hand-rolled JSON writer/parser for the progress save, sharing JsonReader with the
    // technique serializer (no external JSON library in Game.sim).
    //
    // Schema v1:
    // {
    //   "version": 1,
    //   "highestClearedStage": 0
    // }
    public static class ProgressJsonSerializer
    {
        public const int CurrentVersion = 1;

        public static string Serialize(ProgressData progress)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(CurrentVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"highestClearedStage\": ").Append(progress.HighestClearedStage.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("}\n");
            return sb.ToString();
        }

        public static (int version, ProgressData progress) Deserialize(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            object root;
            try
            {
                root = new JsonReader(json).ReadDocument();
            }
            catch (JsonFormatException e)
            {
                throw new ProgressJsonException("Invalid progress JSON: " + e.Message);
            }

            var rootObj = root as Dictionary<string, object> ?? throw new ProgressJsonException("root must be an object.");

            long version = GetLong(rootObj, "version");
            if (version != CurrentVersion)
                throw new ProgressJsonException($"Unsupported progress JSON version {version} (supported: {CurrentVersion}).");

            // A value outside the catalog means a corrupt or tampered save; reject it rather
            // than clamping, so the client can decide how to recover.
            long highest = GetLong(rootObj, "highestClearedStage");
            if (highest < 0 || highest > StageCatalog.StageCount)
                throw new ProgressJsonException($"highestClearedStage out of range: {highest} (expected 0..{StageCatalog.StageCount}).");

            return ((int)version, new ProgressData((int)highest));
        }

        private static long GetLong(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object value))
                throw new ProgressJsonException($"root is missing required field \"{key}\".");
            return value is long l ? l : throw new ProgressJsonException($"root.{key} must be an integer.");
        }
    }
}
