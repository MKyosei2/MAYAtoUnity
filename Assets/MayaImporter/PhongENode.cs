using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;

namespace MayaImporter.Shading
{
    [DisallowMultipleComponent]
    [MayaNodeType("phongE")]
    public sealed class PhongENode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();
            var scene = MayaBuildContext.CurrentScene;

            var meta = GetComponent<MayaMaterialMetadata>() ?? gameObject.AddComponent<MayaMaterialMetadata>();
            meta.mayaShaderType = "phongE";

            meta.baseColor = ReadColor(new[] { "color", ".color", ".c" }, meta.baseColor);

            meta.roughness = Mathf.Clamp01(ReadFloat(new[] { "roughness", ".roughness", ".r" }, 0.5f));
            meta.smoothness = Mathf.Clamp01(1f - meta.roughness);

            var tr = ReadColor(new[] { "transparency", ".transparency", ".t" }, Color.black);
            meta.opacity = 1f - Mathf.Clamp01((tr.r + tr.g + tr.b) / 3f);

            var srcBase = ResolveIncomingSourceNodeByDstContainsAny(new[] { "color", ".color", ".c" });
            var srcNrm = ResolveIncomingSourceNodeByDstContainsAny(new[] { "normalCamera", ".normalCamera", "bumpValue", ".bumpValue" });

            meta.baseColorTextureNode = MayaShadingGraphUtil.ResolveToFirstUpstreamFile(scene, srcBase) ?? srcBase;
            meta.normalTextureNode = MayaShadingGraphUtil.ResolveToFirstUpstreamFile(scene, srcNrm) ?? srcNrm;

            log.Info($"[phongE] baseColor={meta.baseColor} rough={meta.roughness} op={meta.opacity} | tex(nrm={meta.normalTextureNode})");
        }

        private string ResolveIncomingSourceNodeByDstContainsAny(string[] containsAny)
        {
            if (Connections == null || containsAny == null || containsAny.Length == 0) return null;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dst = c.DstPlug ?? "";
                if (string.IsNullOrEmpty(dst)) continue;

                for (int k = 0; k < containsAny.Length; k++)
                {
                    var key = containsAny[k];
                    if (string.IsNullOrEmpty(key)) continue;
                    if (!dst.Contains(key, System.StringComparison.Ordinal)) continue;

                    if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
                    return MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                }
            }

            return null;
        }

        private float ReadFloat(string[] keys, float def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count == 0) continue;
                if (TryF(a.Tokens[0], out var f)) return f;
            }
            return def;
        }

        private Color ReadColor(string[] keys, Color def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count < 3) continue;
                if (TryF(a.Tokens[0], out var r) && TryF(a.Tokens[1], out var g) && TryF(a.Tokens[2], out var b))
                    return new Color(r, g, b, 1f);
            }
            return def;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
