// Assets/MayaImporter/MayaGenerated_AimMatrixNode.cs
// NodeType: aimMatrix
//
// Phase C: Implemented (best-effort reconstruction)
//
// Goal:
// - Produce a deterministic aim/orientation matrix without Autodesk/Maya APIs.
// - Preserve raw attrs/connections (already stored in base).
// - Publish output via MayaImporter.Core.MayaMatrixValue.
//
// Notes / Limitations:
// - Maya aimMatrix supports many modes (worldUp types, matrices, offsets, maintainAxis, etc.).
// - This implementation covers the common case: "aim towards primaryTargetVector using an up vector".
// - Shear/pivots/offsets are ignored.

using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("aimMatrix")]
    public sealed class MayaGenerated_AimMatrixNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (aimMatrix)")]
        [SerializeField] private bool enabled = true;

        [Header("Inputs (best-effort)")]
        [SerializeField] private string inputMatrixSource;
        [SerializeField] private Matrix4x4 inputMatrixMaya = Matrix4x4.identity;

        [SerializeField] private Vector3 primaryTargetVector = Vector3.forward;
        [SerializeField] private Vector3 secondaryTargetVector = Vector3.up;

        [SerializeField] private Vector3 primaryInputAxis = Vector3.forward;
        [SerializeField] private Vector3 secondaryInputAxis = Vector3.up;

        [Header("Output")]
        [SerializeField] private Matrix4x4 outputMatrixMaya = Matrix4x4.identity;
        [SerializeField] private Matrix4x4 outputMatrixUnity = Matrix4x4.identity;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            // ----------------------------
            // Resolve inputMatrix
            // ----------------------------
            inputMatrixSource = "LocalAttr";
            inputMatrixMaya = Matrix4x4.identity;

            var incoming = FindLastIncomingTo("inputMatrix", "im", "inMatrix", "matrix", "input");
            if (!string.IsNullOrEmpty(incoming) && TryResolveConnectedMatrix(incoming, out var mConn, out var srcNode))
            {
                inputMatrixMaya = mConn;
                inputMatrixSource = $"Conn:{srcNode}";
            }
            else
            {
                // Local matrix tokens (common keys)
                if (!TryReadMatrix4x4(".inputMatrix", out inputMatrixMaya) &&
                    !TryReadMatrix4x4("inputMatrix", out inputMatrixMaya) &&
                    !TryReadMatrix4x4(".im", out inputMatrixMaya) &&
                    !TryReadMatrix4x4("im", out inputMatrixMaya) &&
                    !TryReadMatrix4x4(".inMatrix", out inputMatrixMaya) &&
                    !TryReadMatrix4x4("inMatrix", out inputMatrixMaya))
                {
                    inputMatrixMaya = Matrix4x4.identity;
                }
            }

            // ----------------------------
            // Read vectors (best-effort)
            // ----------------------------
            primaryTargetVector = ReadVec3(
                Vector3.forward,
                packedKeys: new[] { ".primaryTargetVector", "primaryTargetVector", ".ptv", "ptv", ".aimVector", "aimVector" },
                xKeys: new[] { ".primaryTargetVectorX", "primaryTargetVectorX", ".ptvx", "ptvx", ".aimVectorX", "aimVectorX" },
                yKeys: new[] { ".primaryTargetVectorY", "primaryTargetVectorY", ".ptvy", "ptvy", ".aimVectorY", "aimVectorY" },
                zKeys: new[] { ".primaryTargetVectorZ", "primaryTargetVectorZ", ".ptvz", "ptvz", ".aimVectorZ", "aimVectorZ" }
            );

            secondaryTargetVector = ReadVec3(
                Vector3.up,
                packedKeys: new[] { ".secondaryTargetVector", "secondaryTargetVector", ".stv", "stv", ".upVector", "upVector", ".worldUpVector", "worldUpVector" },
                xKeys: new[] { ".secondaryTargetVectorX", "secondaryTargetVectorX", ".stvx", "stvx", ".upVectorX", "upVectorX", ".worldUpVectorX", "worldUpVectorX" },
                yKeys: new[] { ".secondaryTargetVectorY", "secondaryTargetVectorY", ".stvy", "stvy", ".upVectorY", "upVectorY", ".worldUpVectorY", "worldUpVectorY" },
                zKeys: new[] { ".secondaryTargetVectorZ", "secondaryTargetVectorZ", ".stvz", "stvz", ".upVectorZ", "upVectorZ", ".worldUpVectorZ", "worldUpVectorZ" }
            );

            primaryInputAxis = ReadVec3(
                Vector3.forward,
                packedKeys: new[] { ".primaryInputAxis", "primaryInputAxis", ".pia", "pia" },
                xKeys: new[] { ".primaryInputAxisX", "primaryInputAxisX", ".piax", "piax" },
                yKeys: new[] { ".primaryInputAxisY", "primaryInputAxisY", ".piay", "piay" },
                zKeys: new[] { ".primaryInputAxisZ", "primaryInputAxisZ", ".piaz", "piaz" }
            );

            secondaryInputAxis = ReadVec3(
                Vector3.up,
                packedKeys: new[] { ".secondaryInputAxis", "secondaryInputAxis", ".sia", "sia" },
                xKeys: new[] { ".secondaryInputAxisX", "secondaryInputAxisX", ".siax", "siax" },
                yKeys: new[] { ".secondaryInputAxisY", "secondaryInputAxisY", ".siay", "siay" },
                zKeys: new[] { ".secondaryInputAxisZ", "secondaryInputAxisZ", ".siaz", "siaz" }
            );

            // Normalize target vectors
            var aim = SafeNormalize(primaryTargetVector, Vector3.forward);
            var up = SafeNormalize(secondaryTargetVector, Vector3.up);

            // Orthonormalize up against aim
            up = up - Vector3.Dot(up, aim) * aim;
            up = SafeNormalize(up, Vector3.up);
            var right = Vector3.Cross(up, aim);
            right = SafeNormalize(right, Vector3.right);
            up = Vector3.Cross(aim, right);

            // Canonical basis (X=right, Y=up, Z=aim)
            Vector3 basisX = right;
            Vector3 basisY = up;
            Vector3 basisZ = aim;

            // Map canonical basis to requested primary/secondary input axes
            // primaryInputAxis indicates which local axis should align with aim.
            // secondaryInputAxis indicates which local axis should align with up.
            var (pAxis, pSign) = AxisFromVector(primaryInputAxis, defaultAxis: 2); // default Z
            var (sAxis, sSign) = AxisFromVector(secondaryInputAxis, defaultAxis: 1); // default Y

            // Avoid same-axis conflict
            if (pAxis == sAxis)
            {
                sAxis = (pAxis + 1) % 3;
                sSign = 1f;
            }

            Vector3 aimSigned = basisZ * pSign;
            Vector3 upSigned = basisY * sSign;

            Vector3 outX = Vector3.right;
            Vector3 outY = Vector3.up;
            Vector3 outZ = Vector3.forward;

            AssignAxis(ref outX, ref outY, ref outZ, pAxis, aimSigned);
            AssignAxis(ref outX, ref outY, ref outZ, sAxis, upSigned);

            // Compute remaining axis to ensure right-handed basis
            int rem = RemainingAxis(pAxis, sAxis);
            var a = GetAxis(outX, outY, outZ, pAxis);
            var b = GetAxis(outX, outY, outZ, sAxis);

            var c = Vector3.Cross(a, b);
            c = SafeNormalize(c, rem == 0 ? Vector3.right : (rem == 1 ? Vector3.up : Vector3.forward));
            AssignAxis(ref outX, ref outY, ref outZ, rem, c);

            // Re-orthonormalize secondary using cross to reduce drift
            b = Vector3.Cross(c, a);
            b = SafeNormalize(b, b);
            AssignAxis(ref outX, ref outY, ref outZ, sAxis, b);

            // Build rotation matrix (Maya space) from columns
            var rotM = Matrix4x4.identity;
            rotM.SetColumn(0, new Vector4(outX.x, outX.y, outX.z, 0f));
            rotM.SetColumn(1, new Vector4(outY.x, outY.y, outY.z, 0f));
            rotM.SetColumn(2, new Vector4(outZ.x, outZ.y, outZ.z, 0f));
            rotM.m33 = 1f;

            // Preserve translation/scale from inputMatrix (best-effort)
            MatrixUtil.DecomposeTRS(inputMatrixMaya, out var tMaya, out _, out var sMaya);
            if (ApproximatelyZeroScale(sMaya)) sMaya = Vector3.one;

            outputMatrixMaya = Matrix4x4.TRS(tMaya, rotM.rotation, sMaya);
            outputMatrixUnity = MayaToUnityConversion.ConvertMatrix(outputMatrixMaya, options.Conversion);

            var outVal = GetComponent<MayaMatrixValue>() ?? gameObject.AddComponent<MayaMatrixValue>();
            outVal.valid = true;
            outVal.matrixMaya = outputMatrixMaya;
            outVal.matrixUnity = outputMatrixUnity;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, src={inputMatrixSource}, aim={aim}, up={up} (published MayaMatrixValue)");
        }

        // ----------------------------
        // Helpers
        // ----------------------------

        private bool TryResolveConnectedMatrix(string srcPlug, out Matrix4x4 mayaMatrix, out string srcNode)
        {
            mayaMatrix = Matrix4x4.identity;
            srcNode = null;
            if (string.IsNullOrEmpty(srcPlug)) return false;

            srcNode = MayaPlugUtil.ExtractNodePart(srcPlug);
            if (string.IsNullOrEmpty(srcNode)) return false;

            var tr = MayaNodeLookup.FindTransform(srcNode);
            if (tr == null) return false;

            var mv = tr.GetComponent<MayaMatrixValue>();
            if (mv == null || !mv.valid) return false;

            mayaMatrix = mv.matrixMaya;
            return true;
        }

        private Vector3 ReadVec3(Vector3 def, string[] packedKeys, string[] xKeys, string[] yKeys, string[] zKeys)
        {
            // packed
            if (packedKeys != null)
            {
                for (int i = 0; i < packedKeys.Length; i++)
                {
                    if (!TryGetTokens(packedKeys[i], out var t) || t == null || t.Count < 3) continue;
                    if (TryF(t[0], out var x) && TryF(t[1], out var y) && TryF(t[2], out var z))
                        return new Vector3(x, y, z);
                }
            }

            float xx = ReadFirstFloat(def.x, xKeys);
            float yy = ReadFirstFloat(def.y, yKeys);
            float zz = ReadFirstFloat(def.z, zKeys);
            return new Vector3(xx, yy, zz);
        }

        private float ReadFirstFloat(float def, string[] keys)
        {
            if (keys == null) return def;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetTokens(keys[i], out var t) || t == null || t.Count == 0) continue;
                if (TryF(t[0], out var f)) return f;
            }
            return def;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);

        private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
        {
            float m = v.magnitude;
            if (m < 1e-7f) return fallback;
            return v / m;
        }

        private static (int axis, float sign) AxisFromVector(Vector3 v, int defaultAxis)
        {
            float ax = Mathf.Abs(v.x);
            float ay = Mathf.Abs(v.y);
            float az = Mathf.Abs(v.z);

            int a = defaultAxis;
            float s = 1f;

            if (ax >= ay && ax >= az) { a = 0; s = Mathf.Sign(v.x); }
            else if (ay >= ax && ay >= az) { a = 1; s = Mathf.Sign(v.y); }
            else { a = 2; s = Mathf.Sign(v.z); }

            if (Mathf.Approximately(s, 0f)) s = 1f;
            return (a, s);
        }

        private static void AssignAxis(ref Vector3 x, ref Vector3 y, ref Vector3 z, int axis, Vector3 v)
        {
            if (axis == 0) x = v;
            else if (axis == 1) y = v;
            else z = v;
        }

        private static Vector3 GetAxis(Vector3 x, Vector3 y, Vector3 z, int axis)
        {
            if (axis == 0) return x;
            if (axis == 1) return y;
            return z;
        }

        private static int RemainingAxis(int a, int b)
        {
            for (int i = 0; i < 3; i++)
                if (i != a && i != b)
                    return i;
            return 2;
        }

        private static bool ApproximatelyZeroScale(Vector3 s)
            => Mathf.Abs(s.x) < 1e-7f || Mathf.Abs(s.y) < 1e-7f || Mathf.Abs(s.z) < 1e-7f;
    }
}
