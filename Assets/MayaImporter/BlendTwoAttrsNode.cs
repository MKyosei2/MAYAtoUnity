// Assets/MayaImporter/BlendTwoAttrsNode.cs
// Phase C placeholder -> implemented (structured decode + best-effort initial evaluation)
//
// blendTwoAttrs (variant):
//  - reads input0/input1 and blender
//  - computes output = lerp(input0, input1, blender)
//  - captures incoming plugs for later value-graph evaluation
using System;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Nodes
{
    [MayaNodeType("blendTwoAttrs")]
    [DisallowMultipleComponent]
    public sealed class BlendTwoAttrsNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaBlendTwoAttrsMetadata>() ?? gameObject.AddComponent<MayaBlendTwoAttrsMetadata>();

            meta.input0 = ReadF(0f,
                ".input[0]", "input[0]", ".in[0]", "in[0]", ".input0", "input0", ".in0", "in0", ".inputA", "inputA");
            meta.input1 = ReadF(0f,
                ".input[1]", "input[1]", ".in[1]", "in[1]", ".input1", "input1", ".in1", "in1", ".inputB", "inputB");

            meta.blender = ReadF(0f,
                ".attributesBlender", "attributesBlender", ".ab", "ab",
                ".blender", "blender", ".weight", "weight", ".w", "w");

            // Capture incoming plugs
            meta.srcInput0Plug = ResolveIncomingSrcPlugByDstContainsAny(new[]
            {
                "input[0]", ".in[0]", "input0", "in0", "inputA"
            });

            meta.srcInput1Plug = ResolveIncomingSrcPlugByDstContainsAny(new[]
            {
                "input[1]", ".in[1]", "input1", "in1", "inputB"
            });

            meta.srcBlenderPlug = ResolveIncomingSrcPlugByDstContainsAny(new[]
            {
                "attributesBlender", ".ab", "blender", "weight", ".w"
            });

            // Evaluate (best-effort)
            float t = Mathf.Clamp01(meta.blender);
            meta.output = Mathf.Lerp(meta.input0, meta.input1, t);

            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[blendTwoAttrs] name='{NodeName}' in0={meta.input0:0.###} in1={meta.input1:0.###} blender={meta.blender:0.###} out={meta.output:0.###}");
        }

        private string ResolveIncomingSrcPlugByDstContainsAny(string[] dstContainsAny)
        {
            if (Connections == null || dstContainsAny == null || dstContainsAny.Length == 0) return null;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dst = c.DstPlug ?? "";
                if (string.IsNullOrEmpty(dst)) continue;

                for (int k = 0; k < dstContainsAny.Length; k++)
                {
                    var key = dstContainsAny[k];
                    if (string.IsNullOrEmpty(key)) continue;

                    if (!dst.Contains(key, StringComparison.Ordinal))
                        continue;

                    return !string.IsNullOrEmpty(c.SrcPlug) ? NormalizePlug(c.SrcPlug) : null;
                }
            }

            return null;
        }

        private static string NormalizePlug(string plug)
        {
            if (string.IsNullOrEmpty(plug)) return plug;
            plug = plug.Trim();
            if (plug.Length >= 2 && plug[0] == '"' && plug[plug.Length - 1] == '"')
                plug = plug.Substring(1, plug.Length - 2);
            return plug;
        }

        private float ReadF(float def, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count > 0 &&
                    float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    return f;
            }
            return def;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MayaBlendTwoAttrsMetadata : MonoBehaviour
    {
        public bool valid;

        [Header("Defaults (from setAttr)")]
        public float input0;
        public float input1;
        public float blender;

        [Header("Initial evaluated output (best-effort)")]
        public float output;

        [Header("Incoming plugs (if connected)")]
        public string srcInput0Plug;
        public string srcInput1Plug;
        public string srcBlenderPlug;

        [Header("Debug")]
        public int lastBuildFrame;
    }
}
