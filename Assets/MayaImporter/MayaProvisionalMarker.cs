using System;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase-7:
    /// 「仮(=best-effort / fallback / heuristic) が使われた」ことを Unity 側で可視化・集計するためのマーカー。
    /// 100%保持は成立している前提で、「100点(再構築)」に向けた本実装優先順位付けに使う。
    ///
    /// - Maya/Autodesk API 不要
    /// - 複数種類を同一 GameObject に付けられる(複数コンポーネント可)
    /// </summary>
    public sealed class MayaProvisionalMarker : MonoBehaviour
    {
        [Tooltip("分類キー (MayaProvisionalKind.* を推奨)")]
        public string kind;

        [Tooltip("補足情報 (短め推奨)")]
        [TextArea]
        public string details;

        [Tooltip("同一 kind の Ensure 呼び出し回数 (同一GO内)")]
        public int count = 1;

        /// <summary>
        /// 既に同種(kind)が存在すれば count++、無ければ追加する。
        /// </summary>
        public static MayaProvisionalMarker Ensure(GameObject go, string kind, string details = null)
        {
            if (go == null) return null;
            if (string.IsNullOrEmpty(kind)) kind = "(null-kind)";

            try
            {
                var existing = go.GetComponents<MayaProvisionalMarker>();
                for (int i = 0; i < existing.Length; i++)
                {
                    var m = existing[i];
                    if (m == null) continue;
                    if (string.Equals(m.kind, kind, StringComparison.OrdinalIgnoreCase))
                    {
                        m.count = Mathf.Max(1, m.count + 1);

                        // details は最初の情報を保持し、空なら埋める（決定性を崩しにくい）
                        if (string.IsNullOrEmpty(m.details) && !string.IsNullOrEmpty(details))
                            m.details = details;

                        return m;
                    }
                }

                var added = go.AddComponent<MayaProvisionalMarker>();
                added.kind = kind;
                added.details = details ?? "";
                added.count = 1;
                return added;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Phase-7:
    /// 仮状態の分類キー定義 (string const)。
    /// 既存コード側は MayaProvisionalKind.Xxx を参照する。
    /// </summary>
    public static class MayaProvisionalKind
    {
        // --- .mb mesh decode (heuristic) ---
        public const string MbMeshHeuristicDecode = "MbMeshHeuristicDecode";
        public const string MbMeshSequentialFallback = "MbMeshSequentialFallback";
        public const string MbMeshPolyFacesTriangulated = "MbMeshPolyFacesTriangulated";

        // --- .mb materials ---
        public const string MbMeshMaterialFallback = "MbMeshMaterialFallback";

        // --- materials/shading network ---
        public const string MaterialFallbackOrNoMeta = "MaterialFallbackOrNoMeta";

        // --- nurbs ---
        public const string NurbsSurfacePreviewMesh = "NurbsSurfacePreviewMesh";
    }
}
