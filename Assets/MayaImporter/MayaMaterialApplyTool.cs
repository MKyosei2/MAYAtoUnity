#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using MayaImporter.Components;
using MayaImporter.Core;

namespace MayaImporter.EditorTools
{
    public static class MayaMaterialApplyTool
    {
        [MenuItem("Tools/Maya Importer/Build & Apply Materials (SubMesh/per-face)")]
        public static void BuildAndApply_SubMesh()
        {
            var log = new MayaImportLog();

            // ---- Collect textures ----
            var textures = new Dictionary<string, (MayaTextureMetadata meta, global::UnityEngine.Texture2D tex)>();
            var allTex = Resources.FindObjectsOfTypeAll<MayaTextureMetadata>();
            for (int i = 0; i < allTex.Length; i++)
            {
                var m = allTex[i];
                if (m == null || !m.gameObject.scene.IsValid()) continue;
                textures[m.gameObject.name] = (m, TryLoadTexture(m, log));
            }

            // ---- Collect material metas ----
            var materialMetas = new Dictionary<string, MayaMaterialMetadata>();
            var allMatMeta = Resources.FindObjectsOfTypeAll<MayaMaterialMetadata>();
            for (int i = 0; i < allMatMeta.Length; i++)
            {
                var m = allMatMeta[i];
                if (m == null || !m.gameObject.scene.IsValid()) continue;
                materialMetas[m.gameObject.name] = m;
            }

            // ---- Build Unity materials ----
            var materials = new Dictionary<string, global::UnityEngine.Material>();
            foreach (var kv in materialMetas)
            {
                var mayaMatName = kv.Key;
                var meta = kv.Value;

                var shader = PickBestLitShader();
                var mat = new global::UnityEngine.Material(shader) { name = "MAYA_" + mayaMatName };

                var baseWithA = new Color(meta.baseColor.r, meta.baseColor.g, meta.baseColor.b, meta.opacity);
                SetColorIfExists(mat, "_BaseColor", baseWithA);
                SetColorIfExists(mat, "_Color", baseWithA);

                SetFloatIfExists(mat, "_Metallic", meta.metallic);
                SetFloatIfExists(mat, "_Smoothness", meta.smoothness);
                SetFloatIfExists(mat, "_Glossiness", meta.smoothness);

                SetColorIfExists(mat, "_EmissionColor", meta.emissionColor);

                TryBindTexture(mat, meta.baseColorTextureNode, textures, "_BaseMap", "_MainTex");
                TryBindTexture(mat, meta.metallicTextureNode, textures, "_MetallicGlossMap");
                TryBindTexture(mat, meta.emissionTextureNode, textures, "_EmissionMap");

                if (TryGetTexture(meta.normalTextureNode, textures, out var ntex))
                {
                    EnableKeywordIfExists(mat, "_NORMALMAP");
                    SetTextureIfExists(mat, "_BumpMap", ntex);
                }

                if (meta.opacity < 0.999f)
                    MakeMaterialTransparentBestEffort(mat);

                materials[mayaMatName] = mat;
            }

            // ---- Collect shading groups ----
            var allSg = Resources.FindObjectsOfTypeAll<MayaShadingGroupMetadata>();

            // Build "renderer GO -> assignments"
            var perRenderer = new Dictionary<GameObject, List<SgAssign>>();

            for (int i = 0; i < allSg.Length; i++)
            {
                var sg = allSg[i];
                if (sg == null || !sg.gameObject.scene.IsValid()) continue;
                if (string.IsNullOrEmpty(sg.surfaceShaderNodeName)) continue;

                var surfLeaf = Leaf(sg.surfaceShaderNodeName);

                global::UnityEngine.Material mat = null;
                if (!materials.TryGetValue(surfLeaf, out mat))
                    materials.TryGetValue(sg.surfaceShaderNodeName, out mat);

                if (mat == null)
                {
                    log.Warn($"[MatApply] material not built for surfaceShader='{sg.surfaceShaderNodeName}' (sg='{sg.gameObject.name}')");
                    continue;
                }

                for (int mIdx = 0; mIdx < sg.members.Count; mIdx++)
                {
                    var mem = sg.members[mIdx];
                    var go = FindRendererGO(mem.nodeName);
                    if (go == null) continue;

                    var r = go.GetComponent<Renderer>();
                    if (r == null) continue;

                    if (!perRenderer.TryGetValue(go, out var list))
                    {
                        list = new List<SgAssign>();
                        perRenderer[go] = list;
                    }

                    var comp = mem.componentSpec;
                    var assign = FindOrCreateAssign(list, sg.gameObject.name, mat);
                    assign.AddComponentSpec(comp);
                }
            }

            int appliedRenderers = 0;
            int submeshed = 0;

            foreach (var kv in perRenderer)
            {
                var go = kv.Key;
                var assigns = kv.Value;

                var renderer = go.GetComponent<Renderer>();
                if (renderer == null) continue;

                bool anyFace = false;
                for (int i = 0; i < assigns.Count; i++)
                {
                    if (assigns[i].HasFaceSpec) { anyFace = true; break; }
                }

                if (!anyFace)
                {
                    renderer.sharedMaterial = assigns[0].material;
                    appliedRenderers++;
                    continue;
                }

                if (TryApplySubMeshes(go, renderer, assigns, log))
                {
                    submeshed++;
                    appliedRenderers++;
                }
                else
                {
                    renderer.sharedMaterial = assigns[0].material;
                    appliedRenderers++;
                }
            }

            Debug.Log($"[MayaImporter] Build & Apply Materials done. builtMats={materials.Count} appliedRenderers={appliedRenderers} submeshed={submeshed}");
        }

