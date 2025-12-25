using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase2 Step18 (MA):
    /// Traverse Connections to infer:
    /// mesh(shape) -> shadingEngine -> material -> file(texture path)
    /// and tag unified keys:
    ///   .shadingGroupName(.shadingGroupName2..)
    ///   .materialName(.materialName2..)
    ///   .textureHint(.textureHint2..)
    ///
    /// NOTE:
    /// Class name is V2 to avoid collisions with any existing MayaMaShadingTagger in the project.
    /// </summary>
    public static class MayaMaShadingTaggerV2
    {
        private const string KeySG = ".shadingGroupName";
        private const string KeyMat = ".materialName";
        private const string KeyTex = ".textureHint";

        public static void Apply(MayaSceneData scene, MayaImportLog log)
        {
            if (scene == null) return;
            if (scene.Nodes == null || scene.Nodes.Count == 0) return;

            if (scene.Connections == null || scene.Connections.Count == 0)
            {
                log?.Info(".ma shading-tag(V2): no connections (OK).");
                return;
            }

            // mesh-ish nodes
            var meshes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in scene.Nodes)
            {
                var r = kv.Value;
                if (r == null) continue;

                var leaf = LeafOfDag(r.Name);
                if (r.NodeType == "mesh") meshes.Add(r.Name);
                else if (!string.IsNullOrEmpty(leaf) && leaf.EndsWith("Shape", StringComparison.Ordinal)) meshes.Add(r.Name);
            }

            if (meshes.Count == 0)
            {
                log?.Info(".ma shading-tag(V2): no mesh nodes found (OK).");
                return;
            }

            // mesh -> SG candidates
            var meshToSg = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var c in scene.Connections)
            {
                if (!TrySplitPlug(c.SrcPlug, out var srcNode, out _)) continue;
                if (!TrySplitPlug(c.DstPlug, out var dstNode, out _)) continue;

                bool srcIsMesh = meshes.Contains(srcNode);
                bool dstIsMesh = meshes.Contains(dstNode);

                if (srcIsMesh && TryIsType(scene, dstNode, "shadingEngine"))
                    AddSlot(meshToSg, srcNode, dstNode);
                else if (dstIsMesh && TryIsType(scene, srcNode, "shadingEngine"))
                    AddSlot(meshToSg, dstNode, srcNode);
            }

            int sgTagged = 0, matTagged = 0, texTagged = 0;

            foreach (var mk in meshes)
            {
                if (!scene.Nodes.TryGetValue(mk, out var meshRec) || meshRec == null) continue;

                if (!meshToSg.TryGetValue(mk, out var sgs) || sgs == null || sgs.Count == 0)
                    continue;

                // SG tags (up to 4)
                for (int i = 0; i < sgs.Count && i < 4; i++)
                    if (TrySetSlot(meshRec, KeySG, sgs[i], 4)) sgTagged++;

                // Use first SG as primary
                var sg = sgs[0];
                var mat = FindMaterialForShadingEngine(scene, sg);
                if (!string.IsNullOrEmpty(mat))
                {
                    if (TrySetSlot(meshRec, KeyMat, mat, 2)) matTagged++;

                    var tex = FindTextureForMaterial(scene, mat);
                    if (!string.IsNullOrEmpty(tex))
                        if (TrySetSlot(meshRec, KeyTex, tex, 4)) texTagged++;
                }
            }

            log?.Info($".ma shading-tag(V2): sg={sgTagged}, mat={matTagged}, tex={texTagged} (Stage: Step18 Unified tags).");
        }

        private static string FindMaterialForShadingEngine(MayaSceneData scene, string sgNode)
        {
            // connectAttr "mat.outColor" "sg.surfaceShader"
            foreach (var c in scene.Connections)
            {
                if (!TrySplitPlug(c.SrcPlug, out var srcNode, out var srcAttr)) continue;
                if (!TrySplitPlug(c.DstPlug, out var dstNode, out var dstAttr)) continue;

                if (string.Equals(dstNode, sgNode, StringComparison.Ordinal) &&
                    dstAttr.IndexOf("surfaceShader", StringComparison.OrdinalIgnoreCase) >= 0)
                    return srcNode;

                if (string.Equals(srcNode, sgNode, StringComparison.Ordinal) &&
                    srcAttr.IndexOf("surfaceShader", StringComparison.OrdinalIgnoreCase) >= 0)
                    return dstNode;
            }
            return null;
        }

        private static string FindTextureForMaterial(MayaSceneData scene, string matNode)
        {
            foreach (var c in scene.Connections)
            {
                if (!TrySplitPlug(c.SrcPlug, out var srcNode, out _)) continue;
                if (!TrySplitPlug(c.DstPlug, out var dstNode, out _)) continue;

                string fileNode = null;

                if (string.Equals(srcNode, matNode, StringComparison.Ordinal) && TryIsType(scene, dstNode, "file"))
                    fileNode = dstNode;
                else if (string.Equals(dstNode, matNode, StringComparison.Ordinal) && TryIsType(scene, srcNode, "file"))
                    fileNode = srcNode;

                if (fileNode == null) continue;

                if (scene.Nodes.TryGetValue(fileNode, out var fr) && fr != null)
                {
                    if (TryGetStringAttr(fr, ".fileTextureName", out var s) && LooksLikeTexture(s)) return s;
                    if (TryGetStringAttr(fr, ".ftn", out s) && LooksLikeTexture(s)) return s;

                    foreach (var kv in fr.Attributes)
                    {
                        var v = kv.Value?.ValueTokens?.Count > 0 ? kv.Value.ValueTokens[0] : null;
                        if (LooksLikeTexture(v)) return v;
                    }
                }
            }
            return null;
        }

        private static bool TryIsType(MayaSceneData scene, string node, string type)
        {
            return scene.Nodes.TryGetValue(node, out var r) && r != null &&
                   string.Equals(r.NodeType, type, StringComparison.Ordinal);
        }

        private static bool TrySplitPlug(string plug, out string node, out string attr)
        {
            node = null; attr = null;
            if (string.IsNullOrEmpty(plug)) return false;
            int dot = plug.IndexOf('.');
            if (dot <= 0 || dot >= plug.Length - 1) return false;
            node = plug.Substring(0, dot);
            attr = plug.Substring(dot + 1);
            return true;
        }

        private static void AddSlot(Dictionary<string, List<string>> map, string key, string value)
        {
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<string>(4);
                map[key] = list;
            }
            if (!list.Contains(value)) list.Add(value);
        }

        private static bool TrySetSlot(NodeRecord rec, string baseKey, string value, int maxSlots)
        {
            if (rec.Attributes == null) return false;
            if (string.IsNullOrEmpty(value)) return false;

            // dedupe
            for (int i = 1; i <= maxSlots; i++)
            {
                var key = (i == 1) ? baseKey : baseKey + i;
                var ex = rec.Attributes.TryGetValue(key, out var e) ? e : null;
                if (ex?.ValueTokens?.Count > 0 && string.Equals(ex.ValueTokens[0], value, StringComparison.Ordinal))
                    return false;
            }

            for (int i = 1; i <= maxSlots; i++)
            {
                var key = (i == 1) ? baseKey : baseKey + i;
                if (!rec.Attributes.ContainsKey(key))
                {
                    rec.Attributes[key] = new RawAttributeValue("string", new List<string> { value });
                    return true;
                }
            }
            return false;
        }

        private static bool TryGetStringAttr(NodeRecord rec, string key, out string value)
        {
            value = null;
            if (rec?.Attributes == null) return false;
            if (!rec.Attributes.TryGetValue(key, out var raw) || raw == null) return false;
            if (raw.ValueTokens == null || raw.ValueTokens.Count == 0) return false;
            value = raw.ValueTokens[0];
            return !string.IsNullOrEmpty(value);
        }

        private static bool LooksLikeTexture(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            var ls = s.ToLowerInvariant();
            return ls.EndsWith(".png") || ls.EndsWith(".jpg") || ls.EndsWith(".jpeg") ||
                   ls.EndsWith(".tga") || ls.EndsWith(".tif") || ls.EndsWith(".tiff") ||
                   ls.EndsWith(".bmp") || ls.EndsWith(".exr") || ls.EndsWith(".hdr");
        }

        private static string LeafOfDag(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            int last = name.LastIndexOf('|');
            if (last >= 0 && last < name.Length - 1) return name.Substring(last + 1);
            return name;
        }
    }
}
