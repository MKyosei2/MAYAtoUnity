// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    [Serializable]
    public sealed class MayaBinaryIndex
    {
        [Serializable]
        public sealed class ChunkInfo
        {
            public string Id;          // 4CC
            public string FormType;    // FOR4/LIST/CAT4 => subtype 4CC
            public int Offset;         // file offset where chunk header starts
            public int DataOffset;     // payload start
            public int DataSize;       // payload size
            public int Depth;          // nesting depth
            public bool IsContainer;   // FOR4/LIST/CAT4

            // Decoding hints
            public MayaBinaryDecoders.Kind DecodedKind;
            public string Preview;

            // Small decoded samples for structured rebuilders
            public string[] DecodedStrings; // max 8
            public float[] DecodedFloats;   // max 16
            public uint[] DecodedUInts;     // max 16
        }

        public string Header4CC;
        public int FileSize;

        public readonly List<ChunkInfo> Chunks = new List<ChunkInfo>(4096);

        /// <summary>
        /// Deduped-ish extracted strings (capped) – used by heuristic rebuilders.
        /// </summary>
        public readonly List<string> ExtractedStrings = new List<string>(2048);
    }
}
