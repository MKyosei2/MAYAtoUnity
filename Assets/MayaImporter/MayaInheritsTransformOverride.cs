using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Maya transform has "inheritsTransform" (it). Unity has no equivalent.
    /// We approximate by keeping the desired world TRS, recomputing local TRS against parent.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MayaInheritsTransformOverride : MonoBehaviour
    {
        public bool Enabled = true;

        public Vector3 DesiredWorldPosition;
        public Quaternion DesiredWorldRotation = Quaternion.identity;
        public Vector3 DesiredWorldScale = Vector3.one;

        private void LateUpdate()
        {
            if (!Enabled) return;
            Apply();
        }

        private void OnEnable()
        {
            if (!Enabled) return;
            Apply();
        }

        public void CaptureFromCurrentWorld()
        {
            DesiredWorldPosition = transform.position;
            DesiredWorldRotation = transform.rotation;
            DesiredWorldScale = transform.lossyScale;
        }

        private void Apply()
        {
            var p = transform.parent;
            if (p == null)
            {
                transform.SetPositionAndRotation(DesiredWorldPosition, DesiredWorldRotation);
                transform.localScale = DesiredWorldScale;
                return;
            }

            // Build desired world matrix from TRS
            var worldM = Matrix4x4.TRS(DesiredWorldPosition, DesiredWorldRotation, DesiredWorldScale);
            // Convert to local: local = parent^-1 * world
            var localM = p.worldToLocalMatrix * worldM;

            DecomposeTRS(localM, out var lp, out var lr, out var ls);

            transform.localPosition = lp;
            transform.localRotation = lr;
            transform.localScale = ls;
        }

        private static void DecomposeTRS(Matrix4x4 m, out Vector3 pos, out Quaternion rot, out Vector3 scale)
        {
            pos = m.GetColumn(3);

            var x = m.GetColumn(0);
            var y = m.GetColumn(1);
            var z = m.GetColumn(2);

            scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            if (scale.x != 0f) x /= scale.x;
            if (scale.y != 0f) y /= scale.y;
            if (scale.z != 0f) z /= scale.z;

            // Reconstruct rotation (best-effort)
            rot = Quaternion.LookRotation(z, y);
        }
    }
}
