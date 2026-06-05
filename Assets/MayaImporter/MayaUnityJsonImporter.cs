// MAYAIMPORTER_PATCH_V13: Unity-side JSON bridge importer with shared import context cache
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
                    var context = MayaImportContext.Build(root);
                    var materials = MayaUnityJsonRuntimeBuilder.BuildMaterials(export, options, log);
                    AttachMeshesFromExport(export, root, context, options, materials, log);
                    MayaUnityJsonRuntimeBuilder.AssignMaterialsToRenderers(context, export, materials, log);
                    MayaUnityJsonRuntimeBuilder.AttachCameras(context, export, options, log);
                    MayaUnityJsonRuntimeBuilder.AttachLights(context, export, options, log);
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

        private static void AttachMeshesFromExport(MayaUnityExport export, GameObject root, MayaImportContext context, MayaImportOptions options, Dictionary<string, Material> materials, MayaImportLog log)
        {
            if (root == null || context == null || export == null || export.meshes == null || export.meshes.Length == 0) return;

            int staticAttached = 0;
            int skinnedAttached = 0;
            int blendShapeAttached = 0;

            foreach (var m in export.meshes)
            {
                if (!MayaUnityJsonMeshBuilder.HasTopology(m)) continue;
                Mesh mesh = MayaUnityJsonMeshBuilder.BuildMesh(m, options, log);
                if (mesh == null) continue;

                Transform target = context.FindTransform(m.path, m.parentPath, m.name, root.transform);
                if (target == null)
                {
                    if (log != null) log.Warn("Could not find target transform for JSON mesh: " + m.name);
                    continue;
                }

                bool skinned = MayaUnityJsonRuntimeBuilder.AttachSkinnedMeshIfNeeded(context, target, m, mesh, materials, log);
                if (skinned)
                {
                    skinnedAttached++;
                    continue;
                }

                if (HasBlendShapes(m))
                {
                    AttachBlendShapeRenderer(context, target, m, mesh, materials, log);
                    blendShapeAttached++;
                    continue;
                }

                MeshFilter mf = context.GetOrAddComponent<MeshFilter>(target);
                mf.sharedMesh = mesh;

                MeshRenderer mr = context.GetOrAddComponent<MeshRenderer>(target);
                if (mr != null) { }

                staticAttached++;
            }

            if (log != null) log.Info("Attached Unity meshes from Maya JSON: static=" + staticAttached + " skinned=" + skinnedAttached + " blendShape=" + blendShapeAttached);
        }

        private static bool HasBlendShapes(MayaUnityExportMesh mesh)
        {
            return mesh != null && mesh.blendShapes != null && mesh.blendShapes.Length > 0;
        }

        private static void AttachBlendShapeRenderer(MayaImportContext context, Transform target, MayaUnityExportMesh src, Mesh mesh, Dictionary<string, Material> materials, MayaImportLog log)
        {
            if (target == null || mesh == null) return;

            MeshFilter mf = target.GetComponent<MeshFilter>();
            MayaUnityJsonRuntimeBuilder.DestroyComponentSafe(mf);
            if (context != null) context.InvalidateComponent<MeshFilter>(target);

            MeshRenderer mr = target.GetComponent<MeshRenderer>();
            MayaUnityJsonRuntimeBuilder.DestroyComponentSafe(mr);
            if (context != null) context.InvalidateComponent<MeshRenderer>(target);

            SkinnedMeshRenderer smr = context != null ? context.GetOrAddComponent<SkinnedMeshRenderer>(target) : target.gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;

            List<Material> assigned = MayaUnityJsonRuntimeBuilder.ResolveMaterials(src, materials);
            if (assigned.Count > 0) smr.sharedMaterials = assigned.ToArray();
            MayaUnityJsonRuntimeBuilder.ApplyBlendShapeWeights(smr, src, log);

            if (log != null) log.Info("Attached BlendShape SkinnedMeshRenderer without skin bones: " + target.name);
        }
    }
}
