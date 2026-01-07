// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Text;

namespace MayaImporter.Core
{
    /// <summary>
    /// Best-effort:
    /// Extract ASCII "command-like" text from .mb raw bytes and parse it with .ma parser.
    /// </summary>
    public static class MayaMbEmbeddedMaExtractor
    {
        public struct ExtractionInfo
        {
            public int CandidateSegments;
            public int StatementCount;
            public int Score;
        }

        private static readonly string[] CommandHints =
        {
            "createNode", "setAttr", "connectAttr", "disconnectAttr", "parent", "rename", "currentUnit",
            "fileInfo", "requires", "namespace", "workspace", "addAttr", "deleteAttr", "lockNode",
            "setKeyframe", "setDrivenKeyframe", "animLayer", "connectDynamic", "scriptNode", "evalDeferred", "expression",
            "select", "sets", "shadingNode", "connectAttr -f", "skinCluster", "blendShape"
        };

        /// <summary>
        /// Extract command-like text segments.
        /// Returns true if we extracted any usable text (even low confidence).
        /// Actual accept/reject is decided by ShouldParse(info, options).
        /// </summary>
        public static bool TryExtractCommandText(
            byte[] bytes,
            MayaBinaryIndex idx,
            MayaImportOptions options,
            MayaImportLog log,
            out string extractedText,
            out ExtractionInfo info)
        {
            extractedText = null;
            info = default;

            if (bytes == null || bytes.Length < 64) return false;
            options ??= new MayaImportOptions();

            int maxExtractChars = Math.Max(64 * 1024, options.MbEmbeddedAsciiMaxChars);
            const int minSegmentChars = 40;

            var outSb = new StringBuilder(256 * 1024);
            var segSb = new StringBuilder(8 * 1024);

            int segCount = 0;
            int score = 0;
            int semiCount = 0;

            // Scan bytes and collect runs of "printable ASCII-ish" characters.
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];

                if (IsAsciiTextByte(b))
                {
                    segSb.Append((char)b);
                    continue;
                }

                if (segSb.Length > 0)
                {
                    FlushSegment();
                    if (outSb.Length >= maxExtractChars) break;
                }
            }

            if (segSb.Length > 0 && outSb.Length < maxExtractChars)
                FlushSegment();

            info.CandidateSegments = segCount;
            info.Score = score;
            info.StatementCount = semiCount;

            if (outSb.Length > 0)
                extractedText = outSb.ToString();

            // gꂽh ̈Ӗ trueiM ShouldParsej
            return !string.IsNullOrWhiteSpace(extractedText);

            void FlushSegment()
            {
                var seg = segSb.ToString();
                segSb.Length = 0;

                if (seg.Length < minSegmentChars) return;

                // Must contain a semicolon to look like a .ma statement stream
                if (seg.IndexOf(';') < 0) return;

                int localHits = CountCommandHits(seg);
                if (localHits <= 0) return;

                // Normalize newlines
                seg = seg.Replace('\r', '\n');

                // Append (bounded)
                int remaining = maxExtractChars - outSb.Length;
                if (remaining <= 0) return;

                if (seg.Length + 2 > remaining)
                    seg = seg.Substring(0, Math.Max(0, remaining - 2));

                outSb.AppendLine(seg);
                segCount++;

                score += localHits;
                semiCount += CountChar(seg, ';');

                if (segCount >= 20000) return; // safety
            }
        }

        /// <summary>
        /// Decide whether we should parse extracted text as .ma statements.
        /// </summary>
        public static bool ShouldParse(in ExtractionInfo info, MayaImportOptions options)
        {
            options ??= new MayaImportOptions();

            if (info.StatementCount >= options.MbEmbeddedAsciiHardMinStatements) return true;
            if (info.Score >= options.MbEmbeddedAsciiHardMinScore) return true;

            if (info.StatementCount >= options.MbEmbeddedAsciiMinStatements &&
                info.Score >= options.MbEmbeddedAsciiMinScore)
                return true;

            return false;
        }

        private static bool IsAsciiTextByte(byte b)
        {
            // allow: tab(9), LF(10), CR(13), and visible ASCII 32..126
            return b == 9 || b == 10 || b == 13 || (b >= 32 && b <= 126);
        }

        private static int CountCommandHits(string s)
        {
            int hits = 0;
            for (int i = 0; i < CommandHints.Length; i++)
            {
                if (s.IndexOf(CommandHints[i], StringComparison.Ordinal) >= 0)
                    hits += 2;
            }
            return hits;
        }

        private static int CountChar(string s, char c)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == c) n++;
            return n;
        }
    }
}
