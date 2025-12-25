// Assets/MayaImporter/TransformNode.cs
// NodeType: transform
//
// 方針:
// - Unity Transform に反映できる範囲は TRS と rotateOrder を確実に反映。
// - Unity Transform で表現できない pivot/shear/rotateAxis/offsetParentMatrix は
//   MayaTransformExtrasComponent に「必ず保存」して lossless に近づける。
// - offsetParentMatrix が非単位のときは、既存仕様どおり parent GO を挿入して表現（可能な範囲でTRSに分解）。

using System;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Animation;
using MayaImporter.Utils;

namespace MayaImporter.DAG
{
    [MayaNodeType("transform")]
    public sealed class TransformNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            // ---- Read Maya channels (Maya space) ----
            var tMaya = ReadVec3(
                packedKeys: new[] { ".t", "t" },
                xKeys: new[] { ".tx", "tx" },
                yKeys: new[] { ".ty", "ty" },
                zKeys: new[] { ".tz", "tz" },
                def: Vector3.zero);

            var rMaya = ReadVec3(
                packedKeys: new[] { ".r", "r" },
                xKeys: new[] { ".rx", "rx" },
                yKeys: new[] { ".ry", "ry" },
                zKeys: new[] { ".rz", "rz" },
                def: Vector3.zero);

            var sMaya = ReadVec3(
                packedKeys: new[] { ".s", "s" },
                xKeys: new[] { ".sx", "sx" },
                yKeys: new[] { ".sy", "sy" },
                zKeys: new[] { ".sz", "sz" },
                def: Vector3.one);

            int ro = ReadInt(new[] { ".ro", "ro" }, 0);

            // Extras
            var sh = ReadVec3(
                packedKeys: new[] { ".sh", "sh" },
                xKeys: new[] { ".shxy", "shxy" },
                yKeys: new[] { ".shxz", "shxz" },
                zKeys: new[] { ".shyz", "shyz" },
                def: Vector3.zero);

            var ra = ReadVec3(
                packedKeys: new[] { ".ra", "ra" },
                xKeys: new[] { ".rax", "rax" },
                yKeys: new[] { ".ray", "ray" },
                zKeys: new[] { ".raz", "raz" },
                def: Vector3.zero);

            var rp = ReadVec3(
                packedKeys: new[] { ".rp", "rp" },
                xKeys: new[] { ".rpx", "rpx" },
                yKeys: new[] { ".rpy", "rpy" },
                zKeys: new[] { ".rpz", "rpz" },
                def: Vector3.zero);

            var rpt = ReadVec3(
                packedKeys: new[] { ".rpt", "rpt" },
                xKeys: new[] { ".rptx", "rptx" },
                yKeys: new[] { ".rpty", "rpty" },
                zKeys: new[] { ".rptz", "rptz" },
                def: Vector3.zero);

            var sp = ReadVec3(
                packedKeys: new[] { ".sp", "sp" },
                xKeys: new[] { ".spx", "spx" },
                yKeys: new[] { ".spy", "spy" },
                zKeys: new[] { ".spz", "spz" },
                def: Vector3.zero);

            var spt = ReadVec3(
                packedKeys: new[] { ".spt", "spt" },
                xKeys: new[] { ".sptx", "sptx" },
                yKeys: new[] { ".spty", "spty" },
                zKeys: new[] { ".sptz", "sptz" },
                def: Vector3.zero);

            bool vis = ReadBool(new[] { ".v", "v", ".visibility", "visibility" }, true);

            // ---- offsetParentMatrix ----
            bool hasOpm = TryGetOffsetParentMatrix(out var opmMaya, out var opmSource);
            bool opmNonIdentity = hasOpm && !IsApproximatelyIdentity(opmMaya);

            // ---- Store extras (lossless-ish) ----
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

            ex.HasOffsetParentMatrix = hasOpm;
            ex.OffsetParentMatrixMaya = hasOpm ? opmMaya : Matrix4x4.identity;
            ex.OffsetParentMatrixSource = hasOpm ? opmSource : null;

