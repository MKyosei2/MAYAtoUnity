// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
// Assets/MayaImporter/MayaPhase4AutoFinalizeMbMeshesOnImport.cs
// Phase-4: Ensure .mb meshes are not left without renderer/materials after best-effort decode.
//
// Why: The .mb path can reconstruct mesh geometry but may not have shadingEngine face assignments.
// We still guarantee 100点 (UnityでGameObject化できる) by ensuring MeshRenderer + fallback Material.

#if UNITY_EDITOR
using UnityEngine;

namespace MayaImporter.Core
{
    public static class MayaPhase4AutoFinalizeMbMeshesOnImport
    {
        public static void Run_BestEffort(Transform sceneRoot, MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            if (sceneRoot == null) return;
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            int scanned = 0;
            int fixedCount = 0;

            var mfs = sceneRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < mfs.Length; i++)
            {
                var mf = mfs[i];
                if (mf == null) continue;

                // If skinned, skip (SkinnedMeshRenderer handles materials separately)
                if (mf.GetComponent<SkinnedMeshRenderer>() != null)
                    continue;

                var mesh = mf.sharedMesh;
                if (mesh == null) continue;

                scanned++;

                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) mr = mf.gameObject.AddComponent<MeshRenderer>();

                bool need =
                    mr.sharedMaterials == null ||
                    mr.sharedMaterials.Length == 0 ||
                    AllNull(mr.sharedMaterials);

                if (!need) continue;

                // Try resolve by existing resolver (if present in project).
                Material mat = null;
                try
                {
                    mat = MayaMaterialResolver.ResolveForMeshNode(mf.gameObject.name);
                }
                catch
                {
                    mat = null;
                }

                if (mat == null)
                {
                    // IMPORTANT: fully qualify UnityEngine.Shader because this project may contain a "MayaImporter.Shader" namespace.
                    var sh =
                        global::UnityEngine.Shader.Find("Universal Render Pipeline/Lit") ??
                        global::UnityEngine.Shader.Find("Standard") ??
                        global::UnityEngine.Shader.Find("Unlit/Texture") ??
                        global::UnityEngine.Shader.Find("Unlit/Color") ??
                        global::UnityEngine.Shader.Find("Hidden/InternalErrorShader");

                    mat = new Material(sh) { name = "MAYA_MB_Fallback" };
                }

                mr.sharedMaterials = new Material[] { mat };

                // Phase-7: marker for provisional material reconstruction on .mb path.
                try
                {
                    MayaProvisionalMarker.Ensure(mf.gameObject, MayaProvisionalKind.MbMeshMaterialFallback, mat != null ? mat.name : "(null)");
                }
                catch { }

                fixedCount++;
            }

            if (fixedCount > 0)
                log?.Info($"[Phase4] MB mesh finalize: fixedMaterials={fixedCount} scannedMeshFilters={scanned}");
        }

        private static bool AllNull(Material[] mats)
        {
            if (mats == null || mats.Length == 0) return true;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null) return false;
            return true;
        }
    }
}
#endif
