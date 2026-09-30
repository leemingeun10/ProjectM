using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Sim.Common;
using Game.Sim.Stroke;

namespace Game.Sim.Technique
{
    // Hand-rolled JSON writer/parser for the technique schema. No external JSON library is used
    // so Game.sim stays dependency-free, and every Fixed is written as its raw long so a
    // save/load round trip is bit-exact (a float detour would break determinism).
    //
    // Schema v2 (current):
    // {
    //   "version": 2,
    //   "steps": [
    //     {
    //       "turnIndex": 0, "isCounter": false,
    //       "strokeType": "Slash",
    //       "length": <raw>, "startDirectionX": <raw>, "startDirectionY": <raw>,
    //       "endDirectionX": <raw>, "endDirectionY": <raw>,          (Slash only)
    //       "area": <raw>,                                           (Thrust only)
    //       "rawPoints": [{"x": <raw>, "y": <raw>}, ...]
    //     }
    //   ]
    // }
    //
    // Schema v1 is identical plus a per-step "stance" string. The stance system was removed,
    // so v1 files are still accepted and their "stance" field is ignored.
    public static class TechniqueJsonSerializer
    {
        public const int CurrentVersion = 2;
        public const int LegacyStanceVersion = 1;

        public static string Serialize(TechniqueBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(CurrentVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"steps\": [");

            IReadOnlyList<TechniqueStep> steps = builder.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                sb.Append(i == 0 ? "\n" : ",\n");
                WriteStep(sb, steps[i]);
            }

            sb.Append(steps.Count == 0 ? "]\n" : "\n  ]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        public static (int version, List<TechniqueStep> steps) Deserialize(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            object root;
            try
            {
                root = new JsonReader(json).ReadDocument();
            }
            catch (JsonFormatException e)
            {
                throw new TechniqueJsonException("Invalid technique JSON: " + e.Message);
            }
            Dictionary<string, object> rootObj = AsObject(root, "root");

            long version = GetLong(rootObj, "version", "root");
            if (version != CurrentVersion && version != LegacyStanceVersion)
                throw new TechniqueJsonException(
                    $"Unsupported technique JSON version {version} (supported: {LegacyStanceVersion}, {CurrentVersion}).");

            List<object> stepArray = AsArray(GetRequired(rootObj, "steps", "root"), "steps");
            var steps = new List<TechniqueStep>(stepArray.Count);
            for (int i = 0; i < stepArray.Count; i++)
                steps.Add(ReadStep(AsObject(stepArray[i], $"steps[{i}]"), $"steps[{i}]"));

            return ((int)version, steps);
        }

        // ---- Writing ----

        private static void WriteStep(StringBuilder sb, TechniqueStep step)
        {
            StrokeResult stroke = step.Stroke;

            sb.Append("    {\n");
            WriteField(sb, "turnIndex", step.TurnIndex.ToString(CultureInfo.InvariantCulture));
            WriteField(sb, "isCounter", step.IsCounter ? "true" : "false");
            WriteField(sb, "strokeType", Quote(StrokeTypeToString(stroke.Type)));

            if (stroke.Type == StrokeType.Slash)
            {
                WriteField(sb, "length", RawString(stroke.Length));
                WriteField(sb, "startDirectionX", RawString(stroke.StartDirectionX));
                WriteField(sb, "startDirectionY", RawString(stroke.StartDirectionY));
                WriteField(sb, "endDirectionX", RawString(stroke.EndDirectionX));
                WriteField(sb, "endDirectionY", RawString(stroke.EndDirectionY));
            }
            else
            {
                WriteField(sb, "area", RawString(stroke.Area));
            }

            sb.Append("      \"rawPoints\": [");
            IReadOnlyList<StrokePoint> points = step.RawPoints;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("{\"x\": ").Append(RawString(points[i].X))
                  .Append(", \"y\": ").Append(RawString(points[i].Y)).Append('}');
            }
            sb.Append("]\n");
            sb.Append("    }");
        }

        private static void WriteField(StringBuilder sb, string name, string value)
            => sb.Append("      \"").Append(name).Append("\": ").Append(value).Append(",\n");

        private static string RawString(Fixed value) => value.Raw.ToString(CultureInfo.InvariantCulture);

        // Only enum names (plain ASCII identifiers) are quoted, so no escaping is needed.
        private static string Quote(string s) => "\"" + s + "\"";

        // Explicit mapping instead of Enum.ToString/Parse: renaming an enum member must not
        // silently change or break the save format.
        private static string StrokeTypeToString(StrokeType type)
        {
            switch (type)
            {
                case StrokeType.Slash: return "Slash";
                case StrokeType.Thrust: return "Thrust";
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        // ---- Reading ----

        private static TechniqueStep ReadStep(Dictionary<string, object> obj, string path)
        {
            long turnIndex = GetLong(obj, "turnIndex", path);
            if (turnIndex < 0 || turnIndex > int.MaxValue)
                throw new TechniqueJsonException($"{path}.turnIndex out of range: {turnIndex}");

            bool isCounter = GetBool(obj, "isCounter", path);
            string strokeTypeName = GetString(obj, "strokeType", path);

            StrokeResult stroke;
            switch (strokeTypeName)
            {
                case "Slash":
                    stroke = StrokeResult.Slash(
                        GetFixed(obj, "length", path),
                        GetFixed(obj, "startDirectionX", path),
                        GetFixed(obj, "startDirectionY", path),
                        GetFixed(obj, "endDirectionX", path),
                        GetFixed(obj, "endDirectionY", path));
                    break;
                case "Thrust":
                    stroke = StrokeResult.Thrust(GetFixed(obj, "area", path));
                    break;
                default:
                    throw new TechniqueJsonException($"{path}.strokeType has unknown value \"{strokeTypeName}\".");
            }

            List<object> pointArray = AsArray(GetRequired(obj, "rawPoints", path), path + ".rawPoints");
            var points = new StrokePoint[pointArray.Count];
            for (int i = 0; i < pointArray.Count; i++)
            {
                string pointPath = $"{path}.rawPoints[{i}]";
                Dictionary<string, object> p = AsObject(pointArray[i], pointPath);
                points[i] = new StrokePoint(GetLong(p, "x", pointPath), GetLong(p, "y", pointPath));
            }

            return new TechniqueStep((int)turnIndex, isCounter, stroke, points);
        }

        private static object GetRequired(Dictionary<string, object> obj, string key, string path)
        {
            if (!obj.TryGetValue(key, out object value))
                throw new TechniqueJsonException($"{path} is missing required field \"{key}\".");
            return value;
        }

        private static long GetLong(Dictionary<string, object> obj, string key, string path)
            => GetRequired(obj, key, path) is long l
                ? l
                : throw new TechniqueJsonException($"{path}.{key} must be an integer.");

        private static Fixed GetFixed(Dictionary<string, object> obj, string key, string path)
            => Fixed.FromRaw(GetLong(obj, key, path));

        private static bool GetBool(Dictionary<string, object> obj, string key, string path)
            => GetRequired(obj, key, path) is bool b
                ? b
                : throw new TechniqueJsonException($"{path}.{key} must be a boolean.");

        private static string GetString(Dictionary<string, object> obj, string key, string path)
            => GetRequired(obj, key, path) is string s
                ? s
                : throw new TechniqueJsonException($"{path}.{key} must be a string.");

        private static Dictionary<string, object> AsObject(object value, string path)
            => value as Dictionary<string, object> ?? throw new TechniqueJsonException($"{path} must be an object.");

        private static List<object> AsArray(object value, string path)
            => value as List<object> ?? throw new TechniqueJsonException($"{path} must be an array.");
    }
}
