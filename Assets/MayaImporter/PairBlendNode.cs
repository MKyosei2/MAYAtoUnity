// Assets/MayaImporter/PairBlendNode.cs
// Production implementation (structured decode)
// pairBlend: stores decoded inputs/weight + initial evaluated output (best-effort).
using System;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Animation;

namespace MayaImporter.Nodes
{
    [MayaNodeType("pairBlend")]
    [DisallowMultipleComponent]
    public sealed class PairBlendNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaPairBlendMetadata>() ?? gameObject.AddComponent<MayaPairBlendMetadata>();

            // ---- Read defaults (local attrs) ----
            meta.weightDefault = ReadF(
                0f,
                ".weight", "weight", ".w", "w",
                ".blend", "blend"
            );
            meta.weightDefault = Mathf.Clamp01(meta.weightDefault);

            // 0: Euler, 1: Quaternion (Maya common). If unknown -> Euler.
            meta.rotInterpolation = Mathf.RoundToInt(ReadF(
                0f,
                ".rotInterpolation", "rotInterpolation",
                ".rotationInterpolation", "rotationInterpolation",
                ".ri", "ri"
            ));

            meta.rotateOrder = Mathf.Clamp(ReadInt(0, ".rotateOrder", "rotateOrder", ".ro", "ro"), 0, 5);

            // Inputs (best-effort: try packed then per-axis)
            meta.inTranslate1 = ReadVec3(
                Vector3.zero,
                new[] { ".inTranslate1", "inTranslate1", ".it1", "it1" },
                new[] { ".inTranslate1X", "inTranslate1X", ".it1x", "it1x" },
                new[] { ".inTranslate1Y", "inTranslate1Y", ".it1y", "it1y" },
                new[] { ".inTranslate1Z", "inTranslate1Z", ".it1z", "it1z" }
            );

            meta.inTranslate2 = ReadVec3(
                Vector3.zero,
                new[] { ".inTranslate2", "inTranslate2", ".it2", "it2" },
                new[] { ".inTranslate2X", "inTranslate2X", ".it2x", "it2x" },
                new[] { ".inTranslate2Y", "inTranslate2Y", ".it2y", "it2y" },
                new[] { ".inTranslate2Z", "inTranslate2Z", ".it2z", "it2z" }
            );

            meta.inRotate1 = ReadVec3(
                Vector3.zero,
                new[] { ".inRotate1", "inRotate1", ".ir1", "ir1" },
                new[] { ".inRotate1X", "inRotate1X", ".ir1x", "ir1x" },
                new[] { ".inRotate1Y", "inRotate1Y", ".ir1y", "ir1y" },
                new[] { ".inRotate1Z", "inRotate1Z", ".ir1z", "ir1z" }
            );

            meta.inRotate2 = ReadVec3(
                Vector3.zero,
                new[] { ".inRotate2", "inRotate2", ".ir2", "ir2" },
                new[] { ".inRotate2X", "inRotate2X", ".ir2x", "ir2x" },
                new[] { ".inRotate2Y", "inRotate2Y", ".ir2y", "ir2y" },
                new[] { ".inRotate2Z", "inRotate2Z", ".ir2z", "ir2z" }
            );

            // ---- Capture incoming plugs (for future runtime evaluation / debugging) ----
            meta.srcWeightPlug = ResolveIncomingSrcPlugByDstContainsAny(new[]
            {
                ".weight", ".w", "weight", " w", ".blend", "blend"
            });

            meta.srcInTranslate1Plug = ResolveIncomingSrcPlugByDstContainsAny(new[]
            {
                ".inTranslate1", ".it1", "inTranslate1"
            });

            meta.srcInTranslate2Plug = ResolveIncomingSrcPlugByDstContainsAny(new[]
            {
                ".inTranslate2", ".it2", "inTranslate2"
            });

            meta.srcInRotate1Plug = ResolveIncomingSrcPlugByDstContainsAny(new[]
            {
                ".inRotate1", ".ir1", "inRotate1"
            });

            meta.srcInRotate2Plug = ResolveIncomingSrcPlugByDstContainsAny(new[]
            {
                ".inRotate2", ".ir2", "inRotate2"
            });

