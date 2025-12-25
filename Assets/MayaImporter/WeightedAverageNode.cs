// Auto-generated placeholder -> Phase C implementation:
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Nodes
{
    [MayaNodeType("weightedAverage")]
    [DisallowMultipleComponent]
    public sealed class WeightedAverageNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (weightedAverage) - local preview")]
        [SerializeField] private bool normalizeWeights = true;

        [SerializeField] private int decodedCount;
        [SerializeField] private float[] inputs;
        [SerializeField] private float[] weights;

        [SerializeField] private float previewOutput;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            normalizeWeights = ReadBool(true, ".normalizeWeights", "normalizeWeights", ".nw", "nw");

            // Best-effort decode: support keys like
            //  ".input[3]" / ".i[3]"
            //  ".input[0:7]" (range) with tokens count matching
            //  ".weight[3]" / ".w[3]"
            var inMap = CollectIndexedScalars(".input", ".i");
            var wMap = CollectIndexedScalars(".weight", ".w");

            decodedCount = Mathf.Max(inMap.Count, wMap.Count);
            decodedCount = Mathf.Clamp(decodedCount, 0, 16);

            inputs = new float[decodedCount];
            weights = new float[decodedCount];

            for (int i = 0; i < decodedCount; i++)
            {
                inputs[i] = inMap.TryGetValue(i, out var iv) ? iv : 0f;
                weights[i] = wMap.TryGetValue(i, out var wv) ? wv : 1f;
            }

            // Local-only preview
            float wsum = 0f;
            float acc = 0f;
            for (int i = 0; i < decodedCount; i++)
            {
                wsum += weights[i];
                acc += inputs[i] * weights[i];
            }

            if (normalizeWeights && Mathf.Abs(wsum) > 1e-8f)
                previewOutput = acc / wsum;
            else
                previewOutput = acc;

            SetNotes($"weightedAverage decoded: N={decodedCount}, normalizeWeights={normalizeWeights}, previewOut={previewOutput} (local-only; connections preserved)");
        }

        private Dictionary<int, float> CollectIndexedScalars(string primaryPrefix, string altPrefix)
        {
            var map = new Dictionary<int, float>();

            if (Attributes == null || Attributes.Count == 0)
                return map;

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0)
                    continue;

                // Keys are stored like ".input[0]" etc
                if (!a.Key.StartsWith(primaryPrefix, StringComparison.Ordinal) &&
                    !a.Key.StartsWith(altPrefix, StringComparison.Ordinal))
                    continue;

                if (!TryParseIndexRange(a.Key, out var start, out var end))
                    continue;

                // If range, map sequentially using token list
                int stride = end - start + 1;
                int count = Mathf.Min(stride, a.Tokens.Count);

                for (int k = 0; k < count; k++)
                {
                    int idx = start + k;
                    if (idx < 0) continue;
                    if (idx >= 64) continue;

                    var s = (a.Tokens[k] ?? "").Trim();
                    if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        map[idx] = f;
                }
            }

            return map;
        }
    }
}
