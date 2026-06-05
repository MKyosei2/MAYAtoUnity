// MAYAIMPORTER_PATCH_V14: JSON bridge importer with schema validation, profiling, and cache statistics
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
            return ParseJsonFile(path, options, out log, null);
        }

        public static MayaSceneData ParseJsonFile(string path, MayaImportOptions options, out MayaImportLog log, MayaImportProfiler profiler)
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
                profiler?.Begin("json_read", log);
                string json = File.ReadAllText(path);
                profiler?.End(log, "bytes=" + json.Length);

                profiler?.Begin("json_parse", log);
                MayaUnityExport export = JsonUtility.FromJson<MayaUnityExport>(json);
                profiler?.End(log, export != null ? "schemaVersion=" + export.schemaVersion : "parse failed");
                if (export == null)
                {
                    log.Error("Failed to parse exporter JSON: " + path);
                    return new MayaSceneData { SourcePath = path };
                }

                profiler?.Begin("schema_validation", log);
                MayaSchemaValidationResult validation = MayaUnityJsonSchemaValidator.Validate(export);
                for (int i = 0; i < validation.Warnings.Count; i++) log.Warn("JSON schema warning: " + validation.Warnings[i]);
                for (int i = 0; i < validation.Errors.Count; i++) log.Error("JSON schema error: " + validation.Errors[i]);
                profiler?.End(log, "warnings=" + validation.Warnings.Count + " errors=" + validation.Errors.Count);

                profiler?.Begin("scene_conversion", log);
                MayaSceneData scene = MayaUnityJsonSceneConverter.Convert(path, json, export, log);
                profiler?.End(log, scene != null ? "nodes=" + scene.Nodes.Count : "scene=null");
                return scene;
            }
            catch (Exception ex)
            {
                log.Error("JSON import failed: " + ex.GetType().Name + ": " + ex.Message);
                return new MayaSceneData { SourcePath = path };
            }
        }

        public static GameObject ImportJsonIntoScene(string path, MayaImportOptions options, out MayaSceneData scene, out MayaImportLog log)
        {
            MayaImportProfile ignored;
            return ImportJsonIntoSceneProfiled(path, options, out scene, out log, out ignored, null);
        }

        public static GameObject ImportJsonIntoSceneProfiled(string path, MayaImportOptions options, out MayaSceneData scene, out MayaImportLog log, out MayaImportProfile profile, string profileReportDirectory = null)
        {
            if (options == null) options = new MayaImportOptions();
            var profiler = new MayaImportProfiler(path);
            scene = ParseJsonFile(path, options, out log, profiler);

            GameObject root = null;
            try
            {
                profiler.Begin("hierarchy_build", log);
                var builder = new UnitySceneBuilder(options, log);
                root = builder.Build(scene);
                profiler.End(log, root != null ? root.name : "root=null");

                profiler.Begin("json_reload_for_runtime_attachment", log);
                MayaUnityExport export = LoadExport(path, log);
                profiler.End(log, export != null ? "ok" : "not available");

                if (export != null)
                {
                    var unsupported = new MayaUnsupportedFeatureRegistry();
                    unsupported.RegisterExportUnsupported(export);

                    profiler.Begin("context_build", log);
                    var context = MayaImportContext.Build(root);
                    profiler.End(log, "transformAliases=" + (context.TransformIndex != null ? context.TransformIndex.Count : 0));

                    profiler.Begin("material_build", log);
                    var materials = MayaUnityJsonRuntimeBuilder.BuildMaterials(export, options, log);
                    profiler.End(log, "materials=" + (materials != null ? materials.Count : 0));

                    profiler.Begin("mesh_skin_blendshape_attach", log);
                    AttachMeshesFromExport(export, root, context, options, materials, log);
                    profiler.End(log, context.Stats.ToReportString());

                    profiler.Begin("material_assign", log);
                    MayaUnityJsonRuntimeBuilder.AssignMaterialsToRenderers(context, export, materials, log);
                    profiler.End(log, context.Stats.ToReportString());

                    profiler.Begin("camera_attach", log);
                    MayaUnityJsonRuntimeBuilder.AttachCameras(context, export, options, log);
                    profiler.End(log);

                    profiler.Begin("light_attach", log);
                    MayaUnityJsonRuntimeBuilder.AttachLights(context, export, options, log);
                    profiler.End(log);

                    profiler.Begin("animation_attach", log);
                    MayaUnityJsonRuntimeBuilder.AttachAnimation(root, export, options, log);
                    profiler.End(log);

                    profiler.SetCacheStats(context);
                    log.Info(unsupported.ToMarkdown());
                    log.Info("Import cache stats: " + context.Stats.ToReportString());
                }

                profiler.Profile.Success = log == null || !log.HasErrors;
                return root;
            }
            catch (Exception ex)
            {
                log.Error("JSON scene build failed: " + ex.GetType().Name + ": " + ex.Message);
                root = new GameObject("MayaJsonScene_BuildFailed");
                profiler.Profile.Success = false;
                return root;
            }
            finally
            {
                if (options.GenerateImportReport)
                {
                    try
                    {
                        profiler.Begin("report_write", log);
                        MayaImportReport.WriteMarkdownReport(scene, options, log, root != null ? root.name : null);
                        profiler.End(log);
                    }
                    catch (Exception reportEx)
                    {
                        log.Warn("JSON import report generation failed: " + reportEx.GetType().Name + ": " + reportEx.Message);
                    }
                }

                profiler.WriteReports(profileReportDirectory, log);
                profile = profiler.Profile;
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

                context.GetOrAddComponent<MeshRenderer>(target);
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
