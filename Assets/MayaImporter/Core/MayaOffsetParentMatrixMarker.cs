using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Marker component placed on an inserted parent GameObject that represents a Maya transform's
    /// offsetParentMatrix (Maya 2020+ transform stack).
    ///
    /// This enables idempotent imports (avoid inserting multiple parents) and keeps debug data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaOffsetParentMatrixMarker : MonoBehaviour
    {
        public string ChildMayaNodeName;
        public string Source; // Attr:... or Conn:...

        public Matrix4x4 OffsetParentMatrixMaya = Matrix4x4.identity;
        public Matrix4x4 OffsetParentMatrixUnity = Matrix4x4.identity;
    }
}