            ex.HasNonTrsExtras =
                sh != Vector3.zero ||
                ra != Vector3.zero ||
                rp != Vector3.zero ||
                rpt != Vector3.zero ||
                sp != Vector3.zero ||
                spt != Vector3.zero ||
                (hasOpm && opmNonIdentity);

            // TRS-only audit matrices (rotateOrderは applier が扱うのでここは概算)
            var mayaRoughQ = Quaternion.Euler(rMaya);
            MayaToUnityConversion.BuildLocalMatrices(tMaya, mayaRoughQ, sMaya, options.Conversion, out ex.LocalTrsMatrixMaya, out ex.LocalTrsMatrixUnity);

            // ---- Represent offsetParentMatrix by inserting a parent GO (best-effort) ----
            if (opmNonIdentity)
            {
                EnsureOffsetParentGameObject(options, opmMaya, opmSource, ex);
            }

            // ---- Apply to Unity Transform (representable subset) ----
            transform.localPosition = MayaToUnityConversion.ConvertPosition(tMaya, options.Conversion);
            transform.localScale = sMaya;

            // rotateOrder を確実に反映（eulerは Maya空間値で保持し、applier側で変換）
            var applier = GetComponent<MayaEulerRotationApplier>() ?? gameObject.AddComponent<MayaEulerRotationApplier>();
            applier.rotateOrder = ro;
            applier.eulerDeg = rMaya;
            applier.autoDetectConversion = false;
            applier.conversion = options.Conversion;
            applier.ClearPreRotation();
            applier.ClearOverride();
            applier.ApplyNow();

