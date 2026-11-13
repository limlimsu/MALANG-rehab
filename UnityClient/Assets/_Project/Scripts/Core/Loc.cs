using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Malang.Core
{
    /// <summary>
    /// 다국어 문자열 사전. Resources/Loc/strings_{lang}.json ({"key": "value", ...}) 을 읽는다.
    /// 언어가 바뀌면 Changed 이벤트로 모든 LocText 가 갱신된다.
    /// </summary>
    public static class Loc
    {
        public const string Korean = "ko";
        public const string English = "en";
        private const string ResourceFormat = "Loc/strings_{0}";

        private static Dictionary<string, string> table;
        private static readonly HashSet<string> warnedKeys = new HashSet<string>();

        public static string Language { get; private set; } = Korean;
        public static bool IsEnglish => Language == English;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            table = null;
            warnedKeys.Clear();
            Language = Korean;
            Changed = null;
        }

        public static void SetLanguage(string language)
        {
            if (language != Korean && language != English)
            {
                Debug.LogWarning("Loc: 지원하지 않는 언어 '" + language + "', ko 로 대체합니다.");
                language = Korean;
            }

            Dictionary<string, string> loaded = Load(language);
            if (loaded == null)
                return;

            table = loaded;
            Language = language;
            warnedKeys.Clear();
            Changed?.Invoke();
        }

        public static void Toggle()
        {
            SetLanguage(IsEnglish ? Korean : English);
        }

        public static string T(string key)
        {
            if (table == null)
                table = Load(Language) ?? new Dictionary<string, string>();

            string value;
            if (table.TryGetValue(key, out value))
                return value;

            if (warnedKeys.Add(key))
                Debug.LogWarning("Loc: '" + Language + "' 에 키가 없습니다: " + key);

            return key;
        }

        public static string F(string key, params object[] args)
        {
            string format = T(key);

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                Debug.LogWarning("Loc: 형식 문자열 오류 " + key + " = " + format);
                return format;
            }
        }

        private static Dictionary<string, string> Load(string language)
        {
            TextAsset asset = Resources.Load<TextAsset>(string.Format(ResourceFormat, language));
            if (asset == null)
            {
                Debug.LogError("Loc: Resources/" + string.Format(ResourceFormat, language) + ".json 이 없습니다.");
                return null;
            }

            try
            {
                return FlatJson.Parse(asset.text);
            }
            catch (FormatException e)
            {
                Debug.LogError("Loc: " + asset.name + " 파싱 실패: " + e.Message);
                return null;
            }
        }
    }

    /// <summary>문자열 값만 있는 평평한 JSON 객체 파서 (JsonUtility 는 사전을 못 읽는다).</summary>
    public static class FlatJson
    {
        public static Dictionary<string, string> Parse(string json)
        {
            var result = new Dictionary<string, string>();
            int i = 0;

            SkipWhitespace(json, ref i);
            Expect(json, ref i, '{');

            while (true)
            {
                SkipWhitespace(json, ref i);
                if (Peek(json, i) == '}')
                {
                    i++;
                    break;
                }

                string key = ReadString(json, ref i);
                SkipWhitespace(json, ref i);
                Expect(json, ref i, ':');
                SkipWhitespace(json, ref i);
                string value = ReadString(json, ref i);
                result[key] = value;

                SkipWhitespace(json, ref i);
                char c = Peek(json, i);
                i++;

                if (c == ',')
                    continue;
                if (c == '}')
                    break;

                throw new FormatException("',' 또는 '}' 가 필요합니다 (위치 " + (i - 1) + ")");
            }

            return result;
        }

        private static char Peek(string s, int i)
        {
            if (i >= s.Length)
                throw new FormatException("JSON 이 중간에 끝났습니다.");
            return s[i];
        }

        private static void Expect(string s, ref int i, char c)
        {
            if (Peek(s, i) != c)
                throw new FormatException("'" + c + "' 가 필요합니다 (위치 " + i + ")");
            i++;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '﻿'))
                i++;
        }

        private static string ReadString(string s, ref int i)
        {
            Expect(s, ref i, '"');
            var sb = new StringBuilder();

            while (true)
            {
                char c = Peek(s, i++);

                if (c == '"')
                    return sb.ToString();

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                char e = Peek(s, i++);
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length)
                            throw new FormatException("잘못된 \\u 이스케이프");
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default:
                        throw new FormatException("알 수 없는 이스케이프 \\" + e);
                }
            }
        }
    }
}
