// MAYAIMPORTER_PATCH_V4_FIXED: restored encoding-safe file
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// 「100%保持」は維持したまま、Unity側の“再構築”だけを選別する設定。
    /// - 生データ（Raw / SceneData / Connections）は常に保持（=100%）
    /// - ここは「GameObject化/Asset化するか」を制御
    /// </summary>
    [Serializable]
    public sealed class MayaReconstructionSelection
    {
        [Tooltip("ONの時だけ、再構築の選別を有効にします。")]
        public bool Enabled = false;

        [Header("Rebuild Targets")]
        [Tooltip("OFFにすると、階層(GameObject)自体を生成しません（Raw/SceneDataは保持）。")]
        public bool ReconstructGameObjects = true;

        [Tooltip("Mesh系ノードの再構築を行う")]
        public bool ImportMeshes = true;

        [Tooltip("Material/Shader系ノードの再構築を行う")]
        public bool ImportMaterials = true;

        [Tooltip("Texture系ノードの再構築を行う")]
        public bool ImportTextures = true;

        [Tooltip("AnimationClip の生成/再構築を行う")]
        public bool ImportAnimationClip = true;

        [Header("Name-based Exclusion")]
        [Tooltip("ONの時だけ、ExcludedNodeNames を使って再構築を除外します。")]
        public bool ExcludeByNameEnabled = true;

        [Tooltip("ここに入っている Mayaノード名(フル名) は再構築しません。")]
        public List<string> ExcludedNodeNames = new List<string>();

        [Tooltip("ONなら『除外されたノードの子孫も除外』として扱います（推奨）。")]
        public bool TreatExcludedAsSubtree = true;

        [Header("NodeType-based Exclusion (optional)")]
        [Tooltip("ONの時だけ、ExcludedNodeTypes を使って再構築を除外します。")]
        public bool ExcludeByNodeTypeEnabled = false;

        [Tooltip("ここに入っている nodeType は再構築しません。")]
        public List<string> ExcludedNodeTypes = new List<string>();

        [NonSerialized] private HashSet<string> _excludedNamesSet;
        [NonSerialized] private HashSet<string> _excludedTypesSet;

        public void InvalidateCache()
        {
            _excludedNamesSet = null;
            _excludedTypesSet = null;
        }

        public bool IsNodeNameExcluded(string name)
        {
            if (!ExcludeByNameEnabled) return false;
            if (string.IsNullOrEmpty(name)) return false;

            _excludedNamesSet ??= new HashSet<string>(ExcludedNodeNames ?? new List<string>(), StringComparer.Ordinal);
            return _excludedNamesSet.Contains(name);
        }

        public bool IsNodeTypeExcluded(string nodeType)
        {
            if (!ExcludeByNodeTypeEnabled) return false;
            if (string.IsNullOrEmpty(nodeType)) return false;

            _excludedTypesSet ??= new HashSet<string>(ExcludedNodeTypes ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            return _excludedTypesSet.Contains(nodeType);
        }
    }

    /// <summary>
    /// Import実行中だけ選別設定を参照できるようにする（引数追加での大改修を避ける）。
    /// </summary>
    public static class MayaReconstructionSelectionContext
    {
        [ThreadStatic] private static Stack<MayaReconstructionSelection> _stack;

        public static MayaReconstructionSelection Current
            => (_stack != null && _stack.Count > 0) ? _stack.Peek() : null;

        public static IDisposable Push(MayaReconstructionSelection selection)
        {
            _stack ??= new Stack<MayaReconstructionSelection>(4);
            _stack.Push(selection);
            return new PopToken();
        }

        private sealed class PopToken : IDisposable
        {
            private bool _disposed;
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                if (_stack == null || _stack.Count == 0) return;
                _stack.Pop();
            }
        }
    }
}
