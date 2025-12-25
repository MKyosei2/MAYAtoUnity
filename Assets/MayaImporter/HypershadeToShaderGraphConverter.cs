using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Shader
{
    /// <summary>
    /// Maya Hypershade Network を
    /// Unity 用の「Shader / Material 再構築用 中間データ」に変換するクラス。
    ///
    /// 役割:
    /// ・ShaderNetwork を解析
    /// ・最終的な Material 出力ノードを特定
    /// ・ノード接続グラフをトラバース可能な構造に変換
    ///
    /// ※ ShaderGraph / Material の実生成は
    ///   ShaderGraphBuilder / ShaderGraphExporter に委ねる
    /// </summary>
    public static class HypershadeToShaderGraphConverter
    {
        /// <summary>
        /// ShaderNetwork から ShaderBuildContext を生成
        /// </summary>
        public static ShaderBuildContext Convert(ShaderNetwork network)
        {
            var context = new ShaderBuildContext();

            if (network == null || network.Nodes.Count == 0)
                return context;

            // ===============================
            // 1. 出力ノード（Material）検出
            // ===============================
            foreach (var node in network.Nodes.Values)
            {
                if (IsMaterialOutputNode(node))
                {
                    context.OutputNodes.Add(node);
                }
            }

            // フォールバック（全部 Unknown の場合など）
            if (context.OutputNodes.Count == 0)
            {
                foreach (var node in network.Nodes.Values)
                {
                    context.OutputNodes.Add(node);
                }
            }

            // ===============================
            // 2. グラフ登録
            // ===============================
            foreach (var node in network.Nodes.Values)
            {
                var info = new ShaderNodeInfo
                {
                    Node = node,
                    Inputs = node.InputConnections,
                    Outputs = node.OutputConnections
                };

                context.AllNodes.Add(node, info);
            }

            return context;
        }

        #region Utilities

        /// <summary>
        /// Maya 的に「Material 出力」とみなせるノード判定
        /// </summary>
        private static bool IsMaterialOutputNode(ShaderNodeComponentBase node)
        {
            if (node == null)
                return false;

            var type = node.MayaNodeType;

            switch (type)
            {
                case "lambert":
                case "phong":
                case "phongE":
                case "blinn":
                case "surfaceShader":
                case "aiStandardSurface":
                    return true;
            }

            return false;
        }

        #endregion
    }

    #region Build Context Data Structures

    /// <summary>
    /// Shader / Material 再構築用の全体コンテキスト
    /// </summary>
    public sealed class ShaderBuildContext
    {
        /// <summary>
        /// Material 出力ノード（複数可）
        /// </summary>
        public readonly List<ShaderNodeComponentBase> OutputNodes =
            new List<ShaderNodeComponentBase>();

        /// <summary>
        /// ネットワーク内の全ノード情報
        /// </summary>
        public readonly Dictionary<ShaderNodeComponentBase, ShaderNodeInfo> AllNodes =
            new Dictionary<ShaderNodeComponentBase, ShaderNodeInfo>();
    }

    /// <summary>
    /// 1ノード分の接続情報
    /// </summary>
    public sealed class ShaderNodeInfo
    {
        public ShaderNodeComponentBase Node;

        /// <summary>
        /// InputPort → 接続元ノード
        /// </summary>
        public Dictionary<string, ShaderNodeComponentBase> Inputs;

        /// <summary>
        /// OutputPort → 接続先ノード一覧
        /// </summary>
        public Dictionary<string, List<ShaderNodeComponentBase>> Outputs;
    }

    #endregion
}