        // ---------------- SubMesh core ----------------

        private static bool TryApplySubMeshes(GameObject go, Renderer r, List<SgAssign> assigns, MayaImportLog log)
        {
            // Get mesh
            Mesh mesh = null;

            var skin = go.GetComponent<SkinnedMeshRenderer>();
            if (skin != null) mesh = skin.sharedMesh;

            if (mesh == null)
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }

            if (mesh == null) return false;

            var tris = mesh.triangles;
            if (tris == null || tris.Length < 3) return false;

            int triCount = tris.Length / 3;

            // face->tri mapping (optional)
            var map = go.GetComponent<MayaMeshFaceTriangleMap>();
            bool hasMap = map != null && map.faceToTriStart != null && map.faceToTriCount != null &&
                          map.faceToTriStart.Length == map.faceToTriCount.Length && map.faceToTriStart.Length > 0;

            var perAssignTri = new List<HashSet<int>>(assigns.Count);
            int maxFaceIndex = -1;

            for (int i = 0; i < assigns.Count; i++)
            {
                var set = new HashSet<int>();
                perAssignTri.Add(set);

                var faces = assigns[i].GetFaceIndices();
                if (faces == null || faces.Count == 0) continue;

                for (int f = 0; f < faces.Count; f++)
                    if (faces[f] > maxFaceIndex) maxFaceIndex = faces[f];

                if (hasMap)
                {
                    for (int f = 0; f < faces.Count; f++)
                    {
                        int face = faces[f];
                        if ((uint)face >= (uint)map.faceToTriStart.Length) continue;
                        int start = map.faceToTriStart[face];
                        int count = map.faceToTriCount[face];
                        for (int t = 0; t < count; t++)
                        {
                            int tri = start + t;
                            if ((uint)tri < (uint)triCount) set.Add(tri);
                        }
                    }
                }
                else
                {
                    // heuristic: face index == triangle index
                    if (maxFaceIndex >= 0 && triCount >= (maxFaceIndex + 1))
                    {
                        for (int f = 0; f < faces.Count; f++)
                        {
                            int tri = faces[f];
                            if ((uint)tri < (uint)triCount) set.Add(tri);
                        }
                    }
                    else
                    {
                        log?.Warn($"[SubMesh] No face->tri map and triCount({triCount}) < maxFace({maxFaceIndex}). Fallback to whole-renderer.");
                        return false;
                    }
                }
            }

            // Clone mesh
            var newMesh = UnityEngine.Object.Instantiate(mesh);
            newMesh.name = mesh.name + "__SubMesh";

            var outMats = new List<global::UnityEngine.Material>();
            var usedTri = new HashSet<int>();
            var subTriangles = new List<List<int>>();

