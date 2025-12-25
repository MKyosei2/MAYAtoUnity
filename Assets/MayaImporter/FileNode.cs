using System;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;

namespace MayaImporter.Shading
{
    [MayaNodeType("file")]
    public sealed class FileNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaTextureMetadata>() ?? gameObject.AddComponent<MayaTextureMetadata>();

            // Maya: fileTextureName
            meta.fileTextureName = ReadString(new[] { ".ftn", "ftn", "fileTextureName", ".fileTextureName" }, meta.fileTextureName);

            // Maya: colorSpace / ignoreColorSpaceFileRules
            meta.colorSpace = ReadString(new[] { ".cs", "cs", "colorSpace", ".colorSpace" }, meta.colorSpace);
            meta.ignoreColorSpaceFileRules = ReadBool(new[] { ".icsfr", "icsfr", "ignoreColorSpaceFileRules", ".ignoreColorSpaceFileRules" }, meta.ignoreColorSpaceFileRules);

            // place2dTexture connection (best effort)
            meta.connectedPlace2dNodeName =
                ResolveIncomingSourceNodeByDstContainsAny(new[]
                {
                    "uvCoord", ".uvCoord",
                    "uvFilterSize", ".uvFilterSize",
                    "coverage", ".coverage",
                    "repeatUV", ".repeatUV",
                    "offset", ".offset"
                });

            // ---- UV copy: place2dTexture -> file's MayaTextureMetadata
            // MaterialResolver ÇÕ connections Ç≈Ç‡íHÇÍÇÈÇ™ÅAUnityë§ÇÃÅuçƒç\íz100%ÅvóvåèÇ∆ÇµÇƒ file ë§Ç…Ç‡ï€éùÇ∑ÇÈÅB
            var scene = MayaBuildContext.CurrentScene;
            if (scene != null && !string.IsNullOrEmpty(meta.connectedPlace2dNodeName))
            {
                if (TryReadPlace2dUv(scene, meta.connectedPlace2dNodeName, out var rep, out var off, out var rot))
                {
                    meta.repeatUV = rep;
                    meta.offsetUV = off;
                    meta.rotateUVDegrees = rot;
                }
            }

