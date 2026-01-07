// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using MayaImporter.Components;
using MayaImporter.Core;
using UnityEngine;

namespace MayaImporter.Shading
{
    /// <summary>
    /// Maya shadingEngine node.
    /// - surfaceShader 
    /// - sets -e -forceElement  RawStatements  members 𕜌i.maj
    /// - connection instObjGroups -> dagSetMembers  members ⊮i.ma/.mb best-effortj
    /// </summary>
    [MayaNodeType("shadingEngine")]
    [DisallowMultipleComponent]
    public sealed class ShadingEngineNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();
            var scene = MayaBuildContext.CurrentScene;

            // Use the unified metadata component already used by other tools.
            var meta = GetComponent<MayaShadingGroupMetadata>() ?? gameObject.AddComponent<MayaShadingGroupMetadata>();

            // ---- surfaceShader
            meta.surfaceShaderNodeName = ResolveSurfaceShaderNodeName() ?? meta.surfaceShaderNodeName;

            // ---- members (rebuild every time to keep deterministic)
            meta.members ??= new List<MayaShadingGroupMetadata.Member>();
            meta.members.Clear();

            // (A) Prefer RawStatements "sets -e -forceElement SG member..."
            int addedFromRaw = 0;
            if (scene?.RawStatements != null && scene.RawStatements.Count > 0)
            {
                addedFromRaw = AddMembersFromRawSetsStatements(scene, NodeName, meta.members);
            }

            // (B) Fallback: connections to dagSetMembers (whole-object membership)
            int addedFromConn = AddMembersFromDagSetMembersConnections(meta.members);

            // (C) Deduplicate (by nodeName + componentSpec)
            Dedup(meta.members);

            log.Info($"[shadingEngine] '{NodeName}' surfaceShader='{meta.surfaceShaderNodeName}' members={meta.members.Count} (raw={addedFromRaw}, conn={addedFromConn})");
        }

        private string ResolveSurfaceShaderNodeName()
        {
            if (Connections == null || Connections.Count == 0) return null;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dst = c.DstPlug ?? "";
                if (dst.IndexOf("surfaceShader", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
                return MayaPlugUtil.ExtractNodePart(c.SrcPlug);
            }

            return null;
        }

        private static int AddMembersFromRawSetsStatements(MayaSceneData scene, string shadingEngineNodeName, List<MayaShadingGroupMetadata.Member> outMembers)
        {
            int added = 0;
            if (scene == null || outMembers == null) return 0;
            if (string.IsNullOrEmpty(shadingEngineNodeName)) return 0;

            var seLeaf = MayaPlugUtil.LeafName(shadingEngineNodeName);

            for (int i = 0; i < scene.RawStatements.Count; i++)
            {
                var st = scene.RawStatements[i];
                if (st == null) continue;

                var tokens = st.Tokens;
                if (tokens == null || tokens.Count == 0) continue;

                var cmd = !string.IsNullOrEmpty(st.Command) ? st.Command : tokens[0];
                if (!string.Equals(cmd, "sets", StringComparison.Ordinal)) continue;

                int fe = IndexOf(tokens, "-forceElement");
                if (fe < 0) fe = IndexOf(tokens, "-fe");
                if (fe < 0) continue;

                if (fe + 1 >= tokens.Count) continue;
                var seTok = TrimQuotes(tokens[fe + 1]);
                if (string.IsNullOrEmpty(seTok)) continue;

                // Match by exact or leaf
                var seTokLeaf = MayaPlugUtil.LeafName(seTok);
                if (!string.Equals(seTok, shadingEngineNodeName, StringComparison.Ordinal) &&
                    !string.Equals(seTokLeaf, seLeaf, StringComparison.Ordinal))
                    continue;

                for (int t = fe + 2; t < tokens.Count; t++)
                {
                    var memberTok = TrimQuotes(tokens[t]);
                    if (string.IsNullOrEmpty(memberTok)) continue;
                    if (memberTok.StartsWith("-", StringComparison.Ordinal)) continue;

                    if (TryParseMemberToken(memberTok, out var nodeName, out var componentSpec))
                    {
                        outMembers.Add(new MayaShadingGroupMetadata.Member
                        {
                            nodeName = nodeName,
                            componentSpec = componentSpec
                        });
                        added++;
                    }
                }
            }

            return added;
        }

        private int AddMembersFromDagSetMembersConnections(List<MayaShadingGroupMetadata.Member> outMembers)
        {
            if (Connections == null || outMembers == null) return 0;

            int added = 0;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dst = c.DstPlug ?? "";
                if (dst.IndexOf("dagSetMembers", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // mesh.instObjGroups[*] -> shadingEngine.dagSetMembers[*]
                var srcNode = !string.IsNullOrEmpty(c.SrcNodePart) ? c.SrcNodePart : MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                if (string.IsNullOrEmpty(srcNode)) continue;

                outMembers.Add(new MayaShadingGroupMetadata.Member
                {
                    nodeName = srcNode,
                    componentSpec = null
                });
                added++;
            }

            return added;
        }

        private static bool TryParseMemberToken(string token, out string nodeName, out string componentSpec)
        {
            nodeName = null;
            componentSpec = null;
            if (string.IsNullOrEmpty(token)) return false;

            // Examples:
            //  - pCubeShape1
            //  - |grp|meshShape
            //  - pCubeShape1.f[0:10]
            //  - |grp|meshShape.f[3]
            //  - "pCubeShape1.f[0:10]"
            var s = TrimQuotes(token).Trim();
            if (string.IsNullOrEmpty(s)) return false;

            // find ".f[" (face component)
            int fi = s.IndexOf(".f[", StringComparison.Ordinal);
            if (fi >= 0)
            {
                nodeName = s.Substring(0, fi);
                componentSpec = s.Substring(fi); // ".f[...]" keep as-is
                return !string.IsNullOrEmpty(nodeName);
            }

            // other component specs could exist; preserve after first '.'
            int dot = s.IndexOf('.', 0);
            if (dot > 0 && dot < s.Length - 1)
            {
                nodeName = s.Substring(0, dot);
                componentSpec = s.Substring(dot);
                return !string.IsNullOrEmpty(nodeName);
            }

            nodeName = s;
            componentSpec = null;
            return true;
        }

        private static void Dedup(List<MayaShadingGroupMetadata.Member> list)
        {
            if (list == null || list.Count <= 1) return;

            var set = new HashSet<string>(StringComparer.Ordinal);
            int w = 0;

            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                var k = (m.nodeName ?? "") + "||" + (m.componentSpec ?? "");
                if (!set.Add(k)) continue;
                list[w++] = m;
            }

            if (w < list.Count) list.RemoveRange(w, list.Count - w);
        }

        private static int IndexOf(List<string> toks, string key)
        {
            if (toks == null || string.IsNullOrEmpty(key)) return -1;
            for (int i = 0; i < toks.Count; i++)
                if (string.Equals(toks[i], key, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        private static string TrimQuotes(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Trim();
            if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
                return s.Substring(1, s.Length - 2);
            return s;
        }
    }
}
