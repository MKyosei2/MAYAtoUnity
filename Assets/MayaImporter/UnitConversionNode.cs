// NodeType: unitConversion
// Production 강화:
// - Decodes conversionFactor + localInput
// - Publishes output via MayaImporter.Core.MayaFloatValue
// - Keeps incoming plug hint

using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Nodes
{
    [MayaNodeType("unitConversion")]
    [DisallowMultipleComponent]
    public sealed class UnitConversionNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (unitConversion)")]
        [SerializeField] private float conversionFactor = 1f;
        [SerializeField] private float localInput = 0f;
        [SerializeField] private float output = 0f;

        [SerializeField] private string incomingInputPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            conversionFactor = ReadFloat(1f,
                ".cf", "cf",
                ".conversionFactor", "conversionFactor",
                ".factor", "factor",
                ".multiplier", "multiplier");

            localInput = ReadFloat(0f,
                ".i", "i",
                ".input", "input",
                ".inputValue", "inputValue");

            incomingInputPlug = FindLastIncomingTo("i", "input", "inputValue");

            output = localInput * conversionFactor;

            var outVal = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            outVal.Set(output, output);

            SetNotes($"unitConversion decoded: factor={conversionFactor:0.#####}, in={localInput:0.#####}, out={output:0.#####}, src={incomingInputPlug ?? "none"}");
            log.Info($"[unitConversion] '{NodeName}' factor={conversionFactor:0.#####} in={localInput:0.#####} out={output:0.#####}");
        }
    }
}
