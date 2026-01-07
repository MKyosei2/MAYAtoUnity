// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Stores mesh normal data.
    /// </summary>
    [DisallowMultipleComponent]
    public class MeshNormalReader : MonoBehaviour
    {
        public Vector3[] normals;

        public void Initialize(Vector3[] data)
        {
            normals = data;
        }
    }
}
