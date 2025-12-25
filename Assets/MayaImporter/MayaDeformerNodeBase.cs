using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Deformer ノード共通基底。
    /// SerializedAttribute の内部構造に依存しない。
    /// </summary>
    public abstract class MayaDeformerNodeBase : MayaNodeComponentBase
    {
        [Header("Maya Deformer Common")]
        [SerializeField] protected string inputGeometry;
        [SerializeField] protected string outputGeometry;

        [SerializeField] protected float envelope = 1.0f;

        protected DGConnectionResolver connectionResolver;

        /// <summary>
        /// Deformer 共通初期化（Phase1: 再構築）
        /// Attribute 名には依存せず、最初に読める float を envelope として扱う。
        /// </summary>
        protected void InitializeDeformerCommon()
        {
            if (Attributes == null) return;

            for (int i = 0; i < Attributes.Count; i++)
            {
                var attr = Attributes[i];
                if (AttributeTypeResolver.TryReadFloat(attr, out var v))
                {
                    envelope = v;
                    break;
                }
            }
        }

        /// <summary>
        /// 接続解決（DG）
        /// </summary>
        protected void ResolveConnections(MayaSceneData scene)
        {
            if (scene == null) return;
            connectionResolver = new DGConnectionResolver(scene);
            ResolveGeometryConnections();
        }

        protected virtual void ResolveGeometryConnections()
        {
            if (connectionResolver == null || string.IsNullOrEmpty(NodeName))
                return;

            var incoming = connectionResolver.GetIncomingToNode(NodeName);
            if (incoming == null) return;

            foreach (var c in incoming)
            {
                if (c == null) continue;

                if (c.DstPlug.Contains("input"))
                    inputGeometry = c.SrcPlug;
                else if (c.DstPlug.Contains("output"))
                    outputGeometry = c.SrcPlug;
            }
        }

        public override string ToString()
        {
            return $"{NodeType} (envelope={envelope}, input={inputGeometry})";
        }
    }
}
