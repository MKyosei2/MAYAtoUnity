using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;

namespace MayaImporter.Generated
{
    /// <summary>
    /// Maya remapHsv:
    /// - Input color -> HSV -> remap hue/sat/val curves -> RGB
    /// - Bakes to PNG and publishes via MayaTextureMetadata (sRGB)
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("remapHsv")]
    public sealed class MayaGenerated_RemapHsvNode : MayaNodeComponentBase
    {
        [Header("Bake Settings")]
        [SerializeField] private int bakeWidth = 256;
        [SerializeField] private int bakeHeight = 256;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            // curves (best effort names)
            MayaImporter.Shading.MayaProceduralTextureBaker.TryCollectRemapCurve(this, "hue", out var hueC);
            MayaImporter.Shading.MayaProceduralTextureBaker.TryCollectRemapCurve(this, "saturation", out var satC);
            MayaImporter.Shading.MayaProceduralTextureBaker.TryCollectRemapCurve(this, "value", out var valC);

            // input
            var inputNode = FindIncomingNodeByDstAttrEqualsAny("color", "input", "inColor", "inputColor", "outColor");
            Texture2D srcTex = null;
            MayaTextureMetadata srcMeta = null;

            if (!string.IsNullOrEmpty(inputNode))
                MayaImporter.Shading.MayaProceduralTextureBaker.TryLoadTextureFromNodeName(inputNode, out srcTex, out srcMeta, log);

            Color constant = Color.white;
            MayaImporter.Shading.MayaProceduralTextureBaker.TryReadColorAttr(this, new[] { "color", ".color", "input", ".input", "inputColor", ".inputColor" }, out constant);

            int w = srcTex != null ? srcTex.width : bakeWidth;
            int h = srcTex != null ? srcTex.height : bakeHeight;

            string bakeId = $"remapHsv_{MayaPlugUtil.LeafName(NodeName)}_{w}x{h}";
            var outPath = MayaImporter.Shading.MayaProceduralTextureBaker.BakeToPng(
                owner: this,
                bakeId: bakeId,
                width: w,
                height: h,
                pixelFunc: (x, y) =>
                {
                    var c = srcTex != null
                        ? srcTex.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h)
                        : constant;

                    Color.RGBToHSV(c, out var h0, out var s0, out var v0);

                    float hh = hueC != null ? MayaImporter.Shading.MayaProceduralTextureBaker.EvalRemapCurve(hueC, h0) : h0;
                    float ss = satC != null ? MayaImporter.Shading.MayaProceduralTextureBaker.EvalRemapCurve(satC, s0) : s0;
                    float vv = valC != null ? MayaImporter.Shading.MayaProceduralTextureBaker.EvalRemapCurve(valC, v0) : v0;

                    var rgb = Color.HSVToRGB(hh, ss, vv, hdr: false);
                    rgb.a = c.a;
                    return rgb;
                },
                log: log);

            var texMeta = GetComponent<MayaTextureMetadata>() ?? gameObject.AddComponent<MayaTextureMetadata>();
            texMeta.fileTextureName = outPath;
            texMeta.ignoreColorSpaceFileRules = true;
            texMeta.colorSpace = "sRGB";

            if (srcMeta != null)
            {
                texMeta.repeatUV = srcMeta.repeatUV;
                texMeta.offsetUV = srcMeta.offsetUV;
                texMeta.rotateUVDegrees = srcMeta.rotateUVDegrees;
                texMeta.connectedPlace2dNodeName = srcMeta.connectedPlace2dNodeName;
            }

            var dbg = GetComponent<MayaProceduralTextureMetadata>() ?? gameObject.AddComponent<MayaProceduralTextureMetadata>();
            dbg.bakedPngPath = outPath;
            dbg.width = w; dbg.height = h;
            dbg.inputNodeA = inputNode;
            dbg.notes = $"remapHsv baked. srcTex={(srcTex != null ? "yes" : "no")} curves(H/S/V)={(hueC != null ? "Y" : "N")}/{(satC != null ? "Y" : "N")}/{(valC != null ? "Y" : "N")}";

            log.Info($"[remapHsv] '{NodeName}' baked='{outPath}' input='{inputNode ?? "null"}' size={w}x{h}");
        }

        private string FindIncomingNodeByDstAttrEqualsAny(params string[] dstAttrNames)
        {
            if (Connections == null || Connections.Count == 0 || dstAttrNames == null || dstAttrNames.Length == 0)
                return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int a = 0; a < dstAttrNames.Length; a++)
                {
                    var want = dstAttrNames[a];
                    if (string.IsNullOrEmpty(want)) continue;
                    if (string.Equals(dstAttr, want, System.StringComparison.Ordinal))
                        return !string.IsNullOrEmpty(c.SrcNodePart) ? c.SrcNodePart : MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                }
            }

            return null;
        }
    }
}
