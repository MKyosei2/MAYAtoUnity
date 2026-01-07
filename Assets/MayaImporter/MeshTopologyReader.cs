// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Stores mesh topology (triangle indices).
    /// </summary>
    [DisallowMultipleComponent]
    public class MeshTopologyReader : MonoBehaviour
    {
        public int[] triangles;

        public void Initialize(int[] indices)
        {
            triangles = indices;
        }
    }
}
