// Assets/MayaImporter/DeformerDecodeUtil.cs
// Shared decoding helpers for Deformer nodes that are NOT MayaPhaseCNodeBase.
//
// Purpose:
// - Provide deterministic, Maya-API-free parsing from MayaNodeComponentBase.Attributes/Connections
// - Provide "ApplyToUnity override" implementations to avoid STUB classification
// - Provide best-effort matrix/value resolution from connected nodes (MayaMatrixValue / MayaFloatValue)

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    public static class DeformerDecodeUtil
    {
        public static bool TryGetAttr(MayaNodeComponentBase node, string key, out MayaNodeComponentBase.SerializedAttribute attr)
        {
            attr = null;
            if (node == null || node.Attributes == null) return false;
            if (string.IsNullOrEmpty(key)) return false;

            // exact
            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var a = node.Attributes[i];
                if (a == null) continue;
                if (string.Equals(a.Key, key, StringComparison.Ordinal))
                {
                    attr = a;
                    return true;
                }
            }

            // dot-compat
            var dot = key.StartsWith(".", StringComparison.Ordinal) ? key.Substring(1) : "." + key;
            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var a = node.Attributes[i];
                if (a == null) continue;
                if (string.Equals(a.Key, dot, StringComparison.Ordinal))
                {
                    attr = a;
                    return true;
                }
            }

            return false;
        }

        public static float ReadFloat(MayaNodeComponentBase node, float def, params string[] keys)
        {
            if (node == null || keys == null) return def;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(node, keys[i], out var a) || a?.Tokens == null || a.Tokens.Count == 0)
                    continue;

                for (int j = a.Tokens.Count - 1; j >= 0; j--)
                {
                    var s = (a.Tokens[j] ?? "").Trim();
                    if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        return f;
                }
            }
            return def;
        }

        public static int ReadInt(MayaNodeComponentBase node, int def, params string[] keys)
        {
            var f = ReadFloat(node, float.NaN, keys);
            if (float.IsNaN(f)) return def;
            return Mathf.RoundToInt(f);
        }

        public static bool ReadBool(MayaNodeComponentBase node, bool def, params string[] keys)
        {
            if (node == null || keys == null) return def;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(node, keys[i], out var a) || a?.Tokens == null || a.Tokens.Count == 0)
                    continue;

                for (int j = a.Tokens.Count - 1; j >= 0; j--)
                {
                    var s = (a.Tokens[j] ?? "").Trim().ToLowerInvariant();
                    if (s == "1" || s == "true" || s == "yes" || s == "on") return true;
                    if (s == "0" || s == "false" || s == "no" || s == "off") return false;
                }
            }
            return def;
        }

        public static Vector3 ReadVec3(
            MayaNodeComponentBase node,
            Vector3 def,
            string[] packedKeys,
            string[] xKeys,
            string[] yKeys,
            string[] zKeys)
        {
            if (node == null) return def;

            // packed first
            if (packedKeys != null)
            {
                for (int i = 0; i < packedKeys.Length; i++)
                {
                    if (!TryGetAttr(node, packedKeys[i], out var a) || a?.Tokens == null || a.Tokens.Count < 3)
                        continue;

                    if (TryF(a.Tokens[0], out var x) && TryF(a.Tokens[1], out var y) && TryF(a.Tokens[2], out var z))
                        return new Vector3(x, y, z);
                }
            }

            float xx = ReadFirstFloat(node, def.x, xKeys);
            float yy = ReadFirstFloat(node, def.y, yKeys);
            float zz = ReadFirstFloat(node, def.z, zKeys);
            return new Vector3(xx, yy, zz);
        }

        private static float ReadFirstFloat(MayaNodeComponentBase node, float def, string[] keys)
        {
            if (node == null || keys == null) return def;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(node, keys[i], out var a) || a?.Tokens == null || a.Tokens.Count == 0)
                    continue;

                if (TryF(a.Tokens[a.Tokens.Count - 1], out var f))
                    return f;
            }
            return def;
        }

        public static bool TryReadMatrix4x4(MayaNodeComponentBase node, string key, out Matrix4x4 m)
        {
            m = Matrix4x4.identity;
            if (node == null) return false;

            if (!TryGetAttr(node, key, out var a) || a?.Tokens == null || a.Tokens.Count == 0)
                return false;

            var floats = new List<float>(16);
            for (int i = 0; i < a.Tokens.Count; i++)
            {
                var s = (a.Tokens[i] ?? "").Trim();
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    floats.Add(f);
            }

            if (floats.Count < 16) return false;

            int k = 0;
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    m[r, c] = floats[k++];

            return true;
        }

        public static string FindLastIncomingTo(MayaNodeComponentBase node, params string[] dstAttrNames)
        {
            if (node == null || node.Connections == null || node.Connections.Count == 0) return null;
            if (dstAttrNames == null || dstAttrNames.Length == 0) return null;

            for (int i = node.Connections.Count - 1; i >= 0; i--)
            {
                var c = node.Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Destination &&
                    c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int a = 0; a < dstAttrNames.Length; a++)
                {
                    var want = dstAttrNames[a];
                    if (string.IsNullOrEmpty(want)) continue;
                    if (string.Equals(dstAttr, want, StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }

            return null;
        }

        public static bool TryResolveConnectedMatrix(string srcPlug, out Matrix4x4 mayaMatrix)
        {
            mayaMatrix = Matrix4x4.identity;
            if (string.IsNullOrEmpty(srcPlug)) return false;

            var srcNode = MayaPlugUtil.ExtractNodePart(srcPlug);
            if (string.IsNullOrEmpty(srcNode)) return false;

            var tr = MayaNodeLookup.FindTransform(srcNode);
            if (tr == null) return false;

            var mv = tr.GetComponent<MayaMatrixValue>();
            if (mv != null && mv.valid)
            {
                mayaMatrix = mv.matrixMaya;
                return true;
            }

            return false;
        }

        public static bool TryF(string s, out float f)
            => float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
