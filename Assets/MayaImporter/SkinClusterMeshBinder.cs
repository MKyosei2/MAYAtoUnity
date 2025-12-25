using UnityEngine;
using MayaImporter.Geometry;
using MayaImporter.Deformers;
using MayaImporter.Core;

namespace MayaImporter.Binder
{
    /// <summary>
    /// Binds Maya skinCluster data to Unity SkinnedMeshRenderer (legacy helper).
    ///
    /// Current recommended path:
    /// - SkinClusterNode.ApplyToUnity() creates MayaSkinClusterComponent and attempts reconstruction.
    /// - This binder calls reconstruction explicitly for manual/debug usage.
    /// </summary>
    [DisallowMultipleComponent]
    public class SkinClusterMeshBinder : MonoBehaviour
    {
        public SkinClusterNode skinClusterNode;
        public MeshNode targetMeshNode;

        [Tooltip("Legacy minimal reader (optional). If set and no FullWeights are available, uses this data.")]
        public SkinWeightReader weightReader;

        public void Bind()
        {
            if (skinClusterNode == null)
                return;

            var options = new MayaImportOptions();
            var log = new MayaImportLog();

            // Ensure PhaseC-ish application (creates MayaSkinClusterComponent and fills FullWeights best-effort)
            skinClusterNode.ApplyToUnity(options, log);

            var skinComp = skinClusterNode.GetComponent<MayaSkinClusterComponent>();
            if (skinComp == null)
                return;

            GameObject targetGo = null;

            if (targetMeshNode != null)
                targetGo = targetMeshNode.gameObject;
            else
                targetGo = MayaSkinReconstruction.FindTargetMeshForSkinCluster(skinClusterNode);

            if (targetGo == null)
                return;

            // Preferred: use preserved FullWeights (best-effort -> top4 applied inside reconstruction)
            if (skinComp.FullWeights != null && skinComp.FullWeights.Length > 0)
            {
                MayaSkinReconstruction.TryApplySkinClusterToTarget(skinComp, targetGo, options, log, out _);
                return;
            }

            // Fallback: legacy 1-weight-per-vertex
            if (weightReader != null)
            {
                var mf = targetGo.GetComponent<MeshFilter>();
                var smr = targetGo.GetComponent<SkinnedMeshRenderer>();
                if (smr == null) smr = targetGo.AddComponent<SkinnedMeshRenderer>();

                Mesh mesh = null;
                if (mf != null && mf.sharedMesh != null) mesh = mf.sharedMesh;
                if (mesh == null && smr.sharedMesh != null) mesh = smr.sharedMesh;

                if (mesh == null) return;

                smr.sharedMesh = mesh;
                if (mf != null)
                {
                    mf.sharedMesh = null;
                    DestroyImmediate(mf, allowDestroyingAssets: false);
                }

                ApplyLegacyWeights(mesh);
            }
        }

        private void ApplyLegacyWeights(Mesh mesh)
        {
            if (weightReader == null || mesh == null) return;
            if (weightReader.vertexCount <= 0) return;
            if (weightReader.jointIndices == null || weightReader.weights == null) return;
            if (weightReader.jointIndices.Length < weightReader.vertexCount) return;
            if (weightReader.weights.Length < weightReader.vertexCount) return;

            var boneWeights = new BoneWeight[weightReader.vertexCount];

            for (int v = 0; v < weightReader.vertexCount; v++)
            {
                BoneWeight bw = new BoneWeight
                {
                    boneIndex0 = weightReader.jointIndices[v],
                    weight0 = weightReader.weights[v],
                    boneIndex1 = 0,
                    weight1 = 0f,
                    boneIndex2 = 0,
                    weight2 = 0f,
                    boneIndex3 = 0,
                    weight3 = 0f
                };
                boneWeights[v] = bw;
            }

            mesh.boneWeights = boneWeights;
        }
    }
}
