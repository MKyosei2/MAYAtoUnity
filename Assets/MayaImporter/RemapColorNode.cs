using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;

namespace MayaImporter.Shading
{
    /// <summary>
    /// Maya remapColor:
    /// - Input color (connected or constant)
    /// - Per-channel curve: red[] / green[] / blue[]
    /// - Output baked PNG in persistentDataPath and publishes via MayaTextureMetadata
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("remapColor")]
    public sealed class RemapColorNode : MayaNodeComponentBase
    {
        [Header("Bake Settings")]
        [SerializeField] private int bakeWidth = 256;
        [SerializeField] private int bakeHeight = 256;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            // curves
            MayaProceduralTextureBaker.TryCollectRemapCurve(this, "red", out var red);
            MayaProceduralTextureBaker.TryCollectRemapCurve(this, "green", out var green);
            MayaProceduralTextureBaker.TryCollectRemapCurve(this, "blue", out var blue);

            // input source (best effort)
            var inputNode = FindIncomingNodeByDstAttrEqualsAny("color", "input", "inColor", "inputColor", "outColor");
            Texture2D srcTex = null;
            MayaTextureMetadata srcMeta = null;

            if (!string.IsNullOrEmpty(inputNode))
                MayaProceduralTextureBaker.TryLoadTextureFromNodeName(inputNode, out srcTex, out srcMeta, log);

            // constant fallback
            Color constant = Color.white;
            MayaProceduralTextureBaker.TryReadColorAttr(this, new[] { "color", ".color", "input", ".input", "inputColor", ".inputColor" }, out constant);

            int w = srcTex != null ? srcTex.width : bakeWidth;
            int h = srcTex != null ? srcTex.height : bakeHeight;

            string bakeId = $"remapColor_{MayaPlugUtil.LeafName(NodeName)}_{w}x{h}";
            var outPath = MayaProceduralTextureBaker.BakeToPng(
                owner: this,
                bakeId: bakeId,
                width: w,
                height: h,
                pixelFunc: (x, y) =>
                {
                    Color c = srcTex != null
                        ? srcTex.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h)
                        : constant;

                    float rr = red != null ? MayaProceduralTextureBaker.EvalRemapCurve(red, c.r) : c.r;
                    float gg = green != null ? MayaProceduralTextureBaker.EvalRemapCurve(green, c.g) : c.g;
                    float bb = blue != null ? MayaProceduralTextureBaker.EvalRemapCurve(blue, c.b) : c.b;

                    return new Color(rr, gg, bb, c.a);
                },
                log: log);

            // publish
            var texMeta = GetComponent<MayaTextureMetadata>() ?? gameObject.AddComponent<MayaTextureMetadata>();
            texMeta.fileTextureName = outPath;
            texMeta.ignoreColorSpaceFileRules = true;
            texMeta.colorSpace = "sRGB";

            // propagate UV if possible
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
            dbg.notes = $"remapColor baked. srcTex={(srcTex != null ? "yes" : "no")} curves(R/G/B)={(red != null ? "Y" : "N")}/{(green != null ? "Y" : "N")}/{(blue != null ? "Y" : "N")}";

            log.Info($"[remapColor] '{NodeName}' baked='{outPath}' input='{inputNode ?? "null"}' size={w}x{h}");
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
