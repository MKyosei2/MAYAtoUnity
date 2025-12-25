using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Shader
{
    /// <summary>
    /// Maya Hypershade ネットワークを Unity 上で再構築するための解決器。
    ///
    /// 役割:
    ///  - Maya ノード → Unity GameObject + ShaderNodeComponent
    ///  - plug 接続（src.attr -> dst.attr）を
    ///    InputConnections / OutputConnections として完全再構築
    ///
    /// ※ 実際の Material / Shader 生成は別クラスで行う
    /// </summary>
    public static class ShaderNetworkResolver
    {
        /// <summary>
        /// Hypershade ネットワークを解決するメインエントリ
        /// </summary>
        public static ShaderNetwork Resolve(
            IEnumerable<MayaShaderNodeData> mayaNodes,
            IEnumerable<MayaShaderConnectionData> connections,
            Transform parent = null)
        {
            var network = new ShaderNetwork();

            // ===============================
            // 1. ノード生成
            // ===============================
            foreach (var mayaNode in mayaNodes)
            {
                var go = new GameObject($"ShaderNode_{mayaNode.Name}");
                if (parent != null)
                    go.transform.SetParent(parent, false);

                var component =
                    ShaderNodeMapper.AttachComponent(go, mayaNode.NodeType);

                component.Attributes = mayaNode.Attributes != null
                    ? new Dictionary<string, object>(mayaNode.Attributes)
                    : new Dictionary<string, object>();

                network.Nodes.Add(mayaNode.Name, component);
            }

            // ===============================
            // 2. 接続解決
            // ===============================
            foreach (var conn in connections)
            {
                if (!network.Nodes.TryGetValue(conn.SourceNode, out var src))
                {
                    Debug.LogWarning(
                        $"[ShaderNetworkResolver] Source node not found: {conn.SourceNode}");
                    continue;
                }

                if (!network.Nodes.TryGetValue(conn.DestinationNode, out var dst))
                {
                    Debug.LogWarning(
                        $"[ShaderNetworkResolver] Destination node not found: {conn.DestinationNode}");
                    continue;
                }

                var outPort =
                    ShaderPortMapper.GetOutputPortName(conn.SourceAttribute);
                var inPort =
                    ShaderPortMapper.GetInputPortName(conn.DestinationAttribute);

                // ---- Input 接続
                dst.InputConnections[inPort] = src;

                // ---- Output 接続
                if (!src.OutputConnections.TryGetValue(outPort, out var list))
                {
                    list = new List<ShaderNodeComponentBase>();
                    src.OutputConnections[outPort] = list;
                }

                if (!list.Contains(dst))
                    list.Add(dst);
            }

            return network;
        }
    }

    #region Network Container

    /// <summary>
    /// Unity 上で再構築された Shader ネットワーク全体
    /// </summary>
    public sealed class ShaderNetwork
    {
        /// <summary>
        /// Maya ノード名 → ShaderNodeComponent
        /// </summary>
        public readonly Dictionary<string, ShaderNodeComponentBase> Nodes =
            new Dictionary<string, ShaderNodeComponentBase>();
    }

    #endregion


    #region Maya Parsed Data Structures

    /// <summary>
    /// .ma / .mb 解析後の Maya Shader ノード情報
    /// </summary>
    public sealed class MayaShaderNodeData
    {
        public string Name;
        public string NodeType;
        public Dictionary<string, object> Attributes;
    }

    /// <summary>
    /// Maya Hypershade の plug 接続情報
    ///
    /// 例:
    ///   file1.outColor → lambert1.color
    /// </summary>
    public sealed class MayaShaderConnectionData
    {
        public string SourceNode;
        public string SourceAttribute;
        public string DestinationNode;
        public string DestinationAttribute;
    }

    #endregion
}
