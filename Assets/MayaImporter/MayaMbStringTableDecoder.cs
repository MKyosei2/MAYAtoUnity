// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase-2 (Binary .mb) additive helper:
    /// Build a deterministic "string table" from the IFF index decode results.
    ///
    /// Goals:
    /// - Unity-only (no Maya/Autodesk API)
    /// - Deterministic across machines/runs
    /// - Additive only: never alters RawBinaryBytes, only populates MayaSceneData.MbStringTable
    ///
    /// This is intentionally conservative: it keeps only short ASCII-ish tokens that are
    /// plausibly useful for rebuilding (node names, attributes, types, file paths).
    /// </summary>
    public static class MayaMbStringTableDecoder
    {
        // Hard limits to keep imports fast and deterministic.
        private const int MaxTokenLen = 256;
        private const int MaxTokens = 20000;

        public static void Populate(MayaSceneData scene, MayaImportLog log)
        {
            if (scene == null || scene.MbIndex == null) return;

            var unique = new HashSet<string>(StringComparer.Ordinal);

            // 1) Global extracted strings (already deterministic order, but we still de-dup)
            var extracted = scene.MbIndex.ExtractedStrings;
            if (extracted != null)
            {
                for (int i = 0; i < extracted.Count; i++)
                    TryAdd(unique, extracted[i]);
            }

            // 2) Per-chunk decoded strings (often contains compact local context)
            var chunks = scene.MbIndex.Chunks;
            if (chunks != null)
            {
                for (int c = 0; c < chunks.Count; c++)
                {
                    var ds = chunks[c].DecodedStrings;
                    if (ds == null) continue;
                    for (int i = 0; i < ds.Length; i++)
                        TryAdd(unique, ds[i]);
                }
            }

            // Deterministic materialization
            var list = new List<string>(Math.Min(unique.Count, MaxTokens));
            foreach (var s in unique)
            {
                if (list.Count >= MaxTokens) break;
                list.Add(s);
            }
            list.Sort(StringComparer.Ordinal);

            scene.MbStringTable.Clear();
            scene.MbStringTable.AddRange(list);

            log?.Info($".mb string-table: tokens={scene.MbStringTable.Count} (additive)");
        }

        private static void TryAdd(HashSet<string> unique, string raw)
        {
            if (unique == null) return;
            if (string.IsNullOrEmpty(raw)) return;

            // Trim and normalize.
            string s = raw.Trim();
            if (s.Length == 0) return;
            if (s.Length > MaxTokenLen) s = s.Substring(0, MaxTokenLen);

            // Filter: accept only "mostly printable" tokens to avoid binary noise.
            // (Keep spaces out; Maya node/type/attr tokens are usually single words)
            int printable = 0;
            int total = s.Length;

            for (int i = 0; i < total; i++)
            {
                char ch = s[i];

                // Reject control chars quickly
                if (ch < 0x20) return;

                // Reject very uncommon high unicode (keeps this deterministic & stable for logs)
                if (ch > 0x7E) return;

                // Reject tokens with tabs/newlines
                if (ch == '\t' || ch == '\r' || ch == '\n') return;

                // Prefer single "word-like" tokens, but allow typical Maya punctuation
                // such as '|', ':', '.', '/', '\\', '-', '@'
                if (IsWordy(ch)) printable++;
                else if (IsAllowedPunct(ch)) printable++;
                else
                {
                    // If there is anything truly weird, drop the token.
                    return;
                }
            }

            // Must contain at least 2 printable chars and at least one letter/digit/underscore.
            if (printable < 2) return;

            bool hasCore = false;
            for (int i = 0; i < total; i++)
            {
                char ch = s[i];
                if (char.IsLetterOrDigit(ch) || ch == '_') { hasCore = true; break; }
            }
            if (!hasCore) return;

            unique.Add(s);
        }

        private static bool IsWordy(char ch)
            => (ch >= 'a' && ch <= 'z')
            || (ch >= 'A' && ch <= 'Z')
            || (ch >= '0' && ch <= '9')
            || ch == '_';

        private static bool IsAllowedPunct(char ch)
            => ch == '|' || ch == ':' || ch == '.' || ch == '/' || ch == '\\'
            || ch == '-' || ch == '@' || ch == '#' || ch == '$'
            || ch == '[' || ch == ']' || ch == '{' || ch == '}' || ch == ','
            || ch == '+' || ch == '*';
    }
}