            for (int i = 0; i < assigns.Count; i++)
            {
                var triSet = perAssignTri[i];
                if (triSet == null || triSet.Count == 0) continue;

                var list = new List<int>(triSet.Count * 3);
                foreach (var tri in triSet)
                {
                    if (!usedTri.Add(tri)) continue;
                    int baseIdx = tri * 3;
                    list.Add(tris[baseIdx + 0]);
                    list.Add(tris[baseIdx + 1]);
                    list.Add(tris[baseIdx + 2]);
                }

                if (list.Count == 0) continue;

                subTriangles.Add(list);
                outMats.Add(assigns[i].material);
            }

            if (subTriangles.Count == 0)
                return false;

            // Remaining triangles
            var rest = new List<int>();
            for (int tri = 0; tri < triCount; tri++)
            {
                if (usedTri.Contains(tri)) continue;
                int baseIdx = tri * 3;
                rest.Add(tris[baseIdx + 0]);
                rest.Add(tris[baseIdx + 1]);
                rest.Add(tris[baseIdx + 2]);
            }

            if (rest.Count > 0)
            {
                subTriangles.Add(rest);
                var restMat = r.sharedMaterial != null ? r.sharedMaterial : assigns[0].material;
                outMats.Add(restMat);
            }

            newMesh.subMeshCount = subTriangles.Count;
            for (int s = 0; s < subTriangles.Count; s++)
                newMesh.SetTriangles(subTriangles[s], s, true);

            newMesh.RecalculateBounds();

            // Assign mesh back (re-use 'skin' variable; do NOT redeclare)
            if (skin != null) skin.sharedMesh = newMesh;
            else
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = newMesh;
            }

