// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using MayaImporter.Components;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MayaImporter.Core
{
    public static class MayaMaterialResolver
    {
        private static readonly Dictionary<string, Material> _matCache =
            new Dictionary<string, Material>(StringComparer.Ordinal);

        //  Shader ՓˉFglobal::UnityEngine.Shader g
        private static global::UnityEngine.Shader _fallbackShader;

        public static Material ResolveForMeshNode(string meshNodeName)
        {
            if (string.IsNullOrEmpty(meshNodeName))
                return GetOrCreateFallback("__MeshDefault__");
            return GetOrCreateFallback("Mesh__" + meshNodeName);
        }

        public static Material ResolveForShadingEngine(string shadingEngineName)
        {
            if (string.IsNullOrEmpty(shadingEngineName))
                return GetOrCreateFallback("__SEDefault__");
            return GetOrCreateFallback("SE__" + shadingEngineName);
        }

        public static Material ResolveFromSceneBestEffort(
            Transform sceneRoot,
            MayaSceneData scene,
            string shadingEngineName,
            MayaImportOptions options,
            MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            if (!string.IsNullOrEmpty(shadingEngineName) &&
                MayaBuildContext.MaterialByShadingEngine.TryGetValue(shadingEngineName, out var cached) &&
                cached != null)
            {
                return cached;
            }

            try
            {
                var sg = FindShadingGroup(sceneRoot, shadingEngineName);
                var sgMeta = sg != null ? sg.GetComponent<MayaShadingGroupMetadata>() : null;

                if (sgMeta == null || string.IsNullOrEmpty(sgMeta.surfaceShaderNodeName))
                {
                    var fb = ResolveForShadingEngine(shadingEngineName);
                    CacheToBuildContext(shadingEngineName, fb);
                    return fb;
                }

                var surfaceShaderName = sgMeta.surfaceShaderNodeName;
                var surfaceLeaf = MayaPlugUtil.LeafName(surfaceShaderName);

                var matMetaGo = FindByMayaNodeName(sceneRoot, surfaceShaderName) ??
                                FindByLeafName(sceneRoot, surfaceLeaf);
                var matMeta = matMetaGo != null ? matMetaGo.GetComponent<MayaMaterialMetadata>() : null;

                if (matMeta == null)
                {
                    var fb = GetOrCreateFallback($"SE__{MayaPlugUtil.LeafName(shadingEngineName)}__NoMeta__{surfaceLeaf}");
                    CacheToBuildContext(shadingEngineName, fb);
                    return fb;
                }

                var key = $"SE__{MayaPlugUtil.LeafName(shadingEngineName)}__Surf__{surfaceLeaf}__{matMeta.mayaShaderType}";
                if (_matCache.TryGetValue(key, out var existing) && existing != null)
                {
                    CacheToBuildContext(shadingEngineName, existing);
                    return existing;
                }

                var shader = PickBestLitShader(matMeta);
                var mat = new Material(shader) { name = "MAYA_" + key };

                ApplyMaterialParams(mat, matMeta);

                // --- textures ---
                var texMetas = sceneRoot != null
                    ? sceneRoot.GetComponentsInChildren<MayaTextureMetadata>(true)
                    : Array.Empty<MayaTextureMetadata>();

                // Base color
                if (TryLoadTextureFromNodeName(sceneRoot, scene, texMetas, matMeta.baseColorTextureNode, options, log,
                    usage: MayaTextureUsage.BaseColor,
                    out var baseTex, out var baseTiling, out var baseOffset))
                {
                    SetTextureIfExists(mat, "_BaseMap", baseTex);
                    SetTextureIfExists(mat, "_MainTex", baseTex);
                    TrySetTilingOffset(mat, "_BaseMap", baseTiling, baseOffset);
                    TrySetTilingOffset(mat, "_MainTex", baseTiling, baseOffset);
                }

                // Emission
                if (TryLoadTextureFromNodeName(sceneRoot, scene, texMetas, matMeta.emissionTextureNode, options, log,
                    usage: MayaTextureUsage.Emission,
                    out var emiTex, out var emiTiling, out var emiOffset))
                {
                    EnableKeywordIfExists(mat, "_EMISSION");
                    SetTextureIfExists(mat, "_EmissionMap", emiTex);
                    TrySetTilingOffset(mat, "_EmissionMap", emiTiling, emiOffset);
                }

                // Normal
                if (TryLoadTextureFromNodeName(sceneRoot, scene, texMetas, matMeta.normalTextureNode, options, log,
                    usage: MayaTextureUsage.Normal,
                    out var nrmTex, out var nrmTiling, out var nrmOffset))
                {
                    EnableKeywordIfExists(mat, "_NORMALMAP");
                    SetTextureIfExists(mat, "_BumpMap", nrmTex);
                    TrySetTilingOffset(mat, "_BumpMap", nrmTiling, nrmOffset);
                }

                // Metallic / Roughness packing (B-4)
                Texture2D metallicTex = null;
                Vector2 metallicTiling = Vector2.one, metallicOffset = Vector2.zero;

                if (TryLoadTextureFromNodeName(sceneRoot, scene, texMetas, matMeta.metallicTextureNode, options, log,
                    usage: MayaTextureUsage.Metallic,
                    out var mTex, out var mTiling, out var mOffset))
                {
                    metallicTex = mTex as Texture2D;
                    metallicTiling = mTiling;
                    metallicOffset = mOffset;
                }

                Texture2D roughnessTex = null;
                Vector2 roughnessTiling = Vector2.one, roughnessOffset = Vector2.zero;

                if (TryLoadTextureFromNodeName(sceneRoot, scene, texMetas, matMeta.roughnessTextureNode, options, log,
                    usage: MayaTextureUsage.Roughness,
                    out var rTex, out var rTiling, out var rOffset))
                {
                    roughnessTex = rTex as Texture2D;
                    roughnessTiling = rTiling;
                    roughnessOffset = rOffset;
                }

                if (metallicTex != null || roughnessTex != null)
                {
                    var tiling = (metallicTex != null) ? metallicTiling : roughnessTiling;
                    var offset = (metallicTex != null) ? metallicOffset : roughnessOffset;

                    var packed = MayaTexturePacker.BuildMetallicSmoothnessPacked_BestEffort(
                        metallicTex,
                        roughnessTex,
                        metallicScalar: Mathf.Clamp01(matMeta.metallic),
                        smoothnessScalar: Mathf.Clamp01(matMeta.smoothness),
                        log);

                    if (packed != null)
                    {
                        SetTextureIfExists(mat, "_MetallicGlossMap", packed);
                        TrySetTilingOffset(mat, "_MetallicGlossMap", tiling, offset);
                    }
                }
                else
                {
                    SetFloatIfExists(mat, "_Metallic", Mathf.Clamp01(matMeta.metallic));
                    SetFloatIfExists(mat, "_Smoothness", Mathf.Clamp01(matMeta.smoothness));
                    SetFloatIfExists(mat, "_Glossiness", Mathf.Clamp01(matMeta.smoothness));
                }

                if (matMeta.opacity < 0.999f)
                    MakeMaterialTransparentBestEffort(mat, matMeta.opacity);

                _matCache[key] = mat;
                CacheToBuildContext(shadingEngineName, mat);

                return mat;
            }
            catch (Exception e)
            {
                log?.Warn("[MaterialResolver] Exception: " + e.GetType().Name + ": " + e.Message);
                var fb = ResolveForShadingEngine(shadingEngineName);
                CacheToBuildContext(shadingEngineName, fb);
                return fb;
            }
        }

        // ------------------------------------------------------------
        // Find graph objects
        // ------------------------------------------------------------
        private static GameObject FindShadingGroup(Transform root, string shadingEngineName)
        {
            if (root == null || string.IsNullOrEmpty(shadingEngineName)) return null;

            var leaf = MayaPlugUtil.LeafName(shadingEngineName);

            var allNodes = root.GetComponentsInChildren<MayaNodeComponentBase>(true);
            for (int i = 0; i < allNodes.Length; i++)
            {
                var n = allNodes[i];
                if (n == null) continue;
                if (!string.Equals(n.NodeType, "shadingEngine", StringComparison.Ordinal)) continue;

                if (string.Equals(n.NodeName, shadingEngineName, StringComparison.Ordinal) ||
                    string.Equals(MayaPlugUtil.LeafName(n.NodeName), leaf, StringComparison.Ordinal))
                {
                    return n.gameObject;
                }
            }

            var metas = root.GetComponentsInChildren<MayaShadingGroupMetadata>(true);
            for (int i = 0; i < metas.Length; i++)
            {
                var m = metas[i];
                if (m == null) continue;

                var goName = m.gameObject.name;
                if (string.Equals(goName, shadingEngineName, StringComparison.Ordinal) ||
                    string.Equals(MayaPlugUtil.LeafName(goName), leaf, StringComparison.Ordinal))
                {
                    return m.gameObject;
                }
            }

            return null;
        }

        private static GameObject FindByMayaNodeName(Transform root, string mayaNodeName)
        {
            if (root == null || string.IsNullOrEmpty(mayaNodeName)) return null;

            var all = root.GetComponentsInChildren<MayaNodeComponentBase>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var n = all[i];
                if (n == null) continue;

                if (string.Equals(n.NodeName, mayaNodeName, StringComparison.Ordinal))
                    return n.gameObject;
            }

            return null;
        }

        private static GameObject FindByLeafName(Transform root, string leaf)
        {
            if (root == null || string.IsNullOrEmpty(leaf)) return null;

            var trs = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                var t = trs[i];
                if (t == null) continue;
                if (string.Equals(t.gameObject.name, leaf, StringComparison.Ordinal))
                    return t.gameObject;
            }

            return null;
        }

        // ------------------------------------------------------------
        // Material construction
        // ------------------------------------------------------------
        private static global::UnityEngine.Shader PickBestLitShader(MayaMaterialMetadata meta)
        {
            var urpLit = global::UnityEngine.Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null) return urpLit;

            var standard = global::UnityEngine.Shader.Find("Standard");
            if (standard != null) return standard;

            var unlit = global::UnityEngine.Shader.Find("Unlit/Texture") ?? global::UnityEngine.Shader.Find("Unlit/Color");
            if (unlit != null) return unlit;

            return GetFallbackShader();
        }

        private static void ApplyMaterialParams(Material mat, MayaMaterialMetadata meta)
        {
            if (mat == null || meta == null) return;

            var baseWithA = new Color(meta.baseColor.r, meta.baseColor.g, meta.baseColor.b, meta.opacity);
            baseWithA = Color.Lerp(Color.black, baseWithA, Mathf.Clamp01(meta.baseWeight));

            SetColorIfExists(mat, "_BaseColor", baseWithA);
            SetColorIfExists(mat, "_Color", baseWithA);

            SetFloatIfExists(mat, "_Metallic", Mathf.Clamp01(meta.metallic));
            SetFloatIfExists(mat, "_Smoothness", Mathf.Clamp01(meta.smoothness));
            SetFloatIfExists(mat, "_Glossiness", Mathf.Clamp01(meta.smoothness));

            if (meta.emissionColor.maxColorComponent > 0.0001f)
            {
                EnableKeywordIfExists(mat, "_EMISSION");
                SetColorIfExists(mat, "_EmissionColor", meta.emissionColor);
            }
        }

        private static bool TryLoadTextureFromNodeName(
            Transform sceneRoot,
            MayaSceneData scene,
            MayaTextureMetadata[] allTexMetas,
            string textureNodeName,
            MayaImportOptions options,
            MayaImportLog log,
            MayaTextureUsage usage,
            out Texture tex,
            out Vector2 tiling,
            out Vector2 offset)
        {
            tex = null;
            tiling = Vector2.one;
            offset = Vector2.zero;

            if (string.IsNullOrEmpty(textureNodeName)) return false;

            var leaf = MayaPlugUtil.LeafName(textureNodeName);

            MayaTextureMetadata found = null;
            for (int i = 0; i < allTexMetas.Length; i++)
            {
                var m = allTexMetas[i];
                if (m == null) continue;

                var goName = m.gameObject.name;
                if (string.Equals(goName, textureNodeName, StringComparison.Ordinal) ||
                    string.Equals(goName, leaf, StringComparison.Ordinal))
                {
                    found = m;
                    break;
                }
            }

            if (found == null) return false;

            tiling = found.repeatUV;
            offset = found.offsetUV;

            tex = MayaTextureLoader.LoadTexture_BestEffort(scene, found, usage, log, out _);
            return tex != null;
        }

        // ------------------------------------------------------------
        // Fallback
        // ------------------------------------------------------------
        private static Material GetOrCreateFallback(string key)
        {
            if (_matCache.TryGetValue(key, out var m) && m != null)
                return m;

            var shader = GetFallbackShader();
            var mat = new Material(shader) { name = key };

            try
            {
                var c = ColorFromName(key);
                SetColorIfExists(mat, "_BaseColor", c);
                SetColorIfExists(mat, "_Color", c);
            }
            catch { }

            _matCache[key] = mat;
            return mat;
        }

        private static global::UnityEngine.Shader GetFallbackShader()
        {
            if (_fallbackShader != null) return _fallbackShader;

            _fallbackShader =
                global::UnityEngine.Shader.Find("Universal Render Pipeline/Lit") ??
                global::UnityEngine.Shader.Find("Standard") ??
                global::UnityEngine.Shader.Find("Unlit/Color") ??
                global::UnityEngine.Shader.Find("UI/Default") ??
                global::UnityEngine.Shader.Find("Hidden/InternalErrorShader");

            return _fallbackShader;
        }

        private static void CacheToBuildContext(string shadingEngineName, Material mat)
        {
            if (string.IsNullOrEmpty(shadingEngineName) || mat == null) return;
            MayaBuildContext.MaterialByShadingEngine[shadingEngineName] = mat;
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------
        private static void SetFloatIfExists(Material mat, string prop, float v)
        {
            if (mat == null) return;
            if (mat.HasProperty(prop)) mat.SetFloat(prop, v);
        }

        private static void SetColorIfExists(Material mat, string prop, Color c)
        {
            if (mat == null) return;
            if (mat.HasProperty(prop)) mat.SetColor(prop, c);
        }

        private static void SetTextureIfExists(Material mat, string prop, Texture tex)
        {
            if (mat == null) return;
            if (tex == null) return;
            if (mat.HasProperty(prop)) mat.SetTexture(prop, tex);
        }

        private static void EnableKeywordIfExists(Material mat, string keyword)
        {
            if (mat == null) return;
            if (string.IsNullOrEmpty(keyword)) return;
            try { mat.EnableKeyword(keyword); } catch { }
        }

        private static void TrySetTilingOffset(Material mat, string prop, Vector2 tiling, Vector2 offset)
        {
            if (mat == null) return;
            if (!mat.HasProperty(prop)) return;
            try
            {
                mat.SetTextureScale(prop, tiling);
                mat.SetTextureOffset(prop, offset);
            }
            catch { }
        }

        private static void MakeMaterialTransparentBestEffort(Material mat, float opacity)
        {
            if (mat == null) return;

            if (mat.shader != null && mat.shader.name == "Standard")
            {
                SetFloatIfExists(mat, "_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                EnableKeywordIfExists(mat, "_ALPHABLEND_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                return;
            }

            if (mat.HasProperty("_Surface"))
            {
                SetFloatIfExists(mat, "_Surface", 1f);
                SetFloatIfExists(mat, "_ZWrite", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            if (mat.HasProperty("_BaseColor"))
            {
                var c = mat.GetColor("_BaseColor");
                c.a = Mathf.Clamp01(opacity);
                mat.SetColor("_BaseColor", c);
            }
            if (mat.HasProperty("_Color"))
            {
                var c = mat.GetColor("_Color");
                c.a = Mathf.Clamp01(opacity);
                mat.SetColor("_Color", c);
            }
        }

        private static Color ColorFromName(string s)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; i < s.Length; i++)
                    h = h * 31 + s[i];

                float r = ((h >> 0) & 255) / 255f;
                float g = ((h >> 8) & 255) / 255f;
                float b = ((h >> 16) & 255) / 255f;

                r = Mathf.Lerp(0.25f, 0.95f, r);
                g = Mathf.Lerp(0.25f, 0.95f, g);
                b = Mathf.Lerp(0.25f, 0.95f, b);

                return new Color(r, g, b, 1f);
            }
        }
    }
}
