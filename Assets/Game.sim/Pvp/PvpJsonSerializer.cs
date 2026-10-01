using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Sim.Common;

namespace Game.Sim.Pvp
{
    // Hand-rolled JSON writer/parser for the PvP record save, sharing JsonReader with the
    // other Game.sim serializers.
    //
    // Schema v1:
    // {
    //   "version": 1,
    //   "wins": 0,
    //   "losses": 0,
    //   "draws": 0
    // }
    public static class PvpJsonSerializer
    {
        public const int CurrentVersion = 1;

        public static string Serialize(PvpRecord record)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(CurrentVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"wins\": ").Append(record.Wins.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"losses\": ").Append(record.Losses.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"draws\": ").Append(record.Draws.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("}\n");
            return sb.ToString();
        }

        public static (int version, PvpRecord record) Deserialize(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            object root;
            try
            {
                root = new JsonReader(json).ReadDocument();
            }
            catch (JsonFormatException e)
            {
                throw new PvpJsonException("Invalid PvP record JSON: " + e.Message);
            }

            var rootObj = root as Dictionary<string, object> ?? throw new PvpJsonException("root must be an object.");

            long version = GetLong(rootObj, "version");
            if (version != CurrentVersion)
                throw new PvpJsonException($"Unsupported PvP record JSON version {version} (supported: {CurrentVersion}).");

            int wins = GetCount(rootObj, "wins");
            int losses = GetCount(rootObj, "losses");
            int draws = GetCount(rootObj, "draws");

            return ((int)version, new PvpRecord(wins, losses, draws));
        }

        // A negative or int-overflowing count means a corrupt or tampered save; reject it rather
        // than clamping, so the client can decide how to recover.
        private static int GetCount(Dictionary<string, object> obj, string key)
        {
            long value = GetLong(obj, key);
            if (value < 0 || value > int.MaxValue)
                throw new PvpJsonException($"{key} out of range: {value} (expected 0..{int.MaxValue}).");
            return (int)value;
        }

        private static long GetLong(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object value))
                throw new PvpJsonException($"root is missing required field \"{key}\".");
            return value is long l ? l : throw new PvpJsonException($"root.{key} must be an integer.");
        }
    }
}
