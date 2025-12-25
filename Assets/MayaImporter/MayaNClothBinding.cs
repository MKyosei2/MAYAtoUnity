using UnityEngine;

namespace MayaImporter.Dynamics
{
    /// <summary>
    /// Keeps the imported nCloth data and resolved Unity references.
    /// Even if Unity Cloth cannot be attached, this component preserves all information.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaNClothBinding : MonoBehaviour
    {
        [Header("Source")]
        public string SourceNodeName;

        [Header("Resolved References")]
        public string MeshNodeName;
        public Transform MeshTransform;

        public string NucleusNodeName;
        public MayaNucleusRuntimeWorld NucleusWorld;

        [Header("nCloth Params (best-effort)")]
        public float StretchResistance = 50f;
        public float BendResistance = 50f;
        public float Damping = 0.1f;
        public float Friction = 0.2f;
        public float Thickness = 0.01f;
        public bool SelfCollision = false;
    }
}
