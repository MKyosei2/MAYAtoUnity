// MAYAIMPORTER_PATCH_V5: IFF container coverage (FOR8/CAT8/LIS8/PRO8) + robust alignment (Unity-only)
// MayaImporter/MayaMbIffReader.cs
//
// Goal:
// - Parse .mb as an IFF-style container tree without Autodesk/Maya API.
// - Support Maya's alignment variants (FOR4/FOR8, CAT4/CAT8, LIS4/LIS8, PRO4/PRO8) as seen in newer Maya versions.
// - Keep behavior deterministic and safe (bounds checks, maxChunks cap).
//
// Notes:
// - The suffix "4/8" indicates *alignment* of child chunks (and typically padding), not a different endian.
// - We still treat the size field as big-endian 32-bit, consistent with common IFF/Maya practice.
// - Unity/.NET byte[] cannot exceed ~2GB, so offsets are int-based; this is acceptable for typical .mb assets.

using System;
using System.Collections.Generic;
using System.Text;

namespace MayaImporter.Core
{
    public static class MayaMbIffReader
    {
        public static MayaBinaryIndex BuildIndex(byte[] bytes, MayaImportLog log, int maxChunks = 50000)
        {
            var idx = new MayaBinaryIndex();
            if (bytes == null) return idx;

            idx.FileSize = bytes.Length;
            idx.Header4CC = bytes.Length >= 4 ? Read4CC(bytes, 0) : null;

            if (bytes.Length < 12)
            {
                log?.Warn(".mb seems too small to be a valid IFF container. Index will be empty.");
                return idx;
            }

            // 1) Prefer root at offset 0 if it looks like a valid container chunk
            if (TryReadChunk(bytes, 0, out var top))
            {
                ParseChunkRecursive(bytes, idx, top, depth: 0, maxChunks: maxChunks);
                return idx;
            }

            // 2) Fallback scan: find top-level containers (FOR4/FOR8/FORM) and index from there
            int offset = 0;
            int safety = 0;

            while (offset <= bytes.Length - 12 && idx.Chunks.Count < maxChunks && safety++ < 2_000_000)
            {
                var id = Read4CC(bytes, offset);
                if (LooksLikeContainerId(id) && TryReadChunk(bytes, offset, out var c))
                {
                    ParseChunkRecursive(bytes, idx, c, depth: 0, maxChunks: maxChunks);
                    offset = c.NextOffsetAligned;
                    continue;
                }

                offset += 1;
            }

            return idx;
        }

        private struct Chunk
        {
            public string Id;
            public int Offset;

            public int Size;       // payload size (does not include 8-byte header)
            public int DataOffset; // start of payload
            public int DataSize;   // == Size

            public bool IsContainer;
            public bool HasTypeField;
            public string FormType;

            public int ChildOffset;       // start of first child (already aligned)
            public int EndOffset;         // end of payload (DataOffset + DataSize)
            public int ChildAlignment;    // 2/4/8
            public int NextOffsetAligned; // start of next sibling (already aligned)
        }

        private static void ParseChunkRecursive(byte[] bytes, MayaBinaryIndex idx, Chunk c, int depth, int maxChunks)
        {
            if (idx.Chunks.Count >= maxChunks) return;

            var info = new MayaBinaryIndex.ChunkInfo
            {
                Id = c.Id,
                FormType = c.FormType,
                Offset = c.Offset,
                DataOffset = c.DataOffset,
                DataSize = c.DataSize,
                Depth = depth,
                IsContainer = c.IsContainer,

                DecodedKind = MayaBinaryDecoders.Kind.Unknown,
                Preview = null,
                DecodedStrings = null,
                DecodedFloats = null,
                DecodedUInts = null
            };

            if (!c.IsContainer)
                TryDecodeLeaf(bytes, info, idx);

            idx.Chunks.Add(info);

            if (!c.IsContainer) return;

            int childOffset = c.ChildOffset;
            int end = c.EndOffset;

            // Safety bounds
            if (childOffset < 0 || end < 0) return;
            if (end > bytes.Length) return;
            if (childOffset > end) return;

            while (childOffset + 8 <= end && idx.Chunks.Count < maxChunks)
            {
                if (!TryReadChunk(bytes, childOffset, out var child))
                    break;

                // Guard: child must reside within the parent's payload
                if (child.Offset < c.DataOffset || child.NextOffsetAligned > end)
                    break;

                ParseChunkRecursive(bytes, idx, child, depth + 1, maxChunks);

                if (child.NextOffsetAligned <= childOffset)
                    break;

                childOffset = child.NextOffsetAligned;
            }
        }