            // ---- Best-effort initial output evaluation (static defaults only) ----
            meta.outTranslate = Vector3.Lerp(meta.inTranslate1, meta.inTranslate2, meta.weightDefault);

            if (meta.rotInterpolation != 0)
            {
                // quaternion blend (best-effort)
                var q1 = MayaEulerRotationApplier.ToQuaternion(meta.inRotate1, meta.rotateOrder);
                var q2 = MayaEulerRotationApplier.ToQuaternion(meta.inRotate2, meta.rotateOrder);
                var qb = Quaternion.Slerp(q1, q2, meta.weightDefault);
                meta.outRotate = MayaEulerRotationApplier.FromQuaternion(qb, meta.rotateOrder);
            }
            else
            {
                // euler blend
                meta.outRotate = Vector3.Lerp(meta.inRotate1, meta.inRotate2, meta.weightDefault);
            }

            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[pairBlend] name='{NodeName}' w={meta.weightDefault:0.###} rotInterp={meta.rotInterpolation} ro={meta.rotateOrder} " +
                     $"outT={meta.outTranslate} outR={meta.outRotate}");
        }

        private string ResolveIncomingSrcPlugByDstContainsAny(string[] dstContainsAny)
        {
            if (Connections == null || dstContainsAny == null || dstContainsAny.Length == 0) return null;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                // upstream -> this node
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

                    // Prefer cached nodePart when available
                    if (!string.IsNullOrEmpty(c.SrcPlug))
                        return MayaAnimValueGraphNormalizePlug(c.SrcPlug);

                    return null;
                }
            }

            return null;
        }

        // Keep normalization logic local (MayaAnimValueGraph.NormalizePlug is private)
        private static string MayaAnimValueGraphNormalizePlug(string plug)
        {
            if (string.IsNullOrEmpty(plug)) return plug;
            plug = plug.Trim();
            if (plug.Length >= 2 && plug[0] == '"' && plug[plug.Length - 1] == '"')
                plug = plug.Substring(1, plug.Length - 2);
            return plug;
        }

        // ----------------- attribute readers -----------------

        private Vector3 ReadVec3(Vector3 def, string[] packedKeys, string[] xKeys, string[] yKeys, string[] zKeys)
        {
            // packed
            for (int i = 0; i < packedKeys.Length; i++)
            {
                var k = packedKeys[i];
                if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                    TryF(a.Tokens[0], out var x) && TryF(a.Tokens[1], out var y) && TryF(a.Tokens[2], out var z))
                    return new Vector3(x, y, z);
            }

            // per-axis
            float xx = ReadF(def.x, xKeys);
            float yy = ReadF(def.y, yKeys);
            float zz = ReadF(def.z, zKeys);
            return new Vector3(xx, yy, zz);
        }

        private float ReadF(float def, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count > 0 && TryF(a.Tokens[0], out var f))
                    return f;
            }
            return def;
        }

        private int ReadInt(int def, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count > 0 &&
                    int.TryParse(a.Tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    return v;
            }
            return def;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }

    /// <summary>
    /// Structured representation of pairBlend.
    /// - Keeps decoded defaults and captured incoming plugs.
    /// - Runtime evaluation (frame-by-frame) is handled later by graph expansion; this is the data foundation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaPairBlendMetadata : MonoBehaviour
    {
        [Header("Defaults (from setAttr)")]
        [Range(0f, 1f)] public float weightDefault = 0f;

        [Tooltip("0: Euler, 1: Quaternion (Maya common)")]
        public int rotInterpolation = 0;

        [Tooltip("Maya rotateOrder: 0=xyz 1=yzx 2=zxy 3=xzy 4=yxz 5=zyx")]
        public int rotateOrder = 0;

        public Vector3 inTranslate1;
        public Vector3 inTranslate2;
        public Vector3 inRotate1;
        public Vector3 inRotate2;

        [Header("Initial Evaluated Output (best-effort)")]
        public Vector3 outTranslate;
        public Vector3 outRotate;

        [Header("Incoming Source Plugs (if connected)")]
        public string srcWeightPlug;
        public string srcInTranslate1Plug;
        public string srcInTranslate2Plug;
        public string srcInRotate1Plug;
        public string srcInRotate2Plug;

        [Header("Debug")]
        public int lastBuildFrame;
    }
}
