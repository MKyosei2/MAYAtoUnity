using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;
using MayaImporter.Geometry;
using MayaImporter.Runtime;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// BlendShape を Unity の Mesh(BlendShapeFrame) と SkinnedMeshRenderer(Weight) へ適用する。
    ///
    /// 強化ポイント:
    /// - Connections でターゲットmeshが見つからない場合でも
    ///   blendShapeノード内の埋め込みデータ（.itg[i].iti[6000].ipt / inputPointsTarget 等）から
    ///   best-effort でデルタを復元して AddBlendShapeFrame を試みる。
    /// - 失敗しても MayaBlendShapeComponent に理由付きで保持（100%担保）
    /// </summary>
    public static class MayaBlendShapeReconstruction
    {
        /// <summary>
        /// Build/import-time setup for blendShape.
        /// </summary>
        public static bool TryBuildBlendShapes(BlendShapeNode blendShapeNode, MayaBlendShapeMetadata meta, MayaImportLog log)
        {
            if (blendShapeNode == null) { log?.Warn("[blendShape] node is null"); return false; }

            meta ??= blendShapeNode.GetComponent<MayaBlendShapeMetadata>();
            if (meta == null)
            {
                log?.Warn("[blendShape] MayaBlendShapeMetadata missing. Nothing to build.");
                return false;
            }

            // Lossless holder
            var bs = blendShapeNode.GetComponent<MayaBlendShapeComponent>()
                     ?? blendShapeNode.gameObject.AddComponent<MayaBlendShapeComponent>();

            bs.BlendShapeNodeName = !string.IsNullOrEmpty(blendShapeNode.NodeName) ? blendShapeNode.NodeName : blendShapeNode.gameObject.name;
            bs.RawNote = $"meta.targets={meta.targets?.Count ?? 0}. (Raw Maya tokens remain on MayaNodeComponentBase.Attributes.)";

            // 1) Find affected base mesh (the mesh being deformed)
            var baseMeshGo = FindTargetMeshForBlendShape(blendShapeNode);
            if (baseMeshGo == null)
            {
                bs.HasDecodedTargets = false;
                bs.DecodeNote = "Base mesh not found from connections. Targets preserved as references only.";
                bs.AppliedToUnity = false;
                bs.ApplyNote = "Skipped (no base mesh).";
                log?.Warn("[blendShape] base mesh not found; preserved only.");
                return false;
            }

            // 2) Ensure we can modify a working Mesh
            if (!TryGetOrCreateWorkingMesh(baseMeshGo, out var smr, out var mesh, log, out string meshNote))
            {
                bs.HasDecodedTargets = false;
                bs.DecodeNote = "Base mesh exists but no editable Mesh found.";
                bs.AppliedToUnity = false;
                bs.ApplyNote = "Skipped (no Mesh).";
                log?.Warn("[blendShape] " + bs.DecodeNote);
                return false;
            }

            // base geometry
            var baseVerts = mesh.vertices;
            var baseNormals = mesh.normals;
            int baseVCount = mesh.vertexCount;

            // Expanded map (triangulation/dup)
            int[] expandedToOrig = null;
            var meshNode = baseMeshGo.GetComponentInChildren<MayaMeshNodeComponent>(true);
            if (meshNode != null && meshNode.ExpandedToOriginalVertex != null && meshNode.ExpandedToOriginalVertex.Length == baseVCount)
                expandedToOrig = meshNode.ExpandedToOriginalVertex;

            // Ensure blendshape name uniqueness
            var existingNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < mesh.blendShapeCount; i++)
                existingNames.Add(mesh.GetBlendShapeName(i) ?? "");

            var builtTargets = new List<MayaBlendShapeTarget>(meta.targets.Count);
            int decodedOk = 0;
            int decodedSkip = 0;

            // 3) Try build per target:
            //    A) target mesh exists -> compute delta from geometry
            //    B) else -> try decode embedded ipt (inputPointsTarget/.ipt)
            for (int i = 0; i < meta.targets.Count; i++)
            {
                var t = meta.targets[i];

                string mayaTargetName = t.targetMesh;
                var targetGo = FindMeshGameObjectByMayaName(mayaTargetName, baseMeshGo.transform.root);

                Vector3[] dv = null;
                Vector3[] dn = null;

                if (targetGo != null && TryGetMeshFromGo(targetGo, out var tMesh))
                {
                    // --- A) from target mesh ---
                    var tVerts = tMesh.vertices;
                    var tNormals = tMesh.normals;
                    int tVCount = tMesh.vertexCount;

                    if (tVCount == baseVCount)
                    {
                        dv = new Vector3[baseVCount];
                        for (int v = 0; v < baseVCount; v++)
                            dv[v] = tVerts[v] - baseVerts[v];

                        dn = (tNormals != null && tNormals.Length == baseVCount && baseNormals != null && baseNormals.Length == baseVCount)
                            ? SubtractArrays(tNormals, baseNormals)
                            : null;
                    }
                    else if (expandedToOrig != null)
                    {
                        // base expanded, target original
                        int origCount = tVCount;

                        dv = new Vector3[baseVCount];
                        dn = (tNormals != null && tNormals.Length == origCount && baseNormals != null && baseNormals.Length == baseVCount)
                            ? new Vector3[baseVCount]
                            : null;

                        for (int v = 0; v < baseVCount; v++)
                        {
                            int ov = expandedToOrig[v];
                            if (ov >= 0 && ov < origCount)
                            {
                                dv[v] = tVerts[ov] - baseVerts[v];
                                if (dn != null) dn[v] = tNormals[ov] - baseNormals[v];
                            }
                        }
                    }
                    else
                    {
                        dv = null;
                    }
                }

                // --- B) fallback: embedded ipt decode ---
                if (dv == null)
                {
                    if (EmbeddedBlendShapeDecoder.TryDecodeEmbeddedPoints(
                        blendShapeNode,
                        t.targetIndex,
                        baseVCount,
                        expandedToOrig,
                        out dv,
                        out string embeddedNote))
                    {
                        // normals not decoded here (best-effort)
                        dn = null;

                        builtTargets.Add(new MayaBlendShapeTarget
                        {
                            Name = MakeUnityBlendShapeName(bs.BlendShapeNodeName, t.targetIndex, mayaTargetName, existingNames),
                            FrameWeight = 100f,
                            DeltaVertices = dv,
                            DeltaNormals = dn,
                            DeltaTangents = null,
                            SourceNote = "OK: embedded ipt decode | " + embeddedNote
                        });
                        decodedOk++;
                        continue;
                    }

                    // still no delta
                    builtTargets.Add(new MayaBlendShapeTarget
                    {
                        Name = MakeUnityBlendShapeName(bs.BlendShapeNodeName, t.targetIndex, mayaTargetName, existingNames),
                        FrameWeight = 100f,
                        DeltaVertices = null,
                        DeltaNormals = null,
                        DeltaTangents = null,
                        SourceNote = $"SKIP: no target mesh delta and no embedded ipt decode (targetMesh='{mayaTargetName}')."
                    });
                    decodedSkip++;
                    continue;
                }

                // If delta is all zero -> keep but skip apply later
                if (IsAllZero(dv))
                {
                    builtTargets.Add(new MayaBlendShapeTarget
                    {
                        Name = MakeUnityBlendShapeName(bs.BlendShapeNodeName, t.targetIndex, mayaTargetName, existingNames),
                        FrameWeight = 100f,
                        DeltaVertices = dv,
                        DeltaNormals = dn,
                        DeltaTangents = null,
                        SourceNote = "SKIP: delta is all zero (kept)."
                    });
                    decodedSkip++;
                    continue;
                }

                builtTargets.Add(new MayaBlendShapeTarget
                {
                    Name = MakeUnityBlendShapeName(bs.BlendShapeNodeName, t.targetIndex, mayaTargetName, existingNames),
                    FrameWeight = 100f,
                    DeltaVertices = dv,
                    DeltaNormals = dn,
                    DeltaTangents = null,
                    SourceNote = $"OK: from target mesh '{mayaTargetName}'"
                });
                decodedOk++;
            }

            bs.Targets = builtTargets.ToArray();
            bs.HasDecodedTargets = true;
            bs.DecodeNote = $"Decoded targets={bs.TargetCount}, ok={decodedOk}, skip={decodedSkip}. {meshNote}";

            // 4) Apply to Unity Mesh
            bool anyApplied = TryApplyBlendShapeToTarget(bs, baseMeshGo, log, out int appliedCount, out int skippedCountApply, out string applyNote);

            // 5) Initial weights
            ApplyInitialWeights(smr, mesh, meta, log);

            // 6) Create/refresh channels
            EnsureChannels(blendShapeNode.gameObject, mesh, meta);

            // 7) Audit
            EnsureAudit(baseMeshGo, bs, meta, anyApplied, appliedCount, skippedCountApply, $"{bs.DecodeNote} | {applyNote}");

            return anyApplied;
        }

        // ------------------------------------------------------------
        // Core apply
        // ------------------------------------------------------------

        private static bool TryApplyBlendShapeToTarget(
            MayaBlendShapeComponent bs,
            GameObject targetMeshGo,
            MayaImportLog log,
            out int appliedTargets,
            out int skippedTargets,
            out string note)
        {
            appliedTargets = 0;
            skippedTargets = 0;
            note = null;

            if (bs == null) { note = "blendshape component null"; return false; }
            if (targetMeshGo == null) { note = "target mesh GO null"; return false; }

            if (!TryGetOrCreateWorkingMesh(targetMeshGo, out var smr, out var mesh, log, out string meshNote))
            {
                note = "No editable mesh found.";
                bs.AppliedToUnity = false;
                bs.ApplyNote = note;
                return false;
            }

            int vc = mesh.vertexCount;

            var existing = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < mesh.blendShapeCount; i++)
                existing.Add(mesh.GetBlendShapeName(i) ?? "");

            if (bs.Targets != null)
            {
                for (int ti = 0; ti < bs.Targets.Length; ti++)
                {
                    var t = bs.Targets[ti];
                    if (t == null) { skippedTargets++; continue; }

                    var name = string.IsNullOrEmpty(t.Name) ? ("BlendTarget_" + ti) : t.Name;
                    if (existing.Contains(name))
                    {
                        appliedTargets++;
                        continue;
                    }

                    if (t.DeltaVertices == null || t.DeltaVertices.Length == 0)
                    {
                        skippedTargets++;
                        continue;
                    }

                    if (t.DeltaVertices.Length != vc)
                    {
                        skippedTargets++;
                        continue;
                    }

                    var dn = (t.DeltaNormals != null && t.DeltaNormals.Length == vc) ? t.DeltaNormals : EmptyVec3(vc);
                    var dt = (t.DeltaTangents != null && t.DeltaTangents.Length == vc) ? t.DeltaTangents : EmptyVec3(vc);

                    try
                    {
                        mesh.AddBlendShapeFrame(name, t.FrameWeight, t.DeltaVertices, dn, dt);
                        existing.Add(name);
                        appliedTargets++;
                    }
                    catch (Exception e)
                    {
                        skippedTargets++;
                        log?.Warn($"[blendShape] AddBlendShapeFrame failed: {name} {e.GetType().Name}: {e.Message}");
                    }
                }
            }

            bool ok = appliedTargets > 0;

            note = $"Apply: decoded={bs.TargetCount}, applied={appliedTargets}, skipped={skippedTargets}. {meshNote} (All data preserved in MayaBlendShapeComponent.)";
            bs.AppliedToUnity = ok;
            bs.ApplyNote = note;

            if (smr != null) smr.sharedMesh = mesh;

            return ok;
        }

        // ------------------------------------------------------------
        // Finding base/target meshes
        // ------------------------------------------------------------

        private static GameObject FindTargetMeshForBlendShape(MayaNodeComponentBase blendShape)
        {
            if (blendShape == null) return null;

            // 1) connections: blendShape -> mesh (inMesh/inputGeometry)
            if (blendShape.Connections != null)
            {
                foreach (var c in blendShape.Connections)
                {
                    if (c == null) continue;

                    if (c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Source &&
                        c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Both)
                        continue;

                    var dst = c.DstPlug ?? "";
                    var src = c.SrcPlug ?? "";

                    bool looksGeom =
                        src.IndexOf("outputGeometry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        src.IndexOf("output", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dst.IndexOf("inMesh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dst.IndexOf("input", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dst.IndexOf("inputGeometry", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!looksGeom) continue;

                    var otherNodeName = c.DstNodePart ?? MayaPlugUtil.ExtractNodePart(dst);
                    if (string.IsNullOrEmpty(otherNodeName))
                        continue;

                    var tr = MayaNodeLookup.FindTransform(otherNodeName);
                    if (tr == null) continue;

                    var meshComp = tr.GetComponentInChildren<MayaMeshNodeComponent>(true);
                    if (meshComp != null) return meshComp.gameObject;

                    if (TryGetMeshFromGo(tr.gameObject, out _)) return tr.gameObject;
                }
            }

            // 2) fallback: search in same root for first mesh
            var root = blendShape.transform != null ? blendShape.transform.root : null;
            if (root != null)
            {
                var meshes = root.GetComponentsInChildren<MayaMeshNodeComponent>(true);
                if (meshes != null && meshes.Length > 0) return meshes[0].gameObject;

                var allTr = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allTr.Length; i++)
                {
                    if (TryGetMeshFromGo(allTr[i].gameObject, out _))
                        return allTr[i].gameObject;
                }
            }

            return null;
        }

        private static GameObject FindMeshGameObjectByMayaName(string mayaNodeNameOrDag, Transform root)
        {
            if (string.IsNullOrEmpty(mayaNodeNameOrDag)) return null;

            // embedded_... は "参照名" なので検索対象外
            if (mayaNodeNameOrDag.StartsWith("embedded_", StringComparison.OrdinalIgnoreCase))
                return null;

            var tr = MayaNodeLookup.FindTransform(mayaNodeNameOrDag);
            if (tr != null)
            {
                var meshComp = tr.GetComponentInChildren<MayaMeshNodeComponent>(true);
                if (meshComp != null) return meshComp.gameObject;

                if (TryGetMeshFromGo(tr.gameObject, out _)) return tr.gameObject;

                var mf = tr.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null) return mf.gameObject;

                var smr = tr.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null && smr.sharedMesh != null) return smr.gameObject;
            }

            if (root != null)
            {
                var leaf = MayaPlugUtil.LeafName(mayaNodeNameOrDag);
                var all = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null) continue;
                    if (!string.Equals(all[i].name, leaf, StringComparison.Ordinal)) continue;

                    var meshComp = all[i].GetComponentInChildren<MayaMeshNodeComponent>(true);
                    if (meshComp != null) return meshComp.gameObject;

                    if (TryGetMeshFromGo(all[i].gameObject, out _)) return all[i].gameObject;
                }
            }

            return null;
        }

        private static bool TryGetMeshFromGo(GameObject go, out Mesh mesh)
        {
            mesh = null;
            if (go == null) return false;

            var smr = go.GetComponent<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null) { mesh = smr.sharedMesh; return true; }

            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) { mesh = mf.sharedMesh; return true; }

            smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMesh != null) { mesh = smr.sharedMesh; return true; }

            var childMf = go.GetComponentInChildren<MeshFilter>(true);
            if (childMf != null && childMf.sharedMesh != null) { mesh = childMf.sharedMesh; return true; }

            return false;
        }

        // ------------------------------------------------------------
        // Mesh ownership (safe editing)
        // ------------------------------------------------------------

        private static bool TryGetOrCreateWorkingMesh(GameObject targetMeshGo, out SkinnedMeshRenderer smr, out Mesh mesh, MayaImportLog log, out string note)
        {
            smr = null;
            mesh = null;
            note = null;

            if (targetMeshGo == null) { note = "target GO null"; return false; }

            smr = targetMeshGo.GetComponent<SkinnedMeshRenderer>();
            var mf = targetMeshGo.GetComponent<MeshFilter>();
            var mr = targetMeshGo.GetComponent<MeshRenderer>();

            if (smr != null && smr.sharedMesh != null)
            {
                mesh = smr.sharedMesh;
                note = "Mesh from SkinnedMeshRenderer.";
                return true;
            }

            if (mf != null && mf.sharedMesh != null)
            {
                var src = mf.sharedMesh;

                if (smr == null) smr = targetMeshGo.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = src;

                if (mr != null && (smr.sharedMaterials == null || smr.sharedMaterials.Length == 0))
                    smr.sharedMaterials = mr.sharedMaterials;

#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(mf, allowDestroyingAssets: false);
                if (mr != null) UnityEngine.Object.DestroyImmediate(mr, allowDestroyingAssets: false);
#else
                UnityEngine.Object.Destroy(mf);
                if (mr != null) UnityEngine.Object.Destroy(mr);
#endif

                mesh = smr.sharedMesh;
                note = "Converted MeshFilter->SkinnedMeshRenderer.";
                return true;
            }

            if (TryGetMeshFromGo(targetMeshGo, out var found))
            {
                mesh = found;
                note = "Mesh from child (best-effort).";
                return true;
            }

            note = "No mesh found.";
            return false;
        }

        // ------------------------------------------------------------
        // Weights + Channels
        // ------------------------------------------------------------

        private static void ApplyInitialWeights(SkinnedMeshRenderer smr, Mesh mesh, MayaBlendShapeMetadata meta, MayaImportLog log)
        {
            if (smr == null || mesh == null || meta == null || meta.targets == null) return;

            for (int i = 0; i < meta.targets.Count; i++)
            {
                var t = meta.targets[i];
                var nm = string.IsNullOrEmpty(t.name) ? null : t.name;
                if (string.IsNullOrEmpty(nm)) continue;

                int idx = mesh.GetBlendShapeIndex(nm);
                if (idx < 0) continue;

                float w = Mathf.Clamp(t.weight * 100f, 0f, 100f);
                try { smr.SetBlendShapeWeight(idx, w); }
                catch (Exception e)
                {
                    log?.Warn($"[blendShape] SetBlendShapeWeight failed: {nm} {e.GetType().Name}: {e.Message}");
                }
            }
        }

        private static void EnsureChannels(GameObject blendShapeGo, Mesh mesh, MayaBlendShapeMetadata meta)
        {
            if (blendShapeGo == null || mesh == null || meta == null || meta.targets == null) return;

            var existing = blendShapeGo.GetComponents<BlendShapeChannelNode>();
            if (existing != null)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] == null) continue;
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(existing[i], allowDestroyingAssets: false);
#else
                    UnityEngine.Object.Destroy(existing[i]);
