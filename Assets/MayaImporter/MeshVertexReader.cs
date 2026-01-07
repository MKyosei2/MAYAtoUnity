// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Stores mesh vertex positions.
    /// </summary>
    [DisallowMultipleComponent]
    public class MeshVertexReader : MonoBehaviour
    {
        public Vector3[] vertices;

        public void Initialize(Vector3[] data)
        {
            vertices = data;
        }
    }
}
