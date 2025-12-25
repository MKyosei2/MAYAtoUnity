using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Maya bindPose node.
    /// Stores bind pose transform information for skinning.
    /// </summary>
    [DisallowMultipleComponent]
    public class BindPoseNode : MonoBehaviour
    {
        [Header("Bind Pose")]
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale = Vector3.one;

        /// <summary>
        /// Initialize bind pose data.
        /// </summary>
        public void Initialize(Vector3 pos, Quaternion rot, Vector3 scl)
        {
            position = pos;
            rotation = rot;
            scale = scl;
        }
    }
}
