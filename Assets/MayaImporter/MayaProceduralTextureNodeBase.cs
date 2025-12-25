// File: Assets/MayaImporter/MayaProceduralTextureNodeBase.cs
using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;
using MayaImporter.Shader;

namespace MayaImporter.Shading
{
    /// <summary>
    /// procedural/utility texture ノード共通基底。
    /// - Maya無しでAttributes/Connectionsから decode
    /// - place2dTexture 接続があれば UV をコピー
    /// - bakeしたPNGを MayaTextureMetadata.fileTextureName に入れて、既存MaterialResolverが拾える形にする
    /// </summary>
    public abstract class MayaProceduralTextureNodeBase : MayaNodeComponentBase
    {
        [Header("Bake")]
        public int bakeWidth = 256;
        public int bakeHeight = 256;

        [Header("Connected (best-effort)")]
        public string connectedPlace2dNodeName;
        public string connectedInputTextureNodeName;

        // ★重要：基底の ApplyToUnity は public override に統一（MayaNodeComponentBase が public のため）
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            // 派生が public override で実装する前提
        }

        protected MayaProceduralTextureMetadata EnsureProcMeta()
        {
            var m = GetComponent<MayaProceduralTextureMetadata>();
            if (m == null) m = gameObject.AddComponent<MayaProceduralTextureMetadata>();
            m.mayaNodeType = NodeType;
            m.mayaNodeName = NodeName;
            m.mayaNodeUUID = Uuid;
            m.bakedWidth = Mathf.Max(2, bakeWidth);
            m.bakedHeight = Mathf.Max(2, bakeHeight);
            return m;
        }

        protected MayaTextureMetadata EnsureTexMeta()
        {
            var m = GetComponent<MayaTextureMetadata>();
            if (m == null) m = gameObject.AddComponent<MayaTextureMetadata>();
            return m;
        }

        protected void CopyUvFromPlace2dIfAny(MayaImportLog log, MayaProceduralTextureMetadata proc, MayaTextureMetadata tex)
        {
            connectedPlace2dNodeName = FindIncomingSourceNodeByDstContainsAny(new[]
            {
                "uvCoord", ".uvCoord",
                "uvFilterSize", ".uvFilterSize",
                "coverage", ".coverage",
                "repeatUV", ".repeatUV",
                "offset", ".offset"
            });

            proc.connectedPlace2dNodeName = connectedPlace2dNodeName;
            tex.connectedPlace2dNodeName = connectedPlace2dNodeName;

            if (string.IsNullOrEmpty(connectedPlace2dNodeName))
                return;

            var tr = MayaNodeLookup.FindTransform(connectedPlace2dNodeName);
            if (tr == null) return;

            var placeMeta = tr.GetComponent<MayaTextureMetadata>();
            if (placeMeta == null) return;

            proc.repeatUV = placeMeta.repeatUV;
            proc.offsetUV = placeMeta.offsetUV;
            proc.rotateUVDegrees = placeMeta.rotateUVDegrees;

            tex.repeatUV = placeMeta.repeatUV;
            tex.offsetUV = placeMeta.offsetUV;
            tex.rotateUVDegrees = placeMeta.rotateUVDegrees;

            log?.Info($"[{NodeType}] UV copied from place2dTexture='{connectedPlace2dNodeName}' rep={tex.repeatUV} off={tex.offsetUV} rot={tex.rotateUVDegrees}");
        }

        protected string FindIncomingSourceNodeByDstContainsAny(string[] dstContainsAny)
        {
            if (Connections == null || dstContainsAny == null || dstContainsAny.Length == 0) return null;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

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

        protected string FindIncomingSourceNodeByDstAttrEqualsAny(params string[] dstAttrNames)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (dstAttrNames == null || dstAttrNames.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int a = 0; a < dstAttrNames.Length; a++)
                {
                    var want = dstAttrNames[a];
                    if (string.IsNullOrEmpty(want)) continue;
                    if (string.Equals(dstAttr, want, StringComparison.Ordinal))
                    {
                        if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
                        return MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                    }
                }
            }

