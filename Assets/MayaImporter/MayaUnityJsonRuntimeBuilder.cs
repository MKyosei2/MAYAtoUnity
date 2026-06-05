// MAYAIMPORTER_PATCH_V9: Runtime builders for JSON bridge material/texture/camera/light/animation/skinning/blendshape
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MayaImporter.Core
{
    public static class MayaUnityJsonRuntimeBuilder
    {
        public static Dictionary<string, Material> BuildMaterials(MayaUnityExport export, MayaImportOptions options, MayaImportLog log)
        {
            var map = new Dictionary<string, Material>(StringComparer.Ordinal);
            if (export == null || export.materials == null) return map;

            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Diffuse");

            foreach (var src in export.materials)
            {
                if (src == null || string.IsNullOrEmpty(src.name)) continue;
                Shader selected = shader != null ? shader : Shader.Find("Hidden/InternalErrorShader");
                if (selected == null) continue;

                var mat = new Material(selected);
                mat.name = src.name;
                Color c = ToColor(src.color, Color.white);
                try
                {
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                    mat.color = c;
                }
                catch { }

                Texture2D diffuse = TryLoadTexture(src.diffuseTexture, log);
                if (diffuse != null)
                {
                    try
                    {
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", diffuse);
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", diffuse);
                    }
                    catch { }
                }

                if (!map.ContainsKey(src.name)) map.Add(src.name, mat);
            }

            log?.Info("Built Unity Materials from Maya JSON: " + map.Count);
            return map;
        }

        public static void AttachCameras(GameObject root, MayaUnityExport export, MayaImportOptions options, MayaImportLog log)
        {
            if (root == null || export == null || export.cameras == null) return;
            var index = BuildTransformIndex(root);
            int count = 0;
            foreach (var cam in export.cameras)
            {
                Transform t = FindTransform(index, cam.path, cam.parentPath, cam.name, root.transform);
                Camera c = t.GetComponent<Camera>();
                if (c == null) c = t.gameObject.AddComponent<Camera>();
                c.nearClipPlane = cam.nearClipPlane > 0 ? cam.nearClipPlane : 0.1f;
                c.farClipPlane = cam.farClipPlane > c.nearClipPlane ? cam.farClipPlane : 1000f;
                if (cam.focalLength > 0f)
                {
                    c.usePhysicalProperties = true;
                    c.focalLength = cam.focalLength;
                }
                count++;
            }
            log?.Info("Attached Unity Cameras from Maya JSON: " + count);
        }

        public static void AttachLights(GameObject root, MayaUnityExport export, MayaImportOptions options, MayaImportLog log)
        {
            if (root == null || export == null || export.lights == null) return;
            var index = BuildTransformIndex(root);
            int count = 0;
            foreach (var src in export.lights)
            {
                Transform t = FindTransform(index, src.path, src.parentPath, src.name, root.transform);
                Light l = t.GetComponent<Light>();
                if (l == null) l = t.gameObject.AddComponent<Light>();
                string type = src.type ?? string.Empty;
                if (Contains(type, "directional")) l.type = LightType.Directional;
                else if (Contains(type, "spot")) l.type = LightType.Spot;
                else if (Contains(type, "area")) l.type = LightType.Area;
                else l.type = LightType.Point;
                l.color = ToColor(src.color, Color.white);
                l.intensity = src.intensity > 0 ? src.intensity : 1f;
                if (l.type == LightType.Spot && src.coneAngle > 0) l.spotAngle = src.coneAngle;
                count++;
            }
            log?.Info("Attached Unity Lights from Maya JSON: " + count);
        }

        public static void AttachAnimation(GameObject root, MayaUnityExport export, MayaImportOptions options, MayaImportLog log)
        {
            if (root == null || export == null || export.animations == null || export.animations.Length == 0) return;

            Animation animation = root.GetComponent<Animation>();
            if (animation == null) animation = root.AddComponent<Animation>();
            var clip = new AnimationClip();
            clip.name = options != null && !string.IsNullOrEmpty(options.AnimationClipName) ? options.AnimationClipName : "MayaJsonAnimation";
            clip.legacy = true;

            int curveCount = 0;
            foreach (var curve in export.animations)
            {
                if (curve == null || curve.times == null || curve.values == null) continue;
                int n = Mathf.Min(curve.times.Length, curve.values.Length);
                if (n == 0 || string.IsNullOrEmpty(curve.unityProperty)) continue;

                var keys = new Keyframe[n];
                for (int i = 0; i < n; i++)
                {
                    float time = curve.times[i];
                    if (options != null) time *= options.AnimationTimeScale;
                    keys[i] = new Keyframe(time, curve.values[i]);
                }

                var ac = new AnimationCurve(keys);
                string relativePath = MakeUnityRelativePath(root.transform, curve.targetPath);
                clip.SetCurve(relativePath, typeof(Transform), curve.unityProperty, ac);
                curveCount++;
            }

            if (curveCount > 0)
            {
                animation.AddClip(clip, clip.name);
                animation.clip = clip;
                log?.Info("Attached Unity AnimationClip from Maya JSON: curves=" + curveCount);
            }
        }

        public static bool AttachSkinnedMeshIfNeeded(GameObject root, Transform target, MayaUnityExportMesh src, Mesh mesh, Dictionary<string, Material> materials, MayaImportLog log)
        {
            if (root == null || target == null || src == null || mesh == null) return false;
            if (!MayaUnityJsonMeshBuilder.HasSkinning(src)) return false;

            var index = BuildTransformIndex(root);
            var bones = new Transform[src.skinJoints.Length];
            for (int i = 0; i < src.skinJoints.Length; i++)
            {
                Transform bone = FindTransform(index, src.skinJoints[i], null, src.skinJoints[i], root.transform);
                bones[i] = bone != null ? bone : root.transform;
            }

            Matrix4x4[] bindposes = BuildBindposes(src, bones, target);
            if (bindposes != null && bindposes.Length == bones.Length)
                mesh.bindposes = bindposes;

            MeshFilter mf = target.GetComponent<MeshFilter>();
            if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
            MeshRenderer mr = target.GetComponent<MeshRenderer>();
            if (mr != null) UnityEngine.Object.DestroyImmediate(mr);

            SkinnedMeshRenderer smr = target.GetComponent<SkinnedMeshRenderer>();
            if (smr == null) smr = target.gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones.Length > 0 ? bones[0] : root.transform;

            List<Material> assigned = ResolveMaterials(src, materials);
            if (assigned.Count > 0) smr.sharedMaterials = assigned.ToArray();
            ApplyBlendShapeWeights(smr, src, log);

            log?.Info("Attached SkinnedMeshRenderer: " + target.name + " bones=" + bones.Length);
            return true;
        }

        public static void ApplyBlendShapeWeights(Renderer renderer, MayaUnityExportMesh src, MayaImportLog log)
        {
            if (renderer == null || src == null || src.blendShapes == null) return;
            SkinnedMeshRenderer smr = renderer as SkinnedMeshRenderer;
            if (smr == null || smr.sharedMesh == null) return;
            ApplyBlendShapeWeights(smr, src, log);
        }

        private static void ApplyBlendShapeWeights(SkinnedMeshRenderer smr, MayaUnityExportMesh src, MayaImportLog log)
        {
            if (smr == null || smr.sharedMesh == null || src == null || src.blendShapes == null) return;
            int applied = 0;
            for (int i = 0; i < src.blendShapes.Length; i++)
            {
                var bs = src.blendShapes[i];
                if (bs == null || string.IsNullOrEmpty(bs.name)) continue;
                int index = smr.sharedMesh.GetBlendShapeIndex(bs.name);
                if (index < 0) continue;
                smr.SetBlendShapeWeight(index, bs.currentWeight);
                applied++;
            }
            if (applied > 0) log?.Info("Applied current blendshape weights: " + applied);
        }

        public static void AssignMaterialsToRenderers(GameObject root, MayaUnityExport export, Dictionary<string, Material> materials, MayaImportLog log)
        {
            if (root == null || export == null || export.meshes == null || materials == null) return;
            var index = BuildTransformIndex(root);
            int count = 0;
            foreach (var mesh in export.meshes)
            {
                Transform t = FindTransform(index, mesh.path, mesh.parentPath, mesh.name, root.transform);
                var renderer = t.GetComponent<Renderer>();
                if (renderer == null) continue;
                var assigned = ResolveMaterials(mesh, materials);
                if (assigned.Count > 0)
                {
                    renderer.sharedMaterials = assigned.ToArray();
                    count++;
                }
                ApplyBlendShapeWeights(renderer, mesh, log);
            }
            log?.Info("Assigned Unity Materials to renderers: " + count);
        }

        public static List<Material> ResolveMaterials(MayaUnityExportMesh mesh, Dictionary<string, Material> materials)
        {
            var assigned = new List<Material>();
            if (mesh == null || materials == null) return assigned;

            if (mesh.subMeshes != null && mesh.subMeshes.Length > 0)
            {
                foreach (var sub in mesh.subMeshes)
                {
                    AppendMaterial(assigned, materials, sub != null ? sub.material : null);
                }
            }
            else if (mesh.materials != null)
            {
                foreach (string name in mesh.materials)
                {
                    AppendMaterial(assigned, materials, name);
                }
            }

            if (assigned.Count == 0 && materials.Count > 0)
            {
                foreach (var kv in materials) { assigned.Add(kv.Value); break; }
            }
            return assigned;
        }

        private static void AppendMaterial(List<Material> assigned, Dictionary<string, Material> materials, string name)
        {
            if (assigned == null || materials == null || string.IsNullOrEmpty(name)) return;
            Material m;
            if (materials.TryGetValue(name, out m))
            {
                assigned.Add(m);
            }
        }

        public static Dictionary<string, Transform> BuildTransformIndex(GameObject root)
        {
            var map = new Dictionary<string, Transform>(StringComparer.Ordinal);
            if (root == null) return map;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                string path = BuildUnityPathRelativeToRoot(root.transform, t);
                if (!string.IsNullOrEmpty(path) && !map.ContainsKey(path)) map.Add(path, t);
                if (!string.IsNullOrEmpty(t.name) && !map.ContainsKey(t.name)) map.Add(t.name, t);
            }
            return map;
        }

        public static Transform FindTransform(Dictionary<string, Transform> index, string path, string parentPath, string name, Transform fallback)
        {
            Transform t;
            string stable = StableName(path, name);
            if (!string.IsNullOrEmpty(stable) && index != null && index.TryGetValue(stable, out t)) return t;
            string parent = StableName(parentPath, null);
            if (!string.IsNullOrEmpty(parent) && index != null && index.TryGetValue(parent, out t)) return t;
            if (!string.IsNullOrEmpty(name) && index != null && index.TryGetValue(name, out t)) return t;
            return fallback;
        }

        public static string BuildUnityPathRelativeToRoot(Transform root, Transform target)
        {
            if (root == null || target == null) return string.Empty;
            var parts = new List<string>();
            Transform t = target;
            while (t != null && t != root)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("|", parts.ToArray());
        }

        public static string MakeUnityRelativePath(Transform root, string mayaPath)
        {
            if (string.IsNullOrEmpty(mayaPath)) return string.Empty;
            string s = StableName(mayaPath, null);
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace('|', '/');
        }

        public static string StableName(string path, string name)
        {
            if (!string.IsNullOrEmpty(path)) return path.Trim().Trim('|');
            return string.IsNullOrEmpty(name) ? string.Empty : name.Trim();
        }

        private static Matrix4x4[] BuildBindposes(MayaUnityExportMesh src, Transform[] bones, Transform meshTransform)
        {
            if (src.bindposes != null && src.bindposes.Length == bones.Length * 16)
            {
                var bindposes = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    int o = i * 16;
                    var m = new Matrix4x4();
                    m.m00 = src.bindposes[o + 0]; m.m01 = src.bindposes[o + 1]; m.m02 = src.bindposes[o + 2]; m.m03 = src.bindposes[o + 3];
                    m.m10 = src.bindposes[o + 4]; m.m11 = src.bindposes[o + 5]; m.m12 = src.bindposes[o + 6]; m.m13 = src.bindposes[o + 7];
                    m.m20 = src.bindposes[o + 8]; m.m21 = src.bindposes[o + 9]; m.m22 = src.bindposes[o + 10]; m.m23 = src.bindposes[o + 11];
                    m.m30 = src.bindposes[o + 12]; m.m31 = src.bindposes[o + 13]; m.m32 = src.bindposes[o + 14]; m.m33 = src.bindposes[o + 15];
                    bindposes[i] = m;
                }
                return bindposes;
            }

            var fallback = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                fallback[i] = bones[i].worldToLocalMatrix * meshTransform.localToWorldMatrix;
            return fallback;
        }

        private static Texture2D TryLoadTexture(string path, MayaImportLog log)
        {
            if (string.IsNullOrEmpty(path)) return null;

#if UNITY_EDITOR
            string assetPath = ToAssetPath(path);
            if (!string.IsNullOrEmpty(assetPath))
            {
                Texture2D assetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (assetTexture != null)
                {
                    log?.Info("Loaded texture asset: " + assetPath);
                    return assetTexture;
                }
            }
#endif

            try
            {
                if (!File.Exists(path)) return null;
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (tex.LoadImage(bytes))
                {
                    tex.name = Path.GetFileNameWithoutExtension(path);
                    log?.Info("Loaded texture from file: " + path);
                    return tex;
                }
            }
            catch (Exception ex)
            {
                log?.Warn("Texture load failed: " + path + " / " + ex.Message);
            }

            return null;
        }

#if UNITY_EDITOR
        private static string ToAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return normalized;

            string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/').TrimEnd('/');
            if (normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                string rel = normalized.Substring(projectRoot.Length + 1);
                if (rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return rel;
            }
            return string.Empty;
        }
#endif

        private static Color ToColor(float[] values, Color fallback)
        {
            if (values == null || values.Length < 3) return fallback;
            float a = values.Length >= 4 ? values[3] : 1f;
            return new Color(values[0], values[1], values[2], a);
        }

        private static bool Contains(string a, string b)
        {
            return !string.IsNullOrEmpty(a) && a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