        private static void TryDecodeLeaf(byte[] bytes, MayaBinaryIndex.ChunkInfo info, MayaBinaryIndex idx)
        {
            var tag = info.Id;
            var kind = MayaBinaryDecoders.GetKindForTag(tag);

            // 1) Known tag kind decode
            if (kind != MayaBinaryDecoders.Kind.Unknown &&
                MayaBinaryDecoders.TryDecode(kind, bytes, info.DataOffset, info.DataSize,
                    out var strings, out var uints, out var floats))
            {
                info.DecodedKind = kind;
                info.Preview = MakePreview(kind, strings, uints, floats);

                if (strings != null)
                {
                    info.DecodedStrings = TakeFirst(strings, 8);
                    AppendStrings(idx.ExtractedStrings, strings);
                }

                if (floats != null) info.DecodedFloats = TakeFirst(floats, 16);
                if (uints != null) info.DecodedUInts = TakeFirst(uints, 16);
                return;
            }

            // 2) Guess StringZ
            if (MayaBinaryDecoders.TryGuessStringZ(bytes, info.DataOffset, info.DataSize, out var guessedStrings))
            {
                info.DecodedKind = MayaBinaryDecoders.Kind.StringZ;
                info.Preview = MakePreview(MayaBinaryDecoders.Kind.StringZ, guessedStrings, null, null);
                info.DecodedStrings = TakeFirst(guessedStrings, 8);
                AppendStrings(idx.ExtractedStrings, guessedStrings);
                return;
            }

            // 3) Guess Float32BE
            if (MayaBinaryDecoders.TryGuessFloat32BE(bytes, info.DataOffset, info.DataSize, out var guessedFloats))
            {
                info.DecodedKind = MayaBinaryDecoders.Kind.Float32BE;
                info.Preview = MakePreview(MayaBinaryDecoders.Kind.Float32BE, null, null, guessedFloats);
                info.DecodedFloats = TakeFirst(guessedFloats, 16);
                return;
            }

            // 4) Fallback token extraction
            ExtractAsciiTokens(bytes, info.DataOffset, info.DataSize, idx.ExtractedStrings, limitTotal: 2000);
        }

        private static string MakePreview(MayaBinaryDecoders.Kind kind, List<string> strings, List<uint> uints, List<float> floats)
        {
            try
            {
                switch (kind)
                {
                    case MayaBinaryDecoders.Kind.StringZ:
                        if (strings == null || strings.Count == 0) return "stringZ: (empty)";
                        return strings.Count == 1
                            ? $"stringZ: {TrimPreview(strings[0])}"
                            : $"stringZ[{strings.Count}]: {TrimPreview(strings[0])} ...";

                    case MayaBinaryDecoders.Kind.UInt32BE:
                        if (uints == null || uints.Count == 0) return "uint32: (empty)";
                        return uints.Count == 1
                            ? $"uint32: {uints[0]}"
                            : $"uint32[{uints.Count}]: {uints[0]} ...";

                    case MayaBinaryDecoders.Kind.Float32BE:
                        if (floats == null || floats.Count == 0) return "float32: (empty)";
                        return floats.Count == 1
                            ? $"float32: {floats[0]}"
                            : $"float32[{floats.Count}]: {floats[0]} ...";

                    default:
                        return null;
                }
            }
            catch { return null; }
        }

        private static string TrimPreview(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("\r", "").Replace("\n", "");
            if (s.Length > 80) return s.Substring(0, 80) + "...";
            return s;
        }

        private static string[] TakeFirst(List<string> src, int max)
        {
            if (src == null || src.Count == 0) return null;
            int n = Math.Min(max, src.Count);
            var a = new string[n];
            for (int i = 0; i < n; i++)
            {
                var s = src[i];
                if (s != null && s.Length > 512) s = s.Substring(0, 512);
                a[i] = s;
            }
            return a;
        }

        private static float[] TakeFirst(List<float> src, int max)
        {
            if (src == null || src.Count == 0) return null;
            int n = Math.Min(max, src.Count);
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = src[i];
            return a;
        }

        private static uint[] TakeFirst(List<uint> src, int max)
        {
            if (src == null || src.Count == 0) return null;
            int n = Math.Min(max, src.Count);
            var a = new uint[n];
            for (int i = 0; i < n; i++) a[i] = src[i];
            return a;
        }

        private static void AppendStrings(List<string> dst, List<string> src)
        {
            if (dst == null || src == null) return;

            var set = new HashSet<string>(dst, StringComparer.Ordinal);

            for (int i = 0; i < src.Count; i++)
            {
                var s = src[i];
                if (string.IsNullOrEmpty(s)) continue;
                if (s.Length > 512) s = s.Substring(0, 512);

                if (set.Add(s)) dst.Add(s);
                if (dst.Count >= 2000) break;
            }
        }

        // --------------------------------------------------------------------
        // IFF low-level
        // --------------------------------------------------------------------

