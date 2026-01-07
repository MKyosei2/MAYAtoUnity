// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MayaImporter.Core
{
    /// <summary>
    /// Unity-only deterministic node enumerator for Maya .mb:
    /// - Uses extracted string table (DAG-like paths) to create stable NodeRecords when
    ///   embedded ASCII command recovery is unavailable/insufficient.
    /// - Additive only: never removes or overwrites existing nodes.
    /// - Marks provenance for Phase6 audit.
    /// </summary>
    public static class MayaMbDeterministicNodeEnumerator
    {
        private static readonly Regex RxSimpleName =
            new Regex(@"^[A-Za-z_][A-Za-z0-9_:]*$", RegexOptions.Compiled);

        private static readonly Regex RxCreateNode =
            // NOTE: verbatim string literal => double quotes are escaped as "" (not \" ).
            new Regex(@"\bcreateNode\s+(?<type>\S+)\s+.*?\s-\s*n\s+""(?<name>[^""]+)""(?:(?:\s+-p\s+""(?<parent>[^""]+)""))?.*?;",
                RegexOptions.Compiled | RegexOptions.Singleline);

        public struct Result
        {
            public bool DidRun;
            public int ConsideredDagPaths;
            public int AcceptedDagPaths;
            public int CreatedNodes;
            public int SkippedExisting;
        }

        public static Result Populate(MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            var r = new Result();

            if (scene == null || scene.SourceKind != MayaSourceKind.BinaryMb)
                return r;

            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            if (!options.MbDeterministicEnumerateNodes)
                return r;

            r.DidRun = true;

            int maxNodes = Math.Max(0, options.MbDeterministicMaxNodes);
            int maxDag = Math.Max(0, options.MbDeterministicMaxDagPaths);

            // 1) If we have command-like extracted text, prefer explicit createNode name/type.
            // (This is additive and only fills gaps; actual command parse/merge remains the main path.)
            if (!string.IsNullOrEmpty(scene.MbExtractedAsciiText))
            {
                r.CreatedNodes += CreateFromCreateNodeStatements(scene, scene.MbExtractedAsciiText, MayaNodeProvenance.MbEmbeddedAscii, maxNodes, log);
            }

            // Null-terminated reconstruction is parsed/merged separately; we still allow a light pass if no nodes exist.
            if (!string.IsNullOrEmpty(scene.MbExtractedAsciiText) == false && scene.MbNullTerminatedStatementCount > 0 && scene.Nodes.Count == 0)
            {
                // no-op (kept intentionally; reconstruction happens earlier in pipeline)
            }

            if (maxNodes > 0 && r.CreatedNodes >= maxNodes)
                return r;

            // 2) Deterministic DAG-like path expansion from string table
            var strings = scene.MbStringTable;
            if (strings == null || strings.Count == 0)
                return r;

            var unique = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < strings.Count; i++)
            {
                if (maxDag > 0 && r.ConsideredDagPaths >= maxDag) break;

                var raw = strings[i];
                if (!LooksLikeDagPath(raw)) continue;

                r.ConsideredDagPaths++;

                if (!TryNormalizeDagPath(raw, out var norm)) continue;
                if (!unique.Add(norm)) continue;

                r.AcceptedDagPaths++;

                // Build parent chain: |a , |a|b , |a|b|c
                string parent = null;
                var segs = SplitDagSegments(norm);
                if (segs.Count == 0) continue;

                string cur = "";
                for (int si = 0; si < segs.Count; si++)
                {
                    if (maxNodes > 0 && r.CreatedNodes >= maxNodes)
                        return r;

                    var seg = segs[si];
                    cur = cur.Length == 0 ? $"|{seg}" : $"{cur}|{seg}";

                    bool created = EnsureNode(scene, cur, InferTypeForSeg(seg, isLeaf: si == segs.Count - 1), parent);
                    if (created)
                    {
                        r.CreatedNodes++;
                        Mark(scene, cur, "stringTableDagPath", norm);
                    }
                    else
                    {
                        r.SkippedExisting++;
                    }

                    parent = cur;
                }
            }

            log?.Info($".mb deterministic enumerate: created={r.CreatedNodes}, acceptedDag={r.AcceptedDagPaths}, skippedExisting={r.SkippedExisting}");
            return r;
        }

        private static int CreateFromCreateNodeStatements(MayaSceneData scene, string text, MayaNodeProvenance provenance, int maxNodes, MayaImportLog log)
        {
            int created = 0;
            try
            {
                foreach (Match m in RxCreateNode.Matches(text))
                {
                    if (maxNodes > 0 && created >= maxNodes) break;

                    var type = m.Groups["type"].Value ?? "unknown";
                    var name = m.Groups["name"].Value ?? "";
                    var parent = m.Groups["parent"].Success ? m.Groups["parent"].Value : null;

                    if (string.IsNullOrEmpty(name)) continue;

                    bool didCreate = EnsureNode(scene, name, type, parent);
                    if (didCreate)
                    {
                        created++;
                        Mark(scene, name, "createNode", name);
                        var n = scene.Nodes[name];
                        n.Provenance = provenance;
                        if (string.IsNullOrEmpty(n.ProvenanceDetail)) n.ProvenanceDetail = "createNode";
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Warn($".mb deterministic createNode scan failed (continue): {ex.GetType().Name}: {ex.Message}");
            }
            return created;
        }

        private static bool LooksLikeDagPath(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return s.IndexOf('|') >= 0;
        }

        private static bool TryNormalizeDagPath(string raw, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrEmpty(raw)) return false;

            var s = raw.Trim();

            if (s.Length >= 2 && ((s[0] == '"' && s[s.Length - 1] == '"') || (s[0] == '\'' && s[s.Length - 1] == '\'')))
                s = s.Substring(1, s.Length - 2);

            s = s.Trim();
            if (s.Length < 3) return false;
            if (s[0] != '|') return false;
            if (s.IndexOf(' ') >= 0 || s.IndexOf('\t') >= 0 || s.IndexOf('\r') >= 0 || s.IndexOf('\n') >= 0) return false;

            while (s.Length > 1 && s[s.Length - 1] == '|') s = s.Substring(0, s.Length - 1);

            var segs = SplitDagSegments(s);
            if (segs.Count < 2) return false;

            for (int i = 0; i < segs.Count; i++)
            {
                var seg = segs[i];
                if (seg.Length == 0 || seg.Length > 128) return false;
                if (!RxSimpleName.IsMatch(seg)) return false;
            }

            normalized = "|" + string.Join("|", segs);
            return true;
        }

        private static List<string> SplitDagSegments(string p)
        {
            var segs = new List<string>(8);
            if (string.IsNullOrEmpty(p)) return segs;

            int start = 0;
            for (int i = 0; i < p.Length; i++)
            {
                if (p[i] == '|')
                {
                    if (i > start)
                    {
                        var seg = p.Substring(start, i - start);
                        if (seg.Length > 0) segs.Add(seg);
                    }
                    start = i + 1;
                }
            }

            if (start < p.Length)
            {
                var seg = p.Substring(start);
                if (seg.Length > 0) segs.Add(seg);
            }

            return segs;
        }

        private static string InferTypeForSeg(string seg, bool isLeaf)
        {
            if (!isLeaf) return "transform";
            if (seg.EndsWith("Shape", StringComparison.Ordinal)) return "mesh";
            return "transform";
        }

        private static bool EnsureNode(MayaSceneData scene, string nodeName, string nodeType, string parentName)
        {
            if (scene.Nodes.TryGetValue(nodeName, out var existing) && existing != null)
            {
                if (!string.IsNullOrEmpty(nodeType) && (existing.NodeType == null || existing.NodeType == "unknown"))
                    existing.NodeType = nodeType;
                if (string.IsNullOrEmpty(existing.ParentName) && !string.IsNullOrEmpty(parentName))
                    existing.ParentName = parentName;
                return false;
            }

            var n = scene.GetOrCreateNode(nodeName, nodeType ?? "transform");
            if (n != null && string.IsNullOrEmpty(n.ParentName))
                n.ParentName = parentName;

            // provenance
            n.Provenance = MayaNodeProvenance.MbDeterministicStringTable;
            if (string.IsNullOrEmpty(n.ProvenanceDetail)) n.ProvenanceDetail = "stringTable";

            return true;
        }

        private static void Mark(MayaSceneData scene, string nodeName, string src, string dagPath)
        {
            SetBoolAttr(scene, nodeName, ".mbDeterministic", true);
            SetStringAttr(scene, nodeName, ".mbDeterministicSource", src ?? "");
            SetStringAttr(scene, nodeName, ".mbDeterministicDagPath", dagPath ?? "");
        }

        private static void SetBoolAttr(MayaSceneData scene, string nodeName, string attr, bool value)
        {
            if (scene == null) return;
            if (!scene.Nodes.TryGetValue(nodeName, out var n) || n == null) return;

            var rav = new RawAttributeValue("bool", new List<string> { value ? "true" : "false" })
            {
                Kind = MayaAttrValueKind.Bool,
                ParsedValue = value
            };
            n.Attributes[attr] = rav;
        }

        private static void SetStringAttr(MayaSceneData scene, string nodeName, string attr, string value)
        {
            if (scene == null) return;
            if (!scene.Nodes.TryGetValue(nodeName, out var n) || n == null) return;

            // Project enum does not contain single "String" kind; keep tokens with typeName="string".
            n.Attributes[attr] = new RawAttributeValue("string", new List<string> { value ?? "" });
        }
    }
}
