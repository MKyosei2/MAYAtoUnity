// MAYAIMPORTER_PATCH_V6: Runtime builders for JSON bridge material/camera/light/animation
using System;
using System.Collections.Generic;
using UnityEngine;

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
                var mat = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
                mat.name = src.name;
                Color c = ToColor(src.color, Color.white);
                try { mat.color = c; } catch { }
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

                var assigned = new List<Material>();
                if (mesh.materials != null)
                {
                    foreach (string name in mesh.materials)
                    {
                        if (string.IsNullOrEmpty(name)) continue;
                        Material m;
                        if (materials.TryGetValue(name, out m)) assigned.Add(m);
                    }
                }
                if (assigned.Count == 0 && materials.Count > 0)
                {
                    foreach (var kv in materials) { assigned.Add(kv.Value); break; }
                }
                if (assigned.Count > 0)
                {
                    renderer.sharedMaterials = assigned.ToArray();
                    count++;
                }
            }
            log?.Info("Assigned Unity Materials to renderers: " + count);
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
