using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;
using MayaImporter.Constraints;

namespace MayaImporter.Animation
{
    [MayaNodeType("parentConstraint")]
    public sealed class ParentConstraintNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            var meta = GetOrAdd<MayaConstraintMetadata>();
            meta.constraintType = "parent";
            meta.maintainOffset = ReadBool(".mo", false);

            meta.targets.Clear();

            meta.interpType = ReadInt(".int", ReadInt(".interpType", 1));
            meta.interpCache = ReadInt(".inc", ReadInt(".interpCache", 0));

            meta.enableRestPosition = ReadBool(".erp", ReadBool(".enableRestPosition", false));
            meta.restTranslate = ReadVec3Any(Vector3.zero,
                ".rst", ".rtx", ".rty", ".rtz",
                "restTranslate", "restTranslateX", "restTranslateY", "restTranslateZ");
            meta.restRotate = ReadVec3Any(Vector3.zero,
                ".rsrr", ".rrx", ".rry", ".rrz",
                "restRotate", "restRotateX", "restRotateY", "restRotateZ");

            meta.useDecompositionTarget = ReadBool(".udt", ReadBool(".useDecompositionTarget", false));
            meta.rotationDecompositionTarget = ReadVec3Any(Vector3.zero,
                ".rdta", ".rdtx", ".rdty", ".rdtz",
                "rotationDecompositionTarget", "rotationDecompositionTargetX", "rotationDecompositionTargetY", "rotationDecompositionTargetZ");

            var offT = ReadVec3(".o", ".ox", ".oy", ".oz", Vector3.zero);
            var offR = ReadVec3(".or", ".orx", ".ory", ".orz", Vector3.zero);

            var indices = CollectTargetIndices();
            int maxIndex = indices.Count > 0 ? indices[indices.Count - 1] : -1;

            // meta.targets を tg index と同じ添字で並べる（穴ありOK）
            if (maxIndex >= 0)
            {
                for (int k = 0; k <= maxIndex; k++)
                    meta.targets.Add(new MayaConstraintMetadata.Target { targetNodeName = null, weight = 0f, offsetScale = Vector3.one });
            }

            for (int ii = 0; ii < indices.Count; ii++)
            {
                int ti = indices[ii];

                var tName =
                    ResolveIncomingSourceNode($".tg[{ti}].targetParentMatrix") ??
                    ResolveIncomingSourceNode($"tg[{ti}].targetParentMatrix") ??
                    ResolveIncomingSourceNode($".target[{ti}].targetParentMatrix") ??
                    ResolveIncomingSourceNode($"target[{ti}].targetParentMatrix") ??
                    $"target_{ti}";

                float w = ReadWeightForIndex(ti, 1f);

                var tOffT = ReadVec3Any(Vector3.zero,
                    $".tg[{ti}].tot", $".tg[{ti}].totx", $".tg[{ti}].toty", $".tg[{ti}].totz",
                    $"target[{ti}].targetOffsetTranslate", $"target[{ti}].targetOffsetTranslateX", $"target[{ti}].targetOffsetTranslateY", $"target[{ti}].targetOffsetTranslateZ");

                var tOffR = ReadVec3Any(Vector3.zero,
                    $".tg[{ti}].tor", $".tg[{ti}].torx", $".tg[{ti}].tory", $".tg[{ti}].torz",
                    $"target[{ti}].targetOffsetRotate", $"target[{ti}].targetOffsetRotateX", $"target[{ti}].targetOffsetRotateY", $"target[{ti}].targetOffsetRotateZ");

                if (ti >= 0 && ti < meta.targets.Count)
                {
                    meta.targets[ti] = new MayaConstraintMetadata.Target
                    {
                        targetNodeName = tName,
                        weight = w,
                        offsetTranslate = tOffT,
                        offsetRotate = tOffR,
                        offsetScale = Vector3.one
                    };
                }
            }

            var constrainedName = ResolveConstrainedNodeName();
            var constrainedTf = MayaNodeLookup.FindTransform(constrainedName);

            if (constrainedTf == null)
            {
                log?.Warn($"[parentConstraint] constrained not found: '{constrainedName}'");
                return;
            }

