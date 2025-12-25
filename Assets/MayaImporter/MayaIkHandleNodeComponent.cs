using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using MayaImporter.Core;
using UnityEngine;

namespace MayaImporter.IK
{
    [MayaNodeType("ikHandle")]
    [DisallowMultipleComponent]
    public sealed class MayaIkHandleNodeComponent : MayaNodeComponentBase
    {
        private const string AutoPoleName = "[MayaPoleVector]";

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var scene = MayaBuildContext.CurrentScene;
            if (scene == null) return;

            var comp = GetComponent<MayaIkHandleComponent>();
            if (comp == null) comp = gameObject.AddComponent<MayaIkHandleComponent>();

            comp.SolverType = ReadStringAttr((IList)Attributes, ".sol", ".solver", comp.SolverType);
            comp.Twist = ReadFloatAttr((IList)Attributes, ".twist", ".tw", comp.Twist);

            // --- Pole Vector (double3) on ikHandle: ".pv" / ".poleVector" / ".pvx/.pvy/.pvz"
            if (TryReadPoleVectorValue((IList)Attributes, out var pv))
            {
                comp.PoleVectorValue = pv;
                comp.HasPoleVectorValue = true;
            }
            else
            {
                comp.HasPoleVectorValue = false;
            }

            // --- Spline IK parameters (best-effort)
            // rootOnCurve (roc) / offset (ofs)
            comp.SplineRootOnCurve = ReadBoolAttr((IList)Attributes, ".rootOnCurve", ".roc", comp.SplineRootOnCurve);
            comp.SplineOffset = ReadFloatAttr((IList)Attributes, ".offset", ".ofs", comp.SplineOffset);

            // Resolve start/end via connections best-effort
            var (startNode, effNode) = FindIkJoints(scene, NodeName);

            comp.StartJoint = MayaNodeLookup.FindTransform(startNode);
            comp.EndEffector = MayaNodeLookup.FindTransform(effNode);

            // Best-effort end joint
            if (comp.EndEffector != null && comp.EndEffector.parent != null)
                comp.EndJoint = comp.EndEffector.parent;
            else
                comp.EndJoint = GuessEndJointFromHierarchy(comp.StartJoint);

            // --- Spline IK: resolve curve connection if solver is ikSplineSolver (or contains "Spline")
            if (IsSplineSolver(comp.SolverType))
            {
                comp.SplineCurveNode = FindSplineCurveNode(scene, NodeName);
                comp.SplineCurve = MayaNodeLookup.FindTransform(comp.SplineCurveNode);

                if (comp.SplineCurve == null && !string.IsNullOrEmpty(comp.SplineCurveNode))
                {
                    // Sometimes curve is a shape node that still has a GO; try leaf name (best-effort)
                    comp.SplineCurve = MayaNodeLookup.FindTransform(MayaPlugUtil.LeafName(comp.SplineCurveNode));
                }

                if (comp.SplineCurve == null)
                    log?.Warn($"[ikSplineSolver] curve not found for ikHandle '{NodeName}'. curveNode='{MayaPlugUtil.LeafName(comp.SplineCurveNode)}'");
            }

            // Ensure we have a usable pole control:
            // 1) If poleVectorConstraint already assigned PoleVector -> keep it
            // 2) Else if authored poleVector value exists -> create helper under StartJoint
            EnsureAutoPoleVectorIfNeeded(comp, log);

            // Ensure runtime solver exists
            var solver = GetComponent<MayaIkRuntimeSolver>();
            if (solver == null) solver = gameObject.AddComponent<MayaIkRuntimeSolver>();
            solver.Data = comp;
            solver.RebuildChain();

            log?.Info(
                $"ikHandle '{NodeName}': solver='{comp.SolverType}', start='{MayaPlugUtil.LeafName(startNode)}', " +
                $"effector='{MayaPlugUtil.LeafName(effNode)}', endJoint='{(comp.EndJoint != null ? comp.EndJoint.name : "(null)")}', " +
                $"pole='{(comp.PoleVector != null ? comp.PoleVector.name : (comp.HasPoleVectorValue ? "AutoPoleVector" : "(none)"))}', " +
                $"twist={comp.Twist}, splineCurve='{MayaPlugUtil.LeafName(comp.SplineCurveNode)}', rootOnCurve={comp.SplineRootOnCurve}, offset={comp.SplineOffset}");
        }

