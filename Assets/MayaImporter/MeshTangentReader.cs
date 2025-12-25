using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Stores mesh tangent data.
    /// </summary>
    [DisallowMultipleComponent]
    public class MeshTangentReader : MonoBehaviour
    {
        public Vector4[] tangents;

        public void Initialize(Vector4[] data)
        {
            tangents = data;
        }
    }
}
