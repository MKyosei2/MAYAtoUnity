using System;
using System.Collections.Generic;
using MayaImporter.Components;
using MayaImporter.Geometry;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Post process after all ApplyToUnity() stages:
    /// - Meshが先に仮Materialを持ってしまう問題を、最終パスで確定Materialへ上書きして解決
    /// - shadingEngine -> material metadata -> textures を辿って Unity Material を構築＆適用
    /// </summary>
    public static class MayaMaterialPostProcessor
    {
        public static void Apply(GameObject sceneRoot, MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            if (sceneRoot == null) return;
            log ??= new MayaImportLog();
            options ??= new MayaImportOptions();

            var rootTr = sceneRoot.transform;

            // Collect all meshes (Maya mesh node is usually a "shape" under a transform)
            var meshes = rootTr.GetComponentsInChildren<MayaMeshNodeComponent>(true);
            if (meshes == null || meshes.Length == 0) return;

            int applied = 0;
            int resolved = 0;

            for (int i = 0; i < meshes.Length; i++)
            {
                var mesh = meshes[i];
                if (mesh == null) continue;

                var targetGO = ResolveMeshTargetGO(mesh.transform);
                if (targetGO == null) continue;

                // Renderer can be MeshRenderer or SkinnedMeshRenderer (best-effort)
                var mr = targetGO.GetComponent<MeshRenderer>();
                var smr = targetGO.GetComponent<SkinnedMeshRenderer>();
                Renderer renderer = (smr != null) ? (Renderer)smr : (Renderer)mr;
                if (renderer == null) continue;

                // Determine shadingEngine keys order (for .ma face assignment it matches submesh order)
                var keys = DetermineSubmeshKeys(sceneRoot.transform, scene, mesh.NodeName);

                if (keys.Count == 0)
                    continue;

                // Build materials
                var outMats = renderer.sharedMaterials;
                if (outMats == null || outMats.Length != keys.Count)
                    outMats = new Material[keys.Count];

                for (int si = 0; si < keys.Count; si++)
                {
                    var se = keys[si];
                    var m = MayaMaterialResolver.ResolveFromSceneBestEffort(rootTr, scene, se, options, log);
                    if (m == null)
                        m = MayaMaterialResolver.ResolveForShadingEngine(se);

                    outMats[si] = m;
                    resolved++;
                }

                renderer.sharedMaterials = outMats;
                applied++;
            }

            log.Info($"[MaterialPost] appliedRenderers={applied} resolvedMaterials={resolved}");
        }

        private static List<string> DetermineSubmeshKeys(Transform root, MayaSceneData scene, string meshNodeName)
        {
            // 1) Prefer .ma raw statement based assignment (exact)
            if (scene != null && MayaFaceMaterialAssignments.TryGetForMesh(scene, meshNodeName, out var assign) && assign != null)
            {
                var list = new List<string>(assign.FacesByShadingEngine.Count);
                for (int i = 0; i < assign.FacesByShadingEngine.Count; i++)
                    list.Add(assign.FacesByShadingEngine[i].Key);

                if (list.Count > 0) return list;
            }

            // 2) Fallback: shading group metadata (whole-object membership) -> take all SGs that include this mesh
            var fromMeta = FindShadingEnginesByMembership(root, meshNodeName);
            if (fromMeta.Count > 0) return fromMeta;

            // 3) Ultimate fallback: one slot (mesh default)
            return new List<string> { "__Default__" };
        }

        private static List<string> FindShadingEnginesByMembership(Transform root, string meshNodeName)
        {
            var res = new List<string>(4);
            if (root == null || string.IsNullOrEmpty(meshNodeName)) return res;

            var meshLeaf = MayaPlugUtil.LeafName(meshNodeName);

            var metas = root.GetComponentsInChildren<MayaShadingGroupMetadata>(true);
            if (metas == null || metas.Length == 0) return res;

            // Deterministic order by SG node leaf name
            Array.Sort(metas, (a, b) =>
            {
                var na = a != null ? a.gameObject.name : "";
                var nb = b != null ? b.gameObject.name : "";
                return string.CompareOrdinal(na, nb);
            });

            for (int i = 0; i < metas.Length; i++)
            {
                var sg = metas[i];
                if (sg == null || sg.members == null) continue;

                bool hit = false;
                for (int mi = 0; mi < sg.members.Count; mi++)
                {
                    var mem = sg.members[mi];
                    if (string.IsNullOrEmpty(mem.nodeName)) continue;

                    var leaf = MayaPlugUtil.LeafName(mem.nodeName);
                    if (string.Equals(leaf, meshLeaf, StringComparison.Ordinal))
                    {
                        hit = true;
                        break;
                    }
                }

                if (!hit) continue;

                // Resolve SG NodeName if possible (prefer MayaNodeComponentBase.NodeName)
                var node = sg.GetComponent<MayaNodeComponentBase>();
                if (node != null && string.Equals(node.NodeType, "shadingEngine", StringComparison.Ordinal))
                    res.Add(node.NodeName);
                else
                    res.Add(sg.gameObject.name);
            }

            return res;
        }

        private static GameObject ResolveMeshTargetGO(Transform meshShapeTransform)
        {
            if (meshShapeTransform == null) return null;

            // Maya mesh is usually a "shape" under a transform/joint.
            if (meshShapeTransform.parent != null)
            {
                var p = meshShapeTransform.parent;
                var pn = p.GetComponent<MayaNodeComponentBase>();
                if (pn != null && (pn.NodeType == "transform" || pn.NodeType == "joint"))
                    return p.gameObject;
            }

            return meshShapeTransform.gameObject;
        }
    }
}