            InferDrivenAxes(constrainedName, out meta.drivePosX, out meta.drivePosY, out meta.drivePosZ, "translate");
            InferDrivenAxes(constrainedName, out meta.driveRotX, out meta.driveRotY, out meta.driveRotZ, "rotate");

            var driver = GetOrAdd<MayaConstraintDriver>();
            driver.Constrained = constrainedTf;
            driver.Kind = MayaConstraintKind.Parent;
            driver.DriveLocalChannels = true;

            driver.MaintainOffset = meta.maintainOffset;
            driver.Priority = 0;

            driver.DrivePosX = meta.drivePosX; driver.DrivePosY = meta.drivePosY; driver.DrivePosZ = meta.drivePosZ;
            driver.DriveRotX = meta.driveRotX; driver.DriveRotY = meta.driveRotY; driver.DriveRotZ = meta.driveRotZ;

            driver.EnableRestPosition = meta.enableRestPosition;
            driver.RestTranslateWorld = meta.restTranslate;
            driver.RestRotateWorldEuler = meta.restRotate;

            driver.RotationInterpType = meta.interpType;
            driver.RotationInterpCache = meta.interpCache;

            driver.OffsetTranslate = offT;
            driver.OffsetRotateEuler = offR;
            driver.OffsetScale = Vector3.one;

            // ★重要: Targets を tg index と同じ添字で並べる（穴埋め）
            driver.Targets.Clear();
            if (maxIndex >= 0)
            {
                for (int k = 0; k <= maxIndex; k++)
                {
                    driver.Targets.Add(new MayaConstraintDriver.Target
                    {
                        Transform = null,
                        Weight = 0f,
                        Offset = Matrix4x4.identity,
                        OffsetAuthored = false,
                        ScaleOffset = Vector3.one,
                        ScaleOffsetAuthored = false
                    });
                }

                for (int ti = 0; ti <= maxIndex; ti++)
                {
                    if (ti < 0 || ti >= meta.targets.Count) continue;
                    var t = meta.targets[ti];
                    if (string.IsNullOrEmpty(t.targetNodeName)) continue;

                    var tf = MayaNodeLookup.FindTransform(t.targetNodeName);

                    bool authored =
                        Mathf.Abs(t.offsetTranslate.x) > 1e-6f ||
                        Mathf.Abs(t.offsetTranslate.y) > 1e-6f ||
                        Mathf.Abs(t.offsetTranslate.z) > 1e-6f ||
                        Mathf.Abs(t.offsetRotate.x) > 1e-6f ||
                        Mathf.Abs(t.offsetRotate.y) > 1e-6f ||
                        Mathf.Abs(t.offsetRotate.z) > 1e-6f;

                    var m = Matrix4x4.TRS(t.offsetTranslate, Quaternion.Euler(t.offsetRotate), Vector3.one);

                    driver.Targets[ti].Transform = tf;
                    driver.Targets[ti].Weight = Mathf.Max(0f, t.weight);
                    driver.Targets[ti].Offset = m;
                    driver.Targets[ti].OffsetAuthored = authored;
                }
            }

