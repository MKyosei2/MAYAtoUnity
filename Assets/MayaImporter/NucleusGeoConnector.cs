using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nucleusGeoConnector")]
    [DisallowMultipleComponent]
    public sealed class NucleusGeoConnector : MayaPhaseCNodeBase
    {
        [Header("Decoded (nucleusGeoConnector)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private string sourceGeo;
        [SerializeField] private string targetGeo;

        [SerializeField] private float strength;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            sourceGeo = ReadString("", ".sourceGeo", "sourceGeo", ".src", "src", ".source", "source");
            targetGeo = ReadString("", ".targetGeo", "targetGeo", ".dst", "dst", ".target", "target");
            strength = ReadFloat(0f, ".strength", "strength", ".s", "s");

            SetNotes($"nucleusGeoConnector decoded: enabled={enabled}, source='{sourceGeo}', target='{targetGeo}', strength={strength} (connections preserved)");
        }
    }
}
