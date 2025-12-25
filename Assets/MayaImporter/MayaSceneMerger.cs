using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Merges parsed data from a "more structured" scene into a base scene.
    /// Used for .mb embedded-ma extraction: tempScene (from extracted text) -> base .mb scene.
    /// Policy:
    /// - Never overwrite RawBinaryBytes / SourceKind / RawSha256 of base scene.
    /// - Merge nodes/connections additively.
    /// - Upgrade NodeType/Parent/Uuid only if base is missing/unknown.
    /// - For attributes: add missing keys; if base has Tokens-only but incoming has ParsedValue => upgrade.
    /// </summary>
    public static class MayaSceneMerger
    {
        public static void MergeInto(MayaSceneData baseScene, MayaSceneData incoming, MayaImportLog log, string provenanceTag)
        {
            if (baseScene == null || incoming == null) return;

            int nodesAdded = 0;
            int nodesUpgraded = 0;
            int attrsAdded = 0;
            int attrsUpgraded = 0;
            int connsAdded = 0;

            // ---- Nodes ----
            foreach (var kv in incoming.Nodes)
            {
                var name = kv.Key;
                var src = kv.Value;
                if (src == null) continue;

                if (!baseScene.Nodes.TryGetValue(name, out var dst) || dst == null)
                {
                    // create new node
                    dst = new NodeRecord(src.Name, src.NodeType)
                    {
                        ParentName = src.ParentName,
                        Uuid = src.Uuid
                    };

                    // copy attributes
                    foreach (var av in src.Attributes)
                        dst.Attributes[av.Key] = CloneAttr(av.Value);

                    // copy structured per-node lists (shallow copy is ok; treated as immutable)
                    dst.AddAttr.AddRange(src.AddAttr);
                    dst.DeleteAttr.AddRange(src.DeleteAttr);
                    dst.LockOps.AddRange(src.LockOps);

                    baseScene.Nodes[name] = dst;
                    nodesAdded++;

                    StampNodeProvenance(dst, provenanceTag);
                    continue;
                }

                // upgrade node type/parent/uuid if base missing
                if ((string.IsNullOrEmpty(dst.NodeType) || dst.NodeType == "unknown" || dst.NodeType == "transform")
                    && !string.IsNullOrEmpty(src.NodeType) && src.NodeType != "unknown")
                {
                    dst.NodeType = src.NodeType;
                    nodesUpgraded++;
                }

                if (string.IsNullOrEmpty(dst.ParentName) && !string.IsNullOrEmpty(src.ParentName))
                {
                    dst.ParentName = src.ParentName;
                    nodesUpgraded++;
                }

                if (string.IsNullOrEmpty(dst.Uuid) && !string.IsNullOrEmpty(src.Uuid))
                {
                    dst.Uuid = src.Uuid;
                    nodesUpgraded++;
                }

                // merge attributes
                foreach (var av in src.Attributes)
                {
                    if (!dst.Attributes.TryGetValue(av.Key, out var existing) || existing == null)
                    {
                        dst.Attributes[av.Key] = CloneAttr(av.Value);
                        attrsAdded++;
                        continue;
                    }

                    // upgrade parsed value if base is token-only
                    if (existing.Kind == MayaAttrValueKind.Tokens && existing.ParsedValue == null)
                    {
                        var incomingAttr = av.Value;
                        if (incomingAttr != null && incomingAttr.Kind != MayaAttrValueKind.Tokens && incomingAttr.ParsedValue != null)
                        {
                            dst.Attributes[av.Key] = CloneAttr(incomingAttr);
                            attrsUpgraded++;
                        }
                    }
                }

                StampNodeProvenance(dst, provenanceTag);
            }

            // ---- Connections ----
            var existingConn = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < baseScene.Connections.Count; i++)
            {
                var c = baseScene.Connections[i];
                if (c == null) continue;
                existingConn.Add($"{c.SrcPlug}->{c.DstPlug}");
            }

            for (int i = 0; i < incoming.Connections.Count; i++)
            {
                var c = incoming.Connections[i];
                if (c == null) continue;

                var key = $"{c.SrcPlug}->{c.DstPlug}";
                if (existingConn.Contains(key)) continue;

                baseScene.Connections.Add(new ConnectionRecord(c.SrcPlug, c.DstPlug, c.Force));
                existingConn.Add(key);
                connsAdded++;
            }

            // ---- Structured scene-level lists (append; dedupe lightly) ----
            MergeFileInfo(baseScene, incoming);
            MergeRequires(baseScene, incoming);
            MergeWorkspace(baseScene, incoming);
            MergeNamespaceOps(baseScene, incoming);

            MergeList(baseScene.SetKeyframes, incoming.SetKeyframes);
            MergeList(baseScene.DrivenKeyframes, incoming.DrivenKeyframes);
            MergeList(baseScene.AnimLayers, incoming.AnimLayers);
            MergeList(baseScene.ConnectDynamics, incoming.ConnectDynamics);

            MergeList(baseScene.ScriptNodes, incoming.ScriptNodes);
            MergeList(baseScene.EvalDeferred, incoming.EvalDeferred);
            MergeList(baseScene.Expressions, incoming.Expressions);

            log?.Info($"SceneMerge({provenanceTag}): nodesAdded={nodesAdded}, nodesUpgraded={nodesUpgraded}, attrsAdded={attrsAdded}, attrsUpgraded={attrsUpgraded}, connsAdded={connsAdded}");
        }

        private static void StampNodeProvenance(NodeRecord node, string tag)
        {
            if (node == null || node.Attributes == null) return;

            // Add/append provenance info
            if (!node.Attributes.TryGetValue(".provenance", out var v) || v == null)
            {
                node.Attributes[".provenance"] = new RawAttributeValue("string", new List<string> { tag ?? "unknown" });
                return;
            }

            // Keep it simple: append token
            if (v.ValueTokens != null)
            {
                if (!v.ValueTokens.Contains(tag))
                    v.ValueTokens.Add(tag);
            }
        }

        private static RawAttributeValue CloneAttr(RawAttributeValue src)
        {
            if (src == null) return null;

            var dst = new RawAttributeValue(src.TypeName, src.ValueTokens)
            {
                SizeHint = src.SizeHint,
                Kind = src.Kind,
                ParsedValue = CloneParsed(src.ParsedValue),
                Flags = src.Flags != null ? new Dictionary<string, string>(src.Flags, StringComparer.Ordinal) : null
            };

            return dst;
        }

        private static object CloneParsed(object v)
        {
            if (v == null) return null;
            if (v is int i) return i;
            if (v is bool b) return b;
            if (v is float f) return f;

            if (v is int[] ia)
            {
                var a = new int[ia.Length];
                Array.Copy(ia, a, ia.Length);
                return a;
            }

            if (v is float[] fa)
            {
                var a = new float[fa.Length];
                Array.Copy(fa, a, fa.Length);
                return a;
            }

            if (v is string[] sa)
            {
                var a = new string[sa.Length];
                Array.Copy(sa, a, sa.Length);
                return a;
            }

            // fallback (shouldn't happen often)
            return v;
        }

        private static void MergeFileInfo(MayaSceneData dst, MayaSceneData src)
        {
            if (dst.FileInfo == null || src.FileInfo == null) return;

            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < dst.FileInfo.Count; i++)
                set.Add($"{dst.FileInfo[i].Key}={dst.FileInfo[i].Value}");

            for (int i = 0; i < src.FileInfo.Count; i++)
            {
                var e = src.FileInfo[i];
                var key = $"{e.Key}={e.Value}";
                if (set.Add(key)) dst.FileInfo.Add(e);
            }
        }

        private static void MergeRequires(MayaSceneData dst, MayaSceneData src)
        {
            if (dst.Requires == null || src.Requires == null) return;

            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < dst.Requires.Count; i++)
                set.Add($"{dst.Requires[i].Plugin}@{dst.Requires[i].Version}");

            for (int i = 0; i < src.Requires.Count; i++)
            {
                var e = src.Requires[i];
                var key = $"{e.Plugin}@{e.Version}";
                if (set.Add(key)) dst.Requires.Add(e);
            }
        }

        private static void MergeWorkspace(MayaSceneData dst, MayaSceneData src)
        {
            if (dst.WorkspaceRules == null || src.WorkspaceRules == null) return;

            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < dst.WorkspaceRules.Count; i++)
                set.Add($"{dst.WorkspaceRules[i].Rule}->{dst.WorkspaceRules[i].Path}");

            for (int i = 0; i < src.WorkspaceRules.Count; i++)
            {
                var e = src.WorkspaceRules[i];
                var key = $"{e.Rule}->{e.Path}";
                if (set.Add(key)) dst.WorkspaceRules.Add(e);
            }
        }

        private static void MergeNamespaceOps(MayaSceneData dst, MayaSceneData src)
        {
            if (dst.NamespaceOps == null || src.NamespaceOps == null) return;

            // order matters sometimes; keep append-only but avoid exact duplicates
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < dst.NamespaceOps.Count; i++)
                set.Add($"{dst.NamespaceOps[i].Operation}:{dst.NamespaceOps[i].Name}");

            for (int i = 0; i < src.NamespaceOps.Count; i++)
            {
                var e = src.NamespaceOps[i];
                var key = $"{e.Operation}:{e.Name}";
                if (set.Add(key)) dst.NamespaceOps.Add(e);
            }
        }

        private static void MergeList<T>(List<T> dst, List<T> src)
        {
            if (dst == null || src == null) return;
            if (src.Count == 0) return;
            dst.AddRange(src);
        }
    }
}
