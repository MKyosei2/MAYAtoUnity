using UnityEngine;

namespace MayaImporter.Components
{
    /// <summary>
    /// Optional: Maya face index -> Unity triangle range mapping.
    /// If you later populate these arrays during mesh decode,
    /// per-face material assignment becomes exact.
    /// </summary>
    public sealed class MayaMeshFaceTriangleMap : MonoBehaviour
    {
        // length = mayaFaceCount
        public int[] faceToTriStart;
        public int[] faceToTriCount;
    }
}
