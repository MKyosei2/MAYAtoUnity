using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Hair
{
    /// <summary>
    /// Maya hairSystem ‚Ì Unity ‘¤•Û{‰ğŒˆÏ‚İQÆB
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaHairSystemBinding : MonoBehaviour
    {
        [Header("Source")]
        public string SourceNodeName;

        [Header("Connected Follicles (names)")]
        public List<string> FollicleNodeNames = new List<string>();

        [Header("Resolved Follicles")]
        public List<Transform> FollicleTransforms = new List<Transform>();

        [Header("Resolved Roots")]
        public List<Transform> RootTransforms = new List<Transform>();

        [Header("Notes")]
        public string Notes;
    }
}
