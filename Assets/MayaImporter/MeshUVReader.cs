// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Stores mesh UV data (single UV set).
    /// </summary>
    [DisallowMultipleComponent]
    public class MeshUVReader : MonoBehaviour
    {
        public Vector2[] uvs;

        public void Initialize(Vector2[] data)
        {
            uvs = data;
        }
    }
}
