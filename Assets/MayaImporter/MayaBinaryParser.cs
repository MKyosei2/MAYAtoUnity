// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.IO;
using System.Text;

namespace MayaImporter.Core
{
    /// <summary>
    /// .mb(Binary) parser entry:
    /// - Unity only (no Maya / Autodesk API)
    /// - Always preserves raw bytes
    /// - Best-effort: build index -> extract embedded ASCII -> parse as .ma -> merge -> heuristic/structured rebuild
    /// </summary>
    public sealed class MayaBinaryParser
    {
        // =========================================================
        // Low-level byte utility (kept for compatibility)
        // =========================================================
        private readonly byte[] _data;
        private int _pos;

        public int Position => _pos;
        public int Length => _data != null ? _data.Length : 0;
        public bool End => _pos >= Length;

        public MayaBinaryParser()
        {
            _data = Array.Empty<byte>();
            _pos = 0;
        }

        public MayaBinaryParser(byte[] data)
        {
            _data = data ?? Array.Empty<byte>();
            _pos = 0;
        }

        public void Seek(int absolute) => _pos = Clamp(absolute, 0, Length);
        public void Skip(int bytes) => _pos = Clamp(_pos + bytes, 0, Length);

        public byte ReadU8()
        {
            if (_pos >= Length) return 0;
            return _data[_pos++];
        }

        public ushort ReadU16LE()
        {
            byte a = ReadU8();
            byte b = ReadU8();
            return (ushort)(a | (b << 8));
        }

        public uint ReadU32LE()
        {
            uint a = ReadU8();
            uint b = ReadU8();
            uint c = ReadU8();
            uint d = ReadU8();
            return a | (b << 8) | (c << 16) | (d << 24);
        }

        public int ReadI32LE() => unchecked((int)ReadU32LE());

        public float ReadF32LE()
        {
            // unsafe禁止でも動くように BitConverter を使用
            uint v = ReadU32LE();
            var bytes = BitConverter.GetBytes(v);
            return BitConverter.ToSingle(bytes, 0);
        }

        public byte[] ReadBytes(int count)
        {
            if (count <= 0) return Array.Empty<byte>();
            int n = Math.Min(count, Length - _pos);
            if (n <= 0) return Array.Empty<byte>();
            var outArr = new byte[n];
            Buffer.BlockCopy(_data, _pos, outArr, 0, n);
            _pos += n;
            return outArr;
        }

        public string ReadAsciiZ(int maxBytes = 4096)
        {
            var sb = new StringBuilder();
            int n = 0;

            while (!End && n < maxBytes)
            {
                byte b = ReadU8();
                n++;
                if (b == 0) break;
                sb.Append((char)b);
            }

            return sb.ToString();
        }