            return null;
        }

        protected bool TryLoadSourceTextureFromNodeName(string nodeNameOrLeaf, out Texture2D tex, out string resolvedPathHint)
        {
            tex = null;
            resolvedPathHint = null;
            if (string.IsNullOrEmpty(nodeNameOrLeaf)) return false;

            var tr = MayaNodeLookup.FindTransform(nodeNameOrLeaf);
            if (tr == null) return false;

            // 1) If it already has MayaTextureMetadata with baked path
            var texMeta = tr.GetComponent<MayaTextureMetadata>();
            if (texMeta != null && !string.IsNullOrEmpty(texMeta.fileTextureName))
            {
                var p = texMeta.fileTextureName.Trim().Trim('"');
                resolvedPathHint = p;
                tex = MayaProceduralTextureBaker.LoadPngOrNull(p);
                if (tex != null) return true;

                // if it's a real file texture path, try load as image too
                if (File.Exists(p))
                {
                    tex = LoadImageFile(p);
                    return tex != null;
                }
            }

            // 2) Fallback: read raw attrs from MayaNodeComponentBase (e.g. fileTextureName)
            var node = tr.GetComponent<MayaNodeComponentBase>();
            if (node != null)
            {
                var rawPath = ReadStringFrom(node, new[] { ".ftn", "ftn", "fileTextureName", ".fileTextureName" }, "");
                if (!string.IsNullOrEmpty(rawPath))
                {
                    rawPath = rawPath.Trim().Trim('"');

                    var scenePath = MayaBuildContext.CurrentScene != null ? MayaBuildContext.CurrentScene.SourcePath : null;
                    var sceneDir = !string.IsNullOrEmpty(scenePath) ? Path.GetDirectoryName(scenePath) : null;

                    var p = rawPath;
                    if (!Path.IsPathRooted(p) && !string.IsNullOrEmpty(sceneDir))
                        p = Path.GetFullPath(Path.Combine(sceneDir, p));

                    resolvedPathHint = p;
                    if (File.Exists(p))
                    {
                        tex = LoadImageFile(p);
                        return tex != null;
                    }
                }
            }

            return false;
        }

        private static Texture2D LoadImageFile(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
                if (!t.LoadImage(bytes)) return null;
                t.name = Path.GetFileNameWithoutExtension(path);
                return t;
            }
            catch
            {
                return null;
            }
        }

        protected void BakeToTextureMeta(MayaImportLog log, Func<Texture2D> bakeFunc, string bakeLabel)
        {
            var proc = EnsureProcMeta();
            var tex = EnsureTexMeta();

            connectedInputTextureNodeName = FindIncomingSourceNodeByDstAttrEqualsAny(
                "input", "in", "i",
                "inColor", "color", "c",
                "value", "v"
            );
            proc.inputTextureNodeName = connectedInputTextureNodeName;

            CopyUvFromPlace2dIfAny(log, proc, tex);

            var w = Mathf.Max(2, proc.bakedWidth);
            var h = Mathf.Max(2, proc.bakedHeight);

            var outPath = MayaProceduralTextureBaker.BuildCachePath(NodeType, NodeName, Uuid, w, h);

            Texture2D outTex = null;
            try
            {
                outTex = bakeFunc != null ? bakeFunc() : null;
            }
            catch (Exception e)
            {
                proc.notes = $"{bakeLabel} bake failed: {e.GetType().Name}: {e.Message}";
            }

            if (outTex != null)
            {
                if (MayaProceduralTextureBaker.SaveAsPng(outTex, outPath))
                {
                    proc.bakedTexturePath = outPath;
                    tex.fileTextureName = outPath;
                    tex.colorSpace = "sRGB";
                    tex.ignoreColorSpaceFileRules = true;

                    proc.notes = $"{bakeLabel} baked => {outPath}";
                    log?.Info($"[{NodeType}] baked texture => {outPath}");
                }
                else
                {
                    proc.notes = $"{bakeLabel} bake produced texture but failed to write png: {outPath}";
                }
            }
            else
            {
                proc.notes = $"{bakeLabel} bake returned null (kept as metadata-only)";
            }
        }

        // -------------------------
        // Raw attr read helpers
        // -------------------------

        protected float ReadFloat(string[] keys, float def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count == 0) continue;
                for (int j = a.Tokens.Count - 1; j >= 0; j--)
                {
                    if (TryF(a.Tokens[j], out var f)) return f;
                }
            }
            return def;
        }

        protected int ReadInt(string[] keys, int def)
        {
            var f = ReadFloat(keys, float.NaN);
            if (float.IsNaN(f)) return def;
            return Mathf.RoundToInt(f);
        }

        protected Color ReadColor3(string[] keys, Color def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count < 3) continue;
                if (TryF(a.Tokens[0], out var r) && TryF(a.Tokens[1], out var g) && TryF(a.Tokens[2], out var b))
                    return new Color(r, g, b, def.a);
            }
            return def;
        }

        protected static string ReadStringFrom(MayaNodeComponentBase node, string[] keys, string def)
        {
            if (node == null || keys == null) return def;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttrFrom(node, keys[i], out var a) || a.Tokens == null || a.Tokens.Count == 0) continue;
                var s = (a.Tokens[a.Tokens.Count - 1] ?? "").Trim();
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return def;
        }

        private static bool TryGetAttrFrom(MayaNodeComponentBase node, string key, out MayaNodeComponentBase.SerializedAttribute attr)
        {
            attr = null;
            if (node == null || node.Attributes == null || string.IsNullOrEmpty(key)) return false;

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

        private static bool TryF(string s, out float f)
            => float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
