// NodeType: animBlendNodeAdditiveFL
// FIX: publish via MayaImporter.Core.MayaFloatValue

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("animBlendNodeAdditiveFL")]
    public sealed class MayaGenerated_AnimBlendNodeAdditiveFLNode : MayaPhaseCNodeBase
    {
        [SerializeField] private bool enabled = true;

        [SerializeField] private float inputA;
        [SerializeField] private float inputB;

        [SerializeField] private float weightA = 1f;
        [SerializeField] private float weightB = 1f;
        [SerializeField] private float weight = 1f;

        [SerializeField] private float output;

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

            output = enabled ? (inputA * weightA + inputB * weightB) : inputA;

            var outVal = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            outVal.Set(output, output);

            SetNotes($"{NodeType} '{NodeName}' out={output:0.###}");
            log.Info($"[animBlendNodeAdditiveFL] '{NodeName}' enabled={enabled} out={output:0.###}");
        }
    }
}
