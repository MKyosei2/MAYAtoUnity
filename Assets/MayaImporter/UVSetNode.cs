using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Represents a Maya UV set.
    /// </summary>
    [DisallowMultipleComponent]
    public class UVSetNode : MonoBehaviour
    {
        [Header("UV Set")]
        public string setName;
        public Vector2[] uvs;

        public void Initialize(string name, Vector2[] data)
        {
            setName = name;
            uvs = data;
        }
    }
}