            log.Info($"[file] ftn='{meta.fileTextureName}' place2d='{meta.connectedPlace2dNodeName}' uv(rep={meta.repeatUV}, off={meta.offsetUV}, rot={meta.rotateUVDegrees})");
        }

        private string ResolveIncomingSourceNodeByDstContainsAny(string[] dstContainsAny)
        {
            if (Connections == null || dstContainsAny == null || dstContainsAny.Length == 0) return null;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                // upstream -> this node
                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dst = c.DstPlug ?? "";
                if (string.IsNullOrEmpty(dst)) continue;

                for (int k = 0; k < dstContainsAny.Length; k++)
                {
                    var key = dstContainsAny[k];
                    if (string.IsNullOrEmpty(key)) continue;
                    if (!dst.Contains(key, StringComparison.Ordinal)) continue;

                    if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
                    return MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                }
            }

            return null;
        }

        private static bool TryReadPlace2dUv(MayaSceneData scene, string place2dNodeName, out Vector2 repeat, out Vector2 offset, out float rotateUVDeg)
        {
            repeat = Vector2.one;
            offset = Vector2.zero;
            rotateUVDeg = 0f;

            if (scene?.Nodes == null || string.IsNullOrEmpty(place2dNodeName)) return false;

            var rec = FindNodeByAnyName(scene, place2dNodeName);
            if (rec == null) return false;
            if (!string.Equals(rec.NodeType, "place2dTexture", StringComparison.Ordinal)) return false;

            bool gotAny = false;

            if (TryReadFloat2(rec, new[] { "repeatUV", ".repeatUV" }, out var rep2))
            {
                repeat = rep2;
                gotAny = true;
            }
            else
            {
                float ru = 1f, rv = 1f;
                bool gotU = TryReadFloat1(rec, new[] { "repeatU", ".repeatU" }, out ru);
                bool gotV = TryReadFloat1(rec, new[] { "repeatV", ".repeatV" }, out rv);
                if (gotU || gotV)
                {
                    repeat = new Vector2(
                        Mathf.Max(0.0001f, gotU ? ru : 1f),
                        Mathf.Max(0.0001f, gotV ? rv : 1f));
                    gotAny = true;
                }
            }

            if (TryReadFloat2(rec, new[] { "offset", ".offset" }, out var off2))
            {
                offset = off2;
                gotAny = true;
            }
            else
            {
                float ou = 0f, ov = 0f;
                bool gotU = TryReadFloat1(rec, new[] { "offsetU", ".offsetU" }, out ou);
                bool gotV = TryReadFloat1(rec, new[] { "offsetV", ".offsetV" }, out ov);
                if (gotU || gotV)
                {
                    offset = new Vector2(gotU ? ou : 0f, gotV ? ov : 0f);
                    gotAny = true;
                }
            }

            // rotateUV ÇÕ scene attribute ñºóhÇÍÇ™Ç†ÇÈÇÃÇ≈ contains Ç≈èEÇ§
            if (TryReadFloatLooseContains(rec, "rotateUV", out var rot))
            {
                rotateUVDeg = rot;
                gotAny = true;
            }

            return gotAny;
        }

        private static NodeRecord FindNodeByAnyName(MayaSceneData scene, string nameOrLeaf)
        {
            if (scene?.Nodes == null || string.IsNullOrEmpty(nameOrLeaf)) return null;

            if (scene.Nodes.TryGetValue(nameOrLeaf, out var exact))
                return exact;

            var leaf = MayaPlugUtil.LeafName(nameOrLeaf);
            foreach (var kv in scene.Nodes)
            {
                var r = kv.Value;
                if (r == null) continue;
                if (string.Equals(MayaPlugUtil.LeafName(r.Name), leaf, StringComparison.Ordinal))
                    return r;
            }

            return null;
        }

        private static bool TryReadFloat2(NodeRecord rec, string[] keys, out Vector2 v)
        {
            v = default;

            if (rec?.Attributes == null) return false;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!rec.Attributes.TryGetValue(keys[i], out var a) || a?.ValueTokens == null || a.ValueTokens.Count < 2)
                    continue;

                if (TryF(a.ValueTokens[0], out var x) && TryF(a.ValueTokens[1], out var y))
                {
                    v = new Vector2(x, y);
                    return true;
                }
            }
            return false;
        }

        private static bool TryReadFloat1(NodeRecord rec, string[] keys, out float f)
        {
            f = 0f;

            if (rec?.Attributes == null) return false;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!rec.Attributes.TryGetValue(keys[i], out var a) || a?.ValueTokens == null || a.ValueTokens.Count == 0)
                    continue;

                if (TryF(a.ValueTokens[0], out f))
                    return true;
            }

            return false;
        }

        private static bool TryReadFloatLooseContains(NodeRecord rec, string containsKey, out float f)
        {
            f = 0f;
            if (rec?.Attributes == null) return false;

            foreach (var kv in rec.Attributes)
            {
                var k = kv.Key ?? "";
                if (k.IndexOf(containsKey, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var a = kv.Value;
                if (a?.ValueTokens == null || a.ValueTokens.Count == 0) continue;

                if (TryF(a.ValueTokens[0], out f))
                    return true;
            }

            return false;
        }

        private string ReadString(string[] keys, string def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count == 0) continue;
                return a.Tokens[0];
            }
            return def;
        }

        private bool ReadBool(string[] keys, bool def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count == 0) continue;

                var s = (a.Tokens[0] ?? "").Trim().ToLowerInvariant();
                if (s == "1" || s == "true" || s == "yes" || s == "on") return true;
                if (s == "0" || s == "false" || s == "no" || s == "off") return false;
            }
            return def;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
