using UnityEngine;

namespace MayaImporter.DAG
{
    /// <summary>
    /// Maya axis node.
    /// Holds local axis display and orientation information.
    /// </summary>
    [DisallowMultipleComponent]
    public class AxisNode : MonoBehaviour
    {
        [Header("Axis Display")]
        public bool displayLocalAxis = false;

        [Header("Orientation")]
        public Vector3 axisOrientationEuler; // degrees

        /// <summary>
        /// Initialize axis data from Maya attributes.
        /// </summary>
        public void Initialize(bool display, Vector3 orientationEuler)
        {
            displayLocalAxis = display;
            axisOrientationEuler = orientationEuler;
        }

        /// <summary>
        /// Optional debug visualization in editor.
        /// </summary>
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!displayLocalAxis) return;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + transform.right * 0.5f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.5f);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);
        }
#endif
    }
}
