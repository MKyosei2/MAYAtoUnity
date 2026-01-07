// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Maya SkinCluster m[hɑΉ Unity NX
    /// MayãXLjO Unity SkinnedMeshRenderer ֍č\z
    /// </summary>
    public class MayaSkinClusterNode : MonoBehaviour
    {
        [Header("Maya SkinCluster Info")]
        public string mayaNodeName;

        [Header("Skin Data")]
        public Transform[] bones;
        public Matrix4x4[] bindPoses;
        public BoneWeight[] boneWeights;

        /// <summary>
        /// SkinnedMeshRenderer \z
        /// </summary>
        public void Apply(Mesh mesh)
        {
            var smr = GetComponent<SkinnedMeshRenderer>();
            if (smr == null)
                smr = gameObject.AddComponent<SkinnedMeshRenderer>();

            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.sharedMesh.bindposes = bindPoses;
            smr.sharedMesh.boneWeights = boneWeights;
        }
    }
}
