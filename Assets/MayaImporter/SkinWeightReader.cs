using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Stores per-vertex skin weights.
    /// </summary>
    [DisallowMultipleComponent]
    public class SkinWeightReader : MonoBehaviour
    {
        [Header("Skin Data")]
        public int vertexCount;
        public int[] jointIndices;
        public float[] weights;

        /// <summary>
        /// jointIndices and weights are parallel arrays.
        /// </summary>
        public void Initialize(int vtxCount, int[] joints, float[] wts)
        {
            vertexCount = vtxCount;
            jointIndices = joints;
            weights = wts;
        }
    }
}
