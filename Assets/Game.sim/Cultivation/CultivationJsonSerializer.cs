using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Sim.Common;

namespace Game.Sim.Cultivation
{
    // Hand-rolled JSON writer/parser for the cultivation save, sharing JsonReader with the
    // other Game.sim serializers.
    //
    // Schema v1:
    // {
    //   "version": 1,
    //   "externalPower": 0,
    //   "internalPower": 0,
    //   "lastTrainingDate": 0
    // }
    public static class CultivationJsonSerializer
    {
        public const int CurrentVersion = 1;

        public static string Serialize(CultivationStats stats)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(CurrentVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"externalPower\": ").Append(stats.ExternalPower.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"internalPower\": ").Append(stats.InternalPower.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"lastTrainingDate\": ").Append(stats.LastTrainingDate.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("}\n");
            return sb.ToString();
        }

        public static (int version, CultivationStats stats) Deserialize(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            object root;
            try
            {
                root = new JsonReader(json).ReadDocument();
            }
            catch (JsonFormatException e)
            {
                throw new CultivationJsonException("Invalid cultivation JSON: " + e.Message);
            }

            var rootObj = root as Dictionary<string, object> ?? throw new CultivationJsonException("root must be an object.");

            long version = GetLong(rootObj, "version");
            if (version != CurrentVersion)
                throw new CultivationJsonException($"Unsupported cultivation JSON version {version} (supported: {CurrentVersion}).");

            int externalPower = GetPower(rootObj, "externalPower");
            int internalPower = GetPower(rootObj, "internalPower");

            // 0 = never trained; anything else must be a real YYYYMMDD date.
            long date = GetLong(rootObj, "lastTrainingDate");
            if (date != 0 && (date > int.MaxValue || date < 0 || !CultivationRules.IsValidDate((int)date)))
                throw new CultivationJsonException($"lastTrainingDate must be 0 or a YYYYMMDD date: {date}.");

            return ((int)version, new CultivationStats(externalPower, internalPower, (int)date));
        }

        // A negative or int-overflowing power means a corrupt or tampered save; reject it rather
        // than clamping, so the client can decide how to recover.
        private static int GetPower(Dictionary<string, object> obj, string key)
        {
            long value = GetLong(obj, key);
            if (value < 0 || value > int.MaxValue)
                throw new CultivationJsonException($"{key} out of range: {value} (expected 0..{int.MaxValue}).");
            return (int)value;
        }

        private static long GetLong(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object value))
                throw new CultivationJsonException($"root is missing required field \"{key}\".");
            return value is long l ? l : throw new CultivationJsonException($"root.{key} must be an integer.");
        }
    }
}
