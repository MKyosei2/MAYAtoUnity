using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;

namespace MayaImporter.Shading
{
    [DisallowMultipleComponent]
    [MayaNodeType("standardSurface")]
    public sealed class StandardSurfaceNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();
            var scene = MayaBuildContext.CurrentScene;

            var meta = GetComponent<MayaMaterialMetadata>() ?? gameObject.AddComponent<MayaMaterialMetadata>();
            meta.mayaShaderType = "standardSurface";

            meta.baseColor = ReadColor(new[] { "baseColor", ".baseColor", "color", ".color", ".c" }, meta.baseColor);
            meta.baseWeight = ReadFloat(new[] { "base", ".base", "baseWeight", ".baseWeight" }, meta.baseWeight);

            meta.metallic = ReadFloat(new[] { "metalness", ".metalness", "metallic", ".metallic" }, meta.metallic);
            meta.roughness = ReadFloat(new[] { "specularRoughness", ".specularRoughness", "roughness", ".roughness" }, meta.roughness);
            meta.smoothness = Mathf.Clamp01(1f - Mathf.Clamp01(meta.roughness));

            var emiColor = ReadColor(new[] { "emissionColor", ".emissionColor", "ec", ".ec" }, meta.emissionColor);
            var emiW = ReadFloat(new[] { "emission", ".emission", "em", ".em" }, 0f);
            meta.emissionColor = emiColor * Mathf.Max(0f, emiW);

            meta.opacity = ReadOpacityColorOrFloat(new[] { "opacity", ".opacity" }, meta.opacity);

            var srcBase = ResolveIncomingSourceNodeByDstContainsAny(new[] { "baseColor", ".baseColor", "color", ".color", ".c" });
            var srcMetal = ResolveIncomingSourceNodeByDstContainsAny(new[] { "metalness", ".metalness", "metallic", ".metallic" });
            var srcRough = ResolveIncomingSourceNodeByDstContainsAny(new[] { "specularRoughness", ".specularRoughness", "roughness", ".roughness" });
            var srcEmi = ResolveIncomingSourceNodeByDstContainsAny(new[] { "emissionColor", ".emissionColor", "emission", ".emission" });
            var srcNrm = ResolveIncomingSourceNodeByDstContainsAny(new[] { "normalCamera", ".normalCamera", "normal", ".normal", "bumpValue", ".bumpValue" });

            meta.baseColorTextureNode = MayaShadingGraphUtil.ResolveToFirstUpstreamFile(scene, srcBase) ?? srcBase;
            meta.metallicTextureNode = MayaShadingGraphUtil.ResolveToFirstUpstreamFile(scene, srcMetal) ?? srcMetal;
            meta.roughnessTextureNode = MayaShadingGraphUtil.ResolveToFirstUpstreamFile(scene, srcRough) ?? srcRough;
            meta.emissionTextureNode = MayaShadingGraphUtil.ResolveToFirstUpstreamFile(scene, srcEmi) ?? srcEmi;
            meta.normalTextureNode = MayaShadingGraphUtil.ResolveToFirstUpstreamFile(scene, srcNrm) ?? srcNrm;

            log.Info($"[standardSurface] baseColor={meta.baseColor} base={meta.baseWeight} metal={meta.metallic} rough={meta.roughness} op={meta.opacity} | tex(nrm={meta.normalTextureNode})");
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

        private float ReadOpacityColorOrFloat(string[] keys, float def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count == 0) continue;

                if (a.Tokens.Count >= 3 &&
                    TryF(a.Tokens[0], out var r) && TryF(a.Tokens[1], out var g) && TryF(a.Tokens[2], out var b))
                    return Mathf.Clamp01((r + g + b) / 3f);

                if (TryF(a.Tokens[0], out var f))
                    return Mathf.Clamp01(f);
            }

            return def;
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
