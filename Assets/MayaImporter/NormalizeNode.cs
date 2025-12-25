// Auto-generated placeholder -> Phase C implementation:
using System;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Nodes
{
    [MayaNodeType("normalize")]
    [DisallowMultipleComponent]
    public sealed class NormalizeNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (normalize) - local preview")]
        [SerializeField] private Vector3 localInput;
        [SerializeField] private float magnitude;
        [SerializeField] private Vector3 normalized;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            // Try common explicit channels first
            float x = ReadFloat(0f, ".inputX", "inputX", ".ix", "ix");
            float y = ReadFloat(0f, ".inputY", "inputY", ".iy", "iy");
            float z = ReadFloat(0f, ".inputZ", "inputZ", ".iz", "iz");

            // Fallback: ".input[0:2]" or ".input[0]"...
            if (Mathf.Approximately(x, 0f) && Mathf.Approximately(y, 0f) && Mathf.Approximately(z, 0f))
            {
                // scan attributes for ".input[0:2]" / ".i[0:2]"
                ReadIndexed3Fallback(".input", ref x, ref y, ref z);
                ReadIndexed3Fallback(".i", ref x, ref y, ref z);
            }

            localInput = new Vector3(x, y, z);
            magnitude = localInput.magnitude;
            normalized = magnitude > 1e-8f ? (localInput / magnitude) : Vector3.zero;

            SetNotes($"normalize decoded: input={localInput}, mag={magnitude}, normalized={normalized} (local-only; connections preserved)");
        }

        private void ReadIndexed3Fallback(string prefix, ref float x, ref float y, ref float z)
        {
            if (Attributes == null) return;

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0)
                    continue;

                if (!a.Key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                if (!TryParseIndexRange(a.Key, out var start, out var end))
                    continue;

                // We only care about the first 3 values
                int count = Mathf.Min(3, a.Tokens.Count);
                for (int k = 0; k < count; k++)
                {
                    int idx = start + k;
                    var s = (a.Tokens[k] ?? "").Trim();
                    if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        continue;

                    if (idx == 0) x = f;
                    else if (idx == 1) y = f;
                    else if (idx == 2) z = f;
                }

                // If we got all three, stop early
                // (we canft reliably know; just break after one good match)
                break;
            }
        }
    }
}
