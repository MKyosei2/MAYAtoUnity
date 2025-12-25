using UnityEngine;

namespace MayaImporter.Runtime
{
    /// <summary>
    /// Unity-side representation for Maya nodes that have no direct Unity equivalent.
    /// This is intentionally minimal and safe:
    /// - provides an explicit "reconstructed" marker component
    /// - can draw Gizmos in scene view for debugging/portfolio demonstration
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaOpaqueNodeRuntime : MonoBehaviour
    {
        [Header("Maya Identity")]
        public string mayaNodeType;
        public string mayaNodeName;
        public string mayaParentName;
        public string mayaUuid;

        [Header("Quick Stats (full data is on MayaNodeComponentBase)")]
        public int attributeCount;
        public int connectionCount;

        [Header("Debug")]
        public bool drawGizmos = true;
        public float gizmoSize = 0.05f;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            // Small wire cube at transform
            var p = transform.position;
            var s = Mathf.Max(0.001f, gizmoSize);
            Gizmos.DrawWireCube(p, new Vector3(s, s, s));

            // Label (Editor only)
            UnityEditor.Handles.Label(
                p,
                $"{mayaNodeType}\n{mayaNodeName}\nattrs:{attributeCount} conns:{connectionCount}"
            );
        }
#endif
    }
}
