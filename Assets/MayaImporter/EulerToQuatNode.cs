// Assets/MayaImporter/EulerToQuatNode.cs
// Production implementation (structured decode + best-effort initial evaluation)
//
// eulerToQuat:
//  - reads inputRotate (deg) + inputRotateOrder (0..5)
//  - computes quaternion (Maya-space) and also Unity-converted quaternion (under CoordinateConversion)
//  - captures incoming plugs for later value-graph evaluation
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Animation;

namespace MayaImporter.Nodes
{
    [MayaNodeType("eulerToQuat")]
    [DisallowMultipleComponent]
    public sealed class EulerToQuatNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaEulerToQuatMetadata>() ?? gameObject.AddComponent<MayaEulerToQuatMetadata>();

            meta.rotateOrder = Mathf.Clamp(ReadInt(0,
                ".inputRotateOrder", "inputRotateOrder",
                ".rotateOrder", "rotateOrder",
                ".ro", "ro"), 0, 5);

            meta.inputEulerDegMaya = ReadVec3(
                Vector3.zero,
                new[] { ".inputRotate", "inputRotate", ".ir", "ir", ".input", "input" },
                new[] { ".inputRotateX", "inputRotateX", ".irx", "irx", ".inputX", "inputX" },
                new[] { ".inputRotateY", "inputRotateY", ".iry", "iry", ".inputY", "inputY" },
                new[] { ".inputRotateZ", "inputRotateZ", ".irz", "irz", ".inputZ", "inputZ" }
            );

            // Capture incoming plugs (if any)
            meta.srcInputRotatePlug = ResolveIncomingSrcPlugByDstContainsAny(new[]
            {
                "inputRotate", ".ir", ".input", ".inRotate", "rotate" // broad but safe for later debugging
            });

            meta.srcRotateOrderPlug = ResolveIncomingSrcPlugByDstContainsAny(new[]
            {
                "inputRotateOrder", "rotateOrder", ".ro"
            });

            // Evaluate (best-effort)
            meta.outputQuatMaya = MayaEulerRotationApplier.ToQuaternion(meta.inputEulerDegMaya, meta.rotateOrder);
            meta.outputQuatUnity = ConvertQuatMayaToUnity(meta.outputQuatMaya, options.Conversion);

            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[eulerToQuat] name='{NodeName}' ro={meta.rotateOrder} eulerMaya={meta.inputEulerDegMaya} " +
                     $"qMaya=({meta.outputQuatMaya.x:0.###},{meta.outputQuatMaya.y:0.###},{meta.outputQuatMaya.z:0.###},{meta.outputQuatMaya.w:0.###})");
        }

        private static Quaternion ConvertQuatMayaToUnity(Quaternion qMaya, CoordinateConversion conversion)
        {
            // Convert using matrix conversion (stable for MirrorZ)
            var m = Matrix4x4.Rotate(qMaya);
            var mu = MayaToUnityConversion.ConvertMatrix(m, conversion);
            return mu.rotation;
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

        private Vector3 ReadVec3(Vector3 def, string[] packedKeys, string[] xKeys, string[] yKeys, string[] zKeys)
        {
            for (int i = 0; i < packedKeys.Length; i++)
            {
                var k = packedKeys[i];
                if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                    TryF(a.Tokens[0], out var x) && TryF(a.Tokens[1], out var y) && TryF(a.Tokens[2], out var z))
                    return new Vector3(x, y, z);
            }

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

    [DisallowMultipleComponent]
    public sealed class MayaEulerToQuatMetadata : MonoBehaviour
    {
        public bool valid;

        [Tooltip("Maya rotateOrder: 0=xyz 1=yzx 2=zxy 3=xzy 4=yxz 5=zyx")]
        public int rotateOrder;

        [Header("Inputs (Maya space)")]
        public Vector3 inputEulerDegMaya;

        [Header("Outputs (best-effort)")]
        public Quaternion outputQuatMaya = Quaternion.identity;
        public Quaternion outputQuatUnity = Quaternion.identity;

        [Header("Incoming plugs (if connected)")]
        public string srcInputRotatePlug;
        public string srcRotateOrderPlug;

        [Header("Debug")]
        public int lastBuildFrame;
    }
}
