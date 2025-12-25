using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Links UV sets to a mesh or material slot.
    /// </summary>
    [DisallowMultipleComponent]
    public class UVLinkNode : MonoBehaviour
    {
        [Header("UV Linking")]
        public UVSetNode sourceUVSet;
        public string targetName;

        public void Initialize(UVSetNode uvSet, string target)
        {
            sourceUVSet = uvSet;
            targetName = target;
        }
    }
}
