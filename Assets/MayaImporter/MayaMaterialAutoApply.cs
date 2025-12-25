using System;
using MayaImporter.Components;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase B-6:
    /// 旧 “MaterialApplyTool(手動)” 相当を Importer に統合。
    /// - Renderers に対し shadingEngine / per-face の割当を best-effort で適用
    /// - 未対応でも UnknownComponent 等の保持で欠損ゼロ（importは止めない）
    /// </summary>
    public static class MayaMaterialAutoApply
    {
        /// <summary>
        /// Importer内から呼ばれる想定（ScriptedImporter）。
        /// ここでは “例外を投げない” を最優先にする。
        /// </summary>
        public static void Run_BestEffort(Transform sceneRoot, MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            if (sceneRoot == null) return;

            try
            {
                // MeshRenderer / SkinnedMeshRenderer 両方に対応
                var renderers = sceneRoot.GetComponentsInChildren<Renderer>(true);

                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;

                    // 1) Maya側の割当情報を持つコンポーネントを探す（既存実装に合わせ best-effort）
                    // 代表例:
                    // - MayaMeshAssignmentMetadata: shadingEngineName / perFace mapping
                    // - MayaShadingGroupRef: shadingEngineName
                    //
                    // ここはプロジェクトに既存がある想定で、見つかったものだけ適用する。
                    string shadingEngineName = null;

                    // パターンA: よくある “shadingEngineName” を持つコンポーネント
                    //（型が無い場合でもコンパイルできるよう、反射でbest-effort）
                    shadingEngineName = TryGetStringFieldOrProperty(r.gameObject, "shadingEngineName")
                                     ?? TryGetStringFieldOrProperty(r.gameObject, "ShadingEngineName")
                                     ?? TryGetStringFieldOrProperty(r.gameObject, "shadingEngine");

                    if (string.IsNullOrEmpty(shadingEngineName))
                        continue;

                    var mat = MayaMaterialResolver.ResolveFromSceneBestEffort(sceneRoot, scene, shadingEngineName, options, log);
                    if (mat == null) continue;

                    // 2) Rendererへ適用（per-faceはUnity標準では submesh/material index 前提）
                    // ここでは最低限 “Renderer.material(0)” に当てて見た目を出す。
                    // 既存で submesh/material array を作っている場合は上書きしない。
                    var shared = r.sharedMaterials;
                    if (shared == null || shared.Length == 0)
                    {
                        r.sharedMaterial = mat;
                    }
                    else
                    {
                        // 既存配列の0番だけ “欠損の穴” を埋める
                        if (shared[0] == null || IsFallback(shared[0]))
                        {
                            shared[0] = mat;
                            r.sharedMaterials = shared;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.Warn("[MaterialAutoApply] Exception: " + e.GetType().Name + ": " + e.Message);
            }
        }

        private static bool IsFallback(Material m)
        {
            if (m == null) return false;
            // fallbackは “SE__” や “Mesh__” といった命名が多い想定（best-effort）
            var n = m.name ?? "";
            return n.StartsWith("SE__", StringComparison.Ordinal) || n.StartsWith("Mesh__", StringComparison.Ordinal) || n.Contains("__NoMeta__");
        }

        private static string TryGetStringFieldOrProperty(GameObject go, string memberName)
        {
            if (go == null || string.IsNullOrEmpty(memberName)) return null;

            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;

                var t = c.GetType();

                // field
                var f = t.GetField(memberName);
                if (f != null && f.FieldType == typeof(string))
                {
                    var v = f.GetValue(c) as string;
                    if (!string.IsNullOrEmpty(v)) return v;
                }

                // property
                var p = t.GetProperty(memberName);
                if (p != null && p.PropertyType == typeof(string) && p.CanRead)
                {
                    var v = p.GetValue(c, null) as string;
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }

            return null;
        }
    }
}
