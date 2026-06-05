// MAYAIMPORTER_PATCH_V5: Unity-side JSON bridge importer + mesh attachment
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MayaImporter.Core
{
    public static class MayaUnityJsonImporter
    {
        public static bool IsSupportedFilePath(string path)
        {
            return !string.IsNullOrEmpty(path) && Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase);
        }

        public static MayaSceneData ParseJsonFile(string path, MayaImportOptions options, out MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log = new MayaImportLog();

            if (string.IsNullOrEmpty(path))
            {
                log.Error("JSON path is null/empty.");
                return new MayaSceneData { SourcePath = path };
            }

            if (!File.Exists(path))
            {
                log.Error("JSON file not found: " + path);
                return new MayaSceneData { SourcePath = path };
            }

            try
            {
                string json = File.ReadAllText(path);
                MayaUnityExport export = JsonUtility.FromJson<MayaUnityExport>(json);
                if (export == null)
                {
                    log.Error("Failed to parse exporter JSON: " + path);
                    return new MayaSceneData { SourcePath = path };
                }

                return MayaUnityJsonSceneConverter.Convert(path, json, export, log);
            }
            catch (Exception ex)
            {
                log.Error("JSON import failed: " + ex.GetType().Name + ": " + ex.Message);
                return new MayaSceneData { SourcePath = path };
            }
        }

        public static GameObject ImportJsonIntoScene(string path, MayaImportOptions options, out MayaSceneData scene, out MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            scene = ParseJsonFile(path, options, out log);

            GameObject root = null;
            try
            {
                var builder = new UnitySceneBuilder(options, log);
                root = builder.Build(scene);
                AttachMeshesFromJson(path, root, options, log);
                return root;
            }
            catch (Exception ex)
            {
                log.Error("JSON scene build failed: " + ex.GetType().Name + ": " + ex.Message);
                root = new GameObject("MayaJsonScene_BuildFailed");
                return root;
            }
            finally
            {
                if (options.GenerateImportReport)
                {
                    try
                    {
                        MayaImportReport.WriteMarkdownReport(scene, options, log, root != null ? root.name : null);
                    }
                    catch (Exception reportEx)
                    {
                        log.Warn("JSON import report generation failed: " + reportEx.GetType().Name + ": " + reportEx.Message);
                    }
                }
            }
        }

        private static void AttachMeshesFromJson(string path, GameObject root, MayaImportOptions options, MayaImportLog log)
        {
            if (root == null || string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            MayaUnityExport export;
            try
            {
                export = JsonUtility.FromJson<MayaUnityExport>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                log?.Warn("Could not reload JSON for mesh attachment: " + ex.Message);
                return;
            }

            if (export == null || export.meshes == null || export.meshes.Length == 0) return;

            Dictionary<string, Transform> byMayaName = BuildTransformIndex(root);
            int attached = 0;

            foreach (var m in export.meshes)
            {
                if (!MayaUnityJsonMeshBuilder.HasTopology(m)) continue;
                Mesh mesh = MayaUnityJsonMeshBuilder.BuildMesh(m, options, log);
                if (mesh == null) continue;

                string shapeName = StableName(m.path, m.name);
                string parentName = StableName(m.parentPath, null);
                Transform target = null;

                if (!string.IsNullOrEmpty(shapeName)) byMayaName.TryGetValue(shapeName, out target);
                if (target == null && !string.IsNullOrEmpty(parentName)) byMayaName.TryGetValue(parentName, out target);
                if (target == null) target = root.transform;

                // Prefer assigning to the transform parent so Unity shows an actual renderable object
                // where artists expect the mesh to be, while the shape node still remains inspectable.
                MeshFilter mf = target.GetComponent<MeshFilter>();
                if (mf == null) mf = target.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                MeshRenderer mr = target.GetComponent<MeshRenderer>();
                if (mr == null) mr = target.gameObject.AddComponent<MeshRenderer>();

                attached++;
            }

            log?.Info("Attached Unity meshes from Maya JSON: " + attached);
        }

        private static Dictionary<string, Transform> BuildTransformIndex(GameObject root)
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

        private static string BuildUnityPathRelativeToRoot(Transform root, Transform target)
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

        private static string StableName(string path, string name)
        {
            if (!string.IsNullOrEmpty(path)) return path.Trim().Trim('|');
            return string.IsNullOrEmpty(name) ? string.Empty : name.Trim();
        }
    }
}
