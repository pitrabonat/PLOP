using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Plop.Core.Json
{
    /// <summary>
    /// Tiny dependency-free JSON reader/writer (UnityEngine.JsonUtility cannot be used in pure C# assemblies).
    /// Parsed values: Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, long, double, bool, null.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var p = new Parser(json);
            p.SkipWs();
            object value = p.ReadValue();
            p.SkipWs();
            if (!p.End) throw p.Error("Unexpected trailing characters");
            return value;
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;
            public Parser(string s) { _s = s; }
            public bool End => _i >= _s.Length;

            public FormatException Error(string msg) => new FormatException("JSON: " + msg + " at position " + _i);

            public void SkipWs()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public object ReadValue()
            {
                if (End) throw Error("Unexpected end");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error("Unexpected character '" + c + "'");
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error("Expected " + word);
                _i += word.Length;
            }

            private Dictionary<string, object> ReadObject()
            {
                var dict = new Dictionary<string, object>(StringComparer.Ordinal);
                _i++; SkipWs();
                if (!End && _s[_i] == '}') { _i++; return dict; }
                while (true)
                {
                    SkipWs();
                    if (End || _s[_i] != '"') throw Error("Expected property name");
                    string key = ReadString();
                    SkipWs();
                    if (End || _s[_i] != ':') throw Error("Expected ':'");
                    _i++; SkipWs();
                    if (dict.ContainsKey(key)) throw Error("Duplicate property '" + key + "'");
                    dict[key] = ReadValue();
                    SkipWs();
                    if (End) throw Error("Unterminated object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return dict; }
                    throw Error("Expected ',' or '}'");
                }
            }

            private List<object> ReadArray()
            {
                var list = new List<object>();
                _i++; SkipWs();
                if (!End && _s[_i] == ']') { _i++; return list; }
                while (true)
                {
                    SkipWs();
                    list.Add(ReadValue());
                    SkipWs();
                    if (End) throw Error("Unterminated array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return list; }
                    throw Error("Expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                var sb = new StringBuilder();
                _i++;
                while (true)
                {
                    if (End) throw Error("Unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (End) throw Error("Bad escape");
                    char e = _s[_i++];
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
                            if (_i + 4 > _s.Length) throw Error("Bad unicode escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw Error("Bad escape '\\" + e + "'");
                    }
                }
            }

            private object ReadNumber()
            {
                int start = _i;
                if (_s[_i] == '-') _i++;
                bool isFloat = false;
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c >= '0' && c <= '9') { _i++; continue; }
                    if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') { isFloat = true; _i++; continue; }
                    break;
                }
                string token = _s.Substring(start, _i - start);
                if (!isFloat && long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return l;
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
                throw Error("Invalid number '" + token + "'");
            }
        }

        // ---------- typed access helpers ----------

        public static Dictionary<string, object> AsObject(object o, string what)
            => o as Dictionary<string, object> ?? throw new FormatException(what + " must be a JSON object.");

        public static List<object> AsArray(object o, string what)
            => o as List<object> ?? throw new FormatException(what + " must be a JSON array.");

        public static object Get(Dictionary<string, object> obj, string key, bool required = true)
        {
            if (obj.TryGetValue(key, out object v)) return v;
            if (required) throw new FormatException("Missing required property '" + key + "'.");
            return null;
        }

        public static int GetInt(Dictionary<string, object> obj, string key)
        {
            object v = Get(obj, key);
            if (v is long l && l >= int.MinValue && l <= int.MaxValue) return (int)l;
            throw new FormatException("Property '" + key + "' must be an integer.");
        }

        public static int GetInt(Dictionary<string, object> obj, string key, int fallback)
            => obj.ContainsKey(key) ? GetInt(obj, key) : fallback;

        public static double GetDouble(Dictionary<string, object> obj, string key)
        {
            object v = Get(obj, key);
            if (v is long l) return l;
            if (v is double d) return d;
            throw new FormatException("Property '" + key + "' must be a number.");
        }

        public static string GetString(Dictionary<string, object> obj, string key, bool required = true)
        {
            object v = Get(obj, key, required);
            if (v == null) return null;
            return v as string ?? throw new FormatException("Property '" + key + "' must be a string.");
        }
    }

    /// <summary>Minimal pretty-printing JSON writer with deterministic output.</summary>
    public sealed class JsonWriter
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly Stack<bool> _first = new Stack<bool>();
        private readonly bool _pretty;
        private bool _afterKey;

        public JsonWriter(bool pretty = true) { _pretty = pretty; }

        public override string ToString() => _sb.ToString();

        public JsonWriter BeginObject() { Prefix(); _sb.Append('{'); _first.Push(true); return this; }
        public JsonWriter EndObject() { Close('}'); return this; }
        public JsonWriter BeginArray() { Prefix(); _sb.Append('['); _first.Push(true); return this; }
        public JsonWriter EndArray() { Close(']'); return this; }

        public JsonWriter Key(string key)
        {
            Prefix();
            WriteString(key);
            _sb.Append(_pretty ? ": " : ":");
            _afterKey = true;
            return this;
        }

        public JsonWriter Value(string v) { Prefix(); if (v == null) _sb.Append("null"); else WriteString(v); return this; }
        public JsonWriter Value(long v) { Prefix(); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        public JsonWriter Value(bool v) { Prefix(); _sb.Append(v ? "true" : "false"); return this; }
        public JsonWriter Value(double v)
        {
            Prefix();
            if (double.IsNaN(v) || double.IsInfinity(v)) _sb.Append("null");
            else _sb.Append(Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture));
            return this;
        }

        /// <summary>Writes a small inline array of ints like [3, 4] on one line.</summary>
        public JsonWriter InlineInts(params int[] values)
        {
            Prefix();
            _sb.Append('[');
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) _sb.Append(_pretty ? ", " : ",");
                _sb.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }
            _sb.Append(']');
            return this;
        }

        private void Prefix()
        {
            if (_afterKey) { _afterKey = false; return; }
            if (_first.Count == 0) return;
            bool first = _first.Pop();
            if (!first) _sb.Append(',');
            _first.Push(false);
            if (_pretty) { _sb.Append('\n'); _sb.Append(' ', _first.Count * 2); }
        }

        private void Close(char c)
        {
            bool empty = _first.Pop();
            if (_pretty && !empty) { _sb.Append('\n'); _sb.Append(' ', _first.Count * 2); }
            _sb.Append(c);
        }

        private void WriteString(string s)
        {
            _sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }
    }
}
