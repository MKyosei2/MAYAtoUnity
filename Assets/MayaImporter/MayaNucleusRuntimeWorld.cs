using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Dynamics
{
    /// <summary>
    /// Unity-only representation of Maya nucleus solver settings.
    /// This does NOT aim for 1:1 simulation; it guarantees "data exists in Unity" without Maya/API.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaNucleusRuntimeWorld : MonoBehaviour
    {
        [Header("Source")]
        public string SourceNodeName;

        [Header("Settings (best-effort)")]
        public Vector3 GravityDirection = Vector3.down;
        public float GravityMagnitude = 9.8f;
        public float TimeScale = 1f;
        public int SubSteps = 4;
        public float SpaceScale = 1f;

        [Header("Connections (names)")]
        public List<string> ConnectedDynamicsNodes = new List<string>();
    }
}
