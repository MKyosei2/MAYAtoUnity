// MayaImporter/MayaMaMbImporterOverride.cs
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MayaImporter.Core
{
    [ScriptedImporter(
        3,
        null,
        new[] { "ma", "mb" },
        AllowCaching = true)]
    public sealed class MayaMaMbImporterOverride : ScriptedImporter
    {
        [Header("Import Options (per-asset)")]
        [SerializeField] private MayaImportOptions _options = new MayaImportOptions();

        [Header("Debug / Audit")]
        [SerializeField] private bool _addImportLogTextAsset = true;

        [Tooltip("ImportLog TextAsset の最大文字数（巨大ログでInspectorが重くなるのを防ぐ）")]
        [SerializeField] private int _importLogMaxChars = 200_000;

        [SerializeField] private bool _addCoverageReportTextAsset = true;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            MayaSceneData scene = null;
            MayaImportLog log = null;

            try
            {
                var abs = TryGetAbsolutePath(ctx.assetPath);
                if (string.IsNullOrEmpty(abs) || !File.Exists(abs))
                {
                    var fallback = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
                    ctx.AddObjectToAsset("root", fallback);
                    ctx.SetMainObject(fallback);
                    ctx.LogImportError($"[MayaImporter] Source file not found: {ctx.assetPath}");
                    return;
                }

                var opt = CloneOptions(_options);

                // ★ ポートフォリオ100点寄せ：AnimationClip を sub-asset 化したいので importer 経由で必ずON
                opt.SaveAnimationClip = true;

                var root = MayaImporter.ImportIntoScene(abs, opt, out scene, out log);
                if (root == null)
                {
                    root = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
                    (log ??= new MayaImportLog()).Error("ImportIntoScene returned null root. Fallback GameObject created.");
                }

                // --- Phase B: Materials/Textures apply ---
                MayaMaterialAutoApply.Run_BestEffort(root.transform, scene, opt, log);

                // --- Phase C: Auto Bake Animation + Audit ---
                MayaPhaseCAutoBakeOnImport.Run_BestEffort(root, scene, opt, log);

                // --- Phase D: Unknown upgrade + Coverage proof (完成) ---
                var coverageReport = MayaPhaseDAutoCoverageOnImport.Run_BestEffort(root, scene, opt, log);

                // ScriptedImporter向けのAsset化（Prefab(main) + sub-assets）
                MayaAssetPipeline.AssetizeForImporter(ctx, root, scene, opt, log);

                if (_addImportLogTextAsset)
                {
                    var txt = (log != null) ? log.ToString() : "(no log)";
                    if (_importLogMaxChars > 0 && txt.Length > _importLogMaxChars)
                        txt = txt.Substring(0, _importLogMaxChars) + "\n...(truncated)...";

                    var ta = new TextAsset(txt) { name = "MayaImportLog" };
                    ctx.AddObjectToAsset("importLog", ta);
                }

                if (_addCoverageReportTextAsset && !string.IsNullOrEmpty(coverageReport))
                {
                    var ta = new TextAsset(coverageReport) { name = "MayaCoverageReport" };
                    ctx.AddObjectToAsset("coverageReport", ta);
                }

                if (log != null)
                {
                    for (int i = 0; i < log.Warnings.Count; i++)
                        ctx.LogImportWarning("[MayaImporter] " + log.Warnings[i]);

                    for (int i = 0; i < log.Errors.Count; i++)
                        ctx.LogImportError("[MayaImporter] " + log.Errors[i]);
                }
            }
            catch (Exception ex)
            {
                var fallback = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
                ctx.AddObjectToAsset("root", fallback);
                ctx.SetMainObject(fallback);

                ctx.LogImportError("[MayaImporter] Exception during import. See console for stack trace.");
                Debug.LogException(ex);
            }
        }

        private static string TryGetAbsolutePath(string assetPath)
        {
            if (MayaImporter.TryGetAbsolutePathFromAssetPath(assetPath, out var abs))
                return abs;

            try { return Path.GetFullPath(assetPath); }
            catch { return ""; }
        }

        private static MayaImportOptions CloneOptions(MayaImportOptions src)
        {
            if (src == null) return new MayaImportOptions();
            try
            {
                var json = JsonUtility.ToJson(src);
                var cloned = JsonUtility.FromJson<MayaImportOptions>(json);
                return cloned ?? new MayaImportOptions();
            }
            catch
            {
                return new MayaImportOptions();
            }
        }
    }
}
#endif
