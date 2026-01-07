// PATCH: ProductionImpl v6 (Unity-only, retention-first)
﻿// NodeType: animBlendNodeAdditiveFA
// FIX: publish via MayaImporter.Core.MayaFloatValue (degrees)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("animBlendNodeAdditiveFA")]
    public sealed class MayaGenerated_AnimBlendNodeAdditiveFANode : MayaPhaseCNodeBase
    {
        [SerializeField] private bool enabled = true;

        [SerializeField] private float inputADeg;
        [SerializeField] private float inputBDeg;

        [SerializeField] private float weightA = 1f;
        [SerializeField] private float weightB = 1f;
        [SerializeField] private float weight = 1f;

        [SerializeField] private bool normalizeAngle = false;
        [SerializeField] private float outputDeg;

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
            if (float.IsNaN(weightB)) weightB = weight;

            bool shortest = ReadBool(false, ".shortest", "shortest", ".useShortest", "useShortest", ".normalizeAngle", "normalizeAngle");
            normalizeAngle = normalizeAngle || shortest;

            outputDeg = enabled ? (inputADeg * weightA + inputBDeg * weightB) : inputADeg;
            if (normalizeAngle) outputDeg = Normalize180(outputDeg);

            var outVal = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            outVal.Set(outputDeg, outputDeg);

            SetNotes($"{NodeType} '{NodeName}' out={outputDeg:0.###}deg");
            log.Info($"[animBlendNodeAdditiveFA] '{NodeName}' enabled={enabled} out={outputDeg:0.###} norm={normalizeAngle}");
        }

        private static float Normalize180(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            if (deg < -180f) deg += 360f;
            return deg;
        }
    }
}
