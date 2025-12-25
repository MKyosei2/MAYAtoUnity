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
