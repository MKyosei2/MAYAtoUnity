using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Shading network ̊čB
    /// 100%j:
    /// - Unityɖm[h/Glbg[ŃuČvuvł͂Ȃirawێj
    /// - ̂ Blocker ͏o Warn/Info ̂
    /// </summary>
    public static class MayaShadingNetworkLimitationsReporter
    {
        [Serializable]
        public sealed class ShadingLimitationRow
        {
            public string NodeName;
            public string NodeType;
            public string Scope; // e.g. Scene / Node / ShaderGraph
            public string IssueKey;
            public string Severity; // Info / Warn
            public string Details;
        }

        public static List<ShadingLimitationRow> Collect(MayaSceneData scene)
        {
            var list = new List<ShadingLimitationRow>(128);
            if (scene == null || scene.Nodes == null) return list;

            foreach (var kv in scene.Nodes)
            {
                var n = kv.Value;
                if (n == null) continue;

                var t = n.NodeType ?? "";
                if (string.IsNullOrEmpty(t)) continue;

                // \I shading m[h
                if (t == "shadingEngine")
                {
                    list.Add(new ShadingLimitationRow
                    {
                        NodeName = n.Name,
                        NodeType = n.NodeType,
                        Scope = "Scene",
                        IssueKey = "ShadingEngine_Present",
                        Severity = "Info",
                        Details = "shadingEngine exists. Unity material is resolved by name; full network data is preserved in scene graph for future decoding."
                    });
                    continue;
                }

                // Maya/Arnold/V-Ray ̓m[h UnityɒڑΉȂ\
                if (t.StartsWith("ai", StringComparison.OrdinalIgnoreCase) ||
                    t.StartsWith("vray", StringComparison.OrdinalIgnoreCase) ||
                    t.IndexOf("arnold", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    list.Add(new ShadingLimitationRow
                    {
                        NodeName = n.Name,
                        NodeType = n.NodeType,
                        Scope = "Scene",
                        IssueKey = "RendererSpecificShader",
                        Severity = "Warn",
                        Details = "Renderer-specific shading node detected. Unity has no direct equivalent; current pipeline uses name-based fallback material. Raw shading network is preserved."
                    });
                    continue;
                }

                // file texture / place2dTexture / ramp Ȃǂ͍̃fR[_Ώ
                if (t == "file" || t == "place2dTexture" || t == "ramp" || t == "noise" || t == "blendColors")
                {
                    list.Add(new ShadingLimitationRow
                    {
                        NodeName = n.Name,
                        NodeType = n.NodeType,
                        Scope = "Scene",
                        IssueKey = "TextureNetwork_Node",
                        Severity = "Info",
                        Details = "Texture/utility node detected. Unity reproduction requires a shading network decoder (future work). Raw attributes and connections are preserved."
                    });
                    continue;
                }
            }

            return list;
        }
    }
}
