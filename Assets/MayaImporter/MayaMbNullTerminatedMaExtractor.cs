// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Text;

namespace MayaImporter.Core
{
    /// <summary>
    /// Best-effort text recovery for Maya Binary (.mb) without Maya/Autodesk API.
    ///
    /// Some .mb files contain many null-terminated ASCII strings (node names, attribute names, flags, etc.).
    /// This extractor:
    /// 1) Collects likely strings.
    /// 2) Finds command anchors (createNode/setAttr/connectAttr/etc.).
    /// 3) Reconstructs MEL-like statements by concatenating neighboring tokens.
    ///
    /// This is intentionally heuristic and is used only as an additive coverage path.
    /// RawBinaryBytes remain the source of truth and are always preserved.
    /// </summary>
    public static class MayaMbNullTerminatedMaExtractor
    {
        private static readonly string[] CommandAnchors =
        {
            "createNode",
            "setAttr",
            "connectAttr",
            "disconnectAttr",
            "addAttr",
            "deleteAttr",
            "lockNode",
            "parent",
            "rename",
            "currentUnit",
            "fileInfo",
            "requires",
            "namespace",
            "workspace",
            "select",
            "sets",
            "shadingNode",
            "skinCluster",
            "blendShape",
            "setKeyframe",
            "setDrivenKeyframe",
            "animLayer",
            "connectDynamic",
            "scriptNode",
            "evalDeferred",
            "expression"
        };

        public static bool TryReconstructCommandText(
            byte[] bytes,
            MayaImportOptions options,
            MayaImportLog log,
            out string reconstructedText,
            out int statementCount,
            out int score)
        {
            reconstructedText = null;
            statementCount = 0;
            score = 0;

            if (bytes == null || bytes.Length < 64) return false;

            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            // Tokenize likely strings
            var tokens = ExtractNullTerminatedStrings(bytes, maxTokens: 2_000_000, maxTokenLen: 256);
            if (tokens.Count == 0) return false;

            var anchorSet = new HashSet<string>(CommandAnchors, StringComparer.Ordinal);

            int maxStatements = Math.Max(1000, options.MbNullTerminatedMaxStatements);
            var sb = new StringBuilder(256 * 1024);

            // Walk the token stream and build statements whenever we hit a known anchor.
            int i = 0;
            while (i < tokens.Count && statementCount < maxStatements)
            {
                if (!anchorSet.Contains(tokens[i]))
                {
                    i++;
                    continue;
                }

                int start = i;
                int end = FindNextAnchor(tokens, start + 1, anchorSet, maxLookahead: 4096);
                if (end <= start) end = Math.Min(tokens.Count, start + 64);

                // Build line
                var line = new StringBuilder(256);
                int localScore = 0;

                for (int j = start; j < end && j < tokens.Count; j++)
                {
                    string t = tokens[j];
                    if (string.IsNullOrEmpty(t)) continue;

                    if (line.Length > 0) line.Append(' ');
                    line.Append(t);

                    // scoring heuristics
                    if (t == "-n" || t == "-p" || t == "-type" || t == "-f" || t == "-l" || t == "-k")
                        localScore += 1;
                    if (t.StartsWith(".", StringComparison.Ordinal))
                        localScore += 1;
                    if (t.IndexOf(';') >= 0)
                        localScore += 2;

                    if (line.Length > 2048) break;
                }

                var lineStr = line.ToString().Trim();
                if (lineStr.Length > 0)
                {
                    if (lineStr.IndexOf(';') < 0) lineStr += ";";
                    sb.AppendLine(lineStr);
                    statementCount++;
                    score += 4 + localScore;
                }

                i = Math.Max(i + 1, end);
            }

            reconstructedText = sb.ToString();
            if (string.IsNullOrWhiteSpace(reconstructedText) || statementCount <= 0)
                return false;

            return true;
        }

        private static int FindNextAnchor(List<string> tokens, int start, HashSet<string> anchors, int maxLookahead)
        {
            int end = Math.Min(tokens.Count, start + Math.Max(32, maxLookahead));
            for (int i = start; i < end; i++)
            {
                if (anchors.Contains(tokens[i])) return i;
            }
            return tokens.Count;
        }

        private static List<string> ExtractNullTerminatedStrings(byte[] bytes, int maxTokens, int maxTokenLen)
        {
            var outTokens = new List<string>(Math.Min(1024 * 64, maxTokens));

            var sb = new StringBuilder(256);
            int emitted = 0;

            // Treat 0 byte as delimiter. Keep only tokens made of printable ASCII.
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];

                if (b == 0)
                {
                    Flush();
                    if (emitted >= maxTokens) break;
                    continue;
                }

                if (IsAsciiTextByte(b))
                {
                    if (sb.Length < maxTokenLen)
                        sb.Append((char)b);
                    continue;
                }

                // Non-ASCII: break token.
                Flush();
                if (emitted >= maxTokens) break;
            }

            Flush();
            return outTokens;

            void Flush()
            {
                if (sb.Length == 0) return;

                string s = sb.ToString().Trim();
                sb.Length = 0;

                // Basic filtering
                if (s.Length < 2) return;
                if (s.Length > maxTokenLen) return;

                // Avoid very long numeric blobs
                int digit = 0;
                for (int k = 0; k < s.Length; k++)
                    if (char.IsDigit(s[k])) digit++;
                if (digit >= s.Length - 1 && s.Length >= 8) return;

                outTokens.Add(s);
                emitted++;
            }
        }

        private static bool IsAsciiTextByte(byte b)
        {
            // allow: tab(9), LF(10), CR(13), and visible ASCII 32..126
            return b == 9 || b == 10 || b == 13 || (b >= 32 && b <= 126);
        }
    }
}
