// MAYAIMPORTER_PATCH_V5: fix MayaBinaryReader endian detection for modern .mb headers (FOR4/FOR8) (Unity-only)
// MayaImporter/MayaBinaryReader.cs
//
// Note:
// The current importer pipeline primarily uses MayaMbIffReader + heuristic extractors.
// This reader is kept as a low-level utility and should NOT depend on Autodesk/Maya APIs.

using System;
using System.IO;
using System.Text;

namespace MayaImporter.Core
{
    /// <summary>
    /// Minimal binary reader with optional big-endian support.
    /// Maya .mb is an IFF-style container and typically uses big-endian for numeric fields.
    /// Root tags commonly seen: FOR4 / FOR8 (and occasionally FORM in classic IFF contexts).
    /// </summary>
    public sealed class MayaBinaryReader : IDisposable
    {
        private readonly BinaryReader _reader;
        private bool _bigEndian;

        public long Position => _reader.BaseStream.Position;
        public long Length => _reader.BaseStream.Length;
        public bool IsBigEndian => _bigEndian;

        public MayaBinaryReader(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            _reader = new BinaryReader(stream);
            DetectEndian();
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }

        private void DetectEndian()
        {
            // IFF-like containers used by Maya are effectively "big-endian" for size fields.
            // We still do a plausibility check to avoid false positives when the stream isn't a Maya file.
            long pos = _reader.BaseStream.Position;

            string tag = ReadFourCCInternal(bigEndian: false);
            if (string.IsNullOrEmpty(tag))
                throw new InvalidDataException("Stream too small for a Maya binary header.");

            // Accept common Maya/IFF top-level container tags.
            bool looksLikeMayaIff =
                tag == "FOR4" || tag == "FOR8" || tag == "FORM" ||
                tag == "CAT4" || tag == "CAT8" || tag == "CAT " ||
                tag == "LIST" || tag == "LIS4" || tag == "LIS8" ||
                tag == "PROP" || tag == "PRO4" || tag == "PRO8";

            if (!looksLikeMayaIff)
                throw new InvalidDataException($"Not a valid Maya/IFF container header. tag='{tag}'");

            // Size is 32-bit big-endian in the common IFF usage.
            uint sizeBE = ReadUInt32Internal(bigEndian: true);
            uint sizeLE = SwapUInt32(sizeBE);

            long remaining = _reader.BaseStream.Length - _reader.BaseStream.Position;

            // Choose the endian that yields a "sane" payload size.
            // Maya/IFF is big-endian, so BE should usually win.
            bool beSane = sizeBE > 0 && sizeBE <= remaining;
            bool leSane = sizeLE > 0 && sizeLE <= remaining;

            _bigEndian = beSane && !leSane ? true :
                         leSane && !beSane ? false :
                         true; // default to big-endian for Maya

            _reader.BaseStream.Position = pos;
        }

        // ---------------- read primitives ----------------

        public string ReadFourCC() => ReadFourCCInternal(_bigEndian);
        public uint ReadUInt32() => ReadUInt32Internal(_bigEndian);

        public int ReadInt32()
        {
            int v = _reader.ReadInt32();
            return _bigEndian ? SwapInt32(v) : v;
        }

        public float ReadFloat()
        {
            var bytes = _reader.ReadBytes(4);
            if (bytes.Length < 4) throw new EndOfStreamException();
            if (_bigEndian) Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        public double ReadDouble()
        {
            var bytes = _reader.ReadBytes(8);
            if (bytes.Length < 8) throw new EndOfStreamException();
            if (_bigEndian) Array.Reverse(bytes);
            return BitConverter.ToDouble(bytes, 0);
        }

        public byte ReadByte() => _reader.ReadByte();

        public byte[] ReadBytes(int count) => _reader.ReadBytes(count);

        // ---------------- strings ----------------

        /// <summary>
        /// Reads a Maya-style length-prefixed UTF-8 string.
        /// Many Maya binary chunks store strings as: uint32 byteLength, then byteLength bytes (often null-terminated).
        /// </summary>
        public string ReadString()
        {
            uint length = ReadUInt32();
            if (length == 0)
                return string.Empty;

            // Defensive: avoid allocating huge arrays on corrupted files.
            // Maya chunks shouldn't contain absurd string lengths.
            if (length > int.MaxValue)
                throw new InvalidDataException($"String length too large: {length}");

            byte[] bytes = _reader.ReadBytes((int)length);
            if (bytes.Length < (int)length)
                throw new EndOfStreamException();

            int actualLen = bytes.Length;
            if (actualLen > 0 && bytes[actualLen - 1] == 0)
                actualLen -= 1;

            return Encoding.UTF8.GetString(bytes, 0, actualLen);
        }

        public void Seek(long offset, SeekOrigin origin) => _reader.BaseStream.Seek(offset, origin);

        // ---------------- skip helpers ----------------

        public void Skip(long size)
        {
            if (size <= 0) return;
            _reader.BaseStream.Seek(size, SeekOrigin.Current);
        }

        public void SkipTo(long endPosition)
        {
            if (endPosition <= Position) return;
            _reader.BaseStream.Seek(endPosition - Position, SeekOrigin.Current);
        }

        // ---------------- internal helpers ----------------

        private string ReadFourCCInternal(bool bigEndian)
        {
            var bytes = _reader.ReadBytes(4);
            if (bytes.Length < 4) return null;

            // FourCC is byte order, not endian; do not reverse.
            return Encoding.ASCII.GetString(bytes, 0, 4);
        }

        private uint ReadUInt32Internal(bool bigEndian)
        {
            var bytes = _reader.ReadBytes(4);
            if (bytes.Length < 4) throw new EndOfStreamException();
            if (bigEndian) Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        private static uint SwapUInt32(uint v)
        {
            return (v >> 24) |
                   ((v >> 8) & 0x0000FF00u) |
                   ((v << 8) & 0x00FF0000u) |
                   (v << 24);
        }

        private static int SwapInt32(int v)
        {
            unchecked
            {
                uint u = (uint)v;
                u = SwapUInt32(u);
                return (int)u;
            }
        }
    }
}
