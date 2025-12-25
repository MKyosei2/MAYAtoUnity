using System;
using System.Collections.Generic;
using System.Text;

namespace MayaImporter.Core
{
    public static class MayaMbIffReader
    {
        private static readonly HashSet<string> Containers = new HashSet<string>(StringComparer.Ordinal)
        {
            "FOR4","LIST","CAT4"
        };

        public static MayaBinaryIndex BuildIndex(byte[] bytes, MayaImportLog log, int maxChunks = 50000)
        {
            var idx = new MayaBinaryIndex();
            if (bytes == null) return idx;

            idx.FileSize = bytes.Length;
            idx.Header4CC = bytes.Length >= 4 ? Read4CC(bytes, 0) : null;

            if (bytes.Length < 12)
            {
                log?.Warn(".mb seems too small to be a valid IFF file. Index will be empty.");
                return idx;
            }

            if (TryReadChunk(bytes, 0, out var top))
            {
                ParseChunkRecursive(bytes, idx, top, depth: 0, maxChunks: maxChunks);
                return idx;
            }

            // fallback scan for FOR4
            int offset = 0;
            int safety = 0;
            while (offset <= bytes.Length - 12 && idx.Chunks.Count < maxChunks && safety++ < 2000000)
            {
                if (Read4CC(bytes, offset) == "FOR4" && TryReadChunk(bytes, offset, out var c))
                {
                    ParseChunkRecursive(bytes, idx, c, depth: 0, maxChunks: maxChunks);
                    offset = c.NextOffsetAligned;
                }
                else offset += 1;
            }

            return idx;
        }

        private struct Chunk
        {
            public string Id;
            public int Offset;
            public int Size;
            public int DataOffset;
            public int DataSize;
            public bool IsContainer;
            public string FormType;
            public int NextOffsetAligned;
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

            int childOffset = c.DataOffset + 4;
            int end = c.DataOffset + c.DataSize;
            if (childOffset < 0 || end > bytes.Length) return;

            while (childOffset + 8 <= end && idx.Chunks.Count < maxChunks)
            {
                if (!TryReadChunk(bytes, childOffset, out var child)) break;
                if (child.Offset < c.DataOffset || child.NextOffsetAligned > end) break;

                ParseChunkRecursive(bytes, idx, child, depth + 1, maxChunks);

                if (child.NextOffsetAligned <= childOffset) break;
                childOffset = child.NextOffsetAligned;
            }
        }

        private static void TryDecodeLeaf(byte[] bytes, MayaBinaryIndex.ChunkInfo info, MayaBinaryIndex idx)
        {
            var tag = info.Id;
            var kind = MayaBinaryDecoders.GetKindForTag(tag);

            // 1) Known tag kind decode
            if (kind != MayaBinaryDecoders.Kind.Unknown &&
                MayaBinaryDecoders.TryDecode(kind, bytes, info.DataOffset, info.DataSize, out var strings, out var uints, out var floats))
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
                        return strings.Count == 1 ? $"stringZ: {TrimPreview(strings[0])}" : $"stringZ[{strings.Count}]: {TrimPreview(strings[0])} ...";

                    case MayaBinaryDecoders.Kind.UInt32BE:
                        if (uints == null || uints.Count == 0) return "uint32: (empty)";
                        return uints.Count == 1 ? $"uint32: {uints[0]}" : $"uint32[{uints.Count}]: {uints[0]} ...";

                    case MayaBinaryDecoders.Kind.Float32BE:
                        if (floats == null || floats.Count == 0) return "float32: (empty)";
                        return floats.Count == 1 ? $"float32: {floats[0]}" : $"float32[{floats.Count}]: {floats[0]} ...";

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

        // ---- IFF low-level ----

        private static bool TryReadChunk(byte[] bytes, int offset, out Chunk c)
        {
            c = default;
            if (bytes == null) return false;
            if (offset < 0 || offset + 8 > bytes.Length) return false;

            string id = Read4CC(bytes, offset);
            int size = ReadBE32(bytes, offset + 4);

            int dataOffset = offset + 8;
            int dataSize = size;

            int next = dataOffset + dataSize;
            if (next < 0 || next > bytes.Length) return false;

            bool isContainer = Containers.Contains(id);
            string formType = null;

            if (isContainer)
            {
                if (dataOffset + 4 > bytes.Length) return false;
                formType = Read4CC(bytes, dataOffset);
            }

            int aligned = next;
            int align = (id == "FOR4" || id == "CAT4") ? 4 : 2;
            int mod = aligned % align;
            if (mod != 0) aligned += (align - mod);

            c = new Chunk
            {
                Id = id,
                Offset = offset,
                Size = size,
                DataOffset = dataOffset,
                DataSize = dataSize,
                IsContainer = isContainer,
                FormType = formType,
                NextOffsetAligned = aligned
            };
            return true;
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

        // ---- fallback token extraction ----

        private static void ExtractAsciiTokens(byte[] bytes, int start, int length, List<string> outList, int limitTotal)
        {
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
