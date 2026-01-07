using System;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;
using MayaImporter.Components;

namespace MayaImporter.DAG
{
    /// <summary>
    /// NodeType: joint
    ///
    /// Strict reconstruction:
    /// - jointOrient (pre-rotation), rotateOrder, rotateAxis, pivots, shear, offsetParentMatrix
    /// - Full local matrix in Maya space -> convert to Unity -> decompose TRS
    ///
    /// Works without Maya/Autodesk API (Unity-only).
    /// </summary>
    [MayaNodeType("joint")]
    public sealed class JointNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            // Disable legacy Euler applier if present (it would overwrite rotation every LateUpdate)
            var legacyEuler = GetComponent<MayaImporter.Animation.MayaEulerRotationApplier>();
            if (legacyEuler != null) legacyEuler.enabled = false;

            // ---- Read Maya channels (Maya space, degrees) ----
            var tMaya = ReadVec3(new[] { ".t", "t", ".translate", "translate" },
                                 new[] { ".tx", "tx", ".translateX", "translateX" },
                                 new[] { ".ty", "ty", ".translateY", "translateY" },
                                 new[] { ".tz", "tz", ".translateZ", "translateZ" },
                                 Vector3.zero);

            var rMaya = ReadVec3(new[] { ".r", "r", ".rotate", "rotate" },
                                 new[] { ".rx", "rx", ".rotateX", "rotateX" },
                                 new[] { ".ry", "ry", ".rotateY", "rotateY" },
                                 new[] { ".rz", "rz", ".rotateZ", "rotateZ" },
                                 Vector3.zero);

            var sMaya = ReadVec3(new[] { ".s", "s", ".scale", "scale" },
                                 new[] { ".sx", "sx", ".scaleX", "scaleX" },
                                 new[] { ".sy", "sy", ".scaleY", "scaleY" },
                                 new[] { ".sz", "sz", ".scaleZ", "scaleZ" },
                                 Vector3.one);

            // jointOrient (pre-rotation)
            var joMaya = ReadVec3(new[] { ".jo", "jo", ".jointOrient", "jointOrient" },
                                  new[] { ".jox", "jox", ".jointOrientX", "jointOrientX" },
                                  new[] { ".joy", "joy", ".jointOrientY", "jointOrientY" },
                                  new[] { ".joz", "joz", ".jointOrientZ", "jointOrientZ" },
                                  Vector3.zero);

            int ro = ReadInt(new[] { ".ro", "ro", ".rotateOrder", "rotateOrder" }, 0);
            bool ssc = ReadBool(new[] { ".ssc", "ssc", ".segmentScaleCompensate", "segmentScaleCompensate" }, true);

            // rotateAxis (rare on joints but possible)
            var ra = ReadVec3(new[] { ".ra", "ra", ".rotateAxis", "rotateAxis" },
                              new[] { ".rax", "rax", ".rotateAxisX", "rotateAxisX" },
                              new[] { ".ray", "ray", ".rotateAxisY", "rotateAxisY" },
                              new[] { ".raz", "raz", ".rotateAxisZ", "rotateAxisZ" },
                              Vector3.zero);

            // pivots (can exist)
            var rp = ReadVec3(new[] { ".rp", "rp", ".rotatePivot", "rotatePivot" },
                              new[] { ".rpx", "rpx", ".rotatePivotX", "rotatePivotX" },
                              new[] { ".rpy", "rpy", ".rotatePivotY", "rotatePivotY" },
                              new[] { ".rpz", "rpz", ".rotatePivotZ", "rotatePivotZ" },
                              Vector3.zero);

            var rpt = ReadVec3(new[] { ".rpt", "rpt", ".rotatePivotTranslate", "rotatePivotTranslate" },
                               new[] { ".rptx", "rptx", ".rotatePivotTranslateX", "rotatePivotTranslateX" },
                               new[] { ".rpty", "rpty", ".rotatePivotTranslateY", "rotatePivotTranslateY" },
                               new[] { ".rptz", "rptz", ".rotatePivotTranslateZ", "rotatePivotTranslateZ" },
                               Vector3.zero);

