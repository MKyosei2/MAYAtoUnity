using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using MayaImporter.Core;
using UnityEngine;

namespace MayaImporter.Constraints
{
    /// <summary>
    /// Generic constraint node handler:
    /// - Finds driven transform by connections: constraint -> driven.translate/rotate
    /// - Finds target transforms by connections: target -> constraint (targetParentMatrix / tpm / target / tg)
    /// - Creates MayaConstraintDriver on the driven object (Unity-side reconstruction)
    ///
    /// Phase-1: best-effort, preserves connection topology and basic behavior.
    /// </summary>
    public abstract class MayaConstraintNodeComponent : MayaNodeComponentBase
    {
        protected abstract MayaConstraintKind Kind { get; }

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var scene = MayaBuildContext.CurrentScene;
            if (scene == null) return;

            var drivenName = FindDrivenNode(scene, NodeName);
            if (string.IsNullOrEmpty(drivenName))
            {
                log?.Warn($"{Kind}Constraint '{NodeName}': driven not found by connections.");
                return;
            }

            var targetNames = FindTargets(scene, NodeName);

            var drivenGo = FindGameObjectByLeaf(drivenName);
            if (drivenGo == null)
            {
                log?.Warn($"{Kind}Constraint '{NodeName}': driven GameObject not found '{drivenName}'.");
                return;
            }

            var drv = drivenGo.GetComponent<MayaConstraintDriver>();
            if (drv == null) drv = drivenGo.AddComponent<MayaConstraintDriver>();

            drv.Kind = Kind;
            drv.Targets.Clear();

            for (int i = 0; i < targetNames.Count; i++)
            {
                var tGo = FindGameObjectByLeaf(targetNames[i]);
                if (tGo == null) continue;

                drv.Targets.Add(new MayaConstraintDriver.Target
                {
                    Transform = tGo.transform,
                    Weight = 1f
                });
            }

            // Aim: optional parse basic aimAxis/upAxis from node attrs if present
            if (Kind == MayaConstraintKind.Aim)
            {
                drv.AimAxis = ReadVector3Attr((IList)Attributes, ".a", ".ax", ".ay", ".az", Vector3.forward);
                drv.UpAxis = ReadVector3Attr((IList)Attributes, ".u", ".ux", ".uy", ".uz", Vector3.up);
            }

            log?.Info($"{Kind}Constraint '{NodeName}': driven='{MayaPlugUtil.LeafName(drivenName)}', targets={drv.Targets.Count}.");
        }

        private static string FindDrivenNode(MayaSceneData scene, string constraintNodeName)
        {
            // Look for constraint -> driven translate/rotate connections
            for (int i = 0; i < scene.Connections.Count; i++)
            {
                var c = scene.Connections[i];
                if (c == null) continue;

                var srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                if (!MayaPlugUtil.NodeMatches(srcNode, constraintNodeName)) continue;

                var dstNode = MayaPlugUtil.ExtractNodePart(c.DstPlug);
                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";

                // driven transform typically gets translate/rotate from constraint
                if (dstAttr.Contains("translate", StringComparison.Ordinal) ||
                    dstAttr.Contains(".t", StringComparison.Ordinal) ||
                    dstAttr.Contains("rotate", StringComparison.Ordinal) ||
                    dstAttr.Contains(".r", StringComparison.Ordinal))
                {
                    return dstNode;
                }
            }
            return null;
        }

        private static List<string> FindTargets(MayaSceneData scene, string constraintNodeName)
        {
            var targets = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < scene.Connections.Count; i++)
            {
                var c = scene.Connections[i];
                if (c == null) continue;

                var dstNode = MayaPlugUtil.ExtractNodePart(c.DstPlug);
                if (!MayaPlugUtil.NodeMatches(dstNode, constraintNodeName)) continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";

                // Typical incoming from target transform:
                // - targetParentMatrix / tpm
                // - target / tg[...] buckets
                if (!(dstAttr.Contains("targetParentMatrix", StringComparison.Ordinal) ||
                      dstAttr.Contains(".tpm", StringComparison.Ordinal) ||
                      dstAttr.Contains("target[", StringComparison.Ordinal) ||
                      dstAttr.Contains("tg[", StringComparison.Ordinal) ||
                      dstAttr.Contains("targetMatrix", StringComparison.Ordinal)))
                    continue;

                var srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                if (string.IsNullOrEmpty(srcNode)) continue;

                var leaf = MayaPlugUtil.LeafName(srcNode);
                if (seen.Add(leaf))
                    targets.Add(srcNode);
            }

            return targets;
        }

        // ======== Attribute helpers (no dependency on a specific attribute type) ========

        private static Vector3 ReadVector3Attr(IList attrs, string packed, string x, string y, string z, Vector3 defaultValue)
        {
            // packed
            if (TryGetAttribute(attrs, packed, out var tokens) && tokens != null && tokens.Count >= 3)
            {
                if (TryF(tokens[0], out var fx) && TryF(tokens[1], out var fy) && TryF(tokens[2], out var fz))
                    return new Vector3(fx, fy, fz);
            }

            float rx = FindFloat(attrs, x, defaultValue.x);
            float ry = FindFloat(attrs, y, defaultValue.y);
            float rz = FindFloat(attrs, z, defaultValue.z);
            return new Vector3(rx, ry, rz);
        }

        private static float FindFloat(IList attrs, string key, float def)
        {
            if (TryGetAttribute(attrs, key, out var tokens) && tokens != null && tokens.Count >= 1)
            {
                if (TryF(tokens[0], out var f)) return f;
            }
            return def;
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

            // Key: property or field named "Key" (fallback "Name")
            key = GetStringMember(type, attrObj, "Key") ?? GetStringMember(type, attrObj, "Name");
            if (string.IsNullOrEmpty(key)) return false;

            // Tokens: property or field named "Tokens" (fallback "ValueTokens")
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

        private static GameObject FindGameObjectByLeaf(string nodeNameOrDag)
        {
            var leaf = MayaPlugUtil.LeafName(nodeNameOrDag);

#if UNITY_2023_1_OR_NEWER
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && string.Equals(all[i].name, leaf, StringComparison.Ordinal))
                    return all[i];
#else
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && string.Equals(all[i].name, leaf, StringComparison.Ordinal))
                    return all[i];
#endif
            return null;
        }
    }
}
