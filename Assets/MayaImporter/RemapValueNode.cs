using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;

namespace MayaImporter.Shading
{
    /// <summary>
    /// Maya remapValue:
    /// - Input scalar (texture or constant)
    /// - Curve: value[]
    /// - Output grayscale baked PNG, published via MayaTextureMetadata (colorSpace=Raw)
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("remapValue")]
    public sealed class RemapValueNode : MayaNodeComponentBase
    {
        [Header("Bake Settings")]
        [SerializeField] private int bakeWidth = 256;
        [SerializeField] private int bakeHeight = 256;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            MayaProceduralTextureBaker.TryCollectRemapCurve(this, "value", out var curve);

            // input source (best effort)
            var inputNode = FindIncomingNodeByDstAttrEqualsAny("inputValue", "input", "inValue", "value", "outValue");
            Texture2D srcTex = null;
            MayaTextureMetadata srcMeta = null;

            if (!string.IsNullOrEmpty(inputNode))
                MayaProceduralTextureBaker.TryLoadTextureFromNodeName(inputNode, out srcTex, out srcMeta, log);

            // constant fallback
            float constant = 0.5f;
            MayaProceduralTextureBaker.TryReadFloatAttr(this, new[] { "inputValue", ".inputValue", "input", ".input", "value", ".value" }, out constant);
            constant = Mathf.Clamp01(constant);

            int w = srcTex != null ? srcTex.width : bakeWidth;
            int h = srcTex != null ? srcTex.height : bakeHeight;

            string bakeId = $"remapValue_{MayaPlugUtil.LeafName(NodeName)}_{w}x{h}";
            var outPath = MayaProceduralTextureBaker.BakeToPng(
                owner: this,
                bakeId: bakeId,
                width: w,
                height: h,
                pixelFunc: (x, y) =>
                {
                    float v = srcTex != null
                        ? srcTex.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h).r
                        : constant;

                    float o = curve != null ? MayaProceduralTextureBaker.EvalRemapCurve(curve, v) : Mathf.Clamp01(v);
                    return new Color(o, o, o, 1f);
                },
                log: log);

            // publish
            var texMeta = GetComponent<MayaTextureMetadata>() ?? gameObject.AddComponent<MayaTextureMetadata>();
            texMeta.fileTextureName = outPath;
            texMeta.ignoreColorSpaceFileRules = true;
            texMeta.colorSpace = "Raw"; // scalar/data

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
            dbg.notes = $"remapValue baked. srcTex={(srcTex != null ? "yes" : "no")} curve={(curve != null ? "Y" : "N")}";

            log.Info($"[remapValue] '{NodeName}' baked='{outPath}' input='{inputNode ?? "null"}' size={w}x{h}");
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
