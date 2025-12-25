using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Maya Binary (.mb) の IFF Chunk を解析し、
    /// Unity 再構築用の「意味データ」を抽出するクラス。
    ///
    /// MayaBinaryReader = 生データ
    /// MayaBinaryChunkParser = 意味構造
    /// </summary>
    public sealed class MayaBinaryChunkParser
    {
        private readonly MayaBinaryReader _reader;

        // ===============================
        // 結果データ
        // ===============================
        public readonly List<MayaBinaryNode> Nodes = new();
        public readonly List<MayaBinaryConnection> Connections = new();

        public MayaBinaryChunkParser(MayaBinaryReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        /// <summary>
        /// パース開始（FORM 直下から）
        /// </summary>
        public void Parse()
        {
            var form = _reader.ReadFourCC();
            if (form != "FORM")
                throw new InvalidDataException("Invalid Maya Binary file (FORM not found)");

            var formSize = _reader.ReadUInt32();
            var end = _reader.Position + formSize;

            while (_reader.Position < end)
            {
                ParseChunk();
            }
        }

        #region Chunk Parse

        private void ParseChunk()
        {
            var chunkId = _reader.ReadFourCC();
            var size = _reader.ReadUInt32();
            var end = _reader.Position + size;

            switch (chunkId)
            {
                case "FORM":
                    ParseForm(end);
                    break;

                case "NODE":
                    ParseNode(end);
                    break;

                case "ATTR":
                    ParseAttribute(end);
                    break;

                case "CONN":
                    ParseConnection(end);
                    break;

                default:
                    // 未知チャンクは完全スキップ
                    _reader.SkipTo(end);
                    break;
            }
        }

        private void ParseForm(long end)
        {
            // FORM の中身はさらにチャンクの塊
            while (_reader.Position < end)
            {
                ParseChunk();
            }
        }

        #endregion


        #region NODE

        private void ParseNode(long end)
        {
            // Maya node 構造（最低限）
            // name (string)
            // type (string)

            var node = new MayaBinaryNode
            {
                Name = _reader.ReadString(),
                Type = _reader.ReadString()
            };

            Nodes.Add(node);

            // 残りは未知データとしてスキップ
            _reader.SkipTo(end);
        }

        #endregion


        #region ATTR

        private void ParseAttribute(long end)
        {
            // 対象ノード名
            var nodeName = _reader.ReadString();
            var attrName = _reader.ReadString();

            var typeTag = _reader.ReadFourCC();

            object value = null;

            switch (typeTag)
            {
                case "INT ":
                    value = _reader.ReadInt32();
                    break;

                case "FLT ":
                    value = _reader.ReadFloat();
                    break;

                case "DBL ":
                    value = _reader.ReadDouble();
                    break;

                case "STR ":
                    value = _reader.ReadString();
                    break;

                case "VEC2":
                    value = new Vector2(_reader.ReadFloat(), _reader.ReadFloat());
                    break;

                case "VEC3":
                    value = new Vector3(
                        _reader.ReadFloat(),
                        _reader.ReadFloat(),
                        _reader.ReadFloat());
                    break;

                case "VEC4":
                    value = new Vector4(
                        _reader.ReadFloat(),
                        _reader.ReadFloat(),
                        _reader.ReadFloat(),
                        _reader.ReadFloat());
                    break;

                default:
                    // 不明型は raw bytes として保持
                    var remaining = (int)(end - _reader.Position);
                    value = _reader.ReadBytes(remaining);
                    break;
            }

            var node = FindNode(nodeName);
            if (node != null)
            {
                node.Attributes[attrName] = value;
            }

            _reader.SkipTo(end);
        }

        #endregion


        #region CONN

        private void ParseConnection(long end)
        {
            // srcNode.srcAttr -> dstNode.dstAttr
            var srcNode = _reader.ReadString();
            var srcAttr = _reader.ReadString();
            var dstNode = _reader.ReadString();
            var dstAttr = _reader.ReadString();

            Connections.Add(new MayaBinaryConnection
            {
                SourceNode = srcNode,
                SourceAttribute = srcAttr,
                DestinationNode = dstNode,
                DestinationAttribute = dstAttr
            });

            _reader.SkipTo(end);
        }

        #endregion


        #region Utilities

        private MayaBinaryNode FindNode(string name)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Name == name)
                    return Nodes[i];
            }

            return null;
        }

        #endregion
    }

    #region Data Containers

    /// <summary>
    /// Maya Binary Node 情報
    /// </summary>
    public sealed class MayaBinaryNode
    {
        public string Name;
        public string Type;

        public Dictionary<string, object> Attributes =
            new Dictionary<string, object>();
    }

    /// <summary>
    /// Maya Binary Connection 情報
    /// </summary>
    public sealed class MayaBinaryConnection
    {
        public string SourceNode;
        public string SourceAttribute;
        public string DestinationNode;
        public string DestinationAttribute;
    }

    #endregion
}
