// Assets/MayaImporter/Core/MayaVector3Value.cs
// Vector/Point carrier used by multiplyPointByMatrix etc.
// Single authoritative definition.

using UnityEngine;

namespace MayaImporter.Core
{
    [DisallowMultipleComponent]
    public sealed class MayaVector3Value : MonoBehaviour
    {
        public enum Kind
        {
            Vector,
            Point
        }

        public bool valid;

        [Header("Type")]
        public Kind kind = Kind.Vector;

        [Header("Values")]
        public Vector3 mayaValue;
        public Vector3 unityValue;

        public void Set(Kind k, Vector3 maya, Vector3 unity)
        {
            kind = k;
            mayaValue = maya;
            unityValue = unity;
            valid = true;
        }
    }
}
