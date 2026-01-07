// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
// MayaImporter/MayaMaMbImporterOverride.cs
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using MayaImporter.Animation;

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

        [Header("Reconstruction Selection (per-asset)")]
        [SerializeField] private bool _useReconstructionSelection = false;
        [SerializeField] private MayaReconstructionSelection _reconstruction = new MayaReconstructionSelection();

        [Header("Debug / Audit")]
        [SerializeField] private bool _addImportLogTextAsset = true;

        [Tooltip("ON: ImportLog の warnings/errors を Unity Console にも出す (既定はOFFでコンソールを綺麗にする)")]
        [SerializeField] private bool _emitLogWarningsToConsole = false;

        [Tooltip("ON: ImportLog の errors を Unity Console にも出す (既定はOFFでコンソールを綺麗にする)")]
        [SerializeField] private bool _emitLogErrorsToConsole = false;

        [Tooltip("ON: 例外発生時に Debug.LogException を出す (既定はOFF: Console を綺麗にする)")]
        [SerializeField] private bool _emitExceptionToConsole = false;

        [Tooltip("ImportLog TextAsset が巨大でInspectorが重くなるのを防ぐ")]
        [SerializeField] private int _importLogMaxChars = 200_000;

        [SerializeField] private bool _addCoverageReportTextAsset = true;

        [Header("Phase 6: Verification / Determinism")]
        [SerializeField] private bool _addPhase6VerificationReportTextAsset = true;

        [Tooltip("VerificationReport TextAsset が巨大になるのを防ぐ")]
        [SerializeField] private int _phase6ReportMaxChars = 200_000;

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

                // 100%保持の根幹：Raw statements は常に保持
                opt.KeepRawStatements = true;

                // 選別（再構築/保存の制御）
                MayaReconstructionSelection sel = null;
                if (_useReconstructionSelection && _reconstruction != null && _reconstruction.Enabled)
                    sel = _reconstruction;

                if (sel != null)
                {
                    opt.SaveMeshes = sel.ImportMeshes;
                    opt.SaveMaterials = sel.ImportMaterials;
                    opt.SaveTextures = sel.ImportTextures;
                    opt.SaveAnimationClip = sel.ImportAnimationClip;
                }
                else
                {
                    // 既定はポートフォリオ向けにClip ON（プレビューでOFFにできる）
                    opt.SaveAnimationClip = true;
                }

                GameObject root;
                using (MayaReconstructionSelectionContext.Push(sel))
                {
                    root = MayaImporter.ImportIntoScene(abs, opt, out scene, out log);
                }

                if (root == null)
                {
                    root = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
                    (log ??= new MayaImportLog()).Error("ImportIntoScene returned null root. Fallback GameObject created.");
                }

                // --- Phase B: Materials/Textures apply ---
                if (opt.SaveMaterials || opt.SaveTextures)
                    MayaMaterialAutoApply.Run_BestEffort(root.transform, scene, opt, log);
                else
                    (log ??= new MayaImportLog()).Info("Phase B material auto-apply skipped (materials/textures disabled).");

                // --- Phase 4: .mb mesh finalize (ensure renderer/materials even when shading is unknown) ---
                MayaPhase4AutoFinalizeMbMeshesOnImport.Run_BestEffort(root.transform, scene, opt, log);

                // --- Phase 5: Post-sample solvers hook (Expressions -> Constraints -> IK) ---
                // This does not require Maya/Autodesk API. It only wires Unity-side evaluation.
                MayaRuntimePostSampleSolvers.EnsureOnRoot(root);

                // --- Phase C: Auto Bake Animation + Audit ---
                if (opt.SaveAnimationClip)
                    MayaPhaseCAutoBakeOnImport.Run_BestEffort(root, scene, opt, log);
                else
                    (log ??= new MayaImportLog()).Info("Phase C auto-bake skipped (SaveAnimationClip disabled).");

                // --- Phase D: Unknown upgrade + Coverage proof ---
                var coverageReport = MayaPhaseDAutoCoverageOnImport.Run_BestEffort(root, scene, opt, log);

                // --- Phase 6: Verification + Determinism fingerprint (portfolio-grade proof) ---
                string phase6Report = null;
                try
                {
                    phase6Report = MayaPhase6Verification.BuildAndAttach(root, scene, opt, sel, log);
                }
                catch (Exception e)
                {
                    (log ??= new MayaImportLog()).Warn($"[Phase6] Verification failed: {e.GetType().Name}: {e.Message}");
                }

                // ScriptedImporter向けのAsset化（Prefab main + sub-assets）
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

                if (_addPhase6VerificationReportTextAsset && !string.IsNullOrEmpty(phase6Report))
                {
                    var txt = phase6Report;
                    if (_phase6ReportMaxChars > 0 && txt.Length > _phase6ReportMaxChars)
                        txt = txt.Substring(0, _phase6ReportMaxChars) + "\n...(truncated)...";

                    var ta = new TextAsset(txt) { name = "MayaPhase6VerificationReport" };
                    ctx.AddObjectToAsset("phase6Report", ta);
                }

                if (log != null)
                {
                    if (_emitLogWarningsToConsole)
                    {
                        for (int i = 0; i < log.Warnings.Count; i++)
                            ctx.LogImportWarning("[MayaImporter] " + log.Warnings[i]);
                    }

                    if (_emitLogErrorsToConsole)
                    {
                        for (int i = 0; i < log.Errors.Count; i++)
                            ctx.LogImportError("[MayaImporter] " + log.Errors[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                var fallback = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
                ctx.AddObjectToAsset("root", fallback);
                ctx.SetMainObject(fallback);

                ctx.LogImportError("[MayaImporter] Exception during import. See console for stack trace.");
                try
                {
                    (log ??= new MayaImportLog()).Error(ex.ToString());
                }
                catch { }

                if (_emitExceptionToConsole)
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
