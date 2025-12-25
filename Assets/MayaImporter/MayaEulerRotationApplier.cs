using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Animation
{
    [DisallowMultipleComponent]
    public sealed class MayaEulerRotationApplier : MonoBehaviour
    {
        // =========================
        // Backward-compatible API
        // =========================

        [Header("Conversion (Back-Compat)")]
        [Tooltip("If true, conversion is taken from MayaBuildContext.CurrentOptions.Conversion when available.")]
        public bool autoDetectConversion = true;

        [Tooltip("Coordinate conversion policy used when autoDetectConversion is false (or as a fallback).")]
        public CoordinateConversion conversion = CoordinateConversion.MayaToUnity_MirrorZ;

        // =========================
        // Main settings
        // =========================

        [Header("Rotation")]
        [Tooltip("Maya rotateOrder: 0=xyz 1=yzx 2=zxy 3=xzy 4=yxz 5=zyx")]
        public int rotateOrder = 0;

        [Tooltip("Euler angles in degrees (Maya channels: rotateX/Y/Z).")]
        public Vector3 eulerDeg;

        [Header("Pre-Rotation (Joint Orient)")]
        [SerializeField] private bool _hasPreRotation = false;
        [SerializeField] private Quaternion _preRotationLocal = Quaternion.identity;

        [Header("Runtime Override (used by constraints)")]
        public bool overrideActive = false;
        public Quaternion overrideLocalRotation = Quaternion.identity;

        private void Awake()
        {
            // rotateOrder auto read (best-effort)
            var node = GetComponent<MayaNodeComponentBase>();
            if (node != null)
            {
                if (TryReadIntAttribute(node, ".ro", out var ro) ||
                    TryReadIntAttribute(node, "ro", out ro) ||
                    TryReadIntAttribute(node, ".rotateOrder", out ro) ||
                    TryReadIntAttribute(node, "rotateOrder", out ro))
                {
                    rotateOrder = Mathf.Clamp(ro, 0, 5);
                }
            }
        }

        private void LateUpdate()
        {
            ApplyNow();
        }

        // =========================
        // Back-compat methods
        // =========================

        /// <summary>Clear jointOrient/pre-rotation.</summary>
        public void ClearPreRotation()
        {
            _hasPreRotation = false;
            _preRotationLocal = Quaternion.identity;
        }

        /// <summary>
        /// Set jointOrient/pre-rotation from Euler degrees (Maya space by default).
        /// </summary>
        public void SetPreRotation(Vector3 eulerDegrees, bool isMayaSpace = true)
        {
            var conv = ResolveConversion();
            var e = isMayaSpace ? MayaToUnityConversion.ConvertEulerDegrees(eulerDegrees, conv) : eulerDegrees;
            _preRotationLocal = ToQuaternion(e, rotateOrder);
            _hasPreRotation = true;
        }

        /// <summary>
        /// Set pre-rotation directly as a local quaternion (Unity local space).
        /// </summary>
        public void SetPreRotation(Quaternion localRotationUnity)
        {
            _preRotationLocal = localRotationUnity;
            _hasPreRotation = true;
        }

        // =========================
        // Constraint integration
        // =========================

        public void SetOverride(Quaternion localRotation)
        {
            overrideActive = true;
            overrideLocalRotation = localRotation;
        }

        public void ClearOverride()
        {
            overrideActive = false;
            overrideLocalRotation = Quaternion.identity;
        }

        public void ApplyNow()
        {
            // Constraints write the FINAL localRotation. Do not multiply pre-rotation here.
            if (overrideActive)
            {
                transform.localRotation = overrideLocalRotation;
                return;
            }

            var conv = ResolveConversion();

            // rotate channels are in Maya space in many pipelines → convert here (back-compat)
            var e = MayaToUnityConversion.ConvertEulerDegrees(eulerDeg, conv);

            var q = ToQuaternion(e, rotateOrder);

            // jointOrient/pre-rotation (final = pre * rotate)
            if (_hasPreRotation)
                q = _preRotationLocal * q;

            transform.localRotation = q;
        }

        public static void ApplyAllNow(Transform root)
        {
            if (root == null) return;
            var appliers = root.GetComponentsInChildren<MayaEulerRotationApplier>(true);
            for (int i = 0; i < appliers.Length; i++)
            {
                if (appliers[i] == null) continue;
                appliers[i].ApplyNow();
            }
        }

        // =========================
        // Euler <-> Quaternion (Maya rotateOrder)
        // =========================

        public static Quaternion ToQuaternion(Vector3 eulerDeg, int ro)
        {
            float x = eulerDeg.x;
            float y = eulerDeg.y;
            float z = eulerDeg.z;

            Quaternion qx = Quaternion.AngleAxis(x, Vector3.right);
            Quaternion qy = Quaternion.AngleAxis(y, Vector3.up);
            Quaternion qz = Quaternion.AngleAxis(z, Vector3.forward);

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

        /// <summary>
        /// Needed for skipRotate axes: convert quaternion to Euler in Maya rotateOrder space (deg).
        /// </summary>
        public static Vector3 FromQuaternion(Quaternion q, int ro)
        {
            var m = Matrix4x4.Rotate(q);

            int A, B, C;
            int signMid;

            switch (ro)
            {
                case 0: A = 0; B = 1; C = 2; signMid = +1; break; // xyz
                case 1: A = 1; B = 2; C = 0; signMid = +1; break; // yzx
                case 2: A = 2; B = 0; C = 1; signMid = +1; break; // zxy
                case 3: A = 0; B = 2; C = 1; signMid = -1; break; // xzy
                case 4: A = 1; B = 0; C = 2; signMid = -1; break; // yxz
                case 5: A = 2; B = 1; C = 0; signMid = -1; break; // zyx
                default: A = 0; B = 1; C = 2; signMid = +1; break;
            }

            float RAC = signMid * GetM(m, A, C);
            RAC = Mathf.Clamp(RAC, -1f, 1f);

            float b = Mathf.Asin(RAC);
            float cb = Mathf.Cos(b);

            float a, c;
            const float EPS = 1e-7f;

            if (Mathf.Abs(cb) > EPS)
            {
                a = Mathf.Atan2(-signMid * GetM(m, B, C), GetM(m, C, C));
                c = Mathf.Atan2(-signMid * GetM(m, A, B), GetM(m, A, A));
            }
            else
            {
                a = Mathf.Atan2(signMid * GetM(m, C, B), GetM(m, B, B));
                c = 0f;
            }

            var ang = Vector3.zero;
            SetAxis(ref ang, A, a * Mathf.Rad2Deg);
            SetAxis(ref ang, B, b * Mathf.Rad2Deg);
            SetAxis(ref ang, C, c * Mathf.Rad2Deg);

            return ang;
        }

        private static float GetM(Matrix4x4 m, int r, int c)
        {
            if (r == 0)
            {
                if (c == 0) return m.m00;
                if (c == 1) return m.m01;
                return m.m02;
            }
            if (r == 1)
            {
                if (c == 0) return m.m10;
                if (c == 1) return m.m11;
                return m.m12;
            }
            if (c == 0) return m.m20;
            if (c == 1) return m.m21;
            return m.m22;
        }

        private static void SetAxis(ref Vector3 v, int axis, float value)
        {
            if (axis == 0) v.x = value;
            else if (axis == 1) v.y = value;
            else v.z = value;
        }

        // =========================
        // Conversion resolution (back-compat)
        // =========================

        private CoordinateConversion ResolveConversion()
        {
            if (autoDetectConversion)
            {
                var opt = MayaBuildContext.CurrentOptions;
                if (opt != null)
                    return opt.Conversion;
            }
            return conversion;
        }

        // =========================
        // Reflection-based attribute read (no protected access)
        // =========================

        private static bool TryReadIntAttribute(MayaNodeComponentBase node, string key, out int value)
        {
            value = 0;
            if (node == null || string.IsNullOrEmpty(key)) return false;

            var attrsObj = GetMember(node.GetType(), node, "Attributes");
            if (attrsObj is not IList attrs) return false;

            for (int i = 0; i < attrs.Count; i++)
            {
                var a = attrs[i];
                if (a == null) continue;

                if (!TryGetKeyAndTokens(a, out var k, out var tokens)) continue;
                if (!string.Equals(k, key, StringComparison.Ordinal)) continue;

                if (tokens != null && tokens.Count > 0 && tokens[0] != null)
                    return int.TryParse(tokens[0].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }

            return false;
        }

        private static bool TryGetKeyAndTokens(object attrObj, out string key, out IList tokens)
        {
            key = null;
            tokens = null;

            var type = attrObj.GetType();

            key = GetStringMember(type, attrObj, "Key") ?? GetStringMember(type, attrObj, "Name");
            if (string.IsNullOrEmpty(key)) return false;

            tokens = GetIListMember(type, attrObj, "Tokens") ?? GetIListMember(type, attrObj, "ValueTokens");
            return tokens != null;
        }

        private static object GetMember(Type t, object obj, string member)
        {
            var p = t.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null) return p.GetValue(obj);

            var f = t.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) return f.GetValue(obj);

            return null;
        }

        private static string GetStringMember(Type t, object obj, string member)
        {
            var p = t.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(string)) return p.GetValue(obj) as string;

            var f = t.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(string)) return f.GetValue(obj) as string;

            return null;
        }

        private static IList GetIListMember(Type t, object obj, string member)
        {
            var p = t.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && typeof(IList).IsAssignableFrom(p.PropertyType)) return p.GetValue(obj) as IList;

            var f = t.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && typeof(IList).IsAssignableFrom(f.FieldType)) return f.GetValue(obj) as IList;

            return null;
        }
    }
}
