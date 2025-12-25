// MayaImporter/MayaImportedAssetManifest.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Import結果の “証拠” を保持するManifest。
    /// - Legacy: .asset として保存される
    /// - ScriptedImporter: sub-asset として登録される
    ///
    /// 目的:
    /// - 100%保持の説明材料（ポートフォリオ防衛）
    /// - Importの再現性・差分検出の土台
    /// </summary>
    public sealed class MayaImportedAssetManifest : ScriptableObject
    {
        [Header("Source")]
        public string SourceHint = "";
        public string ImportedAt = "";
        public string UnityVersion = "";
        public string ToolVersion = "Phase1";

        [Header("Options (snapshot)")]
        [TextArea(2, 8)]
        public string OptionsSummary = "";

        [Header("SceneData snapshot")]
        public int SceneNodeCount = 0;
        public int SceneConnectionCount = 0;

        [Header("Generated Assets (Legacy paths or Importer sub-ids)")]
        public List<GeneratedItem> Generated = new List<GeneratedItem>(256);

        [Serializable]
        public sealed class GeneratedItem
        {
            public string Kind;         // Mesh / Material / Texture / AnimationClip / Prefab / Root / Manifest
            public string Name;
            public string Identifier;   // Legacy asset path OR Importer sub-asset id
        }

        public static MayaImportedAssetManifest CreateFrom(
            GameObject importedRoot,
            MayaSceneData scene,
            MayaImportOptions options,
            MayaAssetPipeline.AssetizeReport report,
            MayaImportLog log,
            string sourceHint)
        {
            var m = CreateInstance<MayaImportedAssetManifest>();
            m.SourceHint = sourceHint ?? "";
            m.ImportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            m.UnityVersion = Application.unityVersion;

            // options snapshot (keep robust even if fields change)
            try
            {
                m.OptionsSummary = BuildOptionsSummary(options);
            }
            catch
            {
                m.OptionsSummary = "(options snapshot unavailable)";
            }

            m.SceneNodeCount = scene?.Nodes?.Count ?? 0;
            m.SceneConnectionCount = scene?.Connections?.Count ?? 0;

            // Always include Root
            if (importedRoot != null)
            {
                m.Generated.Add(new GeneratedItem
                {
                    Kind = "Root",
                    Name = importedRoot.name,
                    Identifier = "(main)"
                });
            }

            // Legacy asset paths
            if (report != null)
            {
                foreach (var p in report.MeshAssets)
                    m.Generated.Add(new GeneratedItem { Kind = "Mesh", Name = SafeNameFromId(p), Identifier = p });

                foreach (var p in report.MaterialAssets)
                    m.Generated.Add(new GeneratedItem { Kind = "Material", Name = SafeNameFromId(p), Identifier = p });

                foreach (var p in report.TextureAssets)
                    m.Generated.Add(new GeneratedItem { Kind = "Texture", Name = SafeNameFromId(p), Identifier = p });

                foreach (var p in report.AnimationClipAssets)
                    m.Generated.Add(new GeneratedItem { Kind = "AnimationClip", Name = SafeNameFromId(p), Identifier = p });

                if (!string.IsNullOrEmpty(report.PrefabAssetPath))
                    m.Generated.Add(new GeneratedItem { Kind = "Prefab", Name = SafeNameFromId(report.PrefabAssetPath), Identifier = report.PrefabAssetPath });

                if (!string.IsNullOrEmpty(report.ManifestAssetPath))
                    m.Generated.Add(new GeneratedItem { Kind = "Manifest", Name = SafeNameFromId(report.ManifestAssetPath), Identifier = report.ManifestAssetPath });

                // Importer sub-asset ids
                foreach (var id in report.MeshSubAssetIds)
                    m.Generated.Add(new GeneratedItem { Kind = "Mesh", Name = id, Identifier = id });

                foreach (var id in report.MaterialSubAssetIds)
                    m.Generated.Add(new GeneratedItem { Kind = "Material", Name = id, Identifier = id });

                foreach (var id in report.TextureSubAssetIds)
                    m.Generated.Add(new GeneratedItem { Kind = "Texture", Name = id, Identifier = id });

                foreach (var id in report.AnimationClipSubAssetIds)
                    m.Generated.Add(new GeneratedItem { Kind = "AnimationClip", Name = id, Identifier = id });
            }

            log?.Info($"Manifest created. Generated items: {m.Generated.Count}");
            return m;
        }

        private static string BuildOptionsSummary(MayaImportOptions options)
        {
            if (options == null) return "(null options)";

            // 手堅く、存在しそうな値だけを列挙（コンパイル時にフィールドが消える可能性を避けたいので reflection は使わない）
            // ※ あなたの現在のMayaImportOptionsに合わせたキー
            return string.Join("\n", new[]
            {
                $"KeepRawStatements: {options.KeepRawStatements}",
                $"CreateUnityComponents: {options.CreateUnityComponents}",
                $"Conversion: {options.Conversion}",
                $"SaveAssets: {options.SaveAssets}",
                $"SaveMeshes: {options.SaveMeshes}",
                $"SaveMaterials: {options.SaveMaterials}",
                $"SaveTextures: {options.SaveTextures}",
                $"SaveAnimationClip: {options.SaveAnimationClip}",
                $"AnimationClipName: {options.AnimationClipName}",
                $"AnimationTimeScale: {options.AnimationTimeScale}",
                $"SavePrefab: {options.SavePrefab}",
                $"OutputFolder: {options.OutputFolder}",
                $"KeepImportedRootInScene: {options.KeepImportedRootInScene}",
                $"AttachOpaqueRuntimeMarker: {options.AttachOpaqueRuntimeMarker}",
                $"AttachOpaqueAttributePreview: {options.AttachOpaqueAttributePreview}",
                $"AttachOpaqueConnectionPreview: {options.AttachOpaqueConnectionPreview}",
                $"AttachDecodedAttributeSummary: {options.AttachDecodedAttributeSummary}",
                $"OpaquePreviewMaxEntries: {options.OpaquePreviewMaxEntries}",
                $"OpaqueRuntimeGizmoSize: {options.OpaqueRuntimeGizmoSize}",
            });
        }

        private static string SafeNameFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            id = id.Replace('\\', '/');
            var lastSlash = id.LastIndexOf('/');
            var name = (lastSlash >= 0) ? id.Substring(lastSlash + 1) : id;
            return name;
        }
    }
}
