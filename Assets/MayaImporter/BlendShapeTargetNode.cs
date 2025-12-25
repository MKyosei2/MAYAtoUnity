using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Maya blendShape target.
    /// Holds delta geometry data.
    /// </summary>
    [DisallowMultipleComponent]
    public class BlendShapeTargetNode : MonoBehaviour
    {
        [Header("Target")]
        public string targetName;
        public int targetIndex;

        [Header("Delta Data")]
        public Vector3[] deltaVertices;
        public Vector3[] deltaNormals;

        /// <summary>
        /// Initialize target geometry deltas.
        /// </summary>
        public void Initialize(
            string name,
            int index,
            Vector3[] vertices,
            Vector3[] normals)
        {
            targetName = name;
            targetIndex = index;
            deltaVertices = vertices;
            deltaNormals = normals;
        }
    }
}