            ApplyVisibility(vis);
        }

        private void EnsureOffsetParentGameObject(MayaImportOptions options, Matrix4x4 opmMaya, string source, MayaTransformExtrasComponent ex)
        {
            // Idempotent check: if parent already has marker for this Maya node, do nothing.
            var parent = transform.parent;
            if (parent != null)
            {
                var parentMarker = parent.GetComponent<MayaOffsetParentMatrixMarker>();
                if (parentMarker != null && MayaPlugUtil.NodeMatches(parentMarker.ChildMayaNodeName, NodeName))
                {
                    ex.InsertedOffsetParentGameObject = true;
                    ex.OffsetParentGameObjectName = parent.name;
                    return;
                }
            }

            // Insert a parent GO between current parent and this transform.
            string opmName = $"{gameObject.name}__offsetParentMatrix";
            var go = new GameObject(opmName);
            var opmTr = go.transform;

            opmTr.SetParent(transform.parent, worldPositionStays: false);

            // Deterministic sibling order: try to keep relative order.
            try { opmTr.SetSiblingIndex(transform.GetSiblingIndex()); }
            catch { /* ignore */ }

            transform.SetParent(opmTr, worldPositionStays: false);

            // Apply offsetParentMatrix to the inserted parent transform (best-effort TRS)
            MatrixUtil.DecomposeTRS(opmMaya, out var tMaya, out var qMaya, out var sMaya);

            opmTr.localPosition = MayaToUnityConversion.ConvertPosition(tMaya, options.Conversion);
            opmTr.localRotation = MayaToUnityConversion.ConvertQuaternion(qMaya, options.Conversion);
            opmTr.localScale = sMaya;

            var marker = go.AddComponent<MayaOffsetParentMatrixMarker>();
            marker.ChildMayaNodeName = NodeName;
            marker.Source = source;
            marker.OffsetParentMatrixMaya = opmMaya;
            marker.OffsetParentMatrixUnity = MayaToUnityConversion.ConvertMatrix(opmMaya, options.Conversion);

            ex.InsertedOffsetParentGameObject = true;
            ex.OffsetParentGameObjectName = opmName;
        }

        private bool TryGetOffsetParentMatrix(out Matrix4x4 m, out string source)
        {
            m = Matrix4x4.identity;
            source = null;

            // 1) Prefer connection: something -> this.offsetParentMatrix
            if (Connections != null)
            {
                for (int i = Connections.Count - 1; i >= 0; i--)
                {
                    var c = Connections[i];
                    if (c == null) continue;

                    if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                        continue;

                    var dst = c.DstPlug ?? "";
                    if (dst.IndexOf("offsetParentMatrix", StringComparison.Ordinal) < 0)
                        continue;

                    var srcNode = c.SrcNodePart;
                    if (string.IsNullOrEmpty(srcNode))
                        srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);

                    if (string.IsNullOrEmpty(srcNode))
                        continue;

                    var tr = MayaNodeLookup.FindTransform(srcNode);
                    if (tr == null) continue;

                    var mv = tr.GetComponent<MayaMatrixValue>();
                    if (mv != null && mv.valid)
                    {
                        m = mv.matrixMaya;
                        source = $"Conn:{srcNode}";
                        return true;
                    }
                }
            }

            // 2) Local attribute: .offsetParentMatrix (16 floats)
            if (TryGetAttr(".offsetParentMatrix", out var a) || TryGetAttr("offsetParentMatrix", out a))
            {
                if (a != null && a.Tokens != null && a.Tokens.Count >= 16 && MatrixUtil.TryParseMatrix4x4(a.Tokens, 0, out var mm))
                {
                    m = mm;
                    source = $"Attr:{a.Key}";
                    return true;
                }
            }

            return false;
        }

        private static bool IsApproximatelyIdentity(Matrix4x4 m, float eps = 1e-6f)
        {
            return Mathf.Abs(m.m00 - 1f) < eps && Mathf.Abs(m.m11 - 1f) < eps && Mathf.Abs(m.m22 - 1f) < eps && Mathf.Abs(m.m33 - 1f) < eps &&
                   Mathf.Abs(m.m01) < eps && Mathf.Abs(m.m02) < eps && Mathf.Abs(m.m03) < eps &&
                   Mathf.Abs(m.m10) < eps && Mathf.Abs(m.m12) < eps && Mathf.Abs(m.m13) < eps &&
                   Mathf.Abs(m.m20) < eps && Mathf.Abs(m.m21) < eps && Mathf.Abs(m.m23) < eps &&
                   Mathf.Abs(m.m30) < eps && Mathf.Abs(m.m31) < eps && Mathf.Abs(m.m32) < eps;
        }

        private void ApplyVisibility(bool visible)
        {
            var r = GetComponent<Renderer>();
            if (r != null) r.enabled = visible;
            else gameObject.SetActive(visible);
        }

        // =========================
        // Attribute Readers
        // =========================

        private Vector3 ReadVec3(string[] packedKeys, string[] xKeys, string[] yKeys, string[] zKeys, Vector3 def)
        {
            // packed
            if (packedKeys != null)
            {
                foreach (var k in packedKeys)
                {
                    if (TryGetAttr(k, out var a) && a?.Tokens != null && a.Tokens.Count >= 3 &&
                        TryF(a.Tokens[0], out var x) && TryF(a.Tokens[1], out var y) && TryF(a.Tokens[2], out var z))
                        return new Vector3(x, y, z);
                }
            }

            // xyz
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
                if (TryGetAttr(k, out var a) && a?.Tokens != null && a.Tokens.Count > 0 && TryF(a.Tokens[0], out var f))
                    return f;
            }
            return def;
        }

        private int ReadInt(string[] keys, int def)
        {
            if (keys == null) return def;
            foreach (var k in keys)
            {
                if (TryGetAttr(k, out var a) && a?.Tokens != null && a.Tokens.Count > 0)
                {
                    for (int i = a.Tokens.Count - 1; i >= 0; i--)
                    {
                        if (int.TryParse(a.Tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                            return v;
                    }
                }
            }
            return def;
        }

        private bool ReadBool(string[] keys, bool def)
        {
            if (keys == null) return def;

            foreach (var k in keys)
            {
                if (!TryGetAttr(k, out var a) || a?.Tokens == null || a.Tokens.Count == 0)
                    continue;

                for (int i = a.Tokens.Count - 1; i >= 0; i--)
                {
                    var s = (a.Tokens[i] ?? "").Trim().ToLowerInvariant();
                    if (s == "1" || s == "true" || s == "yes" || s == "on") return true;
                    if (s == "0" || s == "false" || s == "no" || s == "off") return false;
                }
            }

            return def;
        }

        private static bool TryF(string s, out float f)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
        }
    }
}
