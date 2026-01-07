// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: animBlend
// Production: meaningful decode + best-effort evaluation.
//
// Supports scalar OR vector inputs:
// - If inputA/inputB have 3 tokens -> Vector3 mode
// - Else -> Scalar mode
//
// Scalar: output = lerp(inputA, inputB, weight)
// Vector: output = lerp(vecA, vecB, weight)
//
// Publishes:
// - MayaFloatValue when scalar
// - MayaVector3Value when vector

using System;
using System.Globalization;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("animBlend")]
    public sealed class MayaGenerated_AnimBlendNode : MayaPhaseCNodeBase
    {
        public enum BlendMode
        {
            Scalar,
            Vector3
        }

        [Header("Decoded (animBlend)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float weight = 0.5f;

        [SerializeField] private BlendMode mode = BlendMode.Scalar;

        [SerializeField] private float inputA;
        [SerializeField] private float inputB;
        [SerializeField] private float outputScalar;

        [SerializeField] private Vector3 inputAVec;
        [SerializeField] private Vector3 inputBVec;
        [SerializeField] private Vector3 outputVec;

        [Header("Incoming Plugs (best-effort)")]
        [SerializeField] private string srcInputAPlug;
        [SerializeField] private string srcInputBPlug;
        [SerializeField] private string srcWeightPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            weight = Mathf.Clamp01(ReadFloat(0.5f, ".weight", "weight", ".w", "w", ".blend", "blend", ".blender", "blender"));

            // Decide vector mode if either input has 3 tokens
            bool hasVecA = TryReadVec3(".inputA", "inputA", out inputAVec);
            bool hasVecB = TryReadVec3(".inputB", "inputB", out inputBVec);

            if (hasVecA || hasVecB)
            {
                mode = BlendMode.Vector3;

                // fallback: if one side missing, treat as zero
                if (!hasVecA) inputAVec = Vector3.zero;
                if (!hasVecB) inputBVec = Vector3.zero;

                outputVec = enabled ? Vector3.Lerp(inputAVec, inputBVec, weight) : inputAVec;

                var outVec = GetComponent<MayaVector3Value>() ?? gameObject.AddComponent<MayaVector3Value>();
                outVec.Set(MayaVector3Value.Kind.Vector, outputVec, outputVec);

                // keep float carrier invalid-ish (optional)
                var outFloat = GetComponent<MayaFloatValue>();
                if (outFloat != null) outFloat.valid = false;
            }
            else
            {
                mode = BlendMode.Scalar;

                inputA = ReadFloat(0f, ".inputA", "inputA", ".ia", "ia", ".input", "input");
                inputB = ReadFloat(0f, ".inputB", "inputB", ".ib", "ib", ".additive", "additive");

                outputScalar = enabled ? Mathf.Lerp(inputA, inputB, weight) : inputA;

                var outFloat = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
                outFloat.Set(outputScalar, outputScalar);

                var outVec = GetComponent<MayaVector3Value>();
                if (outVec != null) outVec.valid = false;
            }

            srcInputAPlug = FindIncomingPlugContains("inputA", "ia", "input");
            srcInputBPlug = FindIncomingPlugContains("inputB", "ib");
            srcWeightPlug = FindIncomingPlugContains("weight", "w", "blend", "blender");

            var meta = GetComponent<MayaAnimBlendMetadata>() ?? gameObject.AddComponent<MayaAnimBlendMetadata>();
            meta.valid = true;
            meta.enabled = enabled;
            meta.weight = weight;
            meta.mode = (int)mode;
            meta.outputScalar = outputScalar;
            meta.outputVec = outputVec;
            meta.srcInputAPlug = srcInputAPlug;
            meta.srcInputBPlug = srcInputBPlug;
            meta.srcWeightPlug = srcWeightPlug;
            meta.lastBuildFrame = Time.frameCount;

            SetNotes($"animBlend '{NodeName}' mode={mode} w={weight:0.###}");
            log.Info($"[animBlend] '{NodeName}' enabled={enabled} mode={mode} weight={weight:0.###}");
        }

        private bool TryReadVec3(string k1, string k2, out Vector3 v)
        {
            v = Vector3.zero;
            if (TryGetTokens(k1, out var t) || TryGetTokens(k2, out t))
            {
                if (t != null && t.Count >= 3 &&
                    TryF(t[0], out var x) &&
                    TryF(t[1], out var y) &&
                    TryF(t[2], out var z))
                {
                    v = new Vector3(x, y, z);
                    return true;
                }
            }

            // axis fallback
            bool any = false;
            float x2 = ReadAxis(0f, k1, k2, 'X', ref any);
            float y2 = ReadAxis(0f, k1, k2, 'Y', ref any);
            float z2 = ReadAxis(0f, k1, k2, 'Z', ref any);
            if (any)
            {
                v = new Vector3(x2, y2, z2);
                return true;
            }

            return false;
        }

        private float ReadAxis(float def, string k1, string k2, char axis, ref bool any)
        {
            // .inputAX / inputAX etc
            string[] keys =
            {
                k1 + axis, (k1.StartsWith(".") ? k1.Substring(1) : k1) + axis,
                k2 + axis, (k2.StartsWith(".") ? k2.Substring(1) : k2) + axis
            };

            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetTokens(keys[i], out var t) && t != null && t.Count > 0 && TryF(t[0], out var f))
                {
                    any = true;
                    return f;
                }
            }
            return def;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);

        private string FindIncomingPlugContains(params string[] patterns)
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
                    if (!string.IsNullOrEmpty(pat) && dstAttr.Contains(pat, StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }

            return null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MayaAnimBlendMetadata : MonoBehaviour
    {
        public bool valid;
        public bool enabled;
        public float weight;
        public int mode;

        public float outputScalar;
        public Vector3 outputVec;

        public string srcInputAPlug;
        public string srcInputBPlug;
        public string srcWeightPlug;

        public int lastBuildFrame;
    }
}
