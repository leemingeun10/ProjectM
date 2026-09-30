using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Sim.Common
{
    // Thrown by JsonReader on malformed input. Internal: each serializer wraps it in its own
    // public exception type so callers only ever see one exception per schema.
    internal sealed class JsonFormatException : System.Exception
    {
        public JsonFormatException(string message) : base(message)
        {
        }
    }

    // Minimal JSON reader producing Dictionary<string, object> / List<object> / string /
    // long / bool / null. Numbers must be integers: Game.sim schemas never store fractions
    // (Fixed values are raw longs), and accepting them would invite a lossy float path.
    internal sealed class JsonReader
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

        private JsonFormatException Error(string message)
            => new JsonFormatException($"at position {pos}: {message}.");
    }
}
