// Assets/MayaImporter/MayaFloatValue_NodesCompat.cs
// Compatibility scalar carrier used by legacy/generated nodes.
// Keeps MayaImporter.Nodes.MayaFloatValue alive so we don't have to edit 1000+ scripts again.

using UnityEngine;

namespace MayaImporter.Nodes
{
    [DisallowMultipleComponent]
    public sealed class MayaFloatValue : MonoBehaviour
    {
        public bool valid;
        public float value;

        public void Set(float v)
        {
            value = v;
            valid = true;

            // Optional: mirror into Core carrier if present (helps unify later)
            var core = GetComponent<MayaImporter.Core.MayaFloatValue>();
            if (core != null) core.Set(v);
        }

        public void Set(float maya, float unity) => Set(unity);
    }
}
