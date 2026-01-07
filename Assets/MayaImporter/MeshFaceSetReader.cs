// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Stores face set / material group information.
    /// </summary>
    [DisallowMultipleComponent]
    public class MeshFaceSetReader : MonoBehaviour
    {
        public string[] faceSetNames;
        public int[][] faceIndices;

        public void Initialize(string[] names, int[][] indices)
        {
            faceSetNames = names;
            faceIndices = indices;
        }
    }
}
