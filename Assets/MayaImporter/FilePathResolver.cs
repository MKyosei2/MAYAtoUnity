using System;
using System.Collections.Generic;
using System.IO;

namespace MayaImporter.Utils
{
    /// <summary>
    /// Maya の file ノード等から来るパスを、Unityのみ環境で解決するためのユーティリティ。
    /// Maya/Autodesk API なし前提：環境変数、相対パス、探索ルートで解決する。
    /// </summary>
    public static class FilePathResolver
    {
        public static string ResolvePath(
            string mayaPathToken,
            string mayaScenePath,
            IEnumerable<string> searchRoots = null)
        {
            if (string.IsNullOrEmpty(mayaPathToken)) return null;

            // $VAR / ${VAR} 展開
            string p = ExpandEnvVars(mayaPathToken);

            // スラッシュ正規化
            p = StringParsingUtil.NormalizeSlashes(p);

            // すでに絶対パス
            if (Path.IsPathRooted(p) && File.Exists(p))
                return p;

            // .ma/.mb のあるディレクトリから相対解決
            var sceneDir = (!string.IsNullOrEmpty(mayaScenePath) && Path.IsPathRooted(mayaScenePath))
                ? Path.GetDirectoryName(mayaScenePath)
                : null;

            if (!string.IsNullOrEmpty(sceneDir))
            {
                var candidate = Path.GetFullPath(Path.Combine(sceneDir, p));
                if (File.Exists(candidate)) return candidate;
            }

            // searchRoots で探索
            if (searchRoots != null)
            {
                foreach (var root in searchRoots)
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    var rr = ExpandEnvVars(root);
                    rr = StringParsingUtil.NormalizeSlashes(rr);

                    try
                    {
                        var candidate = Path.GetFullPath(Path.Combine(rr, p));
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch { /* ignore */ }
                }
            }

            // 最後：そのまま返す（呼び出し側で扱う）
            return p;
        }

        public static string ExpandEnvVars(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            // ${VAR}
            s = ExpandBraceEnv(s);

            // $VAR
            s = ExpandDollarEnv(s);

            return s;
        }

        private static string ExpandBraceEnv(string s)
        {
            int idx = 0;
            while (idx < s.Length)
            {
                int open = s.IndexOf("${", idx, StringComparison.Ordinal);
                if (open < 0) break;
                int close = s.IndexOf("}", open + 2, StringComparison.Ordinal);
                if (close < 0) break;

                string name = s.Substring(open + 2, close - (open + 2));
                string val = Environment.GetEnvironmentVariable(name) ?? string.Empty;
                s = s.Substring(0, open) + val + s.Substring(close + 1);
                idx = open + val.Length;
            }
            return s;
        }

        private static string ExpandDollarEnv(string s)
        {
            // 単純版：$ の直後を [A-Za-z0-9_] の連続として読む
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] != '$') { i++; continue; }
                int start = i + 1;
                if (start >= s.Length) break;

                int end = start;
                while (end < s.Length)
                {
                    char c = s[end];
                    bool ok = (c >= 'a' && c <= 'z') ||
                              (c >= 'A' && c <= 'Z') ||
                              (c >= '0' && c <= '9') ||
                              (c == '_');
                    if (!ok) break;
                    end++;
                }

                if (end == start) { i++; continue; }

                string name = s.Substring(start, end - start);
                string val = Environment.GetEnvironmentVariable(name) ?? string.Empty;
                s = s.Substring(0, i) + val + s.Substring(end);
                i = i + val.Length;
            }
            return s;
        }
    }
}