            var sp = ReadVec3(new[] { ".sp", "sp", ".scalePivot", "scalePivot" },
                              new[] { ".spx", "spx", ".scalePivotX", "scalePivotX" },
                              new[] { ".spy", "spy", ".scalePivotY", "scalePivotY" },
                              new[] { ".spz", "spz", ".scalePivotZ", "scalePivotZ" },
                              Vector3.zero);

            var spt = ReadVec3(new[] { ".spt", "spt", ".scalePivotTranslate", "scalePivotTranslate" },
                               new[] { ".sptx", "sptx", ".scalePivotTranslateX", "scalePivotTranslateX" },
                               new[] { ".spty", "spty", ".scalePivotTranslateY", "scalePivotTranslateY" },
                               new[] { ".sptz", "sptz", ".scalePivotTranslateZ", "scalePivotTranslateZ" },
                               Vector3.zero);

            var sh = ReadVec3(new[] { ".sh", "sh", ".shear", "shear" },
                              new[] { ".shxy", "shxy", ".shearXY", "shearXY" },
                              new[] { ".shxz", "shxz", ".shearXZ", "shearXZ" },
                              new[] { ".shyz", "shyz", ".shearYZ", "shearYZ" },
                              Vector3.zero);

            // offsetParentMatrix
            bool hasOpm = TryGetOffsetParentMatrix(out var opmMaya, out var opmSource);
            bool opmNonIdentity = hasOpm && !IsApproximatelyIdentity(opmMaya);

            // ---- Metadata (100% proof) ----
            var meta = GetComponent<MayaJointMetadata>() ?? gameObject.AddComponent<MayaJointMetadata>();
            meta.rotateOrder = ro;
            meta.segmentScaleCompensate = ssc;

            meta.jointOrientMayaDegrees = joMaya;
            meta.jointOrientDegrees = MayaToUnityConversion.ConvertEulerDegrees(joMaya, options.Conversion);

            // Also store the generic transform extras for stack parameters
            var ex = GetComponent<MayaTransformExtrasComponent>() ?? gameObject.AddComponent<MayaTransformExtrasComponent>();
            ex.UsesTransformStack = true;

            ex.Translate = tMaya;
            ex.RotateEuler = rMaya;
            ex.Scale = sMaya;

            ex.RotateOrder = ro;
            ex.RotateAxisEuler = ra;

            ex.RotatePivot = rp;
            ex.RotatePivotTranslate = rpt;

            ex.ScalePivot = sp;
            ex.ScalePivotTranslate = spt;

            ex.Shear = sh;

            ex.HasOffsetParentMatrix = opmNonIdentity;
            ex.OffsetParentMatrixMaya = opmMaya;
            ex.OffsetParentMatrixSource = hasOpm ? opmSource : null;

            ex.HasNonTrsExtras =
                joMaya != Vector3.zero ||
                sh != Vector3.zero ||
                ra != Vector3.zero ||
                rp != Vector3.zero ||
                rpt != Vector3.zero ||
                sp != Vector3.zero ||
                spt != Vector3.zero ||
                opmNonIdentity;

            // ---- Build FULL matrix + apply ----
            var preQ = MayaTransformStackMath.EulerToQuaternion(joMaya, ro);
            var localMaya = MayaTransformStackMath.BuildLocalMatrixMaya(
                t: tMaya,
                rEulerDeg: rMaya,
                s: sMaya,
                rotateOrder: ro,
                rotateAxisEulerDeg: ra,
                rotatePivot: rp,
                rotatePivotTranslate: rpt,
                scalePivot: sp,
                scalePivotTranslate: spt,
                shear: sh,
                jointOrientPreRotation: preQ,
                hasJointOrient: joMaya != Vector3.zero,
                offsetParentMatrixMaya: opmMaya,
                hasOffsetParentMatrix: opmNonIdentity);

            var localUnity = MayaToUnityConversion.ConvertMatrix(localMaya, options.Conversion);

            ex.LocalFullMatrixMaya = localMaya;
            ex.LocalFullMatrixUnity = localUnity;

