// Assets/MayaImporter/JointNode.cs
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;
using MayaImporter.Animation;

namespace MayaImporter.DAG
{
    [MayaNodeType("joint")]
    public sealed class JointNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            if (options == null) options = new MayaImportOptions();

            // ---- Read raw Maya values ----
            var tMaya = ReadVec3(new[] { ".t", "t" }, new[] { ".tx", "tx" }, new[] { ".ty", "ty" }, new[] { ".tz", "tz" }, Vector3.zero);
            var rMaya = ReadVec3(new[] { ".r", "r" }, new[] { ".rx", "rx" }, new[] { ".ry", "ry" }, new[] { ".rz", "rz" }, Vector3.zero);
            var sMaya = ReadVec3(new[] { ".s", "s" }, new[] { ".sx", "sx" }, new[] { ".sy", "sy" }, new[] { ".sz", "sz" }, Vector3.one);

            // jointOrient (Maya: finalRot = jointOrient * rotate)
            var joMaya = ReadVec3(new[] { ".jo", "jo" }, new[] { ".jox", "jox" }, new[] { ".joy", "joy" }, new[] { ".joz", "joz" }, Vector3.zero);

            int ro = ReadInt(new[] { ".ro", "ro" }, 0);
            bool ssc = ReadBool(new[] { ".ssc", "ssc" }, true);

            // ---- Apply to Unity ----
            var tUnity = MayaToUnityConversion.ConvertPosition(tMaya, options.Conversion);
            transform.localPosition = tUnity;
            transform.localScale = sMaya;

            // Rotation via Applier:
            // - eulerDeg stores Maya rotate channels (unconverted)
            // - preRotation stores jointOrient converted to Unity and applied as multiplication
            var applier = GetComponent<MayaEulerRotationApplier>() ?? gameObject.AddComponent<MayaEulerRotationApplier>();
            applier.rotateOrder = ro;
            applier.eulerDeg = rMaya;

            applier.autoDetectConversion = false;
            applier.conversion = options.Conversion;

            // preRotation must be Unity-space quaternion
            var joUnityEuler = MayaToUnityConversion.ConvertEulerDegrees(joMaya, options.Conversion);
            var preQ = MayaEulerRotationApplier.ToQuaternion(joUnityEuler, ro);
            applier.SetPreRotation(preQ);

            applier.ClearOverride();
            applier.ApplyNow();

            // Store joint metadata (UnityÇ…äTîOÇ™Ç»Ç¢Ç‡ÇÃÇÕêVãKÉRÉìÉ|)
            var meta = GetComponent<MayaJointMetadata>() ?? gameObject.AddComponent<MayaJointMetadata>();
            meta.jointOrientDegrees = joUnityEuler; // stored in Unity space for clarity
            meta.rotateOrder = ro;
            meta.segmentScaleCompensate = ssc;

            // Also store transform extras for completeness
            var ex = GetComponent<MayaTransformExtrasComponent>() ?? gameObject.AddComponent<MayaTransformExtrasComponent>();
            ex.UsesTransformStack = true;
            ex.Translate = tMaya;
            ex.RotateEuler = rMaya;
            ex.Scale = sMaya;
            ex.RotateOrder = ro;
            ex.RotateAxisEuler = Vector3.zero;
            ex.RotatePivot = Vector3.zero;
            ex.RotatePivotTranslate = Vector3.zero;
            ex.ScalePivot = Vector3.zero;
            ex.ScalePivotTranslate = Vector3.zero;
            ex.Shear = Vector3.zero;
        }

        private Vector3 ReadVec3(string[] packedKeys, string[] xKeys, string[] yKeys, string[] zKeys, Vector3 def)
        {
            foreach (var k in packedKeys)
            {
                if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                    TryF(a.Tokens[0], out var x) && TryF(a.Tokens[1], out var y) && TryF(a.Tokens[2], out var z))
                    return new Vector3(x, y, z);
            }

            var xx = ReadF(xKeys, def.x);
            var yy = ReadF(yKeys, def.y);
            var zz = ReadF(zKeys, def.z);
            return new Vector3(xx, yy, zz);
        }

        private float ReadF(string[] keys, float def)
        {
            foreach (var k in keys)
            {
                if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count > 0 && TryF(a.Tokens[0], out var f))
                    return f;
            }
            return def;
        }

        private int ReadInt(string[] keys, int def)
        {
            foreach (var k in keys)
            {
                if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count > 0 &&
                    int.TryParse(a.Tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    return v;
            }
            return def;
        }

        private bool ReadBool(string[] keys, bool def)
        {
            foreach (var k in keys)
            {
                if (TryGetAttr(k, out var a) && a.Tokens != null && a.Tokens.Count > 0)
                {
                    var s = a.Tokens[0].Trim().ToLowerInvariant();
                    if (s == "1" || s == "true") return true;
                    if (s == "0" || s == "false") return false;
                }
            }
            return def;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
