using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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

            object root = new JsonReader(json).ReadDocument();
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

        // Minimal JSON reader producing Dictionary<string, object> / List<object> / string /
        // long / bool / null. Numbers must be integers: this schema never stores fractions,
        // and accepting them would invite a lossy float path.
        private sealed class JsonReader
        {
            private readonly string text;
            private int pos;

            public JsonReader(string text)
            {
                this.text = text;
            }

            public object ReadDocument()
            {
                object value = ReadValue();
                SkipWhitespace();
                if (pos != text.Length)
                    throw Error("Unexpected trailing characters");
                return value;
            }

            private object ReadValue()
            {
                SkipWhitespace();
                if (pos >= text.Length)
                    throw Error("Unexpected end of input");

                char c = text[pos];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': ExpectLiteral("true"); return true;
                    case 'f': ExpectLiteral("false"); return false;
                    case 'n': ExpectLiteral("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                            return ReadInteger();
                        throw Error($"Unexpected character '{c}'");
                }
            }

            private Dictionary<string, object> ReadObject()
            {
                pos++; // '{'
                var obj = new Dictionary<string, object>();

                SkipWhitespace();
                if (TryConsume('}'))
                    return obj;

                while (true)
                {
                    SkipWhitespace();
                    if (pos >= text.Length || text[pos] != '"')
                        throw Error("Expected property name");
                    string key = ReadString();

                    SkipWhitespace();
                    if (!TryConsume(':'))
                        throw Error("Expected ':'");

                    if (obj.ContainsKey(key))
                        throw Error($"Duplicate property \"{key}\"");
                    obj[key] = ReadValue();

                    SkipWhitespace();
                    if (TryConsume(',')) continue;
                    if (TryConsume('}')) return obj;
                    throw Error("Expected ',' or '}'");
                }
            }

            private List<object> ReadArray()
            {
                pos++; // '['
                var list = new List<object>();

                SkipWhitespace();
                if (TryConsume(']'))
                    return list;

                while (true)
                {
                    list.Add(ReadValue());

                    SkipWhitespace();
                    if (TryConsume(',')) continue;
                    if (TryConsume(']')) return list;
                    throw Error("Expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                pos++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (pos >= text.Length)
                        throw Error("Unterminated string");

                    char c = text[pos++];
                    if (c == '"')
                        return sb.ToString();
                    if (c < 0x20)
                        throw Error("Control character in string");
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (pos >= text.Length)
                        throw Error("Unterminated escape sequence");

                    char e = text[pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 > text.Length)
                                throw Error("Truncated \\u escape");
                            int code = 0;
                            for (int i = 0; i < 4; i++)
                            {
                                int digit = HexValue(text[pos++]);
                                if (digit < 0)
                                    throw Error("Invalid \\u escape");
                                code = code * 16 + digit;
                            }
                            sb.Append((char)code);
                            break;
                        default:
                            throw Error($"Invalid escape '\\{e}'");
                    }
                }
            }

            private long ReadInteger()
            {
                int start = pos;
                if (text[pos] == '-') pos++;

                int digitStart = pos;
                while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9')
                    pos++;

                int digitCount = pos - digitStart;
                if (digitCount == 0)
                    throw Error("Expected digit");
                if (digitCount > 1 && text[digitStart] == '0')
                    throw Error("Leading zeros are not allowed");
                if (pos < text.Length && (text[pos] == '.' || text[pos] == 'e' || text[pos] == 'E'))
                    throw Error("Only integer numbers are allowed (Fixed values are stored as raw integers)");

                string s = text.Substring(start, pos - start);
                if (!long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
                    throw Error($"Integer out of range: {s}");
                return value;
            }

            private void ExpectLiteral(string literal)
            {
                if (string.CompareOrdinal(text, pos, literal, 0, literal.Length) != 0)
                    throw Error($"Expected '{literal}'");
                pos += literal.Length;
            }

            private bool TryConsume(char c)
            {
                if (pos < text.Length && text[pos] == c)
                {
                    pos++;
                    return true;
                }
                return false;
            }

            private void SkipWhitespace()
            {
                while (pos < text.Length)
                {
                    char c = text[pos];
                    if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                        break;
                    pos++;
                }
            }

            private static int HexValue(char c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                return -1;
            }

            private TechniqueJsonException Error(string message)
                => new TechniqueJsonException($"Invalid technique JSON at position {pos}: {message}.");
        }
    }
}
