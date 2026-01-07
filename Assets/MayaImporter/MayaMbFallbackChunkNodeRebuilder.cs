// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Linq;

namespace MayaImporter.Core
{
    /// <summary>
    /// Production guarantee for .mb:
    /// If we cannot enumerate real Maya nodes (no embedded-ascii, no usable DAG-like paths),
    /// we still create a deterministic, inspectable hierarchy derived from the .mb chunk index.
    ///
    /// Notes:
    /// - This is NOT a semantic Maya reconstruction.
    /// - RawBinaryBytes are always preserved; this hierarchy only provides stable inspection hooks.
    /// - The placeholder nodes are typed as "transform" to avoid increasing missing-mapping count.
    ///   Their "true" identity is stored as attributes.
    /// </summary>
    public static class MayaMbFallbackChunkNodeRebuilder
    {
        public struct Result
        {
            public bool DidRun;
            public string Reason;
            public int CreatedNodes;
            public int CreatedDepthNodes;
            public int CreatedChunkNodes;
        }

        public static Result EnsurePlaceholderNodesIfEmpty(MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            var r = new Result { DidRun = false, Reason = "", CreatedNodes = 0, CreatedDepthNodes = 0, CreatedChunkNodes = 0 };

            if (scene == null) { r.Reason = "scene is null"; return r; }
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            if (scene.SourceKind != MayaSourceKind.BinaryMb) { r.Reason = "not .mb"; return r; }
            if (!options.MbCreateChunkPlaceholderNodes) { r.Reason = "MbCreateChunkPlaceholderNodes=false"; return r; }
            if (scene.Nodes != null && scene.Nodes.Count > 0) { r.Reason = "scene already has nodes"; return r; }

            r.DidRun = true;

            const string MbRoot = "|__mb";
            const string ChunksRoot = "|__mb|chunks";

            // Always add a stable root for preservation/audit
            if (EnsureNode(scene, MbRoot, "transform", null)) r.CreatedNodes++;
            if (EnsureNode(scene, ChunksRoot, "transform", MbRoot)) r.CreatedNodes++;

            SetStringAttr(scene, MbRoot, ".mbPlaceholder", "true");
            SetStringAttr(scene, MbRoot, ".mbPlaceholderReason", "Fallback hierarchy for binary .mb preservation-first import.");
            SetStringAttr(scene, MbRoot, ".sourcePath", scene.SourcePath ?? "");
            SetStringAttr(scene, MbRoot, ".rawSha256", scene.RawSha256 ?? "");
            SetIntAttr(scene, MbRoot, ".rawByteCount", scene.RawBinaryBytes != null ? scene.RawBinaryBytes.Length : 0);

            var idx = scene.MbIndex;
            if (idx == null || idx.Chunks == null || idx.Chunks.Count == 0)
            {
                // Minimal node when we couldn't even index chunks
                var raw = ChunksRoot + "|rawBinary";
                if (EnsureNode(scene, raw, "transform", ChunksRoot)) r.CreatedNodes++;
                r.CreatedChunkNodes += 1;

                SetStringAttr(scene, raw, ".mbChunkId", "(no-index)");
                SetStringAttr(scene, raw, ".mbPlaceholder", "true");
                SetStringAttr(scene, raw, ".mbPlaceholderReason",
                    "Chunk index unavailable (file may be non-IFF or index build failed). Raw binary is preserved in MayaSceneData.RawBinaryBytes.");

                AddAuditStatement(scene, "mbChunkPlaceholder",
                    "// Production: .mb had no chunk index. Created minimal placeholder nodes only (raw binary preserved).");

                log?.Warn(".mb fallback: chunk index unavailable. Created minimal placeholder nodes only.");
                r.Reason = "no chunk index";
                return r;
            }

            int maxNodes = options.MbChunkPlaceholderMaxNodes;
            if (maxNodes <= 0) maxNodes = 20000;

            // Deterministic order
            var chunks = idx.Chunks
                .Where(c => c != null)
                .OrderBy(c => c.Offset)
                .ThenBy(c => c.Depth)
                .ThenBy(c => c.Id ?? "", StringComparer.Ordinal)
                .ToList();

            var madeDepth = new HashSet<int>();

            int createdChunk = 0;
            for (int i = 0; i < chunks.Count && createdChunk < maxNodes; i++)
            {
                var c = chunks[i];
                int depth = Math.Max(0, c.Depth);

                // Create depth container node
                if (madeDepth.Add(depth))
                {
                    var dn = ChunksRoot + "|d" + depth.ToString("00");
                    if (EnsureNode(scene, dn, "transform", ChunksRoot))
                    {
                        r.CreatedNodes++;
                        r.CreatedDepthNodes++;
                    }
                    SetStringAttr(scene, dn, ".mbPlaceholder", "true");
                    SetIntAttr(scene, dn, ".mbChunkDepth", depth);
                }

                var depthNode = ChunksRoot + "|d" + depth.ToString("00");
                var name = BuildChunkNodeName(depthNode, i, c.Id, c.Offset);

                if (EnsureNode(scene, name, "transform", depthNode))
                {
                    r.CreatedNodes++;
                    r.CreatedChunkNodes++;
                }

                // Attributes = decode hints & addresses (audit friendly)
                SetStringAttr(scene, name, ".mbPlaceholder", "true");
                SetStringAttr(scene, name, ".mbChunkId", c.Id ?? "");
                SetStringAttr(scene, name, ".mbChunkFormType", c.FormType ?? "");
                SetIntAttr(scene, name, ".mbChunkOffset", c.Offset);
                SetIntAttr(scene, name, ".mbChunkDataOffset", c.DataOffset);
                SetIntAttr(scene, name, ".mbChunkDataSize", c.DataSize);
                SetIntAttr(scene, name, ".mbChunkDepth", depth);
                SetBoolAttr(scene, name, ".mbChunkIsContainer", c.IsContainer);
                SetStringAttr(scene, name, ".mbChunkDecodedKind", c.DecodedKind.ToString());
                SetStringAttr(scene, name, ".mbChunkPreview", c.Preview ?? "");

                createdChunk++;
            }

            // Summary statement for reports
            AddAuditStatement(scene, "mbChunkPlaceholder",
                $"// Production: created {r.CreatedChunkNodes} placeholder chunk nodes (+{r.CreatedDepthNodes} depth nodes). max={maxNodes} chunksIndexed={idx.Chunks.Count} extractedStrings={idx.ExtractedStrings?.Count ?? 0}");

            log?.Info($".mb fallback: created placeholder chunk nodes: chunks={r.CreatedChunkNodes} depthNodes={r.CreatedDepthNodes} (max={maxNodes}).");
            r.Reason = "chunk placeholders created";
            return r;
        }

