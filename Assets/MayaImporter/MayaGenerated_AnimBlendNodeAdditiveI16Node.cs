// NodeType: animBlendNodeAdditiveI16
// FIX: publish via MayaImporter.Core.MayaFloatValue

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("animBlendNodeAdditiveI16")]
    public sealed class MayaGenerated_AnimBlendNodeAdditiveI16Node : MayaPhaseCNodeBase
    {
        [SerializeField] private bool enabled = true;

        [SerializeField] private int inputA;
        [SerializeField] private int inputB;

        [SerializeField] private float weightA = 1f;
        [SerializeField] private float weightB = 1f;
        [SerializeField] private float weight = 1f;

        [SerializeField] private float outputFloat;
        [SerializeField] private int outputI16;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            inputA = Mathf.RoundToInt(ReadFloat(0f, ".inputA", "inputA", ".ia", "ia", ".input", "input", ".inA", "inA"));
            inputB = Mathf.RoundToInt(ReadFloat(0f, ".inputB", "inputB", ".ib", "ib", ".additive", "additive", ".inB", "inB"));

            weightA = ReadFloat(1f, ".weightA", "weightA", ".wa", "wa");
            weightB = ReadFloat(float.NaN, ".weightB", "weightB", ".wb", "wb");
            weight = ReadFloat(1f, ".weight", "weight", ".w", "w", ".envelope", "envelope", ".env", "env");
            if (float.IsNaN(weightB)) weightB = weight;

            outputFloat = enabled ? (inputA * weightA + inputB * weightB) : inputA;
            outputI16 = ClampI16(Mathf.RoundToInt(outputFloat));

            var outVal = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            outVal.Set(outputFloat, outputFloat);

            SetNotes($"{NodeType} '{NodeName}' outF={outputFloat:0.###} outI16={outputI16}");
            log.Info($"[animBlendNodeAdditiveI16] '{NodeName}' enabled={enabled} outF={outputFloat:0.###} outI16={outputI16}");
        }

        private static int ClampI16(int v)
        {
            if (v < -32768) return -32768;
            if (v > 32767) return 32767;
            return v;
        }
    }
}
