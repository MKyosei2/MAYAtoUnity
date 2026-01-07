// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase B-6:
    /// Importer 内で Material 自動適用を行う (Unity-only / Maya不要)。
    ///
    /// 方針:
    /// - Renderer に対して shadingEngine / per-face 情報を best-effort で適用
    /// - 失敗しても Import を止めない (UnknownComponent 等で保持)
    ///
    /// Phase-7:
    /// - fallback / no-meta 等の "仮" 経路を MayaProvisionalMarker として記録し、
    ///   後から本実装化の優先順位付けができるようにする。
    /// </summary>
    public static class MayaMaterialAutoApply
    {
        /// <summary>
        /// ScriptedImporter から呼ばれる想定。
        /// ここでは "例外を出さずに" 適用できる範囲だけ適用する。
        /// </summary>
        public static void Run_BestEffort(Transform sceneRoot, MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            if (sceneRoot == null) return;

            try
            {
                // MeshRenderer / SkinnedMeshRenderer 両方対応
                var renderers = sceneRoot.GetComponentsInChildren<Renderer>(true);

                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;

                    // best-effort: shadingEngineName を持つメタコンポーネントを反射で探す
                    string shadingEngineName =
                        TryGetStringFieldOrProperty(r.gameObject, "shadingEngineName") ??
                        TryGetStringFieldOrProperty(r.gameObject, "ShadingEngineName") ??
                        TryGetStringFieldOrProperty(r.gameObject, "shadingEngine");

                    if (string.IsNullOrEmpty(shadingEngineName))
                        continue;

                    var mat = MayaMaterialResolver.ResolveFromSceneBestEffort(sceneRoot, scene, shadingEngineName, options, log);
                    if (mat == null) continue;

                    // Phase-7: fallback / no-meta をマーク (結果は変えない)
                    try
                    {
                        if (IsFallback(mat))
                        {
                            var detail = $"shadingEngine={shadingEngineName} mat={mat.name}";
                            MayaProvisionalMarker.Ensure(r.gameObject, MayaProvisionalKind.MaterialFallbackOrNoMeta, detail);
                        }
                    }
                    catch { }

                    // Renderer に適用 (per-face は Unity 標準だけでは完全再現が難しいため、ここは最低限)
                    var shared = r.sharedMaterials;
                    if (shared == null || shared.Length == 0)
                    {
                        r.sharedMaterial = mat;
                    }
                    else
                    {
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
            var n = m.name ?? "";
            // fallback は "SE__" "Mesh__" 等の名前が多い想定 (best-effort)
            return n.StartsWith("SE__", StringComparison.Ordinal) ||
                   n.StartsWith("Mesh__", StringComparison.Ordinal) ||
                   n.Contains("__NoMeta__");
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
