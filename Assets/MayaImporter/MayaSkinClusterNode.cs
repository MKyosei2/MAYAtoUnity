using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Maya SkinCluster ノードに対応する Unity 側クラス
    /// Mayaのスキニング情報を Unity SkinnedMeshRenderer へ再構築する
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
        /// SkinnedMeshRenderer を構築
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