        private static bool TryReadChunk(byte[] bytes, int offset, out Chunk c)
        {
            c = default;

            if (bytes == null) return false;
            if (offset < 0 || offset + 8 > bytes.Length) return false;

            string id = Read4CC(bytes, offset);
            if (string.IsNullOrEmpty(id)) return false;

            int size = ReadBE32(bytes, offset + 4);
            if (size < 0) return false;

            int dataOffset = offset + 8;
            int end = dataOffset + size;

            if (end < 0 || end > bytes.Length) return false;

            bool isContainer = LooksLikeContainerId(id);

            // alignment variants: FOR4/FOR8, CAT4/CAT8, LIS4/LIS8, PRO4/PRO8
            int align = GetAlignmentFromId(id);

            // type field presence depends on container kind
            bool hasType = isContainer && ContainerHasTypeField(id);

            string formType = null;

            int childOffset = dataOffset;

            if (isContainer && hasType)
            {
                if (dataOffset + 4 > bytes.Length) return false;
                formType = Read4CC(bytes, dataOffset);
                childOffset = dataOffset + 4;
                childOffset = Align(childOffset, align);
            }
            else if (isContainer)
            {
                // CAT* group: no type field, children start at payload start (aligned)
                childOffset = Align(childOffset, align);
            }

            // Next chunk starts after payload; apply same alignment for safety
            int nextAligned = Align(end, align);

            c = new Chunk
            {
                Id = id,
                Offset = offset,
                Size = size,
                DataOffset = dataOffset,
                DataSize = size,

                IsContainer = isContainer,
                HasTypeField = hasType,
                FormType = formType,

                ChildOffset = childOffset,
                EndOffset = end,
                ChildAlignment = align,
                NextOffsetAligned = nextAligned
            };

            return true;
        }

        private static bool LooksLikeContainerId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length != 4) return false;

            // Maya tends to use FOR4/FOR8 (root), and sometimes LIST/LIS4/LIS8, PRO4/PRO8, CAT4/CAT8.
            // Also accept classic IFF ids.
            if (id == "FORM" || id == "LIST" || id == "PROP" || id == "CAT ") return true;

            // Alignment variants: FOR4/FOR8, LIS4/LIS8, PRO4/PRO8, CAT4/CAT8
            char last = id[3];
            bool hasAlignSuffix = (last == '4' || last == '8');

            string p3 = id.Substring(0, 3);
            return hasAlignSuffix && (p3 == "FOR" || p3 == "LIS" || p3 == "PRO" || p3 == "CAT");
        }

        private static bool ContainerHasTypeField(string id)
        {
            // FORM/LIST/PROP have a 4CC "type" after the size field. CAT does not.
            if (id == "FORM" || id == "LIST" || id == "PROP") return true;
            if (id == "CAT ") return false;

            // Variants: FOR*/LIS*/PRO* have type; CAT* does not.
            if (id.StartsWith("CAT", StringComparison.Ordinal)) return false;
            if (id.StartsWith("FOR", StringComparison.Ordinal)) return true;
            if (id.StartsWith("LIS", StringComparison.Ordinal)) return true;
            if (id.StartsWith("PRO", StringComparison.Ordinal)) return true;

            return false;
        }

        private static int GetAlignmentFromId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length != 4) return 2;

            char last = id[3];
            if (last == '8') return 8;
            if (last == '4') return 4;

            // Classic IFF is 2-byte aligned.
            return 2;
        }

        private static int Align(int value, int align)
        {
            if (align <= 1) return value;
            int mod = value % align;
            return mod == 0 ? value : value + (align - mod);
        }

        private static int ReadBE32(byte[] b, int o)
        {
            unchecked { return (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]; }
        }

        private static string Read4CC(byte[] b, int o)
        {
            if (o + 4 > b.Length) return null;
            return new string(new[] { (char)b[o], (char)b[o + 1], (char)b[o + 2], (char)b[o + 3] });
        }

        // --------------------------------------------------------------------
        // Fallback token extraction
        // --------------------------------------------------------------------

        private static void ExtractAsciiTokens(byte[] bytes, int start, int length, List<string> outList, int limitTotal)
        {
            if (outList == null) return;
            if (outList.Count >= limitTotal) return;
            if (bytes == null || start < 0 || length <= 0) return;

            int end = Math.Min(bytes.Length, start + length);

            int i = start;
            while (i < end && outList.Count < limitTotal)
            {
                while (i < end && !IsInterestingAscii(bytes[i])) i++;
                int runStart = i;

                while (i < end && IsInterestingAscii(bytes[i])) i++;
                int runLen = i - runStart;

                if (runLen >= 4)
                {
                    int take = Math.Min(runLen, 128);
                    var s = Encoding.ASCII.GetString(bytes, runStart, take);
                    if (HasLetter(s) && !LooksLikeNoise(s)) outList.Add(s);
                }

                i++;
            }
        }

        private static bool IsInterestingAscii(byte b)
        {
            return (b >= (byte)'a' && b <= (byte)'z') ||
                   (b >= (byte)'A' && b <= (byte)'Z') ||
                   (b >= (byte)'0' && b <= (byte)'9') ||
                   b == (byte)'_' || b == (byte)'|' || b == (byte)':' ||
                   b == (byte)'.' || b == (byte)'-' || b == (byte)'/' ||
                   b == (byte)'\\';
        }

        private static bool HasLetter(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (char.IsLetter(s[i])) return true;
            return false;
        }

        private static bool LooksLikeNoise(string s)
        {
            int rep = 1;
            for (int i = 1; i < s.Length; i++)
            {
                if (s[i] == s[i - 1]) rep++;
                else rep = 1;
                if (rep >= 10) return true;
            }
            return false;
        }
    }
}
