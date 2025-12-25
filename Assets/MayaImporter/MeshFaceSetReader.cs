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
