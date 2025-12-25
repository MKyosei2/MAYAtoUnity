// MayaImporter/MayaImporterOverrideTools.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase A:
    /// - .ma/.mb を必ず MayaMaMbImporterOverride に流すための “強制適用” ユーティリティ
    /// - 起動時に既存Assetsを走査して Override を適用
    /// - 新規追加/移動時も Postprocessor で Override→再import
    ///
    /// 参考:
    /// - override拡張子の扱い: ScriptedImporterAttribute.overrideFileExtensions / overrideExts :contentReference[oaicite:2]{index=2}
    /// - Override適用API: AssetDatabase.SetImporterOverride / GetImporterOverride :contentReference[oaicite:3]{index=3}
    /// </summary>
    [InitializeOnLoad]
    public static class MayaImporterOverrideTools
    {
        private const string SessionKey = "MayaImporter.PhaseA.OverrideAppliedOnce";
        private static readonly HashSet<string> _queued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _queuedDelayCall;

        static MayaImporterOverrideTools()
        {
            // Domain reload後、AssetDatabaseが落ち着いたタイミングで一度だけ全体適用
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(SessionKey, false))
                    return;

                SessionState.SetBool(SessionKey, true);
                EnsureOverridesForAllMayaAssets(reimportChanged: true);
            };
        }

        public static bool IsMayaAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            assetPath = assetPath.Replace('\\', '/');

            if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return false;

            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return ext == ".ma" || ext == ".mb";
        }

        public static bool HasOurOverride(string assetPath)
        {
            if (!IsMayaAssetPath(assetPath)) return false;

            var t = AssetDatabase.GetImporterOverride(assetPath);
            return t == typeof(MayaMaMbImporterOverride);
        }

        /// <summary>
        /// 指定アセットに Override を適用。必要なら再import。
        /// </summary>
        public static bool EnsureOverrideForAsset(string assetPath, bool reimportIfChanged)
        {
            if (!IsMayaAssetPath(assetPath)) return false;

            var cur = AssetDatabase.GetImporterOverride(assetPath);
            if (cur == typeof(MayaMaMbImporterOverride))
                return false;

            AssetDatabase.SetImporterOverride<MayaMaMbImporterOverride>(assetPath);

            if (reimportIfChanged)
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            return true;
        }

        /// <summary>
        /// Assets内の .ma/.mb 全てに Override を適用。変更があったものだけ必要なら再import。
        /// </summary>
        public static int EnsureOverridesForAllMayaAssets(bool reimportChanged)
        {
            var all = AssetDatabase.GetAllAssetPaths();
            var changed = new List<string>(256);

            for (int i = 0; i < all.Length; i++)
            {
                var p = all[i];
                if (!IsMayaAssetPath(p)) continue;

                try
                {
                    if (EnsureOverrideForAsset(p, reimportIfChanged: false))
                        changed.Add(p);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MayaImporter] EnsureOverride failed: {p}\n{ex.Message}");
                }
            }

            if (reimportChanged && changed.Count > 0)
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    for (int i = 0; i < changed.Count; i++)
                        AssetDatabase.ImportAsset(changed[i], ImportAssetOptions.ForceUpdate);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }

            if (changed.Count > 0)
                Debug.Log($"[MayaImporter] Override applied to {changed.Count} Maya assets (.ma/.mb).");

            return changed.Count;
        }

        public static void ClearOverrideForAsset(string assetPath, bool reimportAfter)
        {
            if (!IsMayaAssetPath(assetPath)) return;

            AssetDatabase.ClearImporterOverride(assetPath);

            if (reimportAfter)
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        // -----------------------
        // Menu (manual control)
        // -----------------------
        [MenuItem("Tools/Maya Importer/Phase A/Apply Override (ALL .ma/.mb)")]
        private static void MenuApplyAll()
        {
            EnsureOverridesForAllMayaAssets(reimportChanged: true);
        }

        [MenuItem("Tools/Maya Importer/Phase A/Apply Override (Selection)")]
        private static void MenuApplySelection()
        {
            var paths = GetSelectedAssetPaths();
            int n = 0;

            for (int i = 0; i < paths.Count; i++)
                if (EnsureOverrideForAsset(paths[i], reimportIfChanged: true))
                    n++;

            Debug.Log($"[MayaImporter] Override applied (selection): {n} files");
        }

        [MenuItem("Tools/Maya Importer/Phase A/Clear Override (Selection)")]
        private static void MenuClearSelection()
        {
            var paths = GetSelectedAssetPaths();
            int n = 0;

            for (int i = 0; i < paths.Count; i++)
            {
                if (!IsMayaAssetPath(paths[i])) continue;
                ClearOverrideForAsset(paths[i], reimportAfter: true);
                n++;
            }

            Debug.Log($"[MayaImporter] Override cleared (selection): {n} files");
        }

        [MenuItem("Tools/Maya Importer/Phase A/Report Override Status (Selection)")]
        private static void MenuReportSelection()
        {
            var paths = GetSelectedAssetPaths();
            for (int i = 0; i < paths.Count; i++)
            {
                if (!IsMayaAssetPath(paths[i])) continue;
                Debug.Log($"[MayaImporter] {paths[i]} override={AssetDatabase.GetImporterOverride(paths[i])}");
            }
        }

        private static List<string> GetSelectedAssetPaths()
        {
            var list = new List<string>(32);
            var objs = Selection.objects;

            for (int i = 0; i < objs.Length; i++)
            {
                var p = AssetDatabase.GetAssetPath(objs[i]);
                if (string.IsNullOrEmpty(p)) continue;
                p = p.Replace('\\', '/');
                if (p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    list.Add(p);
            }
            return list;
        }

        // -----------------------
        // Internal queue for postprocess
        // -----------------------
        internal static void QueueEnsureAfterImport(string assetPath)
        {
            if (!IsMayaAssetPath(assetPath)) return;

            _queued.Add(assetPath);

            if (_queuedDelayCall) return;
            _queuedDelayCall = true;

            EditorApplication.delayCall += ProcessQueue;
        }

        private static void ProcessQueue()
        {
            _queuedDelayCall = false;

            if (_queued.Count == 0) return;

            var list = new List<string>(_queued);
            _queued.Clear();

            int reapplied = 0;

            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (!IsMayaAssetPath(p)) continue;

                // ファイルが既に消えている/移動中などのケースを弾く
                if (!TryPhysicalFileExists(p)) continue;

                // Overrideが未適用なら適用し、再importして差し替える
                if (EnsureOverrideForAsset(p, reimportIfChanged: true))
                    reapplied++;
            }

            if (reapplied > 0)
                Debug.Log($"[MayaImporter] Postprocess override applied: {reapplied} files");
        }

        private static bool TryPhysicalFileExists(string assetPath)
        {
            try
            {
                if (MayaImporter.TryGetAbsolutePathFromAssetPath(assetPath, out var abs))
                    return File.Exists(abs);
            }
            catch { }
            return true; // 物理確認できない場合はtrue扱い（Importは進める）
        }
    }

    /// <summary>
    /// .ma/.mb が Project に入ってきた瞬間に、Override→再import をかける。
    /// 初回はUnity既存Importerが走って警告/失敗する可能性があるが、
    /// 直後に差し替え再importされるので “Maya無し運用” に収束する。
    /// </summary>
    public sealed class MayaImporterOverridePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets != null)
            {
                for (int i = 0; i < importedAssets.Length; i++)
                {
                    var p = importedAssets[i];
                    if (MayaImporterOverrideTools.IsMayaAssetPath(p))
                        MayaImporterOverrideTools.QueueEnsureAfterImport(p);
                }
            }

            if (movedAssets != null)
            {
                for (int i = 0; i < movedAssets.Length; i++)
                {
                    var p = movedAssets[i];
                    if (MayaImporterOverrideTools.IsMayaAssetPath(p))
                        MayaImporterOverrideTools.QueueEnsureAfterImport(p);
                }
            }
        }
    }
}
#endif
