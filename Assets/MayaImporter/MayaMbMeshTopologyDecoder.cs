// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Additive .mb mesh hint extraction:
    /// Scans the IFF index decoded strings to find chunks that likely contain mesh-related payloads.
    ///
    /// This does NOT decode mesh geometry by itself.
    /// It only tags candidate chunks deterministically so later stages (locator/decoder)
    /// can prioritize better candidates and reduce provisional markers.
    /// </summary>
    public static class MayaMbMeshTopologyDecoder
    {
        private static readonly string[] MeshKeywords =
        {
            // common Maya-ish tokens that appear near mesh payloads
            "polyFaces", "pnts", "vrts", "vtx", "norm", "normal", "uv", "uvst", "map", "face", "edge", "crease",
            "tangent", "binormal", "colorSet", "vertexColor"
        };

        public static void Populate(MayaSceneData scene, MayaImportLog log, int maxHints = 512)
        {
            if (scene == null || scene.MbIndex == null) return;

            scene.MbMeshHints.Clear();

            var chunks = scene.MbIndex.Chunks;
            if (chunks == null || chunks.Count == 0) return;

            for (int i = 0; i < chunks.Count; i++)
            {
                var c = chunks[i];
                var ds = c.DecodedStrings;
                if (ds == null || ds.Length == 0) continue;

                for (int k = 0; k < ds.Length; k++)
                {
                    string token = ds[k];
                    if (string.IsNullOrEmpty(token)) continue;

                    for (int m = 0; m < MeshKeywords.Length; m++)
                    {
                        if (!token.Contains(MeshKeywords[m], StringComparison.Ordinal)) continue;

                        scene.MbMeshHints.Add(new MayaMbMeshHint(
                            chunkId: c.Id,
                            formType: c.FormType,
                            offset: c.Offset,
                            dataOffset: c.DataOffset,
                            dataSize: c.DataSize,
                            keyword: MeshKeywords[m],
                            tokenPreview: token.Length > 64 ? token.Substring(0, 64) : token
                        ));

                        if (scene.MbMeshHints.Count >= maxHints)
                            goto DONE;
                    }
                }
            }

        DONE:
            // Deterministic ordering
            scene.MbMeshHints.Sort((a, b) =>
            {
                int o = a.Offset.CompareTo(b.Offset);
                if (o != 0) return o;
                o = string.CompareOrdinal(a.Keyword, b.Keyword);
                if (o != 0) return o;
                return string.CompareOrdinal(a.ChunkId, b.ChunkId);
            });

            if (scene.MbMeshHints.Count > 0)
                log?.Info($".mb mesh-hints: {scene.MbMeshHints.Count} candidates tagged (additive)");
            else
                log?.Info(".mb mesh-hints: none tagged (OK)");
        }
    }

    /// <summary>
    /// Lightweight, additive hint record for .mb mesh-related chunks.
    /// </summary>
    [Serializable]
    public struct MayaMbMeshHint
    {
        public string ChunkId;
        public string FormType;
        public int Offset;
        public int DataOffset;
        public int DataSize;
        public string Keyword;
        public string TokenPreview;

        public MayaMbMeshHint(string chunkId, string formType, int offset, int dataOffset, int dataSize, string keyword, string tokenPreview)
        {
            ChunkId = chunkId;
            FormType = formType;
            Offset = offset;
            DataOffset = dataOffset;
            DataSize = dataSize;
            Keyword = keyword;
            TokenPreview = tokenPreview;
        }
    }
}
