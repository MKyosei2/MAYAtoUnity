// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Geometry;
using MayaImporter.Runtime;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// NodeType: skinCluster
    ///
    /// 100%j:
    /// - UnityɗƂ͈͂ SkinnedMeshRenderer ɓKp
    /// - Unity(1_4EFCg)ŗ MayaSkinClusterComponent ɊSێ
    /// - Kpʂ^[QbgMesh MayaSkinApplyResult ƂĎcičE؋j
    /// </summary>
    [MayaNodeType("skinCluster")]
    [DisallowMultipleComponent]
    public sealed class SkinClusterNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            // ɕς݂ȂXLbv
            var existing = GetComponent<MayaSkinClusterComponent>();
            if (existing != null && existing.AppliedToUnity)
                return;

            var comp = existing != null ? existing : gameObject.AddComponent<MayaSkinClusterComponent>();

            // ---- influences (index-aligned if possible) ----
            comp.InfluenceNames = MayaSkinWeights.CollectInfluenceNamesIndexed(this);

            // ---- weights (range supported) ----
            MayaSkinWeights.TryParseWeightsFromAttributes(this, out var weights, out var maxV, out var maxInf);

            comp.MaxVertexIndex = maxV;
            comp.MaxInfluenceIndex = maxInf;
            comp.FullWeights = weights ?? Array.Empty<MayaSkinWeights.SparseWeight>();

            // ---- target mesh ----
            var target = MayaSkinReconstruction.FindTargetMeshForSkinCluster(this);
            if (target == null)
            {
                comp.AppliedToUnity = false;
                comp.ApplyNote = "No target mesh found via connections. Weights preserved in component only.";

                log?.Warn("[skinCluster] " + comp.ApplyNote);

                // čiskinClusterɎcj
                EnsureAuditOnSelf(comp, null, applied: false, note: comp.ApplyNote);
                return;
            }

            // ---- apply ----
            bool ok = MayaSkinReconstruction.TryApplySkinClusterToTarget(comp, target, options, log, out var note);

            comp.AppliedToUnity = ok;
            comp.ApplyNote = note;

            if (ok) log?.Info("[skinCluster] " + note);
            else log?.Warn("[skinCluster] " + note);

            // ---- audit: attach to target mesh (portfolio proof) ----
            EnsureAuditOnTarget(comp, target, ok, note);

            // ---- also keep audit on skinCluster node (debug) ----
            EnsureAuditOnSelf(comp, target, ok, note);
        }

        private static void EnsureAuditOnTarget(MayaSkinClusterComponent skin, GameObject target, bool applied, string note)
        {
            if (skin == null || target == null) return;

            var r = target.GetComponent<MayaSkinApplyResult>();
            if (r == null) r = target.AddComponent<MayaSkinApplyResult>();

            r.skinClusterNodeName = skin.gameObject != null ? skin.gameObject.name : "";
            r.influenceCount = skin.InfluenceNames != null ? skin.InfluenceNames.Length : 0;
            r.fullWeightCount = skin.FullWeights != null ? skin.FullWeights.Length : 0;
            r.maxVertexIndex = skin.MaxVertexIndex;
            r.maxInfluenceIndex = skin.MaxInfluenceIndex;

            r.appliedToUnity = applied;
            r.note = note ?? "";

            r.unityClampedVertices = skin.UnityClampedVertices;
            r.unityClampedInfluences = skin.UnityClampedInfluences;

            // missing bone estimate
            r.missingBoneEstimate = EstimateMissingBones(target, skin.InfluenceNames);
            r.updatedUtc = DateTime.UtcNow.ToString("o");
        }

        private static void EnsureAuditOnSelf(MayaSkinClusterComponent skin, GameObject target, bool applied, string note)
        {
            if (skin == null) return;

            var r = skin.GetComponent<MayaSkinApplyResult>();
            if (r == null) r = skin.gameObject.AddComponent<MayaSkinApplyResult>();

            r.skinClusterNodeName = skin.gameObject != null ? skin.gameObject.name : "";
            r.influenceCount = skin.InfluenceNames != null ? skin.InfluenceNames.Length : 0;
            r.fullWeightCount = skin.FullWeights != null ? skin.FullWeights.Length : 0;
            r.maxVertexIndex = skin.MaxVertexIndex;
            r.maxInfluenceIndex = skin.MaxInfluenceIndex;

            r.appliedToUnity = applied;
            r.note = note ?? "";

            r.unityClampedVertices = skin.UnityClampedVertices;
            r.unityClampedInfluences = skin.UnityClampedInfluences;

            r.targetObjectName = target != null ? target.name : "";
            r.missingBoneEstimate = (target != null) ? EstimateMissingBones(target, skin.InfluenceNames) : 0;
            r.updatedUtc = DateTime.UtcNow.ToString("o");
        }

        private static int EstimateMissingBones(GameObject target, string[] influenceNames)
        {
            if (target == null || influenceNames == null || influenceNames.Length == 0) return 0;

            var smr = target.GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.bones == null || smr.bones.Length == 0) return influenceNames.Length;

            // groot fallbackh Ă鍜 missing Ƃ݂Ȃibest-effortj
            var root = target.transform.root;
            int missing = 0;

            int n = Mathf.Min(influenceNames.Length, smr.bones.Length);
            for (int i = 0; i < n; i++)
            {
                var inf = influenceNames[i] ?? "";
                var bone = smr.bones[i];

                if (string.IsNullOrEmpty(inf)) continue;
                if (bone == null) { missing++; continue; }

                // 炸 root ɒuP[X
                if (bone == root && !string.Equals(root.name, inf, StringComparison.Ordinal))
                    missing++;
            }

            return missing;
        }
    }

    /// <summary>
    /// UnityɗƂȂ skinCluster SێR|[lgB
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaSkinClusterComponent : MonoBehaviour
    {
        [Header("Influences (index-aligned if possible)")]
        public string[] InfluenceNames = Array.Empty<string>();

        [Header("Weights (Full Fidelity)")]
        [Tooltip("Sparse list: one entry per (vertex,influence,weight). Unity4{𒴂ĂɕێB")]
        public MayaSkinWeights.SparseWeight[] FullWeights = Array.Empty<MayaSkinWeights.SparseWeight>();

        public int MaxVertexIndex = -1;
        public int MaxInfluenceIndex = -1;

        [Header("Unity Apply Result")]
        public bool AppliedToUnity;
        public string ApplyNote;

        [Header("Audit (Unity limitation)")]
        public int UnityClampedVertices;
        public int UnityClampedInfluences;
    }
}


// ----------------------------------------------------------------------------- 
// INTEGRATED: SkinClusterEvalNode.cs
// -----------------------------------------------------------------------------
// PATCH: ProductionImpl v6 (Unity-only, retention-first)

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// SkinCluster Deformer EvalNode
    /// ���X�L�j���O�� Unity ���s�����߁ADirty �`�d�̂ݒS��
    /// </summary>
    public class SkinClusterEvalNode : EvalNode
    {
        private readonly MayaNode _mayaNode;

        public SkinClusterEvalNode(MayaNode node)
            : base(node.NodeName)
        {
            _mayaNode = node;
        }

        protected override void Evaluate(EvalContext ctx)
        {
            if (ctx == null)
                return;

            // skinCluster �� outMesh ���X�V����
            ctx.MarkAttributeDirty($"{NodeName}.outMesh");
        }
    }
}
