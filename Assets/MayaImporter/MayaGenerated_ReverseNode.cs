using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;

namespace MayaImporter.Generated
{
    /// <summary>
    /// Maya reverse:
    /// - Output = 1 - input (per channel)
    /// - If input is a texture: invert RGB, keep alpha
    /// - Else: invert constant inputX/Y/Z (best effort)
    /// - Publishes baked PNG via MayaTextureMetadata (colorSpace=Raw by default)
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("reverse")]
    public sealed class MayaGenerated_ReverseNode : MayaNodeComponentBase
    {
        [Header("Bake Settings")]
        [SerializeField] private int bakeWidth = 256;
        [SerializeField] private int bakeHeight = 256;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            var inputNode = FindIncomingNodeByDstAttrEqualsAny("input", "inputX", "inputY", "inputZ", "in", "outColor");
            Texture2D srcTex = null;
            MayaTextureMetadata srcMeta = null;

            if (!string.IsNullOrEmpty(inputNode))
                MayaImporter.Shading.MayaProceduralTextureBaker.TryLoadTextureFromNodeName(inputNode, out srcTex, out srcMeta, log);

            // constant fallback
            float ix = 0.5f, iy = 0.5f, iz = 0.5f;
            MayaImporter.Shading.MayaProceduralTextureBaker.TryReadFloatAttr(this, new[] { "inputX", ".inputX" }, out ix);
            MayaImporter.Shading.MayaProceduralTextureBaker.TryReadFloatAttr(this, new[] { "inputY", ".inputY" }, out iy);
            MayaImporter.Shading.MayaProceduralTextureBaker.TryReadFloatAttr(this, new[] { "inputZ", ".inputZ" }, out iz);

            var constant = new Color(1f - Mathf.Clamp01(ix), 1f - Mathf.Clamp01(iy), 1f - Mathf.Clamp01(iz), 1f);

            int w = srcTex != null ? srcTex.width : bakeWidth;
            int h = srcTex != null ? srcTex.height : bakeHeight;

            string bakeId = $"reverse_{MayaPlugUtil.LeafName(NodeName)}_{w}x{h}";
            var outPath = MayaImporter.Shading.MayaProceduralTextureBaker.BakeToPng(
                owner: this,
                bakeId: bakeId,
                width: w,
                height: h,
                pixelFunc: (x, y) =>
                {
                    if (srcTex == null) return constant;

                    var c = srcTex.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
                    return new Color(1f - c.r, 1f - c.g, 1f - c.b, c.a);
                },
                log: log);

            var texMeta = GetComponent<MayaTextureMetadata>() ?? gameObject.AddComponent<MayaTextureMetadata>();
            texMeta.fileTextureName = outPath;
            texMeta.ignoreColorSpaceFileRules = true;
            texMeta.colorSpace = "Raw"; // invert is often used for data (opacity/roughness/etc)

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
            dbg.notes = $"reverse baked. srcTex={(srcTex != null ? "yes" : "no")}";

            log.Info($"[reverse] '{NodeName}' baked='{outPath}' input='{inputNode ?? "null"}' size={w}x{h}");
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
