using System;
using System.IO;
using System.Text;

namespace MayaImporter.Core
{
    /// <summary>
    /// Maya Binary (.mb) 用の低レベル Binary Reader。
    ///
    /// ・IFF (Interchange File Format) ベース
    /// ・FORM / chunk / size 構造対応
    /// ・Big Endian / Little Endian 自動切替
    /// ・Maya / Autodesk API 完全不使用
    ///
    /// このクラスは「読むだけ」。
    /// Maya 固有の意味解釈は MayaBinaryChunkParser に委ねる。
    /// </summary>
    public sealed class MayaBinaryReader : IDisposable
    {
        private readonly BinaryReader _reader;
        private bool _bigEndian;

        /// <summary>
        /// 現在のストリーム位置
        /// </summary>
        public long Position => _reader.BaseStream.Position;

        /// <summary>
        /// ストリーム長
        /// </summary>
        public long Length => _reader.BaseStream.Length;

        /// <summary>
        /// Big Endian かどうか
        /// </summary>
        public bool IsBigEndian => _bigEndian;

        public MayaBinaryReader(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            _reader = new BinaryReader(stream);
            DetectEndian();
        }

        #region Endian

        /// <summary>
        /// Maya Binary のヘッダから Endian を判定
        /// </summary>
        private void DetectEndian()
        {
            // Maya Binary は先頭に FORM
            var pos = _reader.BaseStream.Position;
            var tag = ReadFourCCInternal(false);

            if (tag != "FORM")
                throw new InvalidDataException("Not a valid Maya Binary (.mb) file.");

            // 次の uint32 が chunk size
            // Big Endian なら巨大な値になるため判定できる
            var sizeLE = ReadUInt32Internal(false);
            var sizeBE = SwapUInt32(sizeLE);

            // Maya ファイルサイズと比較して妥当な方を採用
            var remaining = _reader.BaseStream.Length - _reader.BaseStream.Position;

            _bigEndian =
                sizeBE <= remaining && sizeBE > 0
                    ? true
                    : false;

            // 巻き戻す
            _reader.BaseStream.Position = pos;
        }

        #endregion


        #region Read Primitive

        public string ReadFourCC()
        {
            return ReadFourCCInternal(_bigEndian);
        }

        public uint ReadUInt32()
        {
            return ReadUInt32Internal(_bigEndian);
        }

        public int ReadInt32()
        {
            var v = _reader.ReadInt32();
            return _bigEndian ? SwapInt32(v) : v;
        }

        public float ReadFloat()
        {
            var bytes = _reader.ReadBytes(4);
            if (_bigEndian)
                Array.Reverse(bytes);

            return BitConverter.ToSingle(bytes, 0);
        }

        public double ReadDouble()
        {
            var bytes = _reader.ReadBytes(8);
            if (_bigEndian)
                Array.Reverse(bytes);

            return BitConverter.ToDouble(bytes, 0);
        }

        public byte ReadByte()
        {
            return _reader.ReadByte();
        }

        public byte[] ReadBytes(int count)
        {
            return _reader.ReadBytes(count);
        }

        #endregion


        #region Read String

        /// <summary>
        /// Maya Binary で使われる length-prefixed string
        /// </summary>
        public string ReadString()
        {
            var length = ReadUInt32();
            if (length == 0)
                return string.Empty;

            var bytes = _reader.ReadBytes((int)length);

            // null 終端を除去
            if (bytes.Length > 0 && bytes[^1] == 0)
                length--;

            return Encoding.UTF8.GetString(bytes, 0, (int)length);
        }

        #endregion


        #region Chunk Helpers

        /// <summary>
        /// チャンクをスキップ（未知チャンク用）
        /// </summary>
        public void Skip(long size)
        {
            if (size <= 0)
                return;

            _reader.BaseStream.Seek(size, SeekOrigin.Current);
        }

        /// <summary>
        /// チャンク終端までスキップ
        /// </summary>
        public void SkipTo(long endPosition)
        {
            if (endPosition <= Position)
                return;

            _reader.BaseStream.Seek(endPosition - Position, SeekOrigin.Current);
        }

        #endregion


        #region Internal Low-Level

        private string ReadFourCCInternal(bool bigEndian)
        {
            var bytes = _reader.ReadBytes(4);
            if (bytes.Length < 4)
                throw new EndOfStreamException();

            return Encoding.ASCII.GetString(bytes);
        }

        private uint ReadUInt32Internal(bool bigEndian)
        {
            var v = _reader.ReadUInt32();
            return bigEndian ? SwapUInt32(v) : v;
        }

        private static uint SwapUInt32(uint v)
        {
            return (v >> 24) |
                   ((v >> 8) & 0x0000FF00) |
                   ((v << 8) & 0x00FF0000) |
                   (v << 24);
        }

        private static int SwapInt32(int v)
        {
            unchecked
            {
                return (int)SwapUInt32((uint)v);
            }
        }

        #endregion


        #region IDisposable

        public void Dispose()
        {
            _reader?.Dispose();
        }

        #endregion
    }
}
