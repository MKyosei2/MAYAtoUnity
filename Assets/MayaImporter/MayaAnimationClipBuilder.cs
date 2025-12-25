using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Animation
{
    public static class MayaAnimationClipBuilder
    {
        private static readonly Regex RxKtvSingle = new Regex(@"^(?:\.)?ktv\[(\d+)\]$", RegexOptions.Compiled);
        private static readonly Regex RxKtvRange = new Regex(@"^(?:\.)?ktv\[(\d+):(\d+)\]$", RegexOptions.Compiled);

        /// <summary>
        /// animCurveTL/TA/TU 群から legacy AnimationClip を生成（シーン上に存在している前提）
        /// - TryGetAttr(protected) は使わない（Reflectionで属性辞書を読む）
        /// - ConnectionRole 等には依存しない
        /// - Root配下に存在するTransformだけを path 計算してバインドする
        /// </summary>
        public static AnimationClip BuildClipFromAnimCurves(
            IEnumerable<MayaNodeComponentBase> animCurves,
            Transform root,
            MayaImportLog log,
            string clipName = "MayaClip",
            float timeScale = 1.0f)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            var clip = new AnimationClip
            {
                name = clipName,
                legacy = true
            };

            int bound = 0;

            foreach (var curveNode in animCurves)
            {
                if (curveNode == null) continue;

                var nodeType = curveNode.NodeType ?? string.Empty;
                if (nodeType != "animCurveTL" && nodeType != "animCurveTA" && nodeType != "animCurveTU")
                    continue;

                if (curveNode.Connections == null || curveNode.Connections.Count == 0)
                    continue;

                if (!TryBuildCurve_Reflection(curveNode, timeScale, out var curve))
                    continue;

                // connections: MayaNodeComponentBase.SerializedConnection
                for (int ci = 0; ci < curveNode.Connections.Count; ci++)
                {
                    var c = curveNode.Connections[ci];

                    var srcPlug = c.SrcPlug ?? string.Empty;
                    if (!srcPlug.Contains(".output", StringComparison.Ordinal))
                        continue;

                    var dstPlug = c.DstPlug ?? string.Empty;
                    if (!TryMapDstPlugToUnityProperty(dstPlug, out var unityProp))
                        continue;

                    var dstNodeName = !string.IsNullOrEmpty(c.DstNodePart)
                        ? c.DstNodePart
                        : MayaPlugUtil.ExtractNodePart(dstPlug);

                    if (string.IsNullOrEmpty(dstNodeName))
                        continue;

                    var driven = FindTransformByMayaLikeName(dstNodeName);
                    if (driven == null)
                    {
                        log?.Warn($"[Anim] driven Transform not found: '{dstNodeName}'");
                        continue;
                    }

                    var path = ComputeRelativePath(root, driven);
                    if (path == null)
                    {
                        // root配下じゃないならバインド不能（壊さないためスキップ）
                        continue;
                    }

                    clip.SetCurve(path, typeof(Transform), unityProp, curve);
                    bound++;
                }
            }

            log?.Info($"[Anim] AnimationClip built: curvesBound={bound} root='{root.name}'");
            return clip;
        }

        private static bool TryMapDstPlugToUnityProperty(string dstPlug, out string unityProp)
        {
            unityProp = null;
            if (string.IsNullOrEmpty(dstPlug)) return false;

            // translate
            if (dstPlug.Contains(".tx", StringComparison.Ordinal)) { unityProp = "m_LocalPosition.x"; return true; }
            if (dstPlug.Contains(".ty", StringComparison.Ordinal)) { unityProp = "m_LocalPosition.y"; return true; }
            if (dstPlug.Contains(".tz", StringComparison.Ordinal)) { unityProp = "m_LocalPosition.z"; return true; }

            // rotate (Euler degrees, legacy)
            if (dstPlug.Contains(".rx", StringComparison.Ordinal)) { unityProp = "localEulerAnglesRaw.x"; return true; }
            if (dstPlug.Contains(".ry", StringComparison.Ordinal)) { unityProp = "localEulerAnglesRaw.y"; return true; }
            if (dstPlug.Contains(".rz", StringComparison.Ordinal)) { unityProp = "localEulerAnglesRaw.z"; return true; }

            // scale
            if (dstPlug.Contains(".sx", StringComparison.Ordinal)) { unityProp = "m_LocalScale.x"; return true; }
            if (dstPlug.Contains(".sy", StringComparison.Ordinal)) { unityProp = "m_LocalScale.y"; return true; }
            if (dstPlug.Contains(".sz", StringComparison.Ordinal)) { unityProp = "m_LocalScale.z"; return true; }

            return false;
        }

        private static bool TryBuildCurve_Reflection(MayaNodeComponentBase node, float timeScale, out AnimationCurve curve)
        {
            curve = new AnimationCurve();

            var dict = FindAttributeDictionary(node);
            if (dict == null) return false;

            var keyed = new SortedDictionary<int, (float t, float v)>();

            foreach (DictionaryEntry e in dict)
            {
                if (e.Key is not string key) continue;
                if (e.Value is not MayaNodeComponentBase.SerializedAttribute a) continue;
                if (a.Tokens == null || a.Tokens.Count < 2) continue;

                if (TryParseKtvKey(key, out int i0, out int i1))
                {
                    if (i0 == i1)
                    {
                        if (TryF(a.Tokens[0], out var t) && TryF(a.Tokens[1], out var v))
                            keyed[i0] = (t * timeScale, v);
                    }
                    else
                    {
                        int count = (i1 - i0 + 1);
                        int pairCount = Math.Min(count, a.Tokens.Count / 2);

                        for (int k = 0; k < pairCount; k++)
                        {
                            if (!TryF(a.Tokens[k * 2 + 0], out var t)) continue;
                            if (!TryF(a.Tokens[k * 2 + 1], out var v)) continue;
                            keyed[i0 + k] = (t * timeScale, v);
                        }
                    }
                }
            }

            if (keyed.Count == 0) return false;

            foreach (var kv in keyed)
                curve.AddKey(new Keyframe(kv.Value.t, kv.Value.v));

            return curve.length > 0;
        }

        private static bool TryParseKtvKey(string key, out int i0, out int i1)
        {
            i0 = i1 = 0;

            var m = RxKtvSingle.Match(key);
            if (m.Success)
            {
                i0 = i1 = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                return true;
            }

            m = RxKtvRange.Match(key);
            if (m.Success)
            {
                i0 = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                i1 = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                if (i1 < i0) (i0, i1) = (i1, i0);
                return true;
            }

            return false;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);

        private static Transform FindTransformByMayaLikeName(string mayaNodeName)
        {
            var leaf = Leaf(mayaNodeName);

            var all = Resources.FindObjectsOfTypeAll<Transform>();
            Transform best = null;

            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null) continue;
                if (!t.gameObject.scene.IsValid()) continue;

                if (string.Equals(t.name, leaf, StringComparison.Ordinal))
                    return t;

                if (t.name.EndsWith(leaf, StringComparison.Ordinal))
                    best ??= t;
            }

            return best;
        }

        private static string ComputeRelativePath(Transform root, Transform target)
        {
            if (target == root) return "";

            // target が root 配下か検証しつつ逆に辿って path を作る
            var stack = new Stack<string>();
            var t = target;
            while (t != null && t != root)
            {
                stack.Push(t.name);
                t = t.parent;
            }

            if (t != root) return null; // not a child of root

            return string.Join("/", stack.ToArray());
        }

        private static string Leaf(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            int i = name.LastIndexOf('|');
            if (i >= 0 && i < name.Length - 1) return name.Substring(i + 1);

            int j = name.LastIndexOf(':');
            if (j >= 0 && j < name.Length - 1) return name.Substring(j + 1);

            return name;
        }

        private static IDictionary FindAttributeDictionary(MayaNodeComponentBase node)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            for (Type t = node.GetType(); t != null; t = t.BaseType)
            {
                foreach (var fi in t.GetFields(flags))
                {
                    if (!typeof(IDictionary).IsAssignableFrom(fi.FieldType)) continue;
                    if (fi.GetValue(node) is IDictionary dict) return dict;
                }

                foreach (var pi in t.GetProperties(flags))
                {
                    if (!typeof(IDictionary).IsAssignableFrom(pi.PropertyType)) continue;
                    if (!pi.CanRead) continue;
                    try
                    {
                        if (pi.GetValue(node, null) is IDictionary dict) return dict;
                    }
                    catch { }
                }
            }

            return null;
        }
    }
}
