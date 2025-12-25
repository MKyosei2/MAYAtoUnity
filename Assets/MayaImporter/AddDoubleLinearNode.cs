// Assets/MayaImporter/AddDoubleLinearNode.cs
// Phase-1 implementation (not stub)
// NodeType: addDoubleLinear
//
// - Reads input1 + input2 (double/float)
// - output = input1 + input2
// - Publishes output via MayaImporter.Core.MayaFloatValue
// - Keeps incoming plug names for later value-graph wiring

using System;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Nodes
{
    [MayaNodeType("addDoubleLinear")]
    [DisallowMultipleComponent]
    public sealed class AddDoubleLinearNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaAddDoubleLinearMetadata>() ?? gameObject.AddComponent<MayaAddDoubleLinearMetadata>();
            meta.valid = false;

            var outVal = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            outVal.valid = false;

            meta.srcInput1Plug = FindIncomingSrcPlugByDstContainsAny(new[]
            {
                "input1", ".input1", ".i1", "i1"
            });

            meta.srcInput2Plug = FindIncomingSrcPlugByDstContainsAny(new[]
            {
                "input2", ".input2", ".i2", "i2"
            });

            // Best-effort local setAttr decode
            meta.input1 = ReadFloat(0f, ".input1", "input1", ".i1", "i1");
            meta.input2 = ReadFloat(0f, ".input2", "input2", ".i2", "i2");

            meta.output = meta.input1 + meta.input2;

            // Publish
            outVal.Set(meta.output);

            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[addDoubleLinear] '{NodeName}' in1={meta.input1:0.###} in2={meta.input2:0.###} out={meta.output:0.###} " +
                     $"src1='{meta.srcInput1Plug ?? "null"}' src2='{meta.srcInput2Plug ?? "null"}'");
        }

        private float ReadFloat(float def, params string[] keys)
        {
            if (keys == null) return def;

            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                if (string.IsNullOrEmpty(k)) continue;

                if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count > 0)
                {
                    // prefer last numeric token
                    for (int t = a.Tokens.Count - 1; t >= 0; t--)
                    {
                        var s = a.Tokens[t];
                        if (MathUtil.TryParseFloat(s, out var f)) return f;
                        if (MathUtil.TryParseDouble(s, out var d)) return (float)d;
                    }
                }
            }

            return def;
        }

        private string FindIncomingSrcPlugByDstContainsAny(string[] dstContainsAny)
        {
            if (Connections == null || dstContainsAny == null || dstContainsAny.Length == 0)
                return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dst = c.DstPlug ?? "";
                if (string.IsNullOrEmpty(dst)) continue;

                for (int k = 0; k < dstContainsAny.Length; k++)
                {
                    var key = dstContainsAny[k];
                    if (string.IsNullOrEmpty(key)) continue;

                    if (dst.Contains(key, StringComparison.Ordinal))
                        return NormalizePlug(c.SrcPlug);
                }
            }

            return null;
        }

        private static string NormalizePlug(string plug)
        {
            if (string.IsNullOrEmpty(plug)) return null;
            plug = plug.Trim();
            if (plug.Length >= 2 && plug[0] == '"' && plug[plug.Length - 1] == '"')
                plug = plug.Substring(1, plug.Length - 2);
            return plug;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MayaAddDoubleLinearMetadata : MonoBehaviour
    {
        public bool valid;

        [Header("Incoming (if connected)")]
        public string srcInput1Plug;
        public string srcInput2Plug;

        [Header("Maya values (best-effort from setAttr)")]
        public float input1;
        public float input2;
        public float output;

        [Header("Debug")]
        public int lastBuildFrame;
    }
}