            MayaTransformStackMath.ApplyLocalMatrixToTransform(transform, localUnity);
        }

        private bool TryGetOffsetParentMatrix(out Matrix4x4 opmMaya, out string sourceNode)
        {
            opmMaya = Matrix4x4.identity;
            sourceNode = null;

            // attribute tokens
            if (TryGetAttr(".offsetParentMatrix", out var a) || TryGetAttr("offsetParentMatrix", out a))
            {
                if (a != null && a.Tokens != null && a.Tokens.Count >= 16)
                {
                    if (MatrixUtil.TryParseMatrix4x4(a.Tokens, 0, out var m))
                    {
                        opmMaya = m;
                        return true;
                    }
                }
            }

            // connection source (best-effort record only)
            if (Connections != null)
            {
                for (int i = 0; i < Connections.Count; i++)
                {
                    var c = Connections[i];
                    if (c == null) continue;

                    if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                        continue;

                    var dst = c.DstPlug ?? "";
                    if (dst.IndexOf("offsetParentMatrix", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    sourceNode = c.SrcNodePart;
                    if (string.IsNullOrEmpty(sourceNode))
                        sourceNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);

                    return true;
                }
            }

            return false;
        }

        private static bool IsApproximatelyIdentity(in Matrix4x4 m)
        {
            const float eps = 1e-6f;
            return
                Mathf.Abs(m.m00 - 1f) < eps && Mathf.Abs(m.m11 - 1f) < eps && Mathf.Abs(m.m22 - 1f) < eps && Mathf.Abs(m.m33 - 1f) < eps &&
                Mathf.Abs(m.m01) < eps && Mathf.Abs(m.m02) < eps && Mathf.Abs(m.m03) < eps &&
                Mathf.Abs(m.m10) < eps && Mathf.Abs(m.m12) < eps && Mathf.Abs(m.m13) < eps &&
                Mathf.Abs(m.m20) < eps && Mathf.Abs(m.m21) < eps && Mathf.Abs(m.m23) < eps &&
                Mathf.Abs(m.m30) < eps && Mathf.Abs(m.m31) < eps && Mathf.Abs(m.m32) < eps;
        }

        private Vector3 ReadVec3(string[] packedKeys, string[] xKeys, string[] yKeys, string[] zKeys, Vector3 def)
        {
            if (packedKeys != null)
            {
                foreach (var k in packedKeys)
                {
                    if (!TryGetAttr(k, out var a) || a?.Tokens == null || a.Tokens.Count < 3) continue;
                    if (TryF(a.Tokens[0], out var x) && TryF(a.Tokens[1], out var y) && TryF(a.Tokens[2], out var z))
                        return new Vector3(x, y, z);
                }
            }

            var xx = ReadF(xKeys, def.x);
            var yy = ReadF(yKeys, def.y);
            var zz = ReadF(zKeys, def.z);
            return new Vector3(xx, yy, zz);
        }

        private float ReadF(string[] keys, float def)
        {
            if (keys == null) return def;
            foreach (var k in keys)
            {
                if (!TryGetAttr(k, out var a) || a?.Tokens == null || a.Tokens.Count == 0) continue;
                if (TryF(a.Tokens[0], out var f)) return f;
            }
            return def;
        }

        private int ReadInt(string[] keys, int def)
        {
            if (keys == null) return def;
            foreach (var k in keys)
            {
                if (!TryGetAttr(k, out var a) || a?.Tokens == null || a.Tokens.Count == 0) continue;
                if (int.TryParse(a.Tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
            }
            return def;
        }

        private bool ReadBool(string[] keys, bool def)
        {
            if (keys == null) return def;
            foreach (var k in keys)
            {
                if (!TryGetAttr(k, out var a) || a?.Tokens == null || a.Tokens.Count == 0) continue;
                var s = (a.Tokens[0] ?? "").Trim().ToLowerInvariant();
                if (s == "1" || s == "true" || s == "yes" || s == "on") return true;
                if (s == "0" || s == "false" || s == "no" || s == "off") return false;
            }
            return def;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