        private static bool IsSplineSolver(string solverType)
        {
            if (string.IsNullOrEmpty(solverType)) return false;
            return solverType.IndexOf("ikSpline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   solverType.IndexOf("Spline", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void EnsureAutoPoleVectorIfNeeded(MayaIkHandleComponent comp, MayaImportLog log)
        {
            if (comp == null) return;

            // If a poleVectorConstraint already set PoleVector, prefer that.
            if (comp.PoleVector != null) return;

            if (!comp.HasPoleVectorValue) return;
            if (comp.StartJoint == null) return;

            // Create/find helper under StartJoint (assume pole vector is in StartJoint local space; best-effort).
            Transform helper = null;
            for (int i = 0; i < comp.StartJoint.childCount; i++)
            {
                var c = comp.StartJoint.GetChild(i);
                if (c != null && c.name == AutoPoleName)
                {
                    helper = c;
                    break;
                }
            }

            if (helper == null)
            {
                var go = new GameObject(AutoPoleName);
                helper = go.transform;
                helper.SetParent(comp.StartJoint, false);
                go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            helper.localPosition = comp.PoleVectorValue;

            comp.AutoPoleVectorTransform = helper;
            comp.PoleVector = helper;

            log?.Info($"[ikHandle] Auto poleVector helper created under '{comp.StartJoint.name}' local={comp.PoleVectorValue}");
        }

        private static bool TryReadPoleVectorValue(IList attrs, out Vector3 pv)
        {
            pv = Vector3.zero;
            if (attrs == null) return false;

            // Common: ".pv" or ".poleVector" as 3 tokens
            if (TryGetAttribute(attrs, ".pv", out var tokens) || TryGetAttribute(attrs, ".poleVector", out tokens))
            {
                if (tokens != null && tokens.Count >= 3 &&
                    TryF(tokens[0], out var x) &&
                    TryF(tokens[1], out var y) &&
                    TryF(tokens[2], out var z))
                {
                    pv = new Vector3(x, y, z);
                    return true;
                }
            }

            // Split keys: ".pvx .pvy .pvz"
            float fx = 0f, fy = 0f, fz = 0f;
            bool hasX = TryGetAttribute(attrs, ".pvx", out var tx) && tx != null && tx.Count >= 1 && TryF(tx[0], out fx);
            bool hasY = TryGetAttribute(attrs, ".pvy", out var ty) && ty != null && ty.Count >= 1 && TryF(ty[0], out fy);
            bool hasZ = TryGetAttribute(attrs, ".pvz", out var tz) && tz != null && tz.Count >= 1 && TryF(tz[0], out fz);

            if (hasX || hasY || hasZ)
            {
                pv = new Vector3(hasX ? fx : 0f, hasY ? fy : 0f, hasZ ? fz : 0f);
                return true;
            }

            return false;
        }

        private static (string startJoint, string endEffector) FindIkJoints(MayaSceneData scene, string ikHandleName)
        {
            string start = null;
            string end = null;

            for (int i = 0; i < scene.Connections.Count; i++)
            {
                var c = scene.Connections[i];
                if (c == null) continue;

                var dstNode = MayaPlugUtil.ExtractNodePart(c.DstPlug);
                if (!MayaPlugUtil.NodeMatches(dstNode, ikHandleName)) continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";
                var srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);

                if (dstAttr.Contains("startJoint", StringComparison.Ordinal) || dstAttr.Contains(".sj", StringComparison.Ordinal))
                    start = srcNode;

                if (dstAttr.Contains("endEffector", StringComparison.Ordinal) || dstAttr.Contains(".ee", StringComparison.Ordinal) ||
                    dstAttr.Contains("effector", StringComparison.Ordinal))
                    end = srcNode;
            }

            return (start, end);
        }

        private static string FindSplineCurveNode(MayaSceneData scene, string ikHandleName)
        {
            // Incoming connections to ikHandle where dstAttr indicates curve input.
            // Typical: curveShape.worldSpace[0] -> ikHandle.inCurve
            string curve = null;

            for (int i = 0; i < scene.Connections.Count; i++)
            {
                var c = scene.Connections[i];
                if (c == null) continue;

                var dstNode = MayaPlugUtil.ExtractNodePart(c.DstPlug);
                if (!MayaPlugUtil.NodeMatches(dstNode, ikHandleName)) continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";
                if (string.IsNullOrEmpty(dstAttr)) continue;

                bool looks =
                    dstAttr.IndexOf("inCurve", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dstAttr.Equals("ic", StringComparison.OrdinalIgnoreCase) ||
                    dstAttr.IndexOf(".ic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dstAttr.IndexOf("curve", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!looks) continue;

                curve = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                if (!string.IsNullOrEmpty(curve))
                    return curve;
            }

            return curve;
        }

        private static Transform GuessEndJointFromHierarchy(Transform start)
        {
            // fallback: deepest child chain (best-effort)
            if (start == null) return null;
            var t = start;
            while (t != null && t.childCount > 0)
            {
                if (t.childCount == 1)
                {
                    t = t.GetChild(0);
                    continue;
                }
                t = t.GetChild(0);
            }
            return t;
        }

        // ======== Attribute helpers (no dependency on a specific attribute type) ========

        private static string ReadStringAttr(IList attrs, string k1, string k2, string defaultValue)
        {
            if (TryGetAttribute(attrs, k1, out var tokens) || TryGetAttribute(attrs, k2, out tokens))
            {
                if (tokens != null && tokens.Count >= 1)
                    return tokens[0]?.ToString();
            }
            return defaultValue;
        }

        private static float ReadFloatAttr(IList attrs, string k1, string k2, float defaultValue)
        {
            if (TryGetAttribute(attrs, k1, out var tokens) || TryGetAttribute(attrs, k2, out tokens))
            {
                if (tokens != null && tokens.Count >= 1 && TryF(tokens[0], out var f))
                    return f;
            }
            return defaultValue;
        }

        private static bool ReadBoolAttr(IList attrs, string k1, string k2, bool defaultValue)
        {
            if (TryGetAttribute(attrs, k1, out var tokens) || TryGetAttribute(attrs, k2, out tokens))
            {
                if (tokens != null && tokens.Count >= 1)
                {
                    var s = tokens[0]?.ToString()?.Trim()?.ToLowerInvariant();
                    if (s == "1" || s == "true") return true;
                    if (s == "0" || s == "false") return false;
                }
            }
            return defaultValue;
        }

        private static bool TryGetAttribute(IList attrs, string key, out IList tokens)
        {
            tokens = null;
            if (attrs == null) return false;

            for (int i = 0; i < attrs.Count; i++)
            {
                var a = attrs[i];
                if (a == null) continue;

                if (!TryGetKeyAndTokens(a, out var k, out var t)) continue;
                if (!string.Equals(k, key, StringComparison.Ordinal)) continue;

                tokens = t;
                return true;
            }
            return false;
        }

        private static bool TryGetKeyAndTokens(object attrObj, out string key, out IList tokens)
        {
            key = null;
            tokens = null;
            if (attrObj == null) return false;

            var type = attrObj.GetType();

            key = GetStringMember(type, attrObj, "Key") ?? GetStringMember(type, attrObj, "Name");
            if (string.IsNullOrEmpty(key)) return false;

            tokens = GetIListMember(type, attrObj, "Tokens") ?? GetIListMember(type, attrObj, "ValueTokens");
            if (tokens == null) return false;

            return true;
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

        private static bool TryF(object s, out float f)
        {
            f = 0f;
            if (s == null) return false;
            return float.TryParse(s.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);
        }
    }
}
