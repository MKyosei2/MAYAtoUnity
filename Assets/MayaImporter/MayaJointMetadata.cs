using UnityEngine;

namespace MayaImporter.Components
{
    /// <summary>
    /// Unityに存在しない Maya joint 固有情報を保持する（100点条件：捨てない）
    /// </summary>
    public sealed class MayaJointMetadata : MonoBehaviour
    {
        public Vector3 jointOrientDegrees;
        public int rotateOrder;
        public bool segmentScaleCompensate;
    }
}
