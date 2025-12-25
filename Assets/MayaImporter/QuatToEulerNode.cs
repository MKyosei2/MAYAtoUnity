// Assets/MayaImporter/QuatToEulerNode.cs
// Phase C placeholder -> implemented (structured decode + best-effort initial evaluation)
//
// quatToEuler:
//  - reads inputQuat (x,y,z,w) + inputRotateOrder
//  - computes Euler (deg) in Maya rotateOrder
//  - also computes Unity-converted Euler (under CoordinateConversion)
//  - captures incoming plugs for later value-graph evaluation
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Animation;

namespace MayaImporter.Nodes
{
    [MayaNodeType("quatToEuler")]
    [DisallowMultipleComponent]
    public sealed class QuatToEulerNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaQuatToEulerMetadata>() ?? gameObject.AddComponent<MayaQuatToEulerMetadata>();

            meta.rotateOrder = Mathf.Clamp(ReadInt(0,
                ".inputRotateOrder", "inputRotateOrder",
                ".rotateOrder", "rotateOrder",
                ".ro", "ro"), 0, 5);

            meta.inputQuatMaya = ReadQuat(
                Quaternion.identity,
                packedKeys: new[] { ".inputQuat", "inputQuat", ".iq", "iq" },
                xKeys: new[] { ".inputQuatX", "inputQuatX", ".iqx", "iqx", ".inputX", "inputX" },
                yKeys: new[] { ".inputQuatY", "inputQuatY", ".iqy", "iqy", ".inputY", "inputY" },
                zKeys: new[] { ".inputQuatZ", "inputQuatZ", ".iqz", "iqz", ".inputZ", "inputZ" },
                wKeys: new[] { ".inputQuatW", "inputQuatW", ".iqw", "iqw", ".inputW", "inputW" }
            );

            meta.srcInputQuatPlug = ResolveIncomingSrcPlugByDstContainsAny(new[]
            {
                "inputQuat", ".iq", ".inputX", ".inputY", ".inputZ", ".inputW"
            });

            meta.srcRotateOrderPlug = ResolveIncomingSrcPlugByDstContainsAny(new[]
            {
                "inputRotateOrder", "rotateOrder", ".ro"
            });

            // Evaluate (best-effort)
            meta.outputEulerDegMaya = MayaEulerRotationApplier.FromQuaternion(meta.inputQuatMaya, meta.rotateOrder);
            meta.outputEulerDegUnity = MayaToUnityConversion.ConvertEulerDegrees(meta.outputEulerDegMaya, options.Conversion);

            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[quatToEuler] name='{NodeName}' ro={meta.rotateOrder} qMaya=({meta.inputQuatMaya.x:0.###},{meta.inputQuatMaya.y:0.###},{meta.inputQuatMaya.z:0.###},{meta.inputQuatMaya.w:0.###}) " +
                     $"eulerMaya={meta.outputEulerDegMaya}");
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

        // ----------------- attribute readers -----------------

        private Quaternion ReadQuat(Quaternion def, string[] packedKeys, string[] xKeys, string[] yKeys, string[] zKeys, string[] wKeys)
        {
            // packed: 4 tokens
            for (int i = 0; i < packedKeys.Length; i++)
            {
                var k = packedKeys[i];
                if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count >= 4 &&
                    TryF(a.Tokens[0], out var x) && TryF(a.Tokens[1], out var y) && TryF(a.Tokens[2], out var z) && TryF(a.Tokens[3], out var w))
                    return new Quaternion(x, y, z, w);
            }

            float xx = ReadF(def.x, xKeys);
            float yy = ReadF(def.y, yKeys);
            float zz = ReadF(def.z, zKeys);
            float ww = ReadF(def.w, wKeys);

            return new Quaternion(xx, yy, zz, ww);
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

    [DisallowMultipleComponent]
    public sealed class MayaQuatToEulerMetadata : MonoBehaviour
    {
        public bool valid;

        [Tooltip("Maya rotateOrder: 0=xyz 1=yzx 2=zxy 3=xzy 4=yxz 5=zyx")]
        public int rotateOrder;

        [Header("Inputs (Maya space)")]
        public Quaternion inputQuatMaya = Quaternion.identity;

        [Header("Outputs (best-effort)")]
        public Vector3 outputEulerDegMaya;
        public Vector3 outputEulerDegUnity;

        [Header("Incoming plugs (if connected)")]
        public string srcInputQuatPlug;
        public string srcRotateOrderPlug;

        [Header("Debug")]
        public int lastBuildFrame;
    }
}