        private static string BuildChunkNodeName(string depthNode, int index, string id, int offset)
        {
            id = string.IsNullOrEmpty(id) ? "____" : Sanitize4CC(id);
            return depthNode + "|" + index.ToString("00000") + "_" + id + "_" + offset.ToString("X8");
        }

        private static string Sanitize4CC(string id)
        {
            if (string.IsNullOrEmpty(id)) return "____";
            if (id.Length > 4) id = id.Substring(0, 4);
            var a = id.ToCharArray();
            for (int i = 0; i < a.Length; i++)
            {
                char c = a[i];
                if (c < 32 || c > 126) a[i] = '_';
                if (c == '|') a[i] = '_';
            }
            return new string(a);
        }

        private static bool EnsureNode(MayaSceneData scene, string name, string nodeType, string parentName)
        {
            if (scene == null || string.IsNullOrEmpty(name)) return false;

            bool exists = scene.Nodes != null && scene.Nodes.ContainsKey(name);
            var rec = scene.GetOrCreateNode(name, nodeType);
            // Provenance for audit
            rec.Provenance = MayaNodeProvenance.MbChunkPlaceholder;
            if (string.IsNullOrEmpty(rec.ProvenanceDetail)) rec.ProvenanceDetail = "chunkIndex";

            if (!string.Equals(rec.ParentName, parentName, StringComparison.Ordinal))
                rec.ParentName = parentName;

            return !exists;
        }

        private static void AddAuditStatement(MayaSceneData scene, string command, string text)
        {
            if (scene == null) return;
            if (scene.RawStatements == null) return;

            scene.RawStatements.Add(new RawStatement
            {
                LineStart = -1,
                LineEnd = -1,
                Command = command,
                Text = text,
                Tokens = null
            });
        }

        private static void SetStringAttr(MayaSceneData scene, string nodeName, string key, string value)
        {
            if (scene == null || string.IsNullOrEmpty(nodeName) || string.IsNullOrEmpty(key)) return;
            if (!scene.Nodes.TryGetValue(nodeName, out var rec) || rec == null) return;

            rec.Attributes[key] = new RawAttributeValue("string", new List<string> { value ?? "" });
        }

        private static void SetIntAttr(MayaSceneData scene, string nodeName, string key, int value)
        {
            if (scene == null || string.IsNullOrEmpty(nodeName) || string.IsNullOrEmpty(key)) return;
            if (!scene.Nodes.TryGetValue(nodeName, out var rec) || rec == null) return;

            rec.Attributes[key] = new RawAttributeValue("int", new List<string> { value.ToString() });
        }

        private static void SetBoolAttr(MayaSceneData scene, string nodeName, string key, bool value)
        {
            if (scene == null || string.IsNullOrEmpty(nodeName) || string.IsNullOrEmpty(key)) return;
            if (!scene.Nodes.TryGetValue(nodeName, out var rec) || rec == null) return;

            rec.Attributes[key] = new RawAttributeValue("bool", new List<string> { value ? "1" : "0" });
        }
    }
}
