// Assets/MayaImporter/BlendColorsNode.cs
// Phase-1 implementation (not stub)
// NodeType: blendColors
//
// - Decodes color1/color2 (+alpha1/alpha2 if present) + blender
// - Computes output = lerp(color1,color2,blender)
// - Publishes:
//    * MayaVector3Value (Core) for output color
//    * MayaFloatValue (Nodes) for output alpha (best-effort)
// - Stores incoming plug hints

using System;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Nodes
{
    [MayaNodeType("blendColors")]
    [DisallowMultipleComponent]
    public sealed class BlendColorsNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaBlendColorsMetadata>() ?? gameObject.AddComponent<MayaBlendColorsMetadata>();
            meta.valid = false;

            meta.blender = Mathf.Clamp01(ReadFloat(0.5f, ".blender", "blender", ".b", "b"));

            meta.color1 = ReadColor(Vector3.zero,
                packedKeys: new[] { ".color1", "color1", ".c1", "c1" },
                rKeys: new[] { ".color1R", "color1R", ".c1r", "c1r", ".color1X", "color1X" },
                gKeys: new[] { ".color1G", "color1G", ".c1g", "c1g", ".color1Y", "color1Y" },
                bKeys: new[] { ".color1B", "color1B", ".c1b", "c1b", ".color1Z", "color1Z" });

            meta.color2 = ReadColor(Vector3.one,
                packedKeys: new[] { ".color2", "color2", ".c2", "c2" },
                rKeys: new[] { ".color2R", "color2R", ".c2r", "c2r", ".color2X", "color2X" },
                gKeys: new[] { ".color2G", "color2G", ".c2g", "c2g", ".color2Y", "color2Y" },
                bKeys: new[] { ".color2B", "color2B", ".c2b", "c2b", ".color2Z", "color2Z" });

            meta.alpha1 = Mathf.Clamp01(ReadFloat(1f, ".alpha1", "alpha1", ".a1", "a1", ".color1A", "color1A", ".c1a", "c1a"));
            meta.alpha2 = Mathf.Clamp01(ReadFloat(1f, ".alpha2", "alpha2", ".a2", "a2", ".color2A", "color2A", ".c2a", "c2a"));

            meta.incomingColor1Plug = FindIncomingByDstContains("color1", "c1");
            meta.incomingColor2Plug = FindIncomingByDstContains("color2", "c2");
            meta.incomingBlenderPlug = FindIncomingByDstContains("blender", ".b");

            meta.outColor = Vector3.Lerp(meta.color1, meta.color2, meta.blender);
            meta.outAlpha = Mathf.Lerp(meta.alpha1, meta.alpha2, meta.blender);

            // publish
            var outColor = GetComponent<MayaVector3Value>() ?? gameObject.AddComponent<MayaVector3Value>();
            outColor.Set(MayaVector3Value.Kind.Vector, meta.outColor, meta.outColor);

            var outAlpha = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            outAlpha.valid = true;
            outAlpha.value = meta.outAlpha;

            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[blendColors] '{NodeName}' b={meta.blender:0.###} c1=({meta.color1.x:0.###},{meta.color1.y:0.###},{meta.color1.z:0.###}) " +
                     $"c2=({meta.color2.x:0.###},{meta.color2.y:0.###},{meta.color2.z:0.###}) out=({meta.outColor.x:0.###},{meta.outColor.y:0.###},{meta.outColor.z:0.###}) " +
                     $"aOut={meta.outAlpha:0.###} srcB='{meta.incomingBlenderPlug ?? "null"}'");
        }

        private float ReadFloat(float def, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count > 0)
                {
                    for (int t = a.Tokens.Count - 1; t >= 0; t--)
                        if (MathUtil.TryParseFloat(a.Tokens[t], out var f)) return f;
                }
            }
            return def;
        }

        private Vector3 ReadColor(Vector3 def, string[] packedKeys, string[] rKeys, string[] gKeys, string[] bKeys)
        {
            // packed
            if (packedKeys != null)
            {
                for (int i = 0; i < packedKeys.Length; i++)
                {
                    if (TryGetAttr(packedKeys[i], out var a) && a.Tokens != null && a.Tokens.Count >= 3)
                    {
                        if (MathUtil.TryParseFloat(a.Tokens[0], out var r) &&
                            MathUtil.TryParseFloat(a.Tokens[1], out var g) &&
                            MathUtil.TryParseFloat(a.Tokens[2], out var b))
                            return new Vector3(r, g, b);
                    }
                }
            }

            // per channel
            float rr = ReadFloat(def.x, rKeys ?? Array.Empty<string>());
            float gg = ReadFloat(def.y, gKeys ?? Array.Empty<string>());
            float bb = ReadFloat(def.z, bKeys ?? Array.Empty<string>());
            return new Vector3(rr, gg, bb);
        }

        private string FindIncomingByDstContains(params string[] patterns)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (patterns == null || patterns.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;
                    if (dstAttr.Contains(pat, StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }

            return null;
        }
    }

    public sealed class MayaBlendColorsMetadata : MonoBehaviour
    {
        public bool valid;
        public int lastBuildFrame;

        [Header("Decoded")]
        public Vector3 color1 = Vector3.zero;
        public Vector3 color2 = Vector3.one;
        public float alpha1 = 1f;
        public float alpha2 = 1f;
        public float blender = 0.5f;

        [Header("Output (best-effort)")]
        public Vector3 outColor;
        public float outAlpha;

        [Header("Connection Hints")]
        public string incomingColor1Plug;
        public string incomingColor2Plug;
        public string incomingBlenderPlug;
    }
}