#endif
                }
            }

            for (int i = 0; i < meta.targets.Count; i++)
            {
                var t = meta.targets[i];
                if (string.IsNullOrEmpty(t.name)) continue;

                int unityIdx = mesh.GetBlendShapeIndex(t.name);
                if (unityIdx < 0) continue;

                var ch = blendShapeGo.AddComponent<BlendShapeChannelNode>();
                ch.Initialize(t.name, Mathf.Clamp01(t.weight), unityIdx);
            }
        }

        // ------------------------------------------------------------
        // Audit
        // ------------------------------------------------------------

        private static void EnsureAudit(GameObject baseMeshGo, MayaBlendShapeComponent bs, MayaBlendShapeMetadata meta, bool applied, int appliedCount, int skippedCount, string note)
        {
            if (baseMeshGo == null || bs == null) return;

            var a = baseMeshGo.GetComponent<MayaBlendShapeApplyResult>();
            if (a == null) a = baseMeshGo.AddComponent<MayaBlendShapeApplyResult>();

            a.blendShapeNodeName = bs.BlendShapeNodeName ?? "";
            a.metaTargetCount = meta?.targets != null ? meta.targets.Count : 0;

            a.decodedTargetCount = bs.TargetCount;
            a.appliedTargetCount = appliedCount;
            a.skippedTargetCount = skippedCount;

            a.appliedToUnity = applied;
            a.note = note ?? "";
            a.updatedUtc = DateTime.UtcNow.ToString("o");
        }

        // ------------------------------------------------------------
        // Utilities
        // ------------------------------------------------------------

        private static Vector3[] EmptyVec3(int n) => new Vector3[n];

        private static Vector3[] SubtractArrays(Vector3[] a, Vector3[] b)
        {
            int n = Mathf.Min(a != null ? a.Length : 0, b != null ? b.Length : 0);
            var o = new Vector3[n];
            for (int i = 0; i < n; i++) o[i] = a[i] - b[i];
            return o;
        }

        private static bool IsAllZero(Vector3[] v)
        {
            if (v == null || v.Length == 0) return true;
            for (int i = 0; i < v.Length; i++)
                if (v[i].sqrMagnitude > 1e-18f) return false;
            return true;
        }

        private static string MakeUnityBlendShapeName(string blendShapeNodeName, int targetIndex, string mayaTargetMesh, HashSet<string> existingNames)
        {
            string leaf = MayaPlugUtil.LeafName(mayaTargetMesh ?? "");
            if (string.IsNullOrEmpty(leaf)) leaf = $"target_{targetIndex:D3}";

            string baseName = $"MAYA_BS_{targetIndex:D3}_{leaf}";
            string name = baseName;
            int k = 1;
            while (existingNames != null && existingNames.Contains(name))
            {
                name = baseName + "_" + k;
                k++;
            }

            existingNames?.Add(name);
            return name;
        }
    }

    // =========================================================
    // Lossless-ish data holders (100% claim evidence)
    // =========================================================

    [DisallowMultipleComponent]
    public sealed class MayaBlendShapeComponent : MonoBehaviour
    {
        [Header("Targets (decoded when possible)")]
        public MayaBlendShapeTarget[] Targets = Array.Empty<MayaBlendShapeTarget>();
        public int TargetCount => Targets != null ? Targets.Length : 0;

        [Header("Raw (always kept)")]
        public string BlendShapeNodeName;
        public string RawNote;

        [Header("Decode / Apply Result")]
        public bool HasDecodedTargets;
        public string DecodeNote;

        public bool AppliedToUnity;
        public string ApplyNote;
    }

    [Serializable]
    public sealed class MayaBlendShapeTarget
    {
        public string Name;
        public float FrameWeight = 100f;

        public Vector3[] DeltaVertices;
        public Vector3[] DeltaNormals;
        public Vector3[] DeltaTangents;

        public string SourceNote;
    }

    // =========================================================
    // Embedded ipt decoder (best-effort)
    // =========================================================
    internal static class EmbeddedBlendShapeDecoder
    {
        /// <summary>
        /// Try decode embedded point deltas from blendShape Attributes.
        /// Typical Maya patterns (short/long mix):
        /// - ".itg[i].iti[6000].ipt" (short)
        /// - ".inputTargetGroup[i].inputTargetItem[6000].inputPointsTarget" (long)
        ///
        /// Indices (sparse) may appear as:
        /// - ".itg[i].iti[6000].ict" / "inputComponentsTarget"
        /// If indices not found, we accept "full array" if points count == vertexCount.
        /// </summary>
        public static bool TryDecodeEmbeddedPoints(
            MayaNodeComponentBase blendShapeNode,
            int targetIndex,
            int baseVertexCount,
            int[] expandedToOrig,
            out Vector3[] deltaVertices,
            out string note)
        {
            deltaVertices = null;
            note = null;

            if (blendShapeNode == null || blendShapeNode.Attributes == null || blendShapeNode.Attributes.Count == 0)
            {
                note = "No attributes.";
                return false;
            }

            // 1) find points attribute candidate (largest token count wins)
            var pointsAttr = FindBestPointsAttribute(blendShapeNode.Attributes, targetIndex);
            if (pointsAttr == null || pointsAttr.Tokens == null || pointsAttr.Tokens.Count == 0)
            {
                note = "No ipt/inputPointsTarget attribute found.";
                return false;
            }

            var floats = CollectFloats(pointsAttr.Tokens);
            if (floats.Count < 3)
            {
                note = "Points tokens have too few floats.";
                return false;
            }

            // Heuristic: if first float is integer count and remaining >= 3*count -> use it
            int offset = 0;
            int pointCount = -1;

            if (LooksLikeCountHeader(floats[0], out int headerCount) && floats.Count >= 1 + 3 * headerCount)
            {
                pointCount = headerCount;
                offset = 1;
            }
            else
            {
                pointCount = floats.Count / 3;
                offset = 0;
            }

            if (pointCount <= 0 || floats.Count < offset + 3 * pointCount)
            {
                note = $"PointCount invalid. floats={floats.Count}, offset={offset}, pointCount={pointCount}";
                return false;
            }

            // Build point list
            var pts = new Vector3[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                int b = offset + 3 * i;
                pts[i] = new Vector3(floats[b + 0], floats[b + 1], floats[b + 2]);
            }

            // 2) try find indices (ict/inputComponentsTarget)
            var idxAttr = FindBestIndexAttribute(blendShapeNode.Attributes, targetIndex);
            var indices = (idxAttr != null) ? CollectIndices(idxAttr.Tokens) : new List<int>();

            // 3) expand to base vertexCount
            // Case A: sparse indices exist and match pointCount
            if (indices.Count > 0 && indices.Count == pointCount)
            {
                deltaVertices = new Vector3[baseVertexCount];

                if (expandedToOrig != null && expandedToOrig.Length == baseVertexCount)
                {
                    // indices are usually original; convert to expanded by mapping
                    // Build a lookup original->delta, then assign per expanded vertex
                    var map = new Dictionary<int, Vector3>(indices.Count);
                    for (int i = 0; i < indices.Count; i++)
                    {
                        int iv = indices[i];
                        if (iv < 0) continue;
                        map[iv] = pts[i];
                    }

                    for (int ev = 0; ev < baseVertexCount; ev++)
                    {
                        int ov = expandedToOrig[ev];
                        if (ov >= 0 && map.TryGetValue(ov, out var d))
                            deltaVertices[ev] = d;
                    }

                    note = $"OK sparse(indices) remapped via expandedToOrig. points={pointCount}";
                    return true;
                }
                else
                {
                    for (int i = 0; i < indices.Count; i++)
                    {
                        int v = indices[i];
                        if (v < 0 || v >= baseVertexCount) continue;
                        deltaVertices[v] = pts[i];
                    }

                    note = $"OK sparse(indices) direct. points={pointCount}";
                    return true;
                }
            }

            // Case B: full array matches baseVertexCount
            if (pointCount == baseVertexCount)
            {
                deltaVertices = pts;
                note = $"OK full array direct. points={pointCount}";
                return true;
            }

            // Case C: full array matches original (expanded mapping exists)
            if (expandedToOrig != null && expandedToOrig.Length == baseVertexCount)
            {
                // try treat pts as original-space deltas; assign to expanded vertex by original index
                int originalCount = pointCount;
                deltaVertices = new Vector3[baseVertexCount];

                for (int ev = 0; ev < baseVertexCount; ev++)
                {
                    int ov = expandedToOrig[ev];
                    if (ov >= 0 && ov < originalCount)
                        deltaVertices[ev] = pts[ov];
                }

                note = $"OK full array remapped via expandedToOrig. points={pointCount}";
                return true;
            }

            note = $"Cannot place embedded points: points={pointCount}, baseV={baseVertexCount}, indices={indices.Count}";
            deltaVertices = null;
            return false;
        }

        private static MayaNodeComponentBase.SerializedAttribute FindBestPointsAttribute(List<MayaNodeComponentBase.SerializedAttribute> attrs, int targetIndex)
        {
            MayaNodeComponentBase.SerializedAttribute best = null;
            int bestScore = -1;

            string gLong = $"inputTargetGroup[{targetIndex}]";
            string gShort = $"itg[{targetIndex}]";

            for (int i = 0; i < attrs.Count; i++)
            {
                var a = attrs[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null) continue;

                var k = a.Key;

                bool hitGroup = k.IndexOf(gLong, StringComparison.Ordinal) >= 0 ||
                                k.IndexOf(gShort, StringComparison.Ordinal) >= 0;

                if (!hitGroup) continue;

                bool looksPoints =
                    k.IndexOf("inputPointsTarget", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    k.IndexOf(".ipt", StringComparison.Ordinal) >= 0 ||
                    k.IndexOf("ipt", StringComparison.Ordinal) >= 0;

                if (!looksPoints) continue;

                // prefer iti[6000] (base shape item)
                int score = a.Tokens.Count;
                if (k.IndexOf("iti[6000]", StringComparison.OrdinalIgnoreCase) >= 0) score += 100000;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = a;
                }
            }

            return best;
        }

        private static MayaNodeComponentBase.SerializedAttribute FindBestIndexAttribute(List<MayaNodeComponentBase.SerializedAttribute> attrs, int targetIndex)
        {
            MayaNodeComponentBase.SerializedAttribute best = null;
            int bestScore = -1;

            string gLong = $"inputTargetGroup[{targetIndex}]";
            string gShort = $"itg[{targetIndex}]";

            for (int i = 0; i < attrs.Count; i++)
            {
                var a = attrs[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null) continue;

                var k = a.Key;

                bool hitGroup = k.IndexOf(gLong, StringComparison.Ordinal) >= 0 ||
                                k.IndexOf(gShort, StringComparison.Ordinal) >= 0;

                if (!hitGroup) continue;

                bool looksIdx =
                    k.IndexOf("inputComponentsTarget", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    k.IndexOf(".ict", StringComparison.Ordinal) >= 0 ||
                    k.IndexOf("ict", StringComparison.Ordinal) >= 0;

                if (!looksIdx) continue;

                int score = a.Tokens.Count;
                if (k.IndexOf("iti[6000]", StringComparison.OrdinalIgnoreCase) >= 0) score += 100000;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = a;
                }
            }

            return best;
        }

        private static List<float> CollectFloats(List<string> tokens)
        {
            var list = new List<float>(tokens != null ? tokens.Count : 0);
            if (tokens == null) return list;

            for (int i = 0; i < tokens.Count; i++)
            {
                if (float.TryParse((tokens[i] ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    list.Add(f);
            }
            return list;
        }

        private static bool LooksLikeCountHeader(float f, out int count)
        {
            count = 0;
            // integer-ish and non-negative, reasonably small (guard)
            int c = (int)Mathf.Round(f);
            if (c < 0) return false;
            if (Mathf.Abs(f - c) > 1e-4f) return false;
            count = c;
            return true;
        }

        private static List<int> CollectIndices(List<string> tokens)
        {
            var list = new List<int>(tokens != null ? tokens.Count : 0);
            if (tokens == null) return list;

            for (int i = 0; i < tokens.Count; i++)
            {
                var s = tokens[i];
                if (string.IsNullOrEmpty(s)) continue;

                // plain int
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                {
                    list.Add(v);
                    continue;
                }

                // patterns like "vtx[123]" or "vtx[0:7]" (take start..end)
                int p = s.IndexOf('[', StringComparison.Ordinal);
                int q = s.IndexOf(']', StringComparison.Ordinal);
                if (p >= 0 && q > p)
                {
                    var inside = s.Substring(p + 1, q - p - 1);
                    int colon = inside.IndexOf(':');
                    if (colon < 0)
                    {
                        if (int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vi))
                            list.Add(vi);
                    }
                    else
                    {
                        var a = inside.Substring(0, colon);
                        var b = inside.Substring(colon + 1);
                        if (int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vs) &&
                            int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ve))
                        {
                            if (ve < vs) (vs, ve) = (ve, vs);
                            // expand range (guard length)
                            int len = Mathf.Min(ve - vs + 1, 200000);
                            for (int k = 0; k < len; k++)
                                list.Add(vs + k);
                        }
                    }
                }
            }

            return list;
        }
    }
}
