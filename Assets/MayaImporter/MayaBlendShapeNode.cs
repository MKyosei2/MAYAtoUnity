using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Maya BlendShape ノードに対応する Unity 側クラス
    /// Unity Mesh の BlendShape として再構築する
    /// </summary>
    public class MayaBlendShapeNode : MonoBehaviour
    {
        [Header("Maya BlendShape Info")]
        public string mayaNodeName;
        public string blendShapeName;

        [Header("BlendShape Delta Data")]
        public Vector3[] deltaVertices;
        public Vector3[] deltaNormals;
        public Vector3[] deltaTangents;

        /// <summary>
        /// BlendShape を Mesh に適用
        /// </summary>
        public void Apply(Mesh mesh)
        {
            if (mesh == null)
            {
                Debug.LogError("Mesh is null. Cannot apply BlendShape.");
                return;
            }

            mesh.AddBlendShapeFrame(
                blendShapeName,
                100f,
                deltaVertices,
                deltaNormals,
                deltaTangents
            );
        }
    }
}
