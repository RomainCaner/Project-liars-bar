// Mini sérialiseur JSON (objets = Dictionary<string, object>, tableaux = List<object>).
// Suffisant pour le protocole réseau, sans dépendance externe.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Menteur.Core
{
    public static class MiniJson
    {
        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture)); break;
                case IDictionary dict:
                    sb.Append('{');
                    var first = true;
                    foreach (DictionaryEntry e in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, e.Key.ToString());
                        sb.Append(':');
                        Write(sb, e.Value);
                    }
                    sb.Append('}');
                    break;
                case IEnumerable list:
                    sb.Append('[');
                    var f2 = true;
                    foreach (var item in list)
                    {
                        if (!f2) sb.Append(',');
                        f2 = false;
                        Write(sb, item);
                    }
                    sb.Append(']');
                    break;
                default:
                    WriteString(sb, v.ToString());
                    break;
            }
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var p = new Parser(json);
            var v = p.ParseValue();
            return v;
        }

        sealed class Parser
        {
            readonly string s;
            int i;

            public Parser(string s) { this.s = s; }

            void Ws()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            public object ParseValue()
            {
                Ws();
                if (i >= s.Length) throw new FormatException("JSON incomplet");
                var c = s[i];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseString();
                if (c == 't' && Match("true")) return true;
                if (c == 'f' && Match("false")) return false;
                if (c == 'n' && Match("null")) return null;
                return ParseNumber();
            }

            bool Match(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new FormatException("JSON invalide");
                i += word.Length;
                return true;
            }

            Dictionary<string, object> ParseObject()
            {
                var d = new Dictionary<string, object>();
                i++;
                Ws();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws();
                    var key = ParseString();
                    Ws();
                    if (i >= s.Length || s[i] != ':') throw new FormatException("':' attendu");
                    i++;
                    d[key] = ParseValue();
                    Ws();
                    if (i >= s.Length) throw new FormatException("JSON incomplet");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("',' ou '}' attendu");
                }
            }

            List<object> ParseArray()
            {
                var l = new List<object>();
                i++;
                Ws();
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(ParseValue());
                    Ws();
                    if (i >= s.Length) throw new FormatException("JSON incomplet");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("',' ou ']' attendu");
                }
            }

            string ParseString()
            {
                if (s[i] != '"') throw new FormatException("chaîne attendue");
                i++;
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    var c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    var e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
                throw new FormatException("chaîne non terminée");
            }

            object ParseNumber()
            {
                var start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                var num = s.Substring(start, i - start);
                if (num.Length == 0) throw new FormatException("valeur attendue");
                if (num.IndexOfAny(new[] { '.', 'e', 'E' }) < 0 &&
                    long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                    return l;
                return double.Parse(num, CultureInfo.InvariantCulture);
            }
        }

        // ---- Accès typés pratiques
        public static Dictionary<string, object> Obj(this object o) => o as Dictionary<string, object>;
        public static List<object> Arr(this object o) => o as List<object> ?? new List<object>();

        public static object Get(this Dictionary<string, object> d, string k) =>
            d != null && d.TryGetValue(k, out var v) ? v : null;

        public static int Int(this Dictionary<string, object> d, string k, int def = 0)
        {
            var v = d.Get(k);
            if (v is long l) return (int)l;
            if (v is int i) return i;
            if (v is double x) return (int)x;
            return def;
        }

        public static int? IntOrNull(this Dictionary<string, object> d, string k)
        {
            var v = d.Get(k);
            if (v == null) return null;
            return d.Int(k);
        }

        public static string Str(this Dictionary<string, object> d, string k) => d.Get(k) as string;

        public static bool Bool(this Dictionary<string, object> d, string k, bool def = false) =>
            d.Get(k) is bool b ? b : def;

        public static bool? BoolOrNull(this Dictionary<string, object> d, string k) =>
            d.Get(k) is bool b ? b : (bool?)null;
    }
}
