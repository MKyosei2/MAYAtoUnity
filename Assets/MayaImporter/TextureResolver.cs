using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MayaImporter.Shader
{
    /// <summary>
    /// Maya の file / texture ノードから参照されるテクスチャを
    /// Unity 環境で解決するためのユーティリティ。
    ///
    /// ・Maya API 不使用
    /// ・UDIM 対応（<UDIM>, 1001 形式）
    /// ・相対 / 絶対パス対応
    /// ・見つからない場合も「情報保持」する
    /// </summary>
    public static class TextureResolver
    {
        /// <summary>
        /// 解決結果
        /// </summary>
        public sealed class TextureResolveResult
        {
            public string OriginalPath;
            public string ResolvedPath;
            public bool IsUDIM;
            public bool Exists;
            public Texture2D Texture;
        }

        /// <summary>
        /// file ノード属性からテクスチャを解決する
        /// </summary>
        public static TextureResolveResult Resolve(
            Dictionary<string, object> fileNodeAttributes,
            string mayaSceneDirectory)
        {
            var result = new TextureResolveResult();

            if (fileNodeAttributes == null)
                return result;

            if (!fileNodeAttributes.TryGetValue("fileTextureName", out var pathObj))
                return result;

            var rawPath = pathObj as string;
            if (string.IsNullOrEmpty(rawPath))
                return result;

            result.OriginalPath = rawPath;

            // Maya 形式のパスを正規化
            var normalizedPath = NormalizePath(rawPath);

            // 相対パス解決
            if (!Path.IsPathRooted(normalizedPath) &&
                !string.IsNullOrEmpty(mayaSceneDirectory))
            {
                normalizedPath =
                    Path.GetFullPath(Path.Combine(mayaSceneDirectory, normalizedPath));
            }

            // UDIM 判定
            if (IsUDIMPath(normalizedPath))
            {
                result.IsUDIM = true;
                ResolveUDIM(normalizedPath, result);
            }
            else
            {
                ResolveSingleTexture(normalizedPath, result);
            }

            return result;
        }

        #region Single Texture

        private static void ResolveSingleTexture(
            string path,
            TextureResolveResult result)
        {
            result.ResolvedPath = path;
            result.Exists = File.Exists(path);

            if (!result.Exists)
                return;

            result.Texture = LoadTextureFromFile(path);
        }

        #endregion


        #region UDIM

        private static void ResolveUDIM(
            string udimPath,
            TextureResolveResult result)
        {
            var directory = Path.GetDirectoryName(udimPath);
            var fileName = Path.GetFileName(udimPath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            // <UDIM> または 1001 置換
            var searchPattern =
                fileName.Replace("<UDIM>", "*")
                        .Replace("1001", "*");

            var files = Directory.GetFiles(directory, searchPattern);
            if (files == null || files.Length == 0)
                return;

            // とりあえず 1001 を代表として読む（情報保持が主目的）
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var representative = files[0];

            result.ResolvedPath = representative;
            result.Exists = true;
            result.Texture = LoadTextureFromFile(representative);
        }

        private static bool IsUDIMPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return path.Contains("<UDIM>", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("1001");
        }

        #endregion


        #region Texture Loading

        private static Texture2D LoadTextureFromFile(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                tex.LoadImage(bytes);
                tex.name = Path.GetFileNameWithoutExtension(path);
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[TextureResolver] Failed to load texture: {path}\n{e.Message}");
                return null;
            }
        }

        #endregion


        #region Utilities

        private static string NormalizePath(string mayaPath)
        {
            var path = mayaPath.Replace("\\", "/");

            // Maya の env 変数形式（$PROJECT 等）は保持
            return path;
        }

        #endregion
    }
}
