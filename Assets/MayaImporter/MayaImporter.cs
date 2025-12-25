// MayaImporter/MayaImporter.cs
using System.IO;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Public entry point: .ma/.mb -> SceneData + Unity hierarchy.
    /// Absolute condition:
    /// - No Autodesk/Maya API.
    /// - Unity-only parsing + reconstruction.
    /// </summary>
    public static class MayaImporter
    {
        public static bool IsSupportedFilePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".ma" || ext == ".mb";
        }

        public static MayaSceneData Parse(string path, MayaImportOptions options, out MayaImportLog log)
        {
            if (options == null) options = new MayaImportOptions();
            log = new MayaImportLog();

            if (string.IsNullOrEmpty(path))
            {
                log.Error("Path is null/empty.");
                return new MayaSceneData { SourcePath = path };
            }

            if (!File.Exists(path))
            {
                log.Error("File not found: " + path);
                return new MayaSceneData { SourcePath = path };
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            MayaSceneData scene;

            try
            {
                if (ext == ".ma")
                {
                    var p = new MayaAsciiParser();
                    scene = p.ParseFile(path, options, log);
                }
                else if (ext == ".mb")
                {
                    var p = new MayaBinaryParser();
                    scene = p.ParseFile(path, options, log);
                }
                else
                {
                    log.Error("Unsupported extension: " + ext);
                    return new MayaSceneData { SourcePath = path };
                }

                // Post-process hook
                MayaScenePostProcessor.Apply(scene, log);

                return scene;
            }
            catch (System.Exception ex)
            {
                log.Error($"Parse failed: {ex.GetType().Name}: {ex.Message}");
                return new MayaSceneData { SourcePath = path };
            }
        }

        public static GameObject ImportIntoScene(
            string path,
            MayaImportOptions options,
            out MayaSceneData scene,
            out MayaImportLog log)
        {
            scene = Parse(path, options, out log);

            try
            {
                var builder = new UnitySceneBuilderV2(options, log);
                var root = builder.Build(scene);
                return root;
            }
            catch (System.Exception ex)
            {
                log.Error($"Build failed: {ex.GetType().Name}: {ex.Message}");
                var fb = new GameObject("MayaScene_BuildFailed");
                return fb;
            }
        }

#if UNITY_EDITOR
        // -------------------------
        // Phase1 helper (Project Asset -> absolute file)
        // -------------------------
        public static bool TryGetAbsolutePathFromAssetPath(string assetPath, out string absolutePath)
        {
            absolutePath = "";

            if (string.IsNullOrEmpty(assetPath)) return false;
            assetPath = assetPath.Replace('\\', '/');

            if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (!IsSupportedFilePath(assetPath))
                return false;

            // Project root = parent of Assets
            var projectRoot = System.IO.Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return false;

            absolutePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, assetPath));
            return File.Exists(absolutePath);
        }
#endif
    }
}
