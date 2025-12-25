using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Geometry;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// SkinCluster を Unity の SkinnedMeshRenderer へ適用する。
    ///
    /// 注意:
    /// - Unityは1頂点4ウェイト(BoneWeight)が基本。
    /// - それを超えるウェイトは「失わず」MayaSkinClusterComponent.FullWeights に保持し続ける。
    /// - Unity側は上位4本の近似として適用する（可視化・再生目的）。
    /// </summary>
    public static class MayaSkinReconstruction
    {
        /// <summary>
        /// skinCluster が影響する target mesh を探す（ベストエフォート）。
        /// 基本戦略:
        /// - Connections で mesh っぽいノードに辿る
        /// - それが無理なら、同階層/同ルートの MayaMeshNodeComponent を探す
        /// </summary>
        public static GameObject FindTargetMeshForSkinCluster(MayaNodeComponentBase skinCluster)
        {
            if (skinCluster == null) return null;

            // 1) connections: skinCluster -> (outputGeometry) -> mesh
            if (skinCluster.Connections != null)
            {
                foreach (var c in skinCluster.Connections)
                {
                    if (c == null) continue;

                    var dst = c.DstPlug ?? "";
                    var src = c.SrcPlug ?? "";

                    // outputGeometry / outputGeometry[0] とかが mesh に向くことが多い
                    bool looksGeom = src.IndexOf("outputGeometry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     dst.IndexOf("input", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     dst.IndexOf("inMesh", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!looksGeom) continue;

                    var otherNodeName =
                        (c.RoleForThisNode == MayaNodeComponentBase.ConnectionRole.Source || c.RoleForThisNode == MayaNodeComponentBase.ConnectionRole.Both)
                            ? (c.DstNodePart ?? MayaPlugUtil.ExtractNodePart(dst))
                            : (c.SrcNodePart ?? MayaPlugUtil.ExtractNodePart(src));

                    if (string.IsNullOrEmpty(otherNodeName))
                        continue;

                    var tr = MayaNodeLookup.FindTransform(otherNodeName);
                    if (tr == null) continue;

                    // mesh component is usually on the shape GO; but Apply attaches MeshFilter to parent transform
                    var meshComp = tr.GetComponentInChildren<MayaMeshNodeComponent>(true);
                    if (meshComp != null)
                        return meshComp.gameObject;
                }
            }

            // 2) fallback: search in same root for any mesh
            var root = skinCluster.transform != null ? skinCluster.transform.root : null;
            if (root != null)
            {
                var meshes = root.GetComponentsInChildren<MayaMeshNodeComponent>(true);
                if (meshes != null && meshes.Length > 0)
                {
                    // choose the first mesh with ExpandedToOriginalVertex mapping or MeshFilter
                    for (int i = 0; i < meshes.Length; i++)
                    {
                        var go = meshes[i].gameObject;
                        if (go == null) continue;
                        if (go.GetComponent<MeshFilter>() != null) return go;
                    }
                    return meshes[0].gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// MayaSkinClusterComponent を SkinnedMeshRenderer に適用する。
        /// </summary>
        public static bool TryApplySkinClusterToTarget(
            MayaSkinClusterComponent skin,
            GameObject targetMeshGo,
            MayaImportOptions options,
            MayaImportLog log,
            out string note)
        {
            note = null;
            if (skin == null) { note = "skin is null"; return false; }
            if (targetMeshGo == null) { note = "target mesh GO is null"; return false; }

            // Find Mesh + mapping
            var mf = targetMeshGo.GetComponent<MeshFilter>();
            var smr = targetMeshGo.GetComponent<SkinnedMeshRenderer>();
            if (smr == null) smr = targetMeshGo.AddComponent<SkinnedMeshRenderer>();

            Mesh mesh = null;
            if (mf != null && mf.sharedMesh != null) mesh = mf.sharedMesh;
            if (mesh == null && smr.sharedMesh != null) mesh = smr.sharedMesh;

            // If mesh exists on MeshFilter, move it to SkinnedMeshRenderer (Unity typical)
            if (mesh != null)
            {
                smr.sharedMesh = mesh;
                if (mf != null) mf.sharedMesh = null;
                UnityEngine.Object.DestroyImmediate(mf, allowDestroyingAssets: false);
            }

            mesh = smr.sharedMesh;
            if (mesh == null) { note = "No Mesh found on target (MeshFilter/SkinnedMeshRenderer)."; return false; }

            // ExpandedToOriginal mapping
            var meshNode = targetMeshGo.GetComponentInChildren<MayaMeshNodeComponent>(true);
            int[] expandedToOrig = null;
            if (meshNode != null && meshNode.ExpandedToOriginalVertex != null && meshNode.ExpandedToOriginalVertex.Length == mesh.vertexCount)
                expandedToOrig = meshNode.ExpandedToOriginalVertex;

            // influences -> bone transforms
            var bones = ResolveBones(skin, targetMeshGo.transform.root);
            if (bones == null || bones.Length == 0)
            {
                note = "No bones resolved from InfluenceNames. Skin data preserved, Unity apply skipped.";
                return false;
            }

            smr.bones = bones;
            smr.rootBone = ChooseRootBone(bones, targetMeshGo.transform.root);

            // build BoneWeights (top4)
            var full = skin.FullWeights ?? Array.Empty<MayaSkinWeights.SparseWeight>();

            var bw = new BoneWeight[mesh.vertexCount];
            var tmp = new List<(int inf, float w)>(16);

            int clampedInf = 0;
            int clampedVtx = 0;

            for (int vi = 0; vi < mesh.vertexCount; vi++)
            {
                int mayaVtx = expandedToOrig != null ? expandedToOrig[vi] : vi;

                MayaSkinWeights.GetTop4ForVertex(full, mayaVtx, tmp);

                // track clamp
                int rawCount = 0;
                for (int i = 0; i < full.Length; i++)
                    if (full[i].Vertex == mayaVtx && full[i].Weight > 0f) rawCount++;

                if (rawCount > 4) { clampedVtx++; clampedInf += (rawCount - 4); }

                var b = new BoneWeight();
                if (tmp.Count > 0)
                {
                    b.boneIndex0 = Mathf.Clamp(tmp[0].inf, 0, bones.Length - 1);
                    b.weight0 = tmp[0].w;
                }
                if (tmp.Count > 1)
                {
                    b.boneIndex1 = Mathf.Clamp(tmp[1].inf, 0, bones.Length - 1);
                    b.weight1 = tmp[1].w;
                }
                if (tmp.Count > 2)
                {
                    b.boneIndex2 = Mathf.Clamp(tmp[2].inf, 0, bones.Length - 1);
                    b.weight2 = tmp[2].w;
                }
                if (tmp.Count > 3)
                {
                    b.boneIndex3 = Mathf.Clamp(tmp[3].inf, 0, bones.Length - 1);
                    b.weight3 = tmp[3].w;
                }

                bw[vi] = b;
            }

            mesh.boneWeights = bw;

            // bindposes: best-effort. We use current bones' worldToLocal * mesh localToWorld
            var bindposes = new Matrix4x4[bones.Length];
            var meshWorld = targetMeshGo.transform.localToWorldMatrix;
            for (int i = 0; i < bones.Length; i++)
                bindposes[i] = bones[i].worldToLocalMatrix * meshWorld;

            mesh.bindposes = bindposes;

            // keep materials from MeshRenderer if any
            var mr = targetMeshGo.GetComponent<MeshRenderer>();
            if (mr != null && (smr.sharedMaterials == null || smr.sharedMaterials.Length == 0))
            {
                smr.sharedMaterials = mr.sharedMaterials;
                UnityEngine.Object.DestroyImmediate(mr, allowDestroyingAssets: false);
            }

            skin.UnityClampedVertices = clampedVtx;
            skin.UnityClampedInfluences = clampedInf;

            note = $"Applied skin to '{targetMeshGo.name}'. bones={bones.Length}, meshVtx={mesh.vertexCount}, " +
                   $"expandedMap={(expandedToOrig != null ? "yes" : "no")}, clampedVtx={clampedVtx}, clampedInf={clampedInf} (full weights preserved).";
            return true;
        }

        private static Transform[] ResolveBones(MayaSkinClusterComponent skin, Transform root)
        {
            if (skin == null || skin.InfluenceNames == null || skin.InfluenceNames.Length == 0) return Array.Empty<Transform>();
            if (root == null) return Array.Empty<Transform>();

            var all = root.GetComponentsInChildren<Transform>(true);
            var list = new List<Transform>(skin.InfluenceNames.Length);

            for (int i = 0; i < skin.InfluenceNames.Length; i++)
            {
                var name = skin.InfluenceNames[i];
                if (string.IsNullOrEmpty(name))
                {
                    list.Add(root);
                    continue;
                }

                Transform found = null;

                for (int t = 0; t < all.Length; t++)
                {
                    var tr = all[t];
                    if (tr == null) continue;

                    if (string.Equals(tr.name, name, StringComparison.Ordinal) ||
                        string.Equals(MayaPlugUtil.LeafName(tr.name), name, StringComparison.Ordinal))
                    {
                        found = tr;
                        break;
                    }
                }

                if (found == null) found = root;
                list.Add(found);
            }

            return list.ToArray();
        }

        private static Transform ChooseRootBone(Transform[] bones, Transform fallback)
        {
            if (bones == null || bones.Length == 0) return fallback;
            // choose the highest ancestor among bones (closest to hierarchy root)
            Transform best = bones[0];
            int bestDepth = GetDepth(best);

            for (int i = 1; i < bones.Length; i++)
            {
                var b = bones[i];
                int d = GetDepth(b);
                if (d < bestDepth) { best = b; bestDepth = d; }
            }

            return best != null ? best : fallback;
        }

        private static int GetDepth(Transform t)
        {
            int d = 0;
            while (t != null && t.parent != null) { d++; t = t.parent; }
            return d;
        }
    }
}