            driver.ForceReinitializeOffsets();
            log?.Info($"[parentConstraint] constrained='{MayaPlugUtil.LeafName(constrainedName)}' targetsIndexMax={maxIndex} int={meta.interpType} mo={driver.MaintainOffset}");
        }

        private string ResolveConstrainedNodeName()
        {
            if (Connections == null || Connections.Count == 0) return null;

            string best = null;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Source &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                if (string.IsNullOrEmpty(c.DstPlug)) continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";
                bool looksLikeConstrained =
                    dstAttr.StartsWith("translate", System.StringComparison.Ordinal) ||
                    dstAttr.StartsWith("rotate", System.StringComparison.Ordinal) ||
                    dstAttr == "t" || dstAttr == "r" ||
                    dstAttr == "tx" || dstAttr == "ty" || dstAttr == "tz" ||
                    dstAttr == "rx" || dstAttr == "ry" || dstAttr == "rz";

                if (looksLikeConstrained)
                    return c.DstNodePart;

                best ??= c.DstNodePart;
            }

            return best;
        }

        private string ResolveIncomingSourceNode(string dstPlugSuffix)
        {
            if (Connections == null || Connections.Count == 0) return null;

            var suf = NormalizeSuffix(dstPlugSuffix);

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                if (string.IsNullOrEmpty(c.DstPlug)) continue;

                if (EndsWithSuffixCompat(c.DstPlug, suf))
                {
                    if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
                    return MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                }
            }

            return null;
        }

        private void InferDrivenAxes(string constrainedNodeName, out bool x, out bool y, out bool z, string channel)
        {
            x = y = z = true;

            if (Connections == null || Connections.Count == 0 || string.IsNullOrEmpty(constrainedNodeName))
                return;

            bool anyAxis = false;
            bool full = false;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Source &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                if (!string.Equals(c.DstNodePart, constrainedNodeName, System.StringComparison.Ordinal))
                    continue;

                var attr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";

                if (channel == "translate")
                {
                    if (attr == "translate" || attr == "t") full = true;
                    if (attr == "translateX" || attr == "tx") anyAxis = true;
                    if (attr == "translateY" || attr == "ty") anyAxis = true;
                    if (attr == "translateZ" || attr == "tz") anyAxis = true;
                }
                else if (channel == "rotate")
                {
                    if (attr == "rotate" || attr == "r") full = true;
                    if (attr == "rotateX" || attr == "rx") anyAxis = true;
                    if (attr == "rotateY" || attr == "ry") anyAxis = true;
                    if (attr == "rotateZ" || attr == "rz") anyAxis = true;
                }
            }

            if (full || !anyAxis)
            {
                x = y = z = true;
                return;
            }

            bool px = false, py = false, pz = false;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;
                if (c.RoleForThisNode != ConnectionRole.Source && c.RoleForThisNode != ConnectionRole.Both) continue;
                if (!string.Equals(c.DstNodePart, constrainedNodeName, System.StringComparison.Ordinal)) continue;

                var attr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";

                if (channel == "translate")
                {
                    if (attr == "translateX" || attr == "tx") px = true;
                    if (attr == "translateY" || attr == "ty") py = true;
                    if (attr == "translateZ" || attr == "tz") pz = true;
                }
                else if (channel == "rotate")
                {
                    if (attr == "rotateX" || attr == "rx") px = true;
                    if (attr == "rotateY" || attr == "ry") py = true;
                    if (attr == "rotateZ" || attr == "rz") pz = true;
                }
            }

            x = px; y = py; z = pz;
        }

        private List<int> CollectTargetIndices()
        {
            var set = new HashSet<int>();

            if (Attributes != null)
            {
                for (int i = 0; i < Attributes.Count; i++)
                {
                    var a = Attributes[i];
                    if (a == null || string.IsNullOrEmpty(a.Key)) continue;
                    TryCollectIndexFromString(a.Key, set);
                }
            }

            if (Connections != null)
            {
                for (int i = 0; i < Connections.Count; i++)
                {
                    var c = Connections[i];
                    if (c == null) continue;
                    if (!string.IsNullOrEmpty(c.DstPlug)) TryCollectIndexFromString(c.DstPlug, set);
                }
            }

            var list = new List<int>(set);
            list.Sort();
            return list;
        }

        private static void TryCollectIndexFromString(string s, HashSet<int> set)
        {
            if (string.IsNullOrEmpty(s) || set == null) return;

            if (TryExtractBracketIndex(s, ".tg[", out var i)) set.Add(i);
            if (TryExtractBracketIndex(s, "tg[", out i)) set.Add(i);
            if (TryExtractBracketIndex(s, ".target[", out i)) set.Add(i);
            if (TryExtractBracketIndex(s, "target[", out i)) set.Add(i);
        }

        private static bool TryExtractBracketIndex(string s, string marker, out int idx)
        {
            idx = -1;
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(marker)) return false;

            int p = s.IndexOf(marker, System.StringComparison.Ordinal);
            if (p < 0) return false;

            p += marker.Length;
            int r = s.IndexOf(']', p);
            if (r < 0 || r <= p) return false;

            var inner = s.Substring(p, r - p);
            if (!int.TryParse(inner, out idx)) return false;
            return idx >= 0;
        }

        private float ReadWeightForIndex(int index, float def)
        {
            if (TryGetAttr($".tg[{index}].tw", out var a)) return ReadFloatFromAttr(a, def);
            if (TryGetAttr($"tg[{index}].tw", out a)) return ReadFloatFromAttr(a, def);

            if (TryGetAttr($"target[{index}].targetWeight", out a)) return ReadFloatFromAttr(a, def);
            if (TryGetAttr($".target[{index}].targetWeight", out a)) return ReadFloatFromAttr(a, def);

            if (TryGetAttr($"w{index}", out a)) return ReadFloatFromAttr(a, def);
            if (TryGetAttr($".w{index}", out a)) return ReadFloatFromAttr(a, def);

            return def;
        }

        private Vector3 ReadVec3(string packedOrX, string x, string y, string z, Vector3 def)
        {
            if (TryGetAttr(packedOrX, out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                TryF(a.Tokens[0], out var vx) &&
                TryF(a.Tokens[1], out var vy) &&
                TryF(a.Tokens[2], out var vz))
                return new Vector3(vx, vy, vz);

            var sx = ReadFloat(x, def.x);
            var sy = ReadFloat(y, def.y);
            var sz = ReadFloat(z, def.z);
            return new Vector3(sx, sy, sz);
        }

        private Vector3 ReadVec3Any(Vector3 def,
            string packedA, string ax, string ay, string az,
            string packedB, string bx, string by, string bz)
        {
            if (TryGetAttr(packedA, out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                TryF(a.Tokens[0], out var vx) && TryF(a.Tokens[1], out var vy) && TryF(a.Tokens[2], out var vz))
                return new Vector3(vx, vy, vz);

            if (TryGetAttr(packedB, out var b) && b.Tokens != null && b.Tokens.Count >= 3 &&
                TryF(b.Tokens[0], out vx) && TryF(b.Tokens[1], out vy) && TryF(b.Tokens[2], out vz))
                return new Vector3(vx, vy, vz);

            float x = ReadFloat(ax, float.NaN);
            float y = ReadFloat(ay, float.NaN);
            float z = ReadFloat(az, float.NaN);
            if (float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z))
                return new Vector3(x, y, z);

            x = ReadFloat(bx, def.x);
            y = ReadFloat(by, def.y);
            z = ReadFloat(bz, def.z);
            return new Vector3(x, y, z);
        }

        private int ReadInt(string key, int def)
        {
            if (!TryGetAttr(key, out var a) || a.Tokens == null || a.Tokens.Count == 0) return def;
            if (a.Tokens[0] == null) return def;
            return int.TryParse(a.Tokens[0].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        private float ReadFloat(string key, float def)
        {
            if (!TryGetAttr(key, out var a) || a.Tokens == null || a.Tokens.Count == 0) return def;
            if (a.Tokens[0] == null) return def;
            return float.TryParse(a.Tokens[0].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        private bool ReadBool(string key, bool def)
        {
            if (!TryGetAttr(key, out var a) || a.Tokens == null || a.Tokens.Count == 0) return def;
            if (a.Tokens[0] == null) return def;

            var s = a.Tokens[0].ToString();
            if (s == "1" || string.Equals(s, "true", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (s == "0" || string.Equals(s, "false", System.StringComparison.OrdinalIgnoreCase)) return false;
            return def;
        }

        private static bool TryF(object o, out float v)
        {
            v = 0f;
            if (o == null) return false;
            return float.TryParse(o.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        private static float ReadFloatFromAttr(SerializedAttribute a, float def)
        {
            if (a == null || a.Tokens == null || a.Tokens.Count == 0 || a.Tokens[0] == null) return def;
            return float.TryParse(a.Tokens[0].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        private static string NormalizeSuffix(string s)
            => string.IsNullOrEmpty(s) ? s : (s.StartsWith(".", System.StringComparison.Ordinal) ? s : "." + s);

        private static bool EndsWithSuffixCompat(string plug, string suffixWithDot)
        {
            if (plug.EndsWith(suffixWithDot, System.StringComparison.Ordinal)) return true;
            var noDot = suffixWithDot.StartsWith(".", System.StringComparison.Ordinal) ? suffixWithDot.Substring(1) : suffixWithDot;
            return plug.EndsWith(noDot, System.StringComparison.Ordinal);
        }

        private T GetOrAdd<T>() where T : Component
            => GetComponent<T>() ?? gameObject.AddComponent<T>();
    }
}
