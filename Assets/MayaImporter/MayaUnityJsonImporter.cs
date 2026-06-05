// MAYAIMPORTER_PATCH_V10: Unity-side JSON bridge importer + mesh/material/camera/light/animation/skinning/blendshape attachment
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
            if (options == null) options = new MayaImportOptions();
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
            if (options == null) options = new MayaImportOptions();
            scene = ParseJsonFile(path, options, out log);

            GameObject root = null;
            try
            {
                var builder = new UnitySceneBuilder(options, log);
                root = builder.Build(scene);

                MayaUnityExport export = LoadExport(path, log);
                if (export != null)
                {
                    var materials = MayaUnityJsonRuntimeBuilder.BuildMaterials(export, options, log);
                    AttachMeshesFromExport(export, root, options, materials, log);
                    MayaUnityJsonRuntimeBuilder.AssignMaterialsToRenderers(root, export, materials, log);
                    MayaUnityJsonRuntimeBuilder.AttachCameras(root, export, options, log);
                    MayaUnityJsonRuntimeBuilder.AttachLights(root, export, options, log);
                    MayaUnityJsonRuntimeBuilder.AttachAnimation(root, export, options, log);
                }

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

        private static MayaUnityExport LoadExport(string path, MayaImportLog log)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                return JsonUtility.FromJson<MayaUnityExport>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                if (log != null) log.Warn("Could not reload JSON for runtime attachment: " + ex.Message);
                return null;
            }
        }

        private static void AttachMeshesFromExport(MayaUnityExport export, GameObject root, MayaImportOptions options, Dictionary<string, Material> materials, MayaImportLog log)
        {
            if (root == null || export == null || export.meshes == null || export.meshes.Length == 0) return;

            Dictionary<string, Transform> byMayaName = MayaUnityJsonRuntimeBuilder.BuildTransformIndex(root);
            int staticAttached = 0;
            int skinnedAttached = 0;
            int blendShapeAttached = 0;

            foreach (var m in export.meshes)
            {
                if (!MayaUnityJsonMeshBuilder.HasTopology(m)) continue;
                Mesh mesh = MayaUnityJsonMeshBuilder.BuildMesh(m, options, log);
                if (mesh == null) continue;

                Transform target = MayaUnityJsonRuntimeBuilder.FindTransform(byMayaName, m.path, m.parentPath, m.name, root.transform);

                bool skinned = MayaUnityJsonRuntimeBuilder.AttachSkinnedMeshIfNeeded(root, target, m, mesh, materials, log);
                if (skinned)
                {
                    skinnedAttached++;
                    continue;
                }

                if (HasBlendShapes(m))
                {
                    AttachBlendShapeRenderer(target, m, mesh, materials, log);
                    blendShapeAttached++;
                    continue;
                }

                MeshFilter mf = target.GetComponent<MeshFilter>();
                if (mf == null) mf = target.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                MeshRenderer mr = target.GetComponent<MeshRenderer>();
                if (mr == null) mr = target.gameObject.AddComponent<MeshRenderer>();

                staticAttached++;
            }

            if (log != null) log.Info("Attached Unity meshes from Maya JSON: static=" + staticAttached + " skinned=" + skinnedAttached + " blendShape=" + blendShapeAttached);
        }

        private static bool HasBlendShapes(MayaUnityExportMesh mesh)
        {
            return mesh != null && mesh.blendShapes != null && mesh.blendShapes.Length > 0;
        }

        private static void AttachBlendShapeRenderer(Transform target, MayaUnityExportMesh src, Mesh mesh, Dictionary<string, Material> materials, MayaImportLog log)
        {
            if (target == null || mesh == null) return;

            MeshFilter mf = target.GetComponent<MeshFilter>();
            if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
            MeshRenderer mr = target.GetComponent<MeshRenderer>();
            if (mr != null) UnityEngine.Object.DestroyImmediate(mr);

            SkinnedMeshRenderer smr = target.GetComponent<SkinnedMeshRenderer>();
            if (smr == null) smr = target.gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;

            List<Material> assigned = MayaUnityJsonRuntimeBuilder.ResolveMaterials(src, materials);
            if (assigned.Count > 0) smr.sharedMaterials = assigned.ToArray();
            MayaUnityJsonRuntimeBuilder.ApplyBlendShapeWeights(smr, src, log);

            if (log != null) log.Info("Attached BlendShape SkinnedMeshRenderer without skin bones: " + target.name);
        }
    }
}
