// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: timeToUnitConversion
// Production: meaningful decode + publish scalar output.
//
// Best-effort:
//  output = input * conversionFactor
//
// Notes:
// - Maya "time" unit differences are scene-dependent (fps). Here we preserve factor-based conversion.
// - Downstream graph can still be reconstructed deterministically.

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("timeToUnitConversion")]
    public sealed class MayaGenerated_TimeToUnitConversionNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (timeToUnitConversion)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private float conversionFactor = 1f;
        [SerializeField] private float input = 0f;
        [SerializeField] private float output = 0f;

        [SerializeField] private string incomingInputPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            conversionFactor = ReadFloat(1f,
                ".cf", "cf",
                ".conversionFactor", "conversionFactor",
                ".factor", "factor",
                ".multiplier", "multiplier");

            input = ReadFloat(0f,
                ".i", "i",
                ".input", "input",
                ".time", "time",
                ".inputTime", "inputTime");

            incomingInputPlug = FindLastIncomingTo("i", "input", "time", "inputTime");

            output = enabled ? (input * conversionFactor) : input;

            var outVal = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            outVal.Set(output, output);

            SetNotes($"timeToUnitConversion decoded: enabled={enabled}, factor={conversionFactor:0.#####}, in={input:0.#####}, out={output:0.#####}, src={incomingInputPlug ?? "none"}");
            log.Info($"[timeToUnitConversion] '{NodeName}' enabled={enabled} factor={conversionFactor:0.#####} in={input:0.#####} out={output:0.#####}");
        }
    }
}
