// NodeType: animBlendNodeAdditiveF
// FIX: publish via MayaImporter.Core.MayaFloatValue (no ".value")

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("animBlendNodeAdditiveF")]
    public sealed class MayaGenerated_AnimBlendNodeAdditiveFNode : MayaPhaseCNodeBase
    {
        [SerializeField] private bool enabled = true;

        [SerializeField] private float inputA;
        [SerializeField] private float inputB;

        [SerializeField] private float weightA = 1f;
        [SerializeField] private float weightB = 1f;
        [SerializeField] private float weight = 1f;

        [SerializeField] private float output;

        [SerializeField] private string srcInputAPlug;
        [SerializeField] private string srcInputBPlug;
        [SerializeField] private string srcWeightPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            inputA = ReadFloat(0f, ".inputA", "inputA", ".ia", "ia", ".input", "input", ".inA", "inA");
            inputB = ReadFloat(0f, ".inputB", "inputB", ".ib", "ib", ".additive", "additive", ".inB", "inB");

            weightA = ReadFloat(1f, ".weightA", "weightA", ".wa", "wa");
            weightB = ReadFloat(float.NaN, ".weightB", "weightB", ".wb", "wb");
            weight = ReadFloat(1f, ".weight", "weight", ".w", "w", ".envelope", "envelope", ".env", "env");
            if (float.IsNaN(weightB)) weightB = weight;

            srcInputAPlug = FindIncomingPlugContains("inputA", "ia", "inA", "input");
            srcInputBPlug = FindIncomingPlugContains("inputB", "ib", "inB", "additive");
            srcWeightPlug = FindIncomingPlugContains("weight", ".w", "envelope", "env");

            output = enabled ? (inputA * weightA + inputB * weightB) : inputA;

            var outVal = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            outVal.Set(output, output);

            var meta = GetComponent<MayaAnimBlendAdditiveScalarMetadata>() ?? gameObject.AddComponent<MayaAnimBlendAdditiveScalarMetadata>();
            meta.valid = true;
            meta.enabled = enabled;
            meta.inputA = inputA;
            meta.inputB = inputB;
            meta.weightA = weightA;
            meta.weightB = weightB;
            meta.output = output;
            meta.srcInputAPlug = srcInputAPlug;
            meta.srcInputBPlug = srcInputBPlug;
            meta.srcWeightPlug = srcWeightPlug;
            meta.lastBuildFrame = Time.frameCount;

            SetNotes($"{NodeType} '{NodeName}' out={output:0.###}");
            log.Info($"[animBlendNodeAdditiveF] '{NodeName}' enabled={enabled} out={output:0.###}");
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
                    if (!string.IsNullOrEmpty(pat) && dstAttr.Contains(pat, System.StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }
            return null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MayaAnimBlendAdditiveScalarMetadata : MonoBehaviour
    {
        public bool valid;
        public bool enabled;

        public float inputA;
        public float inputB;
        public float weightA;
        public float weightB;
        public float output;

        public string srcInputAPlug;
        public string srcInputBPlug;
        public string srcWeightPlug;

        public int lastBuildFrame;
    }
}