        public bool TryFindAscii(string needle, int startPos, int searchBytes, out int foundPos)
        {
            foundPos = -1;
            if (string.IsNullOrEmpty(needle) || _data == null || _data.Length == 0) return false;

            var nb = Encoding.ASCII.GetBytes(needle);
            int start = Clamp(startPos, 0, Length);
            int end = Clamp(start + Math.Max(0, searchBytes), 0, Length);

            for (int i = start; i + nb.Length <= end; i++)
            {
                bool ok = true;
                for (int k = 0; k < nb.Length; k++)
                {
                    if (_data[i + k] != nb[k]) { ok = false; break; }
                }
                if (ok)
                {
                    foundPos = i;
                    return true;
                }
            }

            return false;
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        // =========================================================
        // .mb Parse Entry
        // =========================================================

        /// <summary>
        /// Parse .mb file into MayaSceneData.
        /// This never requires Maya/Autodesk API.
        /// </summary>
        public MayaSceneData ParseFile(string path, MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var scene = new MayaSceneData();

            if (string.IsNullOrEmpty(path))
            {
                log.Error(".mb path is null/empty.");
                scene.SourcePath = path;
                return scene;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                log.Error($"Failed to read .mb: {ex.GetType().Name}: {ex.Message}");
                scene.SourcePath = path;
                return scene;
            }

            scene.SetRawBinary(path, bytes);

            // ---- Step1: Build IFF-like index ----
            try
            {
                scene.MbIndex = MayaMbIffReader.BuildIndex(bytes, log, maxChunks: 50000);

            // ---- Step1.5: Deterministic hint extraction (Unity-only; additive) ----
            try
            {
                MayaMbStringTableDecoder.Populate(scene, log);
            }
            catch (Exception ex)
            {
                log.Warn($".mb string-table decode failed (continue): {ex.GetType().Name}: {ex.Message}");
            }

            // ---- Step1.6: Deterministic node enumeration (Unity-only; additive) ----
            try
            {
                if (options.MbDeterministicEnumerateNodes)
                    MayaMbDeterministicNodeEnumerator.Populate(scene, options, log);
            }
            catch (Exception ex)
            {
                log.Warn($".mb deterministic enumeration failed (continue): {ex.GetType().Name}: {ex.Message}");
            }

            // ---- Step1.7: Shading/texture hint tagging from strings (Unity-only; additive) ----
            if (options.MbTryTagShadingFromStrings)
            {
                // Older revisions used Populate(); current tagger API is Apply().
                try { MayaMbShadingTagger.Apply(scene, log); }
                catch (Exception ex) { log.Warn($".mb shading tagger failed (continue): {ex.GetType().Name}: {ex.Message}"); }
            }

            if (options.MbTryTagTexturesFromStrings)
            {
                // Older revisions used Populate(); current tagger API is Apply().
                try { MayaMbTextureHintTagger.Apply(scene, log); }
                catch (Exception ex) { log.Warn($".mb texture hint tagger failed (continue): {ex.GetType().Name}: {ex.Message}"); }
            }

            try
            {
                MayaMbMeshTopologyDecoder.Populate(scene, log);
            }
            catch (Exception ex)
            {
                log.Warn($".mb mesh-hints decode failed (continue): {ex.GetType().Name}: {ex.Message}");
            }

            }
            catch (Exception ex)
            {
                log.Warn($".mb index build failed (will continue with raw only): {ex.GetType().Name}: {ex.Message}");
                scene.MbIndex = new MayaBinaryIndex();
                scene.MbIndex.FileSize = bytes.Length;
            }

            // ---- Step2: Try extract embedded ASCII and parse with .ma parser ----
            if (options.MbTryExtractEmbeddedAscii)
            {
                try
                {
                    if (MayaMbEmbeddedMaExtractor.TryExtractCommandText(
                            bytes,
                            scene.MbIndex,
                            options,
                            log,
                            out string extractedText,
                            out MayaMbEmbeddedMaExtractor.ExtractionInfo info))
                    {
                        bool shouldParse = MayaMbEmbeddedMaExtractor.ShouldParse(info, options);

                        if (!shouldParse && options.MbAllowLowConfidenceEmbeddedAscii)
                        {
                            // allow parsing even if low confidence (for coverage)
                            shouldParse = true;
                            log.Info($".mb embedded-ascii: low confidence but allowed by options. segments={info.CandidateSegments}, statements~={info.StatementCount}, score={info.Score}");
                        }

                        if (shouldParse)
                        {
                            // Optionally clamp huge extracted text (safety)
                            if (!string.IsNullOrEmpty(extractedText))
                            {
                                int maxChars = Math.Max(64 * 1024, options.MbEmbeddedAsciiMaxChars);
                                if (extractedText.Length > maxChars)
                                    extractedText = extractedText.Substring(0, maxChars);
                            }

                            scene.MbExtractedAsciiText = extractedText;
                            scene.MbExtractedAsciiStatementCount = info.StatementCount;
                            scene.MbExtractedAsciiConfidence = info.Score;

                            if (options.KeepRawStatements)
                            {
                                scene.RawStatements.Add(new RawStatement
                                {
                                    LineStart = -1,
                                    LineEnd = -1,
                                    Command = "mbEmbeddedAscii",
                                    Text = $"// Extracted embedded command-like text from .mb (segments={info.CandidateSegments}, statements~={info.StatementCount}, score={info.Score})."
                                });
                            }

                            var ma = new MayaAsciiParser();
                            var tmpScene = ma.ParseText($"{Path.GetFileName(path)}::embedded", extractedText, options, log);

                            MayaSceneMerger.MergeInto(scene, tmpScene, log, provenanceTag: "mb:embeddedAscii");
                        }
                        else
                        {
                            log.Info($".mb embedded-ascii extracted but rejected by threshold. segments={info.CandidateSegments}, statements~={info.StatementCount}, score={info.Score}");
                        }
                    }
                    else
                    {
                        log.Info(".mb embedded-ascii: not detected (OK). Will use heuristic/structured rebuilders.");
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($".mb embedded-ascii parse/merge failed (continue): {ex.GetType().Name}: {ex.Message}");
                }
            }

            // ---- Step2.5: Try reconstruct command stream from null-terminated ASCII strings ----
            // This is a second chance for files where the embedded-ascii scan can't find ';'-terminated segments.
            if (options.MbTryExtractNullTerminatedAscii)
            {
                try
                {
                    // If embedded-ascii already yielded a lot, skip to avoid noisy duplication.
                    bool alreadyGood = scene.Nodes != null && scene.Nodes.Count > 0;
                    bool embeddedLooksGood = scene.MbExtractedAsciiStatementCount >= Math.Max(200, options.MbNullTerminatedHardMinStatements * 10);

                    if (!embeddedLooksGood)
                    {
                        if (MayaMbNullTerminatedMaExtractor.TryReconstructCommandText(
                                bytes,
                                options,
                                log,
                                out string reconstructed,
                                out int statementCount,
                                out int score))
                        {
                            bool shouldParse = statementCount >= options.MbNullTerminatedHardMinStatements;
                            if (!shouldParse && options.MbAllowLowConfidenceNullTerminatedAscii)
                                shouldParse = true;

                            if (shouldParse)
                            {
                                var ma = new MayaAsciiParser();
                                var tmpScene = ma.ParseText($"{Path.GetFileName(path)}::mbNullTerminated", reconstructed, options, log);
                                MayaSceneMerger.MergeInto(scene, tmpScene, log, provenanceTag: $"mb:nullTerminated(s={statementCount},score={score})");
                                ApplyMbProvenanceFromProvenanceAttr(scene, "mb:nullTerminated", MayaNodeProvenance.MbNullTerminatedAscii);
// keep a hint for audit
                                scene.MbNullTerminatedStatementCount = statementCount;
                                scene.MbNullTerminatedScore = score;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($".mb null-terminated reconstruction failed (continue): {ex.GetType().Name}: {ex.Message}");
                }
            }

            // ---- Step3: If still no nodes, create DAG placeholders ----
            try
            {
                MayaMbHeuristicSceneRebuilder.PopulatePlaceholdersIfEmpty(scene, log);
            }
            catch (Exception ex)
            {
                log.Warn($".mb heuristic scene rebuild failed (continue): {ex.GetType().Name}: {ex.Message}");
            }

            // ---- Step3.5 (Phase1): If still empty, create chunk-index placeholder nodes (preservation-first) ----
            try
            {
                if (scene.Nodes == null || scene.Nodes.Count == 0)
                {
                    var res = MayaMbFallbackChunkNodeRebuilder.EnsurePlaceholderNodesIfEmpty(scene, options, log);
                    if (res.DidRun)
                        log.Info($".mb Phase1 placeholder nodes: reason={res.Reason} created={res.CreatedNodes} chunkNodes={res.CreatedChunkNodes} depthNodes={res.CreatedDepthNodes}");
                }
            }
            catch (Exception ex)
            {
                log.Warn($".mb Phase1 chunk placeholder failed (continue): {ex.GetType().Name}: {ex.Message}");
            }

            // ---- Step4: Graph enrich (types + plug connections) ----
            try
            {
                MayaMbHeuristicGraphRebuilder.Enrich(scene, log);
            }
            catch (Exception ex)
            {
                log.Warn($".mb heuristic graph rebuild failed (continue): {ex.GetType().Name}: {ex.Message}");
            }

            // ---- Step5: Structured confirm ----
            try
            {
                MayaMbStructuredRebuilder.Apply(scene, log);
            }
            catch (Exception ex)
            {
                log.Warn($".mb structured rebuild failed (continue): {ex.GetType().Name}: {ex.Message}");
            }

            // ---- Step6: TRS confirm ----
            try
            {
                MayaMbTransformRebuilder.Apply(scene, log);
            }
            catch (Exception ex)
            {
                log.Warn($".mb TRS rebuild failed (continue): {ex.GetType().Name}: {ex.Message}");
            }

            // ---- Step7: DAG post ----
            try
            {
                MayaMbDagPostProcessor.Apply(scene, log);
            }
            catch (Exception ex)
            {
                log.Warn($".mb DAG post failed (continue): {ex.GetType().Name}: {ex.Message}");
            }

            // ---- Step8: Mesh chunk refs attach ----
            try
            {
                MayaMbMeshChunkLocator.Apply(scene, log);
            }
            catch (Exception ex)
            {
                log.Warn($".mb mesh chunk locate failed (continue): {ex.GetType().Name}: {ex.Message}");
            }

            return scene;
        }

        /// <summary>
        /// Backfill strongly-typed provenance (NodeRecord.Provenance) from legacy
        /// string-based provenance attribute (".provenance").
        ///
        /// This is used for .mb reconstruction paths that merge a temp ASCII-parsed scene
        /// (e.g. null-terminated extraction) into the base .mb scene.
        /// MayaSceneMerger stamps the ".provenance" attribute; we also want the audit
        /// enum provenance so Phase6 can break down recovery routes deterministically.
        /// </summary>
        private static void ApplyMbProvenanceFromProvenanceAttr(MayaSceneData scene, string tagPrefix, MayaNodeProvenance prov)
        {
            if (scene == null || scene.Nodes == null || scene.Nodes.Count == 0) return;
            if (string.IsNullOrEmpty(tagPrefix)) return;

            foreach (var kv in scene.Nodes)
            {
                var n = kv.Value;
                if (n == null || n.Attributes == null) continue;

                if (!n.Attributes.TryGetValue(".provenance", out var v) || v == null) continue;
                var tokens = v.ValueTokens;
                if (tokens == null || tokens.Count == 0) continue;

                bool hit = false;
                for (int i = 0; i < tokens.Count; i++)
                {
                    var t = tokens[i];
                    if (string.IsNullOrEmpty(t)) continue;
                    if (t.IndexOf(tagPrefix, StringComparison.Ordinal) >= 0)
                    {
                        hit = true;
                        break;
                    }
                }

                if (!hit) continue;

                // Prefer scene's policy to avoid overriding better provenance.
                scene.MarkProvenance(n.Name, prov, tagPrefix);
            }
        }
    }
}