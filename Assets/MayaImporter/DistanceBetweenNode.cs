using System;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Shaders
{
    /// <summary>
    /// Maya DistanceBetween Node
    /// Calculates distance between two points.
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("distanceBetween")]
    public sealed class DistanceBetweenNode : MayaShaderNode
    {
        [Header("Inputs")]

        [Tooltip("First point")]
        public Vector3 point1 = Vector3.zero;

        [Tooltip("Second point")]
        public Vector3 point2 = Vector3.zero;

        [Header("Outputs")]

        [Tooltip("Calculated distance")]
        public float distance = 0f;

        [Header("Debug")]
        public string point1Source = "Unset";
        public string point2Source = "Unset";
        public int lastBuildFrame;

        // ===== Initialization =====

        public void InitializeDistanceBetween(
            string nodeName,
            string uuid,
            Vector3 p1,
            Vector3 p2)
        {
            InitializeNode(nodeName, "distanceBetween", uuid);

            point1 = p1;
            point2 = p2;
        }

        /// <summary>
        /// Phase-1 Step-3 reconstruction/evaluation.
        /// Computes the distance and publishes a float carrier for downstream nodes.
        /// </summary>
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            // Resolve point1
            if (!TryResolveIncomingVec3ByDstContainsAny(
                    new[] { "point1", ".point1", "p1", ".p1", "inPoint1", ".inPoint1" },
                    out var p1,
                    out var p1Src))
            {
                p1 = ReadVec3(Vector3.zero,
                    packedKeys: new[] { ".point1", "point1", ".p1", "p1", ".inPoint1", "inPoint1" },
                    xKeys: new[] { ".point1X", "point1X", ".p1x", "p1x", ".x1", "x1" },
                    yKeys: new[] { ".point1Y", "point1Y", ".p1y", "p1y", ".y1", "y1" },
                    zKeys: new[] { ".point1Z", "point1Z", ".p1z", "p1z", ".z1", "z1" }
                );
                p1Src = "LocalAttr";
            }

            // Resolve point2
            if (!TryResolveIncomingVec3ByDstContainsAny(
                    new[] { "point2", ".point2", "p2", ".p2", "inPoint2", ".inPoint2" },
                    out var p2,
                    out var p2Src))
            {
                p2 = ReadVec3(Vector3.zero,
                    packedKeys: new[] { ".point2", "point2", ".p2", "p2", ".inPoint2", "inPoint2" },
                    xKeys: new[] { ".point2X", "point2X", ".p2x", "p2x", ".x2", "x2" },
                    yKeys: new[] { ".point2Y", "point2Y", ".p2y", "p2y", ".y2", "y2" },
                    zKeys: new[] { ".point2Z", "point2Z", ".p2z", "p2z", ".z2", "z2" }
                );
                p2Src = "LocalAttr";
            }

            point1 = p1;
            point2 = p2;
            point1Source = p1Src;
            point2Source = p2Src;

            // Maya distance
            float dMaya = Vector3.Distance(p1, p2);

            // Unity distance (convert points then distance)
            Vector3 p1U = MayaToUnityConversion.ConvertPosition(p1, options.Conversion);
            Vector3 p2U = MayaToUnityConversion.ConvertPosition(p2, options.Conversion);
            float dUnity = Vector3.Distance(p1U, p2U);

            distance = dMaya;

            // Publish as float attribute too (shader network consumers)
            SetFloat("distance", dMaya);

            // Publish carrier for DG-style downstream nodes
            var outFloat = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            outFloat.Set(dMaya, dUnity);

            lastBuildFrame = Time.frameCount;

            log.Info($"[distanceBetween] '{NodeName}' p1='{p1Src}' p2='{p2Src}' d(Maya)={dMaya:0.######}");
        }

        private Vector3 ReadVec3(Vector3 def, string[] packedKeys, string[] xKeys, string[] yKeys, string[] zKeys)
        {
            for (int i = 0; i < packedKeys.Length; i++)
            {
                if (TryGetAttr(packedKeys[i], out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                    MathUtil.TryParseFloat(a.Tokens[0], out var x) &&
                    MathUtil.TryParseFloat(a.Tokens[1], out var y) &&
                    MathUtil.TryParseFloat(a.Tokens[2], out var z))
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
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count > 0 &&
                    MathUtil.TryParseFloat(a.Tokens[0], out var f))
                    return f;
            }
            return def;
        }

        private bool TryResolveIncomingVec3ByDstContainsAny(string[] dstContainsAny, out Vector3 v, out string srcSummary)
        {
            v = Vector3.zero;
            srcSummary = "None";

            if (Connections == null || dstContainsAny == null || dstContainsAny.Length == 0)
                return false;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dst = c.DstPlug ?? "";
                if (dst.Length == 0) continue;

                bool hit = false;
                for (int k = 0; k < dstContainsAny.Length; k++)
                {
                    var key = dstContainsAny[k];
                    if (!string.IsNullOrEmpty(key) && dst.Contains(key, StringComparison.Ordinal))
                    {
                        hit = true;
                        break;
                    }
                }
                if (!hit) continue;

                var srcNode = c.SrcNodePart;
                if (string.IsNullOrEmpty(srcNode))
                    srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);

                if (string.IsNullOrEmpty(srcNode))
                    continue;

                var tr = MayaNodeLookup.FindTransform(srcNode);
                if (tr == null) continue;

                var vv = tr.GetComponent<MayaVector3Value>();
                if (vv != null && vv.valid)
                {
                    v = vv.mayaValue;
                    srcSummary = $"Incoming:{srcNode}";
                    return true;
                }

                srcSummary = $"Incoming:{srcNode}(no MayaVector3Value)";
            }

            return false;
        }
    }
}
