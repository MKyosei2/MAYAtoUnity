// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
// MayaImporter/MayaImportedAssetManifest.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Import manifest used as "proof" in portfolios and as an audit trail.
    /// - Legacy: saved as .asset
    /// - ScriptedImporter: added as sub-asset
    ///
    /// Policy:
    /// - Unity-only (no Maya/Autodesk API)
    /// - Preservation-first: record source hash and parsing path
    /// </summary>
    public sealed class MayaImportedAssetManifest : ScriptableObject
    {
        [Header("Source")]
        public string SourceHint = "";
        public string ImportedAt = "";
        public string UnityVersion = "";
        public string ToolVersion = "Production";

        [Header("Source Proof")]
        public string SourceKind = "";
        public string RawSha256 = "";
        public int RawByteCount = 0;

        [Header(".mb Proof (best-effort)")]
        public string MbHeader4CC = "";
        public int MbChunkCount = 0;
        public int MbExtractedStringCount = 0;
        public int MbExtractedAsciiStatements = 0;
        public int MbExtractedAsciiScore = 0;
        public bool MbEmbeddedAsciiParsed = false;
        public bool MbUsedChunkPlaceholders = false;

        [Header("Options (snapshot)")]
        [TextArea(2, 10)]
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

            // Source proof
            m.SourceKind = scene != null ? scene.SourceKind.ToString() : "Unknown";
            m.RawSha256 = scene?.RawSha256 ?? "";
            m.RawByteCount = scene?.RawBinaryBytes != null ? scene.RawBinaryBytes.Length : 0;

            if (scene != null && scene.SourceKind == MayaSourceKind.BinaryMb)
            {
                m.MbHeader4CC = scene.MbIndex?.Header4CC ?? "";
                m.MbChunkCount = scene.MbIndex?.Chunks?.Count ?? 0;
                m.MbExtractedStringCount = scene.MbIndex?.ExtractedStrings?.Count ?? 0;
                m.MbExtractedAsciiStatements = scene.MbExtractedAsciiStatementCount;
                m.MbExtractedAsciiScore = scene.MbExtractedAsciiConfidence;

                // detect actual parse path without requiring KeepRawStatements
                m.MbEmbeddedAsciiParsed = scene.MbEmbeddedAsciiParsed;

                bool chunkPlaceholderUsed = false;
                if (scene.Nodes != null)
                {
                    foreach (var kv in scene.Nodes)
                    {
                        var n = kv.Value;
                        if (n == null) continue;
                        if (n.Provenance == MayaNodeProvenance.MbChunkPlaceholder)
                        {
                            chunkPlaceholderUsed = true;
                            break;
                        }
                    }
                }
                m.MbUsedChunkPlaceholders = chunkPlaceholderUsed;
            }

            // Options snapshot
            try { m.OptionsSummary = BuildOptionsSummary(options); }
            catch { m.OptionsSummary = "(options snapshot unavailable)"; }

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

            // Legacy asset paths / Importer sub-asset ids
            if (report != null)
            {
                foreach (var id in report.MeshAssets)
                    m.Generated.Add(new GeneratedItem { Kind = "Mesh", Name = SafeNameFromId(id), Identifier = id });

                foreach (var id in report.MaterialAssets)
                    m.Generated.Add(new GeneratedItem { Kind = "Material", Name = SafeNameFromId(id), Identifier = id });

                foreach (var id in report.TextureAssets)
                    m.Generated.Add(new GeneratedItem { Kind = "Texture", Name = SafeNameFromId(id), Identifier = id });

                foreach (var id in report.AnimationClipAssets)
                    m.Generated.Add(new GeneratedItem { Kind = "AnimationClip", Name = SafeNameFromId(id), Identifier = id });

                if (!string.IsNullOrEmpty(report.PrefabAssetPath))
                    m.Generated.Add(new GeneratedItem { Kind = "Prefab", Name = SafeNameFromId(report.PrefabAssetPath), Identifier = report.PrefabAssetPath });

                if (!string.IsNullOrEmpty(report.ManifestAssetPath))
                    m.Generated.Add(new GeneratedItem { Kind = "Manifest", Name = SafeNameFromId(report.ManifestAssetPath), Identifier = report.ManifestAssetPath });

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
                $"MbTryExtractEmbeddedAscii: {options.MbTryExtractEmbeddedAscii}",
                $"MbAllowLowConfidenceEmbeddedAscii: {options.MbAllowLowConfidenceEmbeddedAscii}",
                $"MbCreateChunkPlaceholderNodes: {options.MbCreateChunkPlaceholderNodes}",
                $"MbChunkPlaceholderMaxNodes: {options.MbChunkPlaceholderMaxNodes}",
            });
        }

        private static string SafeNameFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            id = id.Replace('\\', '/');
            var lastSlash = id.LastIndexOf('/');
            return (lastSlash >= 0) ? id.Substring(lastSlash + 1) : id;
        }
    }
}
