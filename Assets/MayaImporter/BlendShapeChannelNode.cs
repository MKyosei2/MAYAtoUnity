using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Represents a blendShape channel (one slider).
    /// </summary>
    [DisallowMultipleComponent]
    public class BlendShapeChannelNode : MonoBehaviour
    {
        [Header("Channel")]
        public string channelName;
        public float weight; // 0–1 normalized

        [Header("Target Index")]
        public int targetIndex = -1;

        /// <summary>
        /// Initialize channel data.
        /// </summary>
        public void Initialize(string name, float initialWeight, int index)
        {
            channelName = name;
            weight = initialWeight;
            targetIndex = index;
        }
    }
}
