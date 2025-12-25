// Assets/MayaImporter/MultiplyDivideNode.cs
// Phase-1 implementation (not stub)
// NodeType: multiplyDivide
//
// - Decodes operation + input1/input2 (vector)
// - Computes output (component-wise) best-effort
// - Publishes output via MayaVector3Value (Core)
// - Stores incoming plugs for later wiring

using System;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Nodes
{
    [MayaNodeType("multiplyDivide")]
    [DisallowMultipleComponent]
    public sealed class MultiplyDivideNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaMultiplyDivideMetadata>() ?? gameObject.AddComponent<MayaMultiplyDivideMetadata>();
            meta.valid = false;

            meta.operation = ReadInt(1, ".operation", "operation", ".op", "op"); // 1=Multiply,2=Divide,3=Power
            meta.input1 = ReadVec3(Vector3.one, ".input1", "input1", ".i1", "i1", ".input1X", "input1X", ".input1Y", "input1Y", ".input1Z", "input1Z");
            meta.input2 = ReadVec3(Vector3.one, ".input2", "input2", ".i2", "i2", ".input2X", "input2X", ".input2Y", "input2Y", ".input2Z", "input2Z");

            meta.incomingInput1Plug = FindIncomingByDstContains("input1", "i1");
            meta.incomingInput2Plug = FindIncomingByDstContains("input2", "i2");

            meta.output = Eval(meta.operation, meta.input1, meta.input2);
            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            var outVal = GetComponent<MayaVector3Value>() ?? gameObject.AddComponent<MayaVector3Value>();
            outVal.Set(MayaVector3Value.Kind.Vector, meta.output, meta.output);

            log.Info($"[multiplyDivide] '{NodeName}' op={meta.operation} in1=({meta.input1.x:0.###},{meta.input1.y:0.###},{meta.input1.z:0.###}) " +
                     $"in2=({meta.input2.x:0.###},{meta.input2.y:0.###},{meta.input2.z:0.###}) out=({meta.output.x:0.###},{meta.output.y:0.###},{meta.output.z:0.###}) " +
                     $"src1='{meta.incomingInput1Plug ?? "null"}' src2='{meta.incomingInput2Plug ?? "null"}'");
        }

        private Vector3 Eval(int op, Vector3 a, Vector3 b)
        {
            // Maya multiplyDivide is per-component
            switch (op)
            {
                case 2: // Divide
                    return new Vector3(
                        SafeDiv(a.x, b.x),
                        SafeDiv(a.y, b.y),
                        SafeDiv(a.z, b.z));

                case 3: // Power
                    return new Vector3(
                        PowSafe(a.x, b.x),
                        PowSafe(a.y, b.y),
                        PowSafe(a.z, b.z));

                default: // 1 Multiply
                    return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
            }
        }

        private static float SafeDiv(float a, float b) => Mathf.Abs(b) < 1e-8f ? 0f : a / b;

        private static float PowSafe(float a, float b)
        {
            // Mathf.Pow handles most cases; keep deterministic fallback
            if (float.IsNaN(a) || float.IsNaN(b)) return 0f;
            return Mathf.Pow(a, b);
        }

        // -------- decode helpers --------

        private int ReadInt(int def, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count > 0)
                {
                    for (int t = a.Tokens.Count - 1; t >= 0; t--)
                        if (MathUtil.TryParseInt(a.Tokens[t], out var v)) return v;
                }
            }
            return def;
        }

        private float ReadFloat(float def, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count > 0)
                {
                    for (int t = a.Tokens.Count - 1; t >= 0; t--)
                        if (MathUtil.TryParseFloat(a.Tokens[t], out var f)) return f;
                }
            }
            return def;
        }

        private Vector3 ReadVec3(Vector3 def, params string[] keys)
        {
            // keys can contain packed input ("input1") and/or axis keys ("input1X"...)
            // Try packed first
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                if (k == null) continue;

                // packed candidates
                if (k.EndsWith("X", StringComparison.Ordinal) ||
                    k.EndsWith("Y", StringComparison.Ordinal) ||
                    k.EndsWith("Z", StringComparison.Ordinal))
                    continue;

                if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count >= 3)
                {
                    if (MathUtil.TryParseFloat(a.Tokens[0], out var x) &&
                        MathUtil.TryParseFloat(a.Tokens[1], out var y) &&
                        MathUtil.TryParseFloat(a.Tokens[2], out var z))
                        return new Vector3(x, y, z);
                }
            }

            // axis fallback
            float x2 = ReadFloat(def.x, ".input1X", "input1X", ".input2X", "input2X"); // not ideal, overridden by caller keys order
            float y2 = ReadFloat(def.y, ".input1Y", "input1Y", ".input2Y", "input2Y");
            float z2 = ReadFloat(def.z, ".input1Z", "input1Z", ".input2Z", "input2Z");

            // caller supplies its own key list; better axis read:
            // detect by presence in keys
            x2 = ReadAxis(def.x, keys, 'X');
            y2 = ReadAxis(def.y, keys, 'Y');
            z2 = ReadAxis(def.z, keys, 'Z');

            return new Vector3(x2, y2, z2);
        }

        private float ReadAxis(float def, string[] keys, char axis)
        {
            if (keys == null) return def;

            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                if (string.IsNullOrEmpty(k)) continue;
                if (k.Length == 0) continue;

                if (k.EndsWith(axis.ToString(), StringComparison.Ordinal) || k.EndsWith(axis.ToString().ToLowerInvariant(), StringComparison.Ordinal))
                {
                    if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count > 0)
                    {
                        for (int t = a.Tokens.Count - 1; t >= 0; t--)
                            if (MathUtil.TryParseFloat(a.Tokens[t], out var f)) return f;
                    }
                }
            }

            return def;
        }

        private string FindIncomingByDstContains(params string[] patterns)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (patterns == null || patterns.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;
                    if (dstAttr.Contains(pat, StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }

            return null;
        }
    }

    public sealed class MayaMultiplyDivideMetadata : MonoBehaviour
    {
        public bool valid;
        public int lastBuildFrame;

        [Header("Decoded")]
        public int operation = 1;
        public Vector3 input1 = Vector3.one;
        public Vector3 input2 = Vector3.one;

        [Header("Output (best-effort)")]
        public Vector3 output = Vector3.zero;

        [Header("Connection Hints")]
        public string incomingInput1Plug;
        public string incomingInput2Plug;
    }
}
