// File: Assets/MayaImporter/ComposeMatrixNode.cs
using System;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter
{
    /// <summary>
    /// Maya composeMatrix:
    /// Best-effort reconstruction in Unity (no Autodesk API).
    /// Outputs a Matrix4x4 stored on this component (for debug/graph usage).
    ///
    /// Maya typical inputs:
    /// - inputTranslate (it) : double3
    /// - inputRotate (ir)    : double3 (degrees)
    /// - inputScale (is)     : double3
    /// - inputShear (ish)    : double3
    /// - rotateOrder (ro)    : int (0..5)
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("composeMatrix")]
    public sealed class ComposeMatrixNode : MayaNodeComponentBase
    {
        [Header("Decoded (best-effort)")]
        public Vector3 inputTranslate = Vector3.zero;
        public Vector3 inputRotateDeg = Vector3.zero;
        public Vector3 inputScale = Vector3.one;
        public Vector3 inputShear = Vector3.zero;
        public int rotateOrder = 0;

        [Header("Output (Unity)")]
        public Matrix4x4 outputMatrix = Matrix4x4.identity;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            inputTranslate = ReadVec3(new[] { ".inputTranslate", "inputTranslate", ".it", "it" }, inputTranslate);
            inputRotateDeg = ReadVec3(new[] { ".inputRotate", "inputRotate", ".ir", "ir" }, inputRotateDeg);
            inputScale = ReadVec3(new[] { ".inputScale", "inputScale", ".is", "is" }, inputScale);
            inputShear = ReadVec3(new[] { ".inputShear", "inputShear", ".ish", "ish" }, inputShear);
            rotateOrder = ReadInt(new[] { ".rotateOrder", "rotateOrder", ".ro", "ro" }, rotateOrder);
            rotateOrder = Mathf.Clamp(rotateOrder, 0, 5);

            // Build matrix: T * R * Shear * S  (best-effort)
            var q = EulerToQuaternionWithMayaOrder(inputRotateDeg, rotateOrder);
            var tM = Matrix4x4.Translate(inputTranslate);
            var rM = Matrix4x4.Rotate(q);
            var shM = MakeShearMatrix(inputShear);
            var sM = Matrix4x4.Scale(inputScale);

            outputMatrix = tM * rM * shM * sM;

            log.Info($"[composeMatrix] t={inputTranslate} r={inputRotateDeg} ro={rotateOrder} s={inputScale} sh={inputShear}");
        }

        private static Matrix4x4 MakeShearMatrix(Vector3 sh)
        {
            // Maya shear: (xy, xz, yz) in a double3
            // We interpret:
            // [ 1   sh.x sh.y 0 ]
            // [ 0    1   sh.z 0 ]
            // [ 0    0    1  0 ]
            // [ 0    0    0  1 ]
            var m = Matrix4x4.identity;
            m.m01 = sh.x; // XY
            m.m02 = sh.y; // XZ
            m.m12 = sh.z; // YZ
            return m;
        }

        private static Quaternion EulerToQuaternionWithMayaOrder(Vector3 eulerDeg, int ro)
        {
            // Maya rotateOrder:
            // 0 xyz, 1 yzx, 2 zxy, 3 xzy, 4 yxz, 5 zyx
            float rx = eulerDeg.x;
            float ry = eulerDeg.y;
            float rz = eulerDeg.z;

            Quaternion qx = Quaternion.AngleAxis(rx, Vector3.right);
            Quaternion qy = Quaternion.AngleAxis(ry, Vector3.up);
            Quaternion qz = Quaternion.AngleAxis(rz, Vector3.forward);

            // Apply order as multiplication sequence
            // Note: Unity uses left-multiplication for applying rotations in sequence
            switch (ro)
            {
                case 0: return qx * qy * qz; // xyz
                case 1: return qy * qz * qx; // yzx
                case 2: return qz * qx * qy; // zxy
                case 3: return qx * qz * qy; // xzy
                case 4: return qy * qx * qz; // yxz
                case 5: return qz * qy * qx; // zyx
                default: return qx * qy * qz;
            }
        }

        // -------------------------
        // Attr readers (local, single definition only)
        // -------------------------

        private Vector3 ReadVec3(string[] keys, Vector3 def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count < 3) continue;

                if (TryF(a.Tokens[0], out var x) && TryF(a.Tokens[1], out var y) && TryF(a.Tokens[2], out var z))
                    return new Vector3(x, y, z);
            }
            return def;
        }

        private int ReadInt(string[] keys, int def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count == 0) continue;
                for (int j = a.Tokens.Count - 1; j >= 0; j--)
                {
                    if (int.TryParse((a.Tokens[j] ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        return v;
                }
            }
            return def;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
