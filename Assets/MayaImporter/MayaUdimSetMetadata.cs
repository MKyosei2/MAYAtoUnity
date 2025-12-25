using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase B-5:
    /// UnityにはUDIMの概念が無いので “新規コンポーネントで保持” する。
    /// これにより「データ欠損ゼロ」を満たしつつ、表示はプレビューとして1001等を割り当てる。
    ///
    /// - udimPattern: Maya側の fileTextureName パターン（<UDIM>等）
    /// - tilePaths: 解決できたタイル一覧（Assetsパス or 絶対パス）
    /// - previewTileUdim: プレビューに使ったUDIM番号（通常1001）
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaUdimSetMetadata : MonoBehaviour
    {
        [TextArea] public string udimPattern;
        public string[] tilePaths;
        public int previewTileUdim;
    }
}
