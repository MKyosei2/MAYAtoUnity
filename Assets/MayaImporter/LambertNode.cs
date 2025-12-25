using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;

namespace MayaImporter.Shading
{
    [MayaNodeType("lambert")]
    public sealed class LambertNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            var meta = GetComponent<MayaMaterialMetadata>() ?? gameObject.AddComponent<MayaMaterialMetadata>();
            meta.mayaShaderType = "lambert";

            meta.baseColor = ReadColor(new[] { ".c", "color" }, meta.baseColor);
            // transparency -> opacity
            var tr = ReadColor(new[] { ".t", "transparency" }, Color.black);
            meta.opacity = 1f - Mathf.Clamp01((tr.r + tr.g + tr.b) / 3f);

            meta.baseColorTextureNode = ResolveIncomingSourceNodeByDstContains(".color")
                                     ?? ResolveIncomingSourceNodeByDstContains("color");

            log.Info($"[lambert] baseColor={meta.baseColor} opacity={meta.opacity}");
        }

        private string ResolveIncomingSourceNodeByDstContains(string contains)
        {
            if (Connections == null) return null;
            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                var dst = c.DstPlug ?? "";
                if (!dst.Contains(contains, System.StringComparison.Ordinal)) continue;

                if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
                return MayaPlugUtil.ExtractNodePart(c.SrcPlug);
            }
            return null;
        }

        private Color ReadColor(string[] keys, Color def)
        {
            for (int k = 0; k < keys.Length; k++)
            {
                if (!TryGetAttr(keys[k], out var a) || a.Tokens == null || a.Tokens.Count < 3) continue;

                if (TryF(a.Tokens[0], out var r) && TryF(a.Tokens[1], out var g) && TryF(a.Tokens[2], out var b))
                    return new Color(r, g, b, 1f);
            }
            return def;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
