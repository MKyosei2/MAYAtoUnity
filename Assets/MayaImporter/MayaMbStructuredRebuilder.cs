// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase2 Step7:
    /// Use decoded chunk strings (ChunkInfo.DecodedStrings) to "confirm" node types and connections.
    /// This is still best-effort, but more deterministic than raw byte string scanning.
    /// RawBinaryBytes remains source-of-truth; this only adds structured hints.
    /// </summary>
    public static class MayaMbStructuredRebuilder
    {
        private static readonly HashSet<string> KnownNodeTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "transform","mesh","joint","camera",
            "directionalLight","pointLight","spotLight","areaLight",
            "shadingEngine","lambert","blinn","phong",
            "skinCluster","blendShape"
        };

        public static void Apply(MayaSceneData scene, MayaImportLog log)
        {
            if (scene?.MbIndex == null) return;

            // 1) Node type confirmation from decoded strings
            int typeApplied = ConfirmNodeTypes(scene);

            // 2) Connection confirmation from decoded strings (stricter than heuristic)
            int connAdded = ConfirmConnections(scene);

            if (typeApplied > 0 || connAdded > 0)
                log?.Info($".mb structured: nodeTypeApplied={typeApplied}, connectionsAdded={connAdded} (Stage: Structured Confirm).");
            else
                log?.Info(".mb structured: no additional confirmations found (still OK, raw preserved).");
        }

        private static int ConfirmNodeTypes(MayaSceneData scene)
        {
            int applied = 0;

            // Build leaf->fullPath map for already existing DAG placeholders
            var leafToFull = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var kv in scene.Nodes)
            {
                var full = kv.Key;
                var leaf = LeafOfDag(full);
                if (string.IsNullOrEmpty(leaf)) continue;

                if (!leafToFull.TryGetValue(leaf, out var list))
                {
                    list = new List<string>(2);
                    leafToFull[leaf] = list;
                }
                list.Add(full);
            }

            // Walk chunks: if we see (type, name) pair inside a decoded string list, apply it.
            var chunks = scene.MbIndex.Chunks;
            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var c = chunks[ci];
                var ds = c.DecodedStrings;
                if (ds == null || ds.Length == 0) continue;

                for (int i = 0; i < ds.Length; i++)
                {
                    var t = ds[i];
                    if (!IsTypeToken(t)) continue;

                    // find nearest plausible name token within this chunk decoded list
                    string name = null;

                    // prefer immediate next
                    if (i + 1 < ds.Length && LooksLikeNodeName(ds[i + 1])) name = ds[i + 1];

                    // else search neighbors
                    if (name == null)
                    {
                        for (int j = 0; j < ds.Length; j++)
                        {
                            if (j == i) continue;
                            if (LooksLikeNodeName(ds[j]))
                            {
                                name = ds[j];
                                break;
                            }
                        }
                    }

                    if (name == null) continue;

                    // If it's a dag path, use it directly. If leaf, apply to placeholders.
                    if (name.IndexOf('|') >= 0)
                    {
                        var full = NormalizeDag(name);
                        var rec = scene.GetOrCreateNode(full, t);
                        if (rec.NodeType == "unknown" || rec.NodeType == "transform" || string.IsNullOrEmpty(rec.NodeType))
                        {
                            rec.NodeType = t;
                            StampProvenance(rec, c);
                            applied++;
                        }
                    }
                    else
                    {
                        if (!leafToFull.TryGetValue(name, out var fulls)) continue;
                        for (int k = 0; k < fulls.Count; k++)
                        {
                            var full = fulls[k];
                            if (!scene.Nodes.TryGetValue(full, out var rec) || rec == null) continue;

                            if (rec.NodeType == "unknown" || rec.NodeType == "transform" || string.IsNullOrEmpty(rec.NodeType))
                            {
                                rec.NodeType = t;
                                StampProvenance(rec, c);
                                applied++;
                            }
                        }
                    }
                }
            }

            return applied;
        }

        private static int ConfirmConnections(MayaSceneData scene)
        {
            int added = 0;

            // Dedup existing connections
            var existing = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < scene.Connections.Count; i++)
            {
                var c = scene.Connections[i];
                if (c == null) continue;
                existing.Add($"{c.SrcPlug}->{c.DstPlug}");
            }

            // We will only add if both ends "look like" plugs and are not numeric-like
            var chunks = scene.MbIndex.Chunks;
            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var ch = chunks[ci];
                var ds = ch.DecodedStrings;
                if (ds == null || ds.Length < 2) continue;

                // In decoded strings, many chunks store sequences like ["srcPlug","dstPlug"] or similar.
                // We'll scan consecutive pairs within the chunk only (stricter than global scan).
                for (int i = 0; i < ds.Length - 1; i++)
                {
                    var a = ds[i];
                    var b = ds[i + 1];

                    if (!LooksLikePlug(a) || !LooksLikePlug(b))
                        continue;

                    var key = $"{a}->{b}";
                    if (existing.Contains(key)) continue;

                    scene.Connections.Add(new ConnectionRecord(a, b, force: false));
                    existing.Add(key);
                    added++;

                    // Save provenance for auditing
                    scene.RawStatements.Add(new RawStatement
                    {
                        LineStart = -1,
                        LineEnd = -1,
                        Command = "mbConnectStructured",
                        Text = $"connectAttr \"{a}\" \"{b}\"; // chunk {ch.Id} @ {ch.Offset}",
                        Tokens = new List<string> { "connectAttr", a, b }
                    });

                    if (added >= 20000) return added;
                }
            }

            return added;
        }

        private static void StampProvenance(NodeRecord rec, MayaBinaryIndex.ChunkInfo chunk)
        {
            if (rec.Attributes == null || chunk == null) return;

            if (!rec.Attributes.ContainsKey(".mbChunkId"))
                rec.Attributes[".mbChunkId"] = new RawAttributeValue("string", new List<string> { chunk.Id ?? "" });

            if (!rec.Attributes.ContainsKey(".mbChunkOffset"))
                rec.Attributes[".mbChunkOffset"] = new RawAttributeValue("int", new List<string> { chunk.Offset.ToString() });

            if (!rec.Attributes.ContainsKey(".mbChunkPreview") && !string.IsNullOrEmpty(chunk.Preview))
                rec.Attributes[".mbChunkPreview"] = new RawAttributeValue("string", new List<string> { chunk.Preview });
        }

        private static bool IsTypeToken(string s) => !string.IsNullOrEmpty(s) && KnownNodeTypes.Contains(s);

        private static bool LooksLikeNodeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Length < 2 || s.Length > 128) return false;
            if (s.IndexOf('/') >= 0 || s.IndexOf('\\') >= 0) return false;

            // Accept dag path or leaf
            if (s[0] == '|' && s.IndexOf('|', 1) > 0) return true;

            // leaf: identifier-like
            bool hasLetter = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetter(c)) hasLetter = true;
                if (char.IsLetterOrDigit(c)) continue;
                if (c == '_' || c == ':' || c == '-') continue;
                return false;
            }
            return hasLetter;
        }

        private static bool LooksLikePlug(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Length < 4 || s.Length > 160) return false;

            int dot = s.IndexOf('.');
            if (dot <= 0 || dot >= s.Length - 1) return false;

            // reject numeric-like "0.5"
            if (IsNumericLike(s)) return false;

            // reject file paths
            if (s.IndexOf('/') >= 0 || s.IndexOf('\\') >= 0) return false;

            // node part may be dag path or leaf
            var node = s.Substring(0, dot);
            var attr = s.Substring(dot + 1);

            return HasIdentifierChar(node) && HasIdentifierChar(attr);
        }

        private static bool HasIdentifierChar(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetter(c) || c == '_' || c == '|' || c == ':') return true;
            }
            return false;
        }

        private static bool IsNumericLike(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsDigit(c)) continue;
                if (c == '+' || c == '-' || c == '.') continue;
                if (c == 'e' || c == 'E') continue;
                return false;
            }
            return true;
        }

        private static string LeafOfDag(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int last = name.LastIndexOf('|');
            if (last >= 0 && last < name.Length - 1) return name.Substring(last + 1);
            return name;
        }

        private static string NormalizeDag(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // ensure leading |
            if (s[0] != '|') s = "|" + s;

            // strip trailing pipes
            while (s.Length > 1 && s[s.Length - 1] == '|')
                s = s.Substring(0, s.Length - 1);

            return s;
        }
    }
}
