// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Production:
    /// Generate a preservation-centric report.
    /// This report remains meaningful even when semantic reconstruction is incomplete
    /// (Unity-only, no Maya/Autodesk API).
    /// </summary>
    public static class MayaProductionPreservationReport
    {
        public static string Build(MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var sb = new StringBuilder(32 * 1024);
            sb.AppendLine("# MayaImporter Preservation Report (Production)");
            sb.AppendLine();
            sb.AppendLine($"- Unity: `{Application.unityVersion}`");
            sb.AppendLine($"- Timestamp(Local): `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            sb.AppendLine();

            if (scene == null)
            {
                sb.AppendLine("(scene is null)");
                return sb.ToString();
            }

            // -------------------------------------------------
            // 1) Source identity & preservation proof
            // -------------------------------------------------
            sb.AppendLine("## 1) Source identity & preservation");
            sb.AppendLine();
            sb.AppendLine($"- SourcePath: `{scene.SourcePath}`");
            sb.AppendLine($"- SourceKind: `{scene.SourceKind}`");
            sb.AppendLine($"- SchemaVersion: `{scene.SchemaVersion}`");
            sb.AppendLine($"- RawSha256: `{scene.RawSha256}`");
            sb.AppendLine($"- RawByteCount: **{(scene.RawBinaryBytes != null ? scene.RawBinaryBytes.Length : 0)}**");
            sb.AppendLine();
            sb.AppendLine("> Guarantee: Raw source bytes are stored in `MayaSceneData.RawBinaryBytes` during import, and its SHA-256 is recorded.");
            sb.AppendLine();

            // -------------------------------------------------
            // 2) Parse path overview
            // -------------------------------------------------
            sb.AppendLine("## 2) Parse path (best-effort)");
            sb.AppendLine();

            bool usedEmbeddedAscii = scene.MbEmbeddedAsciiParsed;
            bool usedChunkPlaceholder = false;
            if (scene.Nodes != null)
            {
                foreach (var kv in scene.Nodes)
                {
                    var n = kv.Value;
                    if (n == null) continue;
                    if (n.Provenance == MayaNodeProvenance.MbChunkPlaceholder)
                    {
                        usedChunkPlaceholder = true;
                        break;
                    }
                }
            }

            sb.AppendLine($"- EmbeddedAsciiExtractedStatements: **{scene.MbExtractedAsciiStatementCount}**");
            sb.AppendLine($"- EmbeddedAsciiConfidenceScore: **{scene.MbExtractedAsciiConfidence}**");
            sb.AppendLine($"- EmbeddedAsciiParsed: **{usedEmbeddedAscii}**");
            sb.AppendLine($"- ChunkPlaceholderNodesUsed: **{usedChunkPlaceholder}**");
            sb.AppendLine();

            // -------------------------------------------------
            // 3) .mb index proof
            // -------------------------------------------------
            if (scene.SourceKind == MayaSourceKind.BinaryMb)
            {
                sb.AppendLine("## 3) .mb binary index (IFF) proof");
                sb.AppendLine();

                var idx = scene.MbIndex;
                sb.AppendLine($"- Header4CC: `{idx?.Header4CC ?? ""}`");
                sb.AppendLine($"- FileSize(from index): **{idx?.FileSize ?? 0}**");
                sb.AppendLine($"- ChunkCount: **{idx?.Chunks?.Count ?? 0}**");
                sb.AppendLine($"- ExtractedStringsCount: **{idx?.ExtractedStrings?.Count ?? 0}**");
                sb.AppendLine();

                if (idx?.Chunks != null && idx.Chunks.Count > 0)
                {
                    // show top chunk IDs (frequency)
                    try
                    {
                        var freq = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
                        for (int i = 0; i < idx.Chunks.Count; i++)
                        {
                            var c = idx.Chunks[i];
                            if (c == null) continue;
                            var k = c.Id ?? "";
                            if (freq.TryGetValue(k, out var n)) freq[k] = n + 1;
                            else freq[k] = 1;
                        }

                        var top = freq.OrderByDescending(kv => kv.Value)
                                      .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                                      .Take(16);
                        sb.AppendLine("### 3.1 Top chunk IDs (frequency)");
                        sb.AppendLine();
                        foreach (var kv in top)
                            sb.AppendLine($"- `{kv.Key}`: {kv.Value}");
                        sb.AppendLine();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            // -------------------------------------------------
            // 4) Scene summary (what Unity can reconstruct)
            // -------------------------------------------------
            sb.AppendLine("## 4) Scene summary (Unity reconstruction)");
            sb.AppendLine();
            sb.AppendLine($"- NodeRecords: **{scene.Nodes?.Count ?? 0}**");
            sb.AppendLine($"- Connections: **{scene.Connections?.Count ?? 0}**");
            sb.AppendLine();

            if (scene.Nodes != null && scene.Nodes.Count > 0)
            {
                try
                {
                    var counts = scene.CountNodeTypes();
                    var take = counts.OrderByDescending(kv => kv.Value)
                                     .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                                     .Take(20);
                    sb.AppendLine("### 4.1 Top nodeTypes (from parsed/placeholder NodeRecords)");
                    sb.AppendLine();
                    foreach (var kv in take)
                        sb.AppendLine($"- `{kv.Key}`: {kv.Value}");
                    sb.AppendLine();
                }
                catch
                {
                    // ignore
                }
            }

            // -------------------------------------------------
            // 5) Warnings / errors
            // -------------------------------------------------
            sb.AppendLine("## 5) Import log summary");
            sb.AppendLine();
            sb.AppendLine($"- Warnings: **{log?.Warnings?.Count ?? 0}**");
            sb.AppendLine($"- Errors: **{log?.Errors?.Count ?? 0}**");
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine("### Interpretation tips");
            sb.AppendLine();
            sb.AppendLine("- If `EmbeddedAsciiParsed` is true, the importer recovered a command-like ASCII stream from the .mb and parsed it as .ma statements.");
            sb.AppendLine("- If `ChunkPlaceholderNodesUsed` is true, the importer could not enumerate real Maya nodes, and instead generated a deterministic placeholder hierarchy from the binary chunk index (raw bytes are still preserved).");
            sb.AppendLine();

            return sb.ToString();
        }
    }
}
#endif
