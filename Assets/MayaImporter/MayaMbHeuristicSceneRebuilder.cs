using System;
using System.Collections.Generic;
using System.Globalization;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase2 Step4:
    /// Heuristic reconstruction for .mb without Maya:
    /// - Use MbIndex.ExtractedStrings to detect DAG-like paths (e.g. "|grp|node|shape").
    /// - Create placeholder NodeRecords (transform/mesh) with ParentName relationships.
    /// - Keep RawBinaryBytes as the source of truth (no data drop).
    /// </summary>
    public static class MayaMbHeuristicSceneRebuilder
    {
        public static void PopulatePlaceholdersIfEmpty(MayaSceneData scene, MayaImportLog log)
        {
            if (scene == null) return;
            if (scene.MbIndex == null) return;

            // If we already have nodes (future real parser), do nothing.
            if (scene.Nodes != null && scene.Nodes.Count > 0) return;

            var paths = new HashSet<string>(StringComparer.Ordinal);

            var strings = scene.MbIndex.ExtractedStrings;
            if (strings != null)
            {
                for (int i = 0; i < strings.Count; i++)
                {
                    var s = strings[i];
                    if (TryNormalizeDagPathCandidate(s, out var p))
                        paths.Add(p);
                }
            }

            if (paths.Count == 0)
            {
                log?.Warn(".mb heuristic: no DAG-like paths found in extracted strings. Keeping raw only.");
                return;
            }

            int created = 0;
            foreach (var p in paths)
                created += EnsurePath(scene, p);

            log?.Info($".mb heuristic: created/updated {created} placeholder nodes from {paths.Count} DAG path candidates.");
        }

        private static int EnsurePath(MayaSceneData scene, string dagPath)
        {
            // dagPath normalized: starts with "|" and has at least one segment.
            var segs = SplitDagSegments(dagPath);
            if (segs.Count == 0) return 0;

            int createdOrUpdated = 0;

            string parentFull = null;
            string full = null;

            for (int i = 0; i < segs.Count; i++)
            {
                var seg = segs[i];

                // build full path key: |a|b|c
                full = full == null ? "|" + seg : full + "|" + seg;

                bool exists = scene.Nodes.TryGetValue(full, out var rec);

                var type = GuessNodeType(seg, i == segs.Count - 1);
                rec = scene.GetOrCreateNode(full, type);

                // Ensure ParentName
                if (!string.Equals(rec.ParentName, parentFull, StringComparison.Ordinal))
                    rec.ParentName = parentFull;

                // Also store leaf/original as attributes (optional, helps debugging)
                // keys must start with '.'
                if (rec.Attributes != null)
                {
                    if (!rec.Attributes.ContainsKey(".mbLeaf"))
                        rec.Attributes[".mbLeaf"] = new RawAttributeValue("string", new List<string> { seg });
                    if (!rec.Attributes.ContainsKey(".mbDagPath"))
                        rec.Attributes[".mbDagPath"] = new RawAttributeValue("string", new List<string> { full });
                }

                if (!exists) createdOrUpdated++;

                parentFull = full;
            }

            return createdOrUpdated;
        }

        private static string GuessNodeType(string seg, bool isLast)
        {
            // Very conservative. We only need "is DAG" right now.
            // Shape-like => mesh, else transform.
            if (!string.IsNullOrEmpty(seg) && seg.EndsWith("Shape", StringComparison.Ordinal))
                return "mesh";

            // If it looks like a joint name, still keep transform-ish DAG
            // (proper joint parsing later)
            if (!string.IsNullOrEmpty(seg) && seg.IndexOf("joint", StringComparison.OrdinalIgnoreCase) >= 0)
                return "joint";

            return "transform";
        }

        private static List<string> SplitDagSegments(string dagPath)
        {
            var list = new List<string>(8);
            if (string.IsNullOrEmpty(dagPath)) return list;

            // Normalize: "|a|b|c" -> ["a","b","c"]
            int i = 0;
            while (i < dagPath.Length)
            {
                while (i < dagPath.Length && dagPath[i] == '|') i++;
                if (i >= dagPath.Length) break;

                int start = i;
                while (i < dagPath.Length && dagPath[i] != '|') i++;

                var seg = dagPath.Substring(start, i - start);
                if (IsValidDagSegment(seg))
                    list.Add(seg);
            }
            return list;
        }

        private static bool TryNormalizeDagPathCandidate(string s, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrEmpty(s)) return false;

            s = s.Trim();
            if (s.Length < 5 || s.Length > 160) return false;
            if (s.IndexOf('|') < 0) return false;

            // Avoid file paths / URLs / obvious noise
            if (s.IndexOf(' ') >= 0 || s.IndexOf('\t') >= 0) return false;
            if (s.IndexOf('/') >= 0 || s.IndexOf('\\') >= 0) return false;
            if (s.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.IndexOf(":", StringComparison.Ordinal) >= 0 && s.IndexOf('|') < s.IndexOf(':')) return false; // "C:..."-like

            // Reduce noise: reject '.' heavily (often file ext / numeric)
            if (s.IndexOf('.') >= 0) return false;

            // Must contain at least two segments: "|a|b"
            // Normalize to start with '|'
            if (s[0] != '|')
            {
                // sometimes extracted as "a|b|c"
                int first = s.IndexOf('|');
                if (first <= 0) return false;
                s = "|" + s; // best-effort
            }

            // Validate segments
            var segs = SplitDagSegments(s);
            if (segs.Count < 2) return false;

            // Rebuild clean normalized form
            var clean = "|" + string.Join("|", segs);
            normalized = clean;
            return true;
        }

        private static bool IsValidDagSegment(string seg)
        {
            if (string.IsNullOrEmpty(seg)) return false;
            if (seg.Length > 80) return false;

            // Must contain at least one letter (reduce numeric garbage)
            bool hasLetter = false;

            for (int i = 0; i < seg.Length; i++)
            {
                char c = seg[i];

                if (char.IsLetter(c)) hasLetter = true;

                // allow Maya-ish identifiers
                if (char.IsLetterOrDigit(c)) continue;
                if (c == '_' || c == ':' || c == '-') continue;

                // reject others
                return false;
            }

            return hasLetter;
        }
    }
}
