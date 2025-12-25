// MayaImporter/RgbToHsvNode.cs
// NodeType: rgbToHsv
// Phase-1 (real decode + value publish)
//
// Publishes:
// - MayaVector3Value : outHsv = (H,S,V) in xyz
// - MayaFloatValue   : outH as convenience

using System.Globalization;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Shaders
{
    [DisallowMultipleComponent]
    [MayaNodeType("rgbToHsv")]
    public sealed class RgbToHsvNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (rgbToHsv)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private Vector3 inRgb = Vector3.zero;

        [Header("Incoming (best-effort)")]
        [SerializeField] private string incomingInRgb;

        [Header("Output (computed)")]
        [SerializeField] private Vector3 outHsv = Vector3.zero;
        [SerializeField] private float outH = 0f;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            inRgb = ReadVec3(
                def: Vector3.zero,
                packedKeys: new[] { ".inRgb", "inRgb", ".input", "input", ".inColor", "inColor", ".color", "color" },
                xKeys: new[] { ".inRgbR", "inRgbR", ".inputR", "inputR", ".inColorR", "inColorR", ".colorR", "colorR" },
                yKeys: new[] { ".inRgbG", "inRgbG", ".inputG", "inputG", ".inColorG", "inColorG", ".colorG", "colorG" },
                zKeys: new[] { ".inRgbB", "inRgbB", ".inputB", "inputB", ".inColorB", "inColorB", ".colorB", "colorB" }
            );

            incomingInRgb = FindLastIncomingTo(
                "inRgb", "inRgbR", "inRgbG", "inRgbB",
                "input", "inputR", "inputG", "inputB",
                "inColor", "inColorR", "inColorG", "inColorB",
                "color", "colorR", "colorG", "colorB"
            );

            if (!enabled)
            {
                outHsv = Vector3.zero;
                outH = 0f;
                Publish();
                SetNotes($"{NodeType} '{NodeName}' disabled.");
                return;
            }

            // stable clamp (shader nodes typically operate in 0..1)
            float r = Mathf.Clamp01(inRgb.x);
            float g = Mathf.Clamp01(inRgb.y);
            float b = Mathf.Clamp01(inRgb.z);

            Color.RGBToHSV(new Color(r, g, b, 1f), out float h, out float s, out float v);
            outHsv = new Vector3(h, s, v);
            outH = h;

            Publish();

            SetNotes(
                $"{NodeType} '{NodeName}' enabled inRgb=({r:0.###},{g:0.###},{b:0.###}) " +
                $"outHsv=({outHsv.x:0.###},{outHsv.y:0.###},{outHsv.z:0.###}) incoming={(string.IsNullOrEmpty(incomingInRgb) ? "none" : incomingInRgb)}"
            );
        }

        private void Publish()
        {
            var v3 = GetComponent<MayaVector3Value>() ?? gameObject.AddComponent<MayaVector3Value>();
            v3.Set(MayaVector3Value.Kind.Vector, outHsv, outHsv);

            var f = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            f.Set(outH);
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
