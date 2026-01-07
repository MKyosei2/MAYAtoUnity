// MayaImporter/HsvToRgbNode.cs
// NodeType: hsvToRgb
// Production (real decode + value publish)
//
// Publishes:
// - MayaVector3Value : outRgb = (R,G,B) in xyz
// - MayaFloatValue   : outR as convenience

using System.Globalization;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Shaders
{
    [DisallowMultipleComponent]
    [MayaNodeType("hsvToRgb")]
    public sealed class HsvToRgbNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (hsvToRgb)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private Vector3 inHsv = Vector3.zero;

        [Header("Incoming (best-effort)")]
        [SerializeField] private string incomingInHsv;

        [Header("Output (computed)")]
        [SerializeField] private Vector3 outRgb = Vector3.zero;
        [SerializeField] private float outR = 0f;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            inHsv = ReadVec3(
                def: Vector3.zero,
                packedKeys: new[] { ".inHsv", "inHsv", ".input", "input" },
                xKeys: new[] { ".inHsvH", "inHsvH", ".inputX", "inputX", ".inHsvX", "inHsvX" },
                yKeys: new[] { ".inHsvS", "inHsvS", ".inputY", "inputY", ".inHsvY", "inHsvY" },
                zKeys: new[] { ".inHsvV", "inHsvV", ".inputZ", "inputZ", ".inHsvZ", "inHsvZ" }
            );

            incomingInHsv = FindLastIncomingTo(
                "inHsv", "inHsvH", "inHsvS", "inHsvV", "inHsvX", "inHsvY", "inHsvZ",
                "input", "inputX", "inputY", "inputZ"
            );

            if (!enabled)
            {
                outRgb = Vector3.zero;
                outR = 0f;
                Publish();
                SetNotes($"{NodeType} '{NodeName}' disabled.");
                return;
            }

            // Hue wraps; S/V clamp
            float h = Mathf.Repeat(inHsv.x, 1f);
            float s = Mathf.Clamp01(inHsv.y);
            float v = Mathf.Clamp01(inHsv.z);

            var c = Color.HSVToRGB(h, s, v, false);
            outRgb = new Vector3(c.r, c.g, c.b);
            outR = c.r;

            Publish();

            SetNotes(
                $"{NodeType} '{NodeName}' enabled inHsv=({h:0.###},{s:0.###},{v:0.###}) " +
                $"outRgb=({outRgb.x:0.###},{outRgb.y:0.###},{outRgb.z:0.###}) incoming={(string.IsNullOrEmpty(incomingInHsv) ? "none" : incomingInHsv)}"
            );
        }

        private void Publish()
        {
            var v3 = GetComponent<MayaVector3Value>() ?? gameObject.AddComponent<MayaVector3Value>();
            v3.Set(MayaVector3Value.Kind.Vector, outRgb, outRgb);

            var f = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            f.Set(outR);
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
