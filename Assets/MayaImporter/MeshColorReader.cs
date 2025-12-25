using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Reads and stores per-vertex color data from Maya.
    /// </summary>
    [DisallowMultipleComponent]
    public class MeshColorReader : MonoBehaviour
    {
        [Header("Vertex Colors")]
        public Color[] colors;

        /// <summary>
        /// Assign vertex colors.
        /// </summary>
        public void Initialize(Color[] vertexColors)
        {
            colors = vertexColors;
        }

        /// <summary>
        /// Apply colors to Unity mesh (optional helper).
        /// </summary>
        public void ApplyToMesh(Mesh mesh)
        {
            if (mesh == null || colors == null) return;
            if (colors.Length == mesh.vertexCount)
            {
                mesh.colors = colors;
            }
        }
    }
}
