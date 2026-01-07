// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace MayaImporter.Core
{
    /// <summary>
    /// Loads Maya standard node types from a TextAsset in Resources if present.
    /// Expected file:
    ///   Assets/MayaImporter/Resources/Maya2026_StandardNodeTypes.txt
    /// Format:
    ///   one nodeType per line (comments allowed with #)
    ///
    /// IMPORTANT:
    /// - Do NOT cache failures forever. If the file didn't exist once, it may exist later.
    /// </summary>
    public static class MayaStandardNodeTypes
    {
        private const string ResourceName = "Maya2026_StandardNodeTypes";
        private const string ExpectedAssetPath = "Assets/MayaImporter/Resources/Maya2026_StandardNodeTypes.txt";

        private static HashSet<string> _cached; // cache only on success

        public static void InvalidateCache()
        {
            _cached = null;
        }

        public static bool TryGet(out HashSet<string> nodeTypes)
        {
            // If already loaded successfully, return it.
            if (_cached != null)
            {
                nodeTypes = _cached;
                return _cached.Count > 0;
            }

            TextAsset ta = null;

            // 1) Normal: load from any Resources folder by name (no extension).
            ta = Resources.Load<TextAsset>(ResourceName);

#if UNITY_EDITOR
            // 2) Editor fallback: load from the exact expected asset path.
            if (ta == null)
            {
                ta = AssetDatabase.LoadAssetAtPath<TextAsset>(ExpectedAssetPath);
            }

            // 3) Last fallback: try reading the disk file directly (still Editor-only).
            if (ta == null)
            {
                try
                {
                    var abs = Path.Combine(Application.dataPath, "MayaImporter/Resources/Maya2026_StandardNodeTypes.txt")
                        .Replace("\\", "/");

                    if (File.Exists(abs))
                    {
                        var text = File.ReadAllText(abs);
                        var parsed = ParseToSet(text);

                        if (parsed.Count > 0)
                        {
                            _cached = parsed;
                            nodeTypes = parsed;
                            return true;
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }
#endif

            // Not found or empty: do NOT cache failure
            if (ta == null)
            {
                nodeTypes = null;
                return false;
            }

            var set = ParseToSet(ta.text);

            // Cache only if successful & non-empty
            if (set.Count > 0)
            {
                _cached = set;
                nodeTypes = set;
                return true;
            }

            nodeTypes = set; // empty but parsed
            return false;
        }

        private static HashSet<string> ParseToSet(string text)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);

            if (string.IsNullOrEmpty(text))
                return set;

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("#", StringComparison.Ordinal)) continue;

                // allow inline comment
                var hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash).Trim();
                if (string.IsNullOrEmpty(line)) continue;

                set.Add(line);
            }

            return set;
        }
    }
}
