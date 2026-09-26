using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OdinsPaths
{
    /// <summary>
    /// A small JSON reader for the blueprints: objects become dictionaries, arrays lists, numbers
    /// doubles. Unity's <c>JsonUtility</c> read the blueprints' names but left their piece lists
    /// empty in game, and the game ships no other JSON library. Throws FormatException on bad text.
    /// </summary>
    internal static class Json
    {
        public static object Parse(string text)
        {
            int at = 0;
            object value = Value(text, ref at);
            Space(text, ref at);
            if (at < text.Length)
            {
                throw Error(text, at, "more after the end");
            }
            return value;
        }

        public static string String(Dictionary<string, object> o, string key) => o.TryGetValue(key, out object v) ? v as string : null;

        public static float Float(Dictionary<string, object> o, string key) => o.TryGetValue(key, out object v) && v is double d ? (float)d : 0f;

        public static bool Bool(Dictionary<string, object> o, string key) => o.TryGetValue(key, out object v) && v is bool b && b;

        public static string[] Strings(Dictionary<string, object> o, string key)
        {
            List<string> items = new List<string>();
            if (o.TryGetValue(key, out object v) && v is List<object> list)
            {
                foreach (object item in list)
                {
                    if (item is string s)
                    {
                        items.Add(s);
                    }
                }
            }
            return items.ToArray();
        }

        /// <summary>A number array; null if the key is missing or holds anything but numbers.</summary>
        public static float[] Floats(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out object v) || !(v is List<object> list))
            {
                return null;
            }
            float[] values = new float[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i] is double d))
                {
                    return null;
                }
                values[i] = (float)d;
            }
            return values;
        }

        public static IEnumerable<Dictionary<string, object>> Objects(Dictionary<string, object> o, string key)
        {
            if (o.TryGetValue(key, out object v) && v is List<object> list)
            {
                foreach (object item in list)
                {
                    if (item is Dictionary<string, object> d)
                    {
                        yield return d;
                    }
                }
            }
        }

        private static object Value(string s, ref int at)
        {
            Space(s, ref at);
            if (at >= s.Length)
            {
                throw Error(s, at, "a value expected");
            }
            char c = s[at];
            if (c == '{')
            {
                at++;
                Dictionary<string, object> o = new Dictionary<string, object>();
                Space(s, ref at);
                if (at < s.Length && s[at] == '}')
                {
                    at++;
                    return o;
                }
                while (true)
                {
                    Space(s, ref at);
                    if (at >= s.Length || s[at] != '"')
                    {
                        throw Error(s, at, "a \"name\" expected");
                    }
                    string key = Text(s, ref at);
                    Space(s, ref at);
                    Expect(s, ref at, ':');
                    o[key] = Value(s, ref at);
                    Space(s, ref at);
                    if (at < s.Length && s[at] == ',')
                    {
                        at++;
                        continue;
                    }
                    Expect(s, ref at, '}');
                    return o;
                }
            }
            if (c == '[')
            {
                at++;
                List<object> list = new List<object>();
                Space(s, ref at);
                if (at < s.Length && s[at] == ']')
                {
                    at++;
                    return list;
                }
                while (true)
                {
                    list.Add(Value(s, ref at));
                    Space(s, ref at);
                    if (at < s.Length && s[at] == ',')
                    {
                        at++;
                        continue;
                    }
                    Expect(s, ref at, ']');
                    return list;
                }
            }
            if (c == '"')
            {
                return Text(s, ref at);
            }
            if (Word(s, ref at, "true"))
            {
                return true;
            }
            if (Word(s, ref at, "false"))
            {
                return false;
            }
            if (Word(s, ref at, "null"))
            {
                return null;
            }
            int start = at;
            while (at < s.Length && "+-0123456789.eE".IndexOf(s[at]) >= 0)
            {
                at++;
            }
            if (at > start && double.TryParse(s.Substring(start, at - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                return number;
            }
            throw Error(s, start, "a value expected");
        }

        private static string Text(string s, ref int at)
        {
            at++;
            StringBuilder text = new StringBuilder();
            while (at < s.Length)
            {
                char c = s[at++];
                if (c == '"')
                {
                    return text.ToString();
                }
                if (c != '\\')
                {
                    text.Append(c);
                    continue;
                }
                if (at >= s.Length)
                {
                    break;
                }
                char e = s[at++];
                switch (e)
                {
                    case 'n': text.Append('\n'); break;
                    case 't': text.Append('\t'); break;
                    case 'r': text.Append('\r'); break;
                    case 'b': text.Append('\b'); break;
                    case 'f': text.Append('\f'); break;
                    case 'u':
                        if (at + 4 > s.Length)
                        {
                            throw Error(s, at, "a \\u escape cut short");
                        }
                        text.Append((char)Convert.ToInt32(s.Substring(at, 4), 16));
                        at += 4;
                        break;
                    default: text.Append(e); break;
                }
            }
            throw Error(s, at, "a string not closed");
        }

        private static bool Word(string s, ref int at, string word)
        {
            if (string.CompareOrdinal(s, at, word, 0, word.Length) != 0)
            {
                return false;
            }
            at += word.Length;
            return true;
        }

        private static void Expect(string s, ref int at, char c)
        {
            if (at >= s.Length || s[at] != c)
            {
                throw Error(s, at, "'" + c + "' expected");
            }
            at++;
        }

        private static void Space(string s, ref int at)
        {
            while (at < s.Length && char.IsWhiteSpace(s[at]))
            {
                at++;
            }
        }

        private static FormatException Error(string s, int at, string what)
        {
            int line = 1;
            for (int i = 0; i < at && i < s.Length; i++)
            {
                if (s[i] == '\n')
                {
                    line++;
                }
            }
            return new FormatException(what + " at line " + line);
        }
    }
}
