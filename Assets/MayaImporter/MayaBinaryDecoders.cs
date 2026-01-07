// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Text;

namespace MayaImporter.Core
{
    /// <summary>
    /// Safe best-effort decoders for chunk payloads.
    /// </summary>
    public static class MayaBinaryDecoders
    {
        public enum Kind
        {
            Unknown = 0,
            StringZ = 1,
            UInt32BE = 2,
            Float32BE = 3
        }

        /// <summary>
        /// Tag -> preferred decode kind (NOT exhaustive; purely hint table).
        /// </summary>
        public static readonly Dictionary<string, Kind> TagEncoding = new Dictionary<string, Kind>(StringComparer.Ordinal)
        {
            { "INFO", Kind.StringZ },
            { "VERS", Kind.StringZ },
            { "MADE", Kind.StringZ },
            { "CHNG", Kind.StringZ },

            { "ETIM", Kind.UInt32BE },
            { "STIM", Kind.UInt32BE },

            { "CHNM", Kind.StringZ },
            { "NAME", Kind.StringZ },
            { "SIZE", Kind.UInt32BE },
        };

        public static Kind GetKindForTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return Kind.Unknown;
            return TagEncoding.TryGetValue(tag, out var k) ? k : Kind.Unknown;
        }

        public static bool TryDecode(Kind kind, byte[] bytes, int dataOffset, int dataSize,
            out List<string> strings, out List<uint> uints, out List<float> floats)
        {
            strings = null;
            uints = null;
            floats = null;

            if (!IsRangeValid(bytes, dataOffset, dataSize))
                return false;

            switch (kind)
            {
                case Kind.StringZ:
                    return TryDecodeStringZ(bytes, dataOffset, dataSize, out strings);

                case Kind.UInt32BE:
                    return TryDecodeUInt32BE(bytes, dataOffset, dataSize, out uints);

                case Kind.Float32BE:
                    return TryDecodeFloat32BE(bytes, dataOffset, dataSize, out floats);

                default:
                    return false;
            }
        }

        public static bool TryGuessStringZ(byte[] bytes, int dataOffset, int dataSize, out List<string> strings)
        {
            strings = null;
            if (!IsRangeValid(bytes, dataOffset, dataSize)) return false;

            int end = dataOffset + dataSize;

            bool hasNull = false;
            int printable = 0;
            int checkedBytes = 0;

            int sampleEnd = Math.Min(end, dataOffset + 512);
            for (int i = dataOffset; i < sampleEnd; i++)
            {
                byte b = bytes[i];
                checkedBytes++;
                if (b == 0) hasNull = true;
                if (IsPrintableOrNull(b)) printable++;
            }

            if (!hasNull) return false;

            float ratio = checkedBytes > 0 ? (float)printable / checkedBytes : 0f;
            if (ratio < 0.65f) return false;

            return TryDecodeStringZ(bytes, dataOffset, dataSize, out strings);
        }

        public static bool TryGuessFloat32BE(byte[] bytes, int dataOffset, int dataSize, out List<float> floats)
        {
            floats = null;
            if (!IsRangeValid(bytes, dataOffset, dataSize)) return false;
            if (dataSize < 12) return false;              // need at least 3 floats for TRS usefulness
            if ((dataSize % 4) != 0) return false;

            int count = Math.Min(dataSize / 4, 1024);

            int finite = 0;
            int inRange = 0;

            // sample up to first 64 floats
            int sampleCount = Math.Min(count, 64);
            for (int i = 0; i < sampleCount; i++)
            {
                float f = ReadFloat32BE(bytes, dataOffset + i * 4);
                if (!float.IsNaN(f) && !float.IsInfinity(f))
                {
                    finite++;
                    if (Math.Abs(f) <= 1_000_000f) inRange++;
                }
            }

            if (finite < (int)(sampleCount * 0.85f)) return false;
            if (inRange < (int)(sampleCount * 0.70f)) return false;

            return TryDecodeFloat32BE(bytes, dataOffset, dataSize, out floats);
        }

        private static bool TryDecodeStringZ(byte[] bytes, int dataOffset, int dataSize, out List<string> strings)
        {
            strings = new List<string>(8);

            int end = dataOffset + dataSize;
            int i = dataOffset;

            while (i < end)
            {
                while (i < end && bytes[i] == 0) i++;
                if (i >= end) break;

                int start = i;
                while (i < end && bytes[i] != 0) i++;

                int len = i - start;
                if (len > 0)
                {
                    int take = Math.Min(len, 1024);
                    var s = Encoding.ASCII.GetString(bytes, start, take).Trim();
                    if (!string.IsNullOrEmpty(s))
                        strings.Add(s);
                }

                i++; // skip null
                if (strings.Count >= 256) break;
            }

            return strings.Count > 0;
        }

        private static bool TryDecodeUInt32BE(byte[] bytes, int dataOffset, int dataSize, out List<uint> values)
        {
            values = null;
            if (dataSize < 4) return false;

            int count = dataSize / 4;
            values = new List<uint>(Math.Min(count, 256));

            for (int i = 0; i < count; i++)
            {
                int o = dataOffset + i * 4;
                uint v = (uint)((bytes[o] << 24) | (bytes[o + 1] << 16) | (bytes[o + 2] << 8) | bytes[o + 3]);
                values.Add(v);
                if (values.Count >= 1024) break;
            }

            return values.Count > 0;
        }

        private static bool TryDecodeFloat32BE(byte[] bytes, int dataOffset, int dataSize, out List<float> values)
        {
            values = null;
            if (dataSize < 4) return false;
            if ((dataSize % 4) != 0) return false;

            int count = dataSize / 4;
            values = new List<float>(Math.Min(count, 256));

            for (int i = 0; i < count; i++)
            {
                float f = ReadFloat32BE(bytes, dataOffset + i * 4);
                if (float.IsNaN(f) || float.IsInfinity(f)) f = 0f;
                values.Add(f);
                if (values.Count >= 4096) break;
            }

            return values.Count > 0;
        }

        private static float ReadFloat32BE(byte[] bytes, int o)
        {
            uint u = (uint)((bytes[o] << 24) | (bytes[o + 1] << 16) | (bytes[o + 2] << 8) | bytes[o + 3]);
            var le = BitConverter.GetBytes(u); // u is now host-endian integer; bytes are LE on typical platforms
            return BitConverter.ToSingle(le, 0);
        }

        private static bool IsPrintableOrNull(byte b) => (b == 0) || (b >= 32 && b <= 126);

        private static bool IsRangeValid(byte[] bytes, int dataOffset, int dataSize)
        {
            return bytes != null &&
                   dataOffset >= 0 &&
                   dataSize > 0 &&
                   dataOffset + dataSize <= bytes.Length;
        }
    }
}
