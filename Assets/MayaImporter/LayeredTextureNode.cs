using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;
using MayaImporter.Utils;

namespace MayaImporter.Shading
{
    /// <summary>
    /// Maya layeredTexture:
    /// - inputs[i].color / inputs[i].alpha / inputs[i].blendMode を best-effort decode
    /// - Layer compositing and bake to PNG (colorSpace=sRGB)
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("layeredTexture")]
    public sealed class LayeredTextureNode : MayaNodeComponentBase
    {
        [Header("Bake Settings")]
        [SerializeField] private int bakeWidth = 256;
        [SerializeField] private int bakeHeight = 256;

        private sealed class Layer
        {
            public int index;
            public string colorSrcNode;
            public string alphaSrcNode;
            public Color constantColor = Color.white;
            public float constantAlpha = 1f;
            public int blendMode = 0; // 0=Over(default), 3=Add, 4=Subtract, 5=Multiply, 6=Difference (best-effort)
        }

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            var layers = CollectLayers(log);
            if (layers.Count == 0)
            {
                // still publish a tiny fallback
                PublishSolid(Color.white, "layeredTexture fallback (no layers)", log);
                return;
            }

            // Determine bake size from first available color source texture
            Texture2D firstTex = null;
            MayaTextureMetadata firstMeta = null;

            for (int i = 0; i < layers.Count && firstTex == null; i++)
            {
                if (!string.IsNullOrEmpty(layers[i].colorSrcNode))
                    MayaProceduralTextureBaker.TryLoadTextureFromNodeName(layers[i].colorSrcNode, out firstTex, out firstMeta, log);
            }

            int w = firstTex != null ? firstTex.width : bakeWidth;
            int h = firstTex != null ? firstTex.height : bakeHeight;

            string bakeId = $"layeredTexture_{MayaPlugUtil.LeafName(NodeName)}_{w}x{h}";

            var outPath = MayaProceduralTextureBaker.BakeToPng(
                owner: this,
                bakeId: bakeId,
                width: w,
                height: h,
                pixelFunc: (x, y) =>
                {
                    float u = (x + 0.5f) / w;
                    float v = (y + 0.5f) / h;

                    Color dst = new Color(0, 0, 0, 0);

                    for (int li = 0; li < layers.Count; li++)
                    {
                        var L = layers[li];

                        Color srcCol = L.constantColor;
                        float srcA = Mathf.Clamp01(L.constantAlpha);

                        if (!string.IsNullOrEmpty(L.colorSrcNode) &&
                            MayaProceduralTextureBaker.TryLoadTextureFromNodeName(L.colorSrcNode, out var ctex, out _, log) &&
                            ctex != null)
                        {
                            srcCol = ctex.GetPixelBilinear(u, v);
                        }

                        if (!string.IsNullOrEmpty(L.alphaSrcNode) &&
                            MayaProceduralTextureBaker.TryLoadTextureFromNodeName(L.alphaSrcNode, out var atex, out _, log) &&
                            atex != null)
                        {
                            srcA = Mathf.Clamp01(atex.GetPixelBilinear(u, v).r);
                        }

                        dst = Composite(dst, srcCol, srcA, L.blendMode);
                    }

                    dst.a = 1f; // keep opaque for baseColor usage; alpha composition can be added later if needed
                    return dst;
                },
                log: log);

            var texMeta = GetComponent<MayaTextureMetadata>() ?? gameObject.AddComponent<MayaTextureMetadata>();
            texMeta.fileTextureName = outPath;
            texMeta.ignoreColorSpaceFileRules = true;
            texMeta.colorSpace = "sRGB";

            if (firstMeta != null)
            {
                texMeta.repeatUV = firstMeta.repeatUV;
                texMeta.offsetUV = firstMeta.offsetUV;
                texMeta.rotateUVDegrees = firstMeta.rotateUVDegrees;
                texMeta.connectedPlace2dNodeName = firstMeta.connectedPlace2dNodeName;
            }

            var dbg = GetComponent<MayaProceduralTextureMetadata>() ?? gameObject.AddComponent<MayaProceduralTextureMetadata>();
            dbg.bakedPngPath = outPath;
            dbg.width = w; dbg.height = h;
            dbg.inputNodeA = layers.Count > 0 ? layers[0].colorSrcNode : null;
            dbg.inputNodeB = layers.Count > 1 ? layers[1].colorSrcNode : null;
            dbg.notes = $"layeredTexture baked. layers={layers.Count}";

            log.Info($"[layeredTexture] '{NodeName}' baked='{outPath}' size={w}x{h} layers={layers.Count}");
        }

        private void PublishSolid(Color c, string notes, MayaImportLog log)
        {
            string bakeId = $"layeredTexture_{MayaPlugUtil.LeafName(NodeName)}_solid";
            var outPath = MayaProceduralTextureBaker.BakeToPng(this, bakeId, 8, 8, (_, __) => c, log);

            var texMeta = GetComponent<MayaTextureMetadata>() ?? gameObject.AddComponent<MayaTextureMetadata>();
            texMeta.fileTextureName = outPath;
            texMeta.ignoreColorSpaceFileRules = true;
            texMeta.colorSpace = "sRGB";

            var dbg = GetComponent<MayaProceduralTextureMetadata>() ?? gameObject.AddComponent<MayaProceduralTextureMetadata>();
            dbg.bakedPngPath = outPath;
            dbg.width = 8; dbg.height = 8;
            dbg.notes = notes;

            log?.Info($"[layeredTexture] '{NodeName}' baked solid '{outPath}' ({notes})");
        }

        private List<Layer> CollectLayers(MayaImportLog log)
        {
            var dict = new Dictionary<int, Layer>();

            // ---- Connections: inputs[i].color / inputs[i].alpha
            if (Connections != null)
            {
                for (int i = 0; i < Connections.Count; i++)
                {
                    var c = Connections[i];
                    if (c == null) continue;

                    if (c.RoleForThisNode != ConnectionRole.Destination &&
                        c.RoleForThisNode != ConnectionRole.Both)
                        continue;

                    var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                    if (string.IsNullOrEmpty(dstAttr)) continue;

                    // expect "inputs[3].color" etc
                    if (dstAttr.IndexOf("inputs[", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (!TryParseInputsIndex(dstAttr, out var idx))
                        continue;

                    if (!dict.TryGetValue(idx, out var L))
                    {
                        L = new Layer { index = idx };
                        dict[idx] = L;
                    }

                    if (dstAttr.EndsWith(".color", StringComparison.OrdinalIgnoreCase) || dstAttr.EndsWith("color", StringComparison.OrdinalIgnoreCase))
                        L.colorSrcNode = !string.IsNullOrEmpty(c.SrcNodePart) ? c.SrcNodePart : MayaPlugUtil.ExtractNodePart(c.SrcPlug);

                    if (dstAttr.EndsWith(".alpha", StringComparison.OrdinalIgnoreCase) || dstAttr.EndsWith("alpha", StringComparison.OrdinalIgnoreCase))
                        L.alphaSrcNode = !string.IsNullOrEmpty(c.SrcNodePart) ? c.SrcNodePart : MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                }
            }

            // ---- Attributes: inputs[i].color / inputs[i].alpha / inputs[i].blendMode
            if (Attributes != null)
            {
                for (int i = 0; i < Attributes.Count; i++)
                {
                    var a = Attributes[i];
                    if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0) continue;

                    var k = a.Key;

                    if (k.IndexOf("inputs[", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (!TryParseInputsIndex(k, out var idx))
                        continue;

                    if (!dict.TryGetValue(idx, out var L))
                    {
                        L = new Layer { index = idx };
                        dict[idx] = L;
                    }

                    // color token: 3 floats
                    if (k.IndexOf(".color", StringComparison.OrdinalIgnoreCase) >= 0 && a.Tokens.Count >= 3)
                    {
                        if (MathUtil.TryParseFloat(a.Tokens[0], out var r) &&
                            MathUtil.TryParseFloat(a.Tokens[1], out var g) &&
                            MathUtil.TryParseFloat(a.Tokens[2], out var b))
                            L.constantColor = new Color(r, g, b, 1f);
                    }

                    // alpha token: 1 float
                    if (k.IndexOf(".alpha", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var last = a.Tokens[a.Tokens.Count - 1];
                        if (MathUtil.TryParseFloat(last, out var f))
                            L.constantAlpha = Mathf.Clamp01(f);
                    }

                    if (k.IndexOf("blendMode", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var last = a.Tokens[a.Tokens.Count - 1];
                        if (MathUtil.TryParseInt(last, out var m))
                            L.blendMode = m;
                    }
                }
            }

            var list = new List<Layer>(dict.Values);
            list.Sort((a, b) => a.index.CompareTo(b.index));
            return list;
        }

        private static bool TryParseInputsIndex(string s, out int idx)
        {
            idx = -1;
            int lb = s.IndexOf('[');
            int rb = s.IndexOf(']', lb + 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            var inner = s.Substring(lb + 1, rb - lb - 1);
            int colon = inner.IndexOf(':');
            if (colon >= 0) inner = inner.Substring(0, colon);

            return int.TryParse(inner, out idx);
        }

        private static Color Composite(Color dst, Color src, float srcAlpha, int blendMode)
        {
            srcAlpha = Mathf.Clamp01(srcAlpha);

            // best-effort subset
            switch (blendMode)
            {
                case 3: // Add
                    {
                        var add = new Color(dst.r + src.r * srcAlpha, dst.g + src.g * srcAlpha, dst.b + src.b * srcAlpha, 1f);
                        add.r = Mathf.Clamp01(add.r); add.g = Mathf.Clamp01(add.g); add.b = Mathf.Clamp01(add.b);
                        return add;
                    }
                case 4: // Subtract
                    {
                        var sub = new Color(dst.r - src.r * srcAlpha, dst.g - src.g * srcAlpha, dst.b - src.b * srcAlpha, 1f);
                        sub.r = Mathf.Clamp01(sub.r); sub.g = Mathf.Clamp01(sub.g); sub.b = Mathf.Clamp01(sub.b);
                        return sub;
                    }
                case 5: // Multiply
                    {
                        var mul = new Color(
                            dst.r * Mathf.Lerp(1f, src.r, srcAlpha),
                            dst.g * Mathf.Lerp(1f, src.g, srcAlpha),
                            dst.b * Mathf.Lerp(1f, src.b, srcAlpha),
                            1f);
                        mul.r = Mathf.Clamp01(mul.r); mul.g = Mathf.Clamp01(mul.g); mul.b = Mathf.Clamp01(mul.b);
                        return mul;
                    }
                case 6: // Difference
                    {
                        var dif = new Color(
                            Mathf.Abs(dst.r - src.r) * srcAlpha + dst.r * (1f - srcAlpha),
                            Mathf.Abs(dst.g - src.g) * srcAlpha + dst.g * (1f - srcAlpha),
                            Mathf.Abs(dst.b - src.b) * srcAlpha + dst.b * (1f - srcAlpha),
                            1f);
                        dif.r = Mathf.Clamp01(dif.r); dif.g = Mathf.Clamp01(dif.g); dif.b = Mathf.Clamp01(dif.b);
                        return dif;
                    }
                default: // Over
                    {
                        var outC = new Color(
                            src.r * srcAlpha + dst.r * (1f - srcAlpha),
                            src.g * srcAlpha + dst.g * (1f - srcAlpha),
                            src.b * srcAlpha + dst.b * (1f - srcAlpha),
                            1f);
                        outC.r = Mathf.Clamp01(outC.r); outC.g = Mathf.Clamp01(outC.g); outC.b = Mathf.Clamp01(outC.b);
                        return outC;
                    }
            }
        }
    }
}
