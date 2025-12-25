using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Geometry;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Maya motionPath node -> Unity runtime driver (no Maya/API needed).
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("motionPath")]
    public sealed class MotionPathNode : MayaNodeComponentBase
    {
        [Header("Inputs")]
        public string curveNode;

        [Tooltip("Legacy single driven (kept for compatibility).")]
        public string constrainedNode;

        [Tooltip("All driven transforms detected from outgoing connections.")]
        public List<string> constrainedNodes = new List<string>();

        [Header("Parameters")]
        [Tooltip("uValue. If fractionMode=true: 0..1. If false: curve parameter (best-effort normalized). Can be animated in Unity.")]
        public float uValue;

        [Tooltip("fractionMode (fm). true: uValue treated as 0..1 of curve length. false: parameter domain (best-effort).")]
        public bool fractionMode = true;

        [Tooltip("If fractionMode=false and uMin/uMax exist, uValue is normalized by them.")]
        public float uMin = 0f;
        public float uMax = 1f;

        [Header("Follow / Axes")]
        public bool follow = true;

        [Tooltip("frontAxis in Maya (0:X 1:Y 2:Z 3:-X 4:-Y 5:-Z).")]
        public int followAxis = 2;

        [Tooltip("upAxis in Maya (0:X 1:Y 2:Z 3:-X 4:-Y 5:-Z).")]
        public int upAxis = 1;

        public int worldUpType = 0;
        public bool inverseFront = false;
        public bool inverseUp = false;

        [Header("Bank / Twist (best-effort)")]
        public bool bank = false;
        public float bankScale = 1f;
        public float bankThreshold = 0f; // degrees
        public float frontTwist = 0f;    // degrees
        public float upTwist = 0f;       // degrees
        public float sideTwist = 0f;     // degrees

        [Header("World Up")]
        public string worldUpMatrixNode;
        public Vector3 worldUpVector = Vector3.up;

        [Header("Debug")]
        public string notes;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            // ---- Read attrs (best-effort keys) ----
            uValue = ReadFloatAny(uValue, ".uValue", ".u", ".uval");

            fractionMode = ReadBoolAny(fractionMode, ".fractionMode", ".fm");
            uMin = ReadFloatAny(uMin, ".uMin", ".umin");
            uMax = ReadFloatAny(uMax, ".uMax", ".umax");

            follow = ReadBoolAny(follow, ".follow", ".f");

            // Maya uses "frontAxis" commonly; accept followAxis too.
            followAxis = ReadIntAny(followAxis, ".frontAxis", ".fa", ".followAxis");
            upAxis = ReadIntAny(upAxis, ".upAxis", ".ua");
            worldUpType = ReadIntAny(worldUpType, ".worldUpType", ".wut");

            inverseFront = ReadBoolAny(inverseFront, ".inverseFront", ".invf");
            inverseUp = ReadBoolAny(inverseUp, ".inverseUp", ".invu");

            bank = ReadBoolAny(bank, ".bank", ".bk");
            bankScale = ReadFloatAny(bankScale, ".bankScale", ".bks");
            bankThreshold = ReadFloatAny(bankThreshold, ".bankThreshold", ".bkt");

            frontTwist = ReadFloatAny(frontTwist, ".frontTwist", ".ft");
            upTwist = ReadFloatAny(upTwist, ".upTwist", ".ut");
            sideTwist = ReadFloatAny(sideTwist, ".sideTwist", ".st");

            worldUpVector = ReadVec3Any(worldUpVector, ".worldUpVector", ".wuv", ".wux", ".wuy", ".wuz");

            // ---- Resolve curve node from incoming connection to geometryPath/gp ----
            curveNode = ResolveIncomingNodeByDstAttrContains("geometryPath") ??
                        ResolveIncomingNodeByDstAttrContains(".gp") ??
                        curveNode;

            // ---- Resolve driven nodes from outgoing connections ----
            constrainedNodes = ResolveDrivenNodeNames();
            constrainedNode = constrainedNodes.Count > 0 ? constrainedNodes[0] : (constrainedNode ?? "");

            var curveTf = MayaNodeLookup.FindTransform(curveNode);

            if (curveTf == null)
            {
                log?.Warn($"[motionPath] curve not found: '{MayaPlugUtil.LeafName(curveNode)}' (node='{NodeName}')");
                return;
            }

            // Ensure curve has polyline component
            var poly = curveTf.GetComponent<MayaCurvePolylineComponent>();
            if (poly == null)
                log?.Warn($"[motionPath] curve has no MayaCurvePolylineComponent: '{curveTf.name}' (need nurbsCurve import)");

            // worldUp object (best-effort)
            worldUpMatrixNode =
                ResolveIncomingNodeByDstAttrContains("worldUpMatrix") ??
                ResolveIncomingNodeByDstAttrContains(".wum") ??
                ResolveIncomingNodeByDstAttrContains(".wuo") ??
                worldUpMatrixNode;

            var driver = GetComponent<MayaMotionPathDriver>();
            if (driver == null) driver = gameObject.AddComponent<MayaMotionPathDriver>();

            driver.Source = this;
            driver.CurveTransform = curveTf;
            driver.WorldUpObject = MayaNodeLookup.FindTransform(worldUpMatrixNode);
            driver.WorldUpVector = worldUpVector;

            // Bind driven transforms list
            driver.ConstrainedTargets.Clear();
            for (int i = 0; i < constrainedNodes.Count; i++)
            {
                var tf = MayaNodeLookup.FindTransform(constrainedNodes[i]);
                if (tf != null) driver.ConstrainedTargets.Add(tf);
            }

            // Legacy fallback
            if (driver.ConstrainedTargets.Count == 0)
            {
                var tf = MayaNodeLookup.FindTransform(constrainedNode);
                if (tf != null) driver.ConstrainedTargets.Add(tf);
            }

            MayaMotionPathManager.EnsureExists();

            log?.Info($"[motionPath] node='{NodeName}' curve='{curveTf.name}' driven={driver.ConstrainedTargets.Count} follow={follow} fractionMode={fractionMode} u={uValue}");
        }

        private List<string> ResolveDrivenNodeNames()
        {
            var list = new List<string>(4);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (Connections == null || Connections.Count == 0)
                return list;

            // Outgoing from motionPath -> transform translate/rotate
            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Source &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                if (string.IsNullOrEmpty(c.DstPlug)) continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";

                bool looks =
                    dstAttr.StartsWith("translate", StringComparison.Ordinal) ||
                    dstAttr.StartsWith("rotate", StringComparison.Ordinal) ||
                    dstAttr == "t" || dstAttr == "r";

                if (!looks) continue;

                var dstNode = c.DstNodePart;
                if (string.IsNullOrEmpty(dstNode)) continue;

                // keep full name; uniqueness by leaf is risky with namespaces, so use full
                if (seen.Add(dstNode))
                    list.Add(dstNode);
            }

            // Fallback: if nothing matched, take "best" destination anyway
            if (list.Count == 0)
            {
                for (int i = 0; i < Connections.Count; i++)
                {
                    var c = Connections[i];
                    if (c == null) continue;

                    if (c.RoleForThisNode != ConnectionRole.Source &&
                        c.RoleForThisNode != ConnectionRole.Both)
                        continue;

                    if (string.IsNullOrEmpty(c.DstNodePart)) continue;
                    if (seen.Add(c.DstNodePart))
                        list.Add(c.DstNodePart);
                }
            }

            return list;
        }

        private string ResolveIncomingNodeByDstAttrContains(string contains)
        {
            if (string.IsNullOrEmpty(contains)) return null;
            if (Connections == null || Connections.Count == 0) return null;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                if (string.IsNullOrEmpty(c.DstPlug)) continue;

                if (c.DstPlug.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
                    return MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                }
            }

            return null;
        }

        // ----------------- Attr readers -----------------

        private float ReadFloatAny(float def, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count > 0 &&
                    float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    return f;
            }
            return def;
        }

        private int ReadIntAny(int def, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count > 0 &&
                    int.TryParse(a.Tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    return v;
            }
            return def;
        }

        private bool ReadBoolAny(bool def, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count > 0)
                {
                    var s = a.Tokens[0].Trim().ToLowerInvariant();
                    if (s == "1" || s == "true") return true;
                    if (s == "0" || s == "false") return false;
                }
            }
            return def;
        }

        private Vector3 ReadVec3Any(Vector3 def, string packed, string packed2, string x, string y, string z)
        {
            if (TryGetAttr(packed, out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                TryF(a.Tokens[0], out var vx) && TryF(a.Tokens[1], out var vy) && TryF(a.Tokens[2], out var vz))
                return new Vector3(vx, vy, vz);

            if (TryGetAttr(packed2, out var b) && b.Tokens != null && b.Tokens.Count >= 3 &&
                TryF(b.Tokens[0], out vx) && TryF(b.Tokens[1], out vy) && TryF(b.Tokens[2], out vz))
                return new Vector3(vx, vy, vz);

            return new Vector3(ReadFloatAny(def.x, x), ReadFloatAny(def.y, y), ReadFloatAny(def.z, z));
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
