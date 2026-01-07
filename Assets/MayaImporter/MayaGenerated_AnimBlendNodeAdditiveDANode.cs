// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: animBlendNodeAdditiveDA
// Production: meaningful decode + best-effort evaluation (doubleAngle -> scalar degrees)
//
// Best-effort semantics (degrees):
//  outputDeg = inputAdeg * weightA + inputBdeg * weightB
//
// Optional: normalize to [-180,180] if normalizeAngle = true (default false unless attr suggests shortest).

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("animBlendNodeAdditiveDA")]
    public sealed class MayaGenerated_AnimBlendNodeAdditiveDANode : MayaPhaseCNodeBase
    {
        [Header("Decoded (animBlendNodeAdditiveDA)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private float inputADeg;
        [SerializeField] private float inputBDeg;

        [SerializeField] private float weightA = 1f;
        [SerializeField] private float weightB = 1f;
        [SerializeField] private float weight = 1f;

        [SerializeField] private bool normalizeAngle = false;
        [SerializeField] private float outputDeg;

        [Header("Incoming Plugs (best-effort)")]
        [SerializeField] private string srcInputAPlug;
        [SerializeField] private string srcInputBPlug;
        [SerializeField] private string srcWeightPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            inputADeg = ReadFloat(0f, ".inputA", "inputA", ".ia", "ia", ".input", "input", ".inA", "inA");
            inputBDeg = ReadFloat(0f, ".inputB", "inputB", ".ib", "ib", ".additive", "additive", ".inB", "inB");

            weightA = ReadFloat(1f, ".weightA", "weightA", ".wa", "wa");
            weightB = ReadFloat(float.NaN, ".weightB", "weightB", ".wb", "wb");
            weight = ReadFloat(1f, ".weight", "weight", ".w", "w", ".envelope", "envelope", ".env", "env");

            if (float.IsNaN(weightB))
                weightB = weight;

            // heuristic: if node has attribute names suggesting shortest/normalize, enable normalization
            bool shortest = ReadBool(false, ".shortest", "shortest", ".useShortest", "useShortest", ".normalizeAngle", "normalizeAngle");
            normalizeAngle = normalizeAngle || shortest;

            srcInputAPlug = FindIncomingPlugContains("inputA", "ia", "inA", "input");
            srcInputBPlug = FindIncomingPlugContains("inputB", "ib", "inB", "additive");
            srcWeightPlug = FindIncomingPlugContains("weight", ".w", "envelope", "env");

            outputDeg = enabled ? (inputADeg * weightA + inputBDeg * weightB) : inputADeg;
            if (normalizeAngle) outputDeg = Normalize180(outputDeg);

            // publish scalar
            var outVal = GetComponent<MayaImporter.Nodes.MayaFloatValue>() ?? gameObject.AddComponent<MayaImporter.Nodes.MayaFloatValue>();
            outVal.valid = true;
            outVal.value = outputDeg;

            var meta = GetComponent<MayaAnimBlendAdditiveAngleMetadata>() ?? gameObject.AddComponent<MayaAnimBlendAdditiveAngleMetadata>();
            meta.valid = true;
            meta.enabled = enabled;
            meta.inputADeg = inputADeg;
            meta.inputBDeg = inputBDeg;
            meta.weightA = weightA;
            meta.weightB = weightB;
            meta.normalizeAngle = normalizeAngle;
            meta.outputDeg = outputDeg;
            meta.srcInputAPlug = srcInputAPlug;
            meta.srcInputBPlug = srcInputBPlug;
            meta.srcWeightPlug = srcWeightPlug;
            meta.lastBuildFrame = Time.frameCount;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, inA={inputADeg:0.###}deg, inB={inputBDeg:0.###}deg, wa={weightA:0.###}, wb={weightB:0.###}, out={outputDeg:0.###}deg, norm={normalizeAngle}");
            log.Info($"[animBlendNodeAdditiveDA] '{NodeName}' enabled={enabled} inA={inputADeg:0.###} inB={inputBDeg:0.###} wa={weightA:0.###} wb={weightB:0.###} out={outputDeg:0.###} norm={normalizeAngle}");
        }

        private static float Normalize180(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            if (deg < -180f) deg += 360f;
            return deg;
        }

        private string FindIncomingPlugContains(params string[] patterns)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (patterns == null || patterns.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;
                    if (dstAttr.Contains(pat, System.StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }
            return null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MayaAnimBlendAdditiveAngleMetadata : MonoBehaviour
    {
        public bool valid;
        public bool enabled;

        public float inputADeg;
        public float inputBDeg;
        public float weightA;
        public float weightB;

        public bool normalizeAngle;
        public float outputDeg;

        public string srcInputAPlug;
        public string srcInputBPlug;
        public string srcWeightPlug;

        public int lastBuildFrame;
    }
}
