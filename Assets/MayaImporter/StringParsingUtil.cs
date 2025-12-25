using System;
using System.Collections.Generic;
using System.Text;

namespace MayaImporter.Utils
{
    /// <summary>
    /// Maya .ma トークンを扱うための文字列ユーティリティ。
    /// Tokenizer は quotes を除去しているが、数値末尾のカンマ/括弧等が残る場合があるため正規化する。
    /// </summary>
    public static class StringParsingUtil
    {
        /// <summary>
        /// 末尾の ',' ';' ')' ']' '}' などを削り、先頭の '(' '[' '{' なども削る。
        /// </summary>
        public static string CleanNumericToken(string s)
        {
            if (s == null) return string.Empty;
            s = s.Trim();
            if (s.Length == 0) return s;

            // 先頭側
            int start = 0;
            while (start < s.Length)
            {
                char c = s[start];
                if (c == '(' || c == '[' || c == '{') { start++; continue; }
                break;
            }

            // 末尾側
            int end = s.Length - 1;
            while (end >= start)
            {
                char c = s[end];
                if (c == ',' || c == ';' || c == ')' || c == ']' || c == '}')
                {
                    end--;
                    continue;
                }
                break;
            }

            if (start == 0 && end == s.Length - 1) return s;
            if (end < start) return string.Empty;
            return s.Substring(start, end - start + 1);
        }

        public static bool IsNullOrEmpty(string s) => string.IsNullOrEmpty(s);

        public static string NullIfEmpty(string s)
            => string.IsNullOrEmpty(s) ? null : s;

        /// <summary>
        /// Maya のパス表現（バックスラッシュ/スラッシュ混在）を Unity で扱いやすくする。
        /// </summary>
        public static string NormalizeSlashes(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.Replace('\\', '/');
        }

        /// <summary>
        /// Maya のノード名/パスは | や : を含む。プラグ文字列 "node.attr" から node と attr を分ける。
        /// attr 側は '.' を含む場合がある（compound）ので、最初の '.' で分割する。
        /// </summary>
        public static bool TrySplitPlug(string plug, out string nodeName, out string attrPath)
        {
            nodeName = null;
            attrPath = null;
            if (string.IsNullOrEmpty(plug)) return false;

            int dot = plug.IndexOf('.');
            if (dot <= 0 || dot >= plug.Length - 1) return false;

            nodeName = plug.Substring(0, dot);
            attrPath = plug.Substring(dot + 1);
            return true;
        }

        public static string JoinNonEmpty(string sep, params string[] parts)
        {
            if (parts == null || parts.Length == 0) return string.Empty;
            var sb = new StringBuilder();
            bool first = true;
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (string.IsNullOrEmpty(p)) continue;
                if (!first) sb.Append(sep);
                sb.Append(p);
                first = false;
            }
            return sb.ToString();
        }

        public static List<string> CopyTokens(IReadOnlyList<string> tokens)
        {
            var list = new List<string>(tokens == null ? 0 : tokens.Count);
            if (tokens == null) return list;
            for (int i = 0; i < tokens.Count; i++) list.Add(tokens[i]);
            return list;
        }
    }
}