            r.sharedMaterials = outMats.ToArray();
            return true;
        }

        // ---------------- Assign aggregation ----------------

        private sealed class SgAssign
        {
            public string shadingGroupName;
            public global::UnityEngine.Material material;

            private readonly List<string> _componentSpecs = new List<string>();

            public bool HasFaceSpec
            {
                get
                {
                    for (int i = 0; i < _componentSpecs.Count; i++)
                        if (!string.IsNullOrEmpty(_componentSpecs[i]) && _componentSpecs[i].Contains(".f[", StringComparison.Ordinal))
                            return true;
                    return false;
                }
            }

            public void AddComponentSpec(string spec)
            {
                if (string.IsNullOrEmpty(spec)) return;
                _componentSpecs.Add(spec);
            }

            public List<int> GetFaceIndices()
            {
                var faces = new HashSet<int>();

                for (int i = 0; i < _componentSpecs.Count; i++)
                {
                    var s = _componentSpecs[i];
                    if (string.IsNullOrEmpty(s)) continue;

                    int p = s.IndexOf(".f[", StringComparison.Ordinal);
                    if (p < 0) continue;

                    int start = p + 3;
                    int end = s.IndexOf(']', start);
                    if (end < 0) continue;

                    var body = s.Substring(start, end - start);
                    ParseFaceBody(body, faces);
                }

                if (faces.Count == 0) return null;

                var list = new List<int>(faces);
                list.Sort();
                return list;
            }

            private static void ParseFaceBody(string body, HashSet<int> outFaces)
            {
                if (string.IsNullOrEmpty(body)) return;

                if (body.IndexOf(',', StringComparison.Ordinal) >= 0)
                {
                    var parts = body.Split(',');
                    for (int i = 0; i < parts.Length; i++)
                        ParseFaceBody(parts[i].Trim(), outFaces);
                    return;
                }

                int c = body.IndexOf(':');
                if (c >= 0)
                {
                    if (!int.TryParse(body.Substring(0, c), out var a)) return;
                    if (!int.TryParse(body.Substring(c + 1), out var b)) return;
                    if (b < a) (a, b) = (b, a);
                    for (int i = a; i <= b; i++) outFaces.Add(i);
                    return;
                }

                if (int.TryParse(body.Trim(), out var v))
                    outFaces.Add(v);
            }
        }

        private static SgAssign FindOrCreateAssign(List<SgAssign> list, string sgName, global::UnityEngine.Material mat)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].shadingGroupName == sgName && list[i].material == mat)
                    return list[i];
            }

            var a = new SgAssign { shadingGroupName = sgName, material = mat };
            list.Add(a);
            return a;
        }

        // ---------------- Shared helpers ----------------

        private static global::UnityEngine.Shader PickBestLitShader()
        {
            var s = global::UnityEngine.Shader.Find("Universal Render Pipeline/Lit");
            if (s != null) return s;

            s = global::UnityEngine.Shader.Find("HDRP/Lit");
            if (s != null) return s;

            s = global::UnityEngine.Shader.Find("Standard");
            return s != null ? s : global::UnityEngine.Shader.Find("Diffuse");
        }

        private static bool TryGetTexture(
            string nodeName,
            Dictionary<string, (MayaTextureMetadata meta, global::UnityEngine.Texture2D tex)> textures,
            out global::UnityEngine.Texture2D tex)
        {
            tex = null;
            if (string.IsNullOrEmpty(nodeName)) return false;

            var leaf = Leaf(nodeName);

            if (textures.TryGetValue(leaf, out var t) && t.tex != null) { tex = t.tex; return true; }
            if (textures.TryGetValue(nodeName, out t) && t.tex != null) { tex = t.tex; return true; }

            return false;
        }

        private static void TryBindTexture(
            global::UnityEngine.Material mat,
            string texNodeName,
            Dictionary<string, (MayaTextureMetadata meta, global::UnityEngine.Texture2D tex)> textures,
            params string[] propNames)
        {
            if (!TryGetTexture(texNodeName, textures, out var tex) || tex == null) return;

            for (int i = 0; i < propNames.Length; i++)
                SetTextureIfExists(mat, propNames[i], tex);
        }

        private static global::UnityEngine.Texture2D TryLoadTexture(MayaTextureMetadata meta, MayaImportLog log)
        {
            if (meta == null || string.IsNullOrEmpty(meta.fileTextureName)) return null;

            var path = meta.fileTextureName.Trim().Trim('"');
            if (string.IsNullOrEmpty(path)) return null;

            if (Path.IsPathRooted(path) && File.Exists(path))
                return LoadTextureFromFile(path, log);

            var fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName)) return null;

            var assetsRoot = Application.dataPath;
            try
            {
                var found = Directory.GetFiles(assetsRoot, fileName, SearchOption.AllDirectories);
                if (found != null && found.Length > 0)
                    return LoadTextureFromFile(found[0], log);
            }
            catch { }

            return null;
        }

        private static global::UnityEngine.Texture2D LoadTextureFromFile(string filePath, MayaImportLog log)
        {
            try
            {
                var bytes = File.ReadAllBytes(filePath);
                var tex = new global::UnityEngine.Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!tex.LoadImage(bytes))
                    return null;

                tex.name = Path.GetFileNameWithoutExtension(filePath);
                return tex;
            }
            catch (Exception e)
            {
                log?.Warn($"[MatApply] texture load failed: '{filePath}' {e.Message}");
                return null;
            }
        }

        private static GameObject FindRendererGO(string mayaNodeName)
        {
            var leaf = Leaf(mayaNodeName);
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            GameObject best = null;

            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (go == null) continue;
                if (!go.scene.IsValid()) continue;

                if (go.GetComponent<Renderer>() == null) continue;

                if (string.Equals(go.name, leaf, StringComparison.Ordinal))
                    return go;

                if (go.name.EndsWith(leaf, StringComparison.Ordinal))
                    best ??= go;
            }
            return best;
        }

        private static string Leaf(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            int i = name.LastIndexOf('|');
            if (i >= 0 && i < name.Length - 1) name = name.Substring(i + 1);

            int j = name.LastIndexOf(':');
            if (j >= 0 && j < name.Length - 1) name = name.Substring(j + 1);

            return name;
        }

        private static void SetColorIfExists(global::UnityEngine.Material m, string prop, Color v)
        {
            if (m != null && m.HasProperty(prop)) m.SetColor(prop, v);
        }

        private static void SetFloatIfExists(global::UnityEngine.Material m, string prop, float v)
        {
            if (m != null && m.HasProperty(prop)) m.SetFloat(prop, v);
        }

        private static void SetTextureIfExists(global::UnityEngine.Material m, string prop, global::UnityEngine.Texture v)
        {
            if (m != null && m.HasProperty(prop)) m.SetTexture(prop, v);
        }

        private static void EnableKeywordIfExists(global::UnityEngine.Material m, string keyword)
        {
            if (m == null || string.IsNullOrEmpty(keyword)) return;
            m.EnableKeyword(keyword);
        }

        private static void MakeMaterialTransparentBestEffort(global::UnityEngine.Material m)
        {
            if (m == null) return;

            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);

            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
#endif
