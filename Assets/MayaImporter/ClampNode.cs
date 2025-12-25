// MayaImporter/ClampNode.cs
// NodeType: clamp
// Phase-1 (real decode + value publish)
//
// Maya clamp (best-effort):
// - input (RGB) + min (RGB) + max (RGB) -> output (RGB)
// - also: inputA/minA/maxA -> outputA (best-effort)
//
// Publishes:
// - MayaVector3Value : output RGB
// - MayaFloatValue   : outputA

using System.Globalization;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Shaders
{
    [DisallowMultipleComponent]
    [MayaNodeType("clamp")]
    public sealed class ClampNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (clamp)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private Vector3 input = Vector3.zero;
        [SerializeField] private Vector3 min = Vector3.zero;
        [SerializeField] private Vector3 max = Vector3.one;

        [SerializeField] private float inputA = 1f;
        [SerializeField] private float minA = 0f;
        [SerializeField] private float maxA = 1f;

        [Header("Incoming (best-effort)")]
        [SerializeField] private string incomingInput;
        [SerializeField] private string incomingMin;
        [SerializeField] private string incomingMax;

        [Header("Output (computed)")]
        [SerializeField] private Vector3 output = Vector3.zero;
        [SerializeField] private float outputA = 1f;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            input = ReadVec3(
                def: Vector3.zero,
                packedKeys: new[] { ".input", "input", ".in", "in" },
                xKeys: new[] { ".inputR", "inputR", ".inR", "inR", ".inputX", "inputX" },
                yKeys: new[] { ".inputG", "inputG", ".inG", "inG", ".inputY", "inputY" },
                zKeys: new[] { ".inputB", "inputB", ".inB", "inB", ".inputZ", "inputZ" }
            );

            min = ReadVec3(
                def: Vector3.zero,
                packedKeys: new[] { ".min", "min" },
                xKeys: new[] { ".minR", "minR", ".minX", "minX" },
                yKeys: new[] { ".minG", "minG", ".minY", "minY" },
                zKeys: new[] { ".minB", "minB", ".minZ", "minZ" }
            );

            max = ReadVec3(
                def: Vector3.one,
                packedKeys: new[] { ".max", "max" },
                xKeys: new[] { ".maxR", "maxR", ".maxX", "maxX" },
                yKeys: new[] { ".maxG", "maxG", ".maxY", "maxY" },
                zKeys: new[] { ".maxB", "maxB", ".maxZ", "maxZ" }
            );

            inputA = ReadFloat(1f, ".inputA", "inputA", ".inA", "inA", ".inputAlpha", "inputAlpha");
            minA = ReadFloat(0f, ".minA", "minA", ".minAlpha", "minAlpha");
            maxA = ReadFloat(1f, ".maxA", "maxA", ".maxAlpha", "maxAlpha");

            incomingInput = FindLastIncomingTo("input", "inputR", "inputG", "inputB", "inputX", "inputY", "inputZ", "in", "inR", "inG", "inB");
            incomingMin = FindLastIncomingTo("min", "minR", "minG", "minB", "minX", "minY", "minZ");
            incomingMax = FindLastIncomingTo("max", "maxR", "maxG", "maxB", "maxX", "maxY", "maxZ");

            if (!enabled)
            {
                output = Vector3.zero;
                outputA = 0f;
                Publish();
                SetNotes($"{NodeType} '{NodeName}' disabled.");
                return;
            }

            output = new Vector3(
                Mathf.Clamp(input.x, min.x, max.x),
                Mathf.Clamp(input.y, min.y, max.y),
                Mathf.Clamp(input.z, min.z, max.z)
            );

            outputA = Mathf.Clamp(inputA, minA, maxA);

            Publish();

            SetNotes(
                $"{NodeType} '{NodeName}' enabled " +
                $"in=({input.x:0.###},{input.y:0.###},{input.z:0.###}) " +
                $"min=({min.x:0.###},{min.y:0.###},{min.z:0.###}) max=({max.x:0.###},{max.y:0.###},{max.z:0.###}) " +
                $"out=({output.x:0.###},{output.y:0.###},{output.z:0.###}) outA={outputA:0.###}"
            );
        }

        private void Publish()
        {
            var v3 = GetComponent<MayaVector3Value>() ?? gameObject.AddComponent<MayaVector3Value>();
            v3.Set(MayaVector3Value.Kind.Vector, output, output);

            var f = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            f.Set(outputA);
        }

        private Vector3 ReadVec3(Vector3 def, string[] packedKeys, string[] xKeys, string[] yKeys, string[] zKeys)
        {
            if (packedKeys != null)
            {
                for (int i = 0; i < packedKeys.Length; i++)
                {
                    var k = packedKeys[i];
                    if (string.IsNullOrEmpty(k)) continue;

                    if (TryGetTokens(k, out var t) && t != null && t.Count >= 3)
                    {
                        if (TryParseFloat(t[0], out var x) &&
                            TryParseFloat(t[1], out var y) &&
                            TryParseFloat(t[2], out var z))
                            return new Vector3(x, y, z);
                    }
                }
            }

            float xx = ReadFloat(def.x, xKeys);
            float yy = ReadFloat(def.y, yKeys);
            float zz = ReadFloat(def.z, zKeys);
            return new Vector3(xx, yy, zz);
        }

        private static bool TryParseFloat(string s, out float v)
        {
            return float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }
    }
}
