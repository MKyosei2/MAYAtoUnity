using System;
using System.Text;

namespace MayaImporter.Utils
{
    /// <summary>
    /// .mb（バイナリ）解析のための “APIなし” Reader。
    /// Core(.mb) 実装は後回し方針だが、ここは先に100点（読める道具）にしておく。
    /// </summary>
    public sealed class BinaryDataReader
    {
        private readonly byte[] _data;
        private int _pos;
        public bool LittleEndian { get; set; } = true;

        public int Position => _pos;
        public int Length => _data?.Length ?? 0;
        public int Remaining => Length - _pos;

        public BinaryDataReader(byte[] data, bool littleEndian = true)
        {
            _data = data ?? Array.Empty<byte>();
            _pos = 0;
            LittleEndian = littleEndian;
        }

        public void Seek(int position)
        {
            if (position < 0 || position > Length) throw new ArgumentOutOfRangeException(nameof(position));
            _pos = position;
        }

        public void Skip(int bytes)
        {
            Seek(_pos + bytes);
        }

        public byte ReadByte()
        {
            Ensure(1);
            return _data[_pos++];
        }

        public sbyte ReadSByte()
        {
            Ensure(1);
            return unchecked((sbyte)_data[_pos++]);
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            Ensure(count);
            var arr = new byte[count];
            Buffer.BlockCopy(_data, _pos, arr, 0, count);
            _pos += count;
            return arr;
        }

        public ushort ReadUInt16()
        {
            Ensure(2);
            ushort v = LittleEndian
                ? (ushort)(_data[_pos] | (_data[_pos + 1] << 8))
                : (ushort)((_data[_pos] << 8) | _data[_pos + 1]);
            _pos += 2;
            return v;
        }

        public short ReadInt16() => unchecked((short)ReadUInt16());

        public uint ReadUInt32()
        {
            Ensure(4);
            uint b0 = _data[_pos + 0];
            uint b1 = _data[_pos + 1];
            uint b2 = _data[_pos + 2];
            uint b3 = _data[_pos + 3];
            _pos += 4;

            if (LittleEndian)
                return (b0) | (b1 << 8) | (b2 << 16) | (b3 << 24);
            return (b3) | (b2 << 8) | (b1 << 16) | (b0 << 24);
        }

        public int ReadInt32() => unchecked((int)ReadUInt32());

        public ulong ReadUInt64()
        {
            Ensure(8);
            ulong b0 = _data[_pos + 0];
            ulong b1 = _data[_pos + 1];
            ulong b2 = _data[_pos + 2];
            ulong b3 = _data[_pos + 3];
            ulong b4 = _data[_pos + 4];
            ulong b5 = _data[_pos + 5];
            ulong b6 = _data[_pos + 6];
            ulong b7 = _data[_pos + 7];
            _pos += 8;

            if (LittleEndian)
                return (b0) | (b1 << 8) | (b2 << 16) | (b3 << 24) | (b4 << 32) | (b5 << 40) | (b6 << 48) | (b7 << 56);
            return (b7) | (b6 << 8) | (b5 << 16) | (b4 << 24) | (b3 << 32) | (b2 << 40) | (b1 << 48) | (b0 << 56);
        }

        public long ReadInt64() => unchecked((long)ReadUInt64());

        public float ReadSingle()
        {
            var u = ReadUInt32();
            return BitConverter.Int32BitsToSingle(unchecked((int)u));
        }

        public double ReadDouble()
        {
            var u = ReadUInt64();
            return BitConverter.Int64BitsToDouble(unchecked((long)u));
        }

        /// <summary>
        /// 7bit 可変長（BinaryReader 互換）
        /// </summary>
        public int Read7BitEncodedInt()
        {
            int count = 0;
            int shift = 0;
            while (true)
            {
                if (shift >= 35) throw new FormatException("7-bit encoded int is too large.");
                byte b = ReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
                if ((b & 0x80) == 0) break;
            }
            return count;
        }

        public string ReadUtf8StringWith7BitLength()
        {
            int len = Read7BitEncodedInt();
            if (len == 0) return string.Empty;
            var bytes = ReadBytes(len);
            return Encoding.UTF8.GetString(bytes);
        }

        public string ReadCString()
        {
            // '\0' まで
            int start = _pos;
            while (_pos < Length && _data[_pos] != 0) _pos++;
            int len = _pos - start;
            if (_pos < Length && _data[_pos] == 0) _pos++; // consume null
            if (len <= 0) return string.Empty;
            return Encoding.UTF8.GetString(_data, start, len);
        }

        private void Ensure(int count)
        {
            if (_pos + count > Length)
                throw new IndexOutOfRangeException($"BinaryDataReader: need {count} bytes but remaining {Remaining}.");
        }
    }
}
