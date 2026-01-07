// Assets/MayaImporter/MayaAssetPipeline.cs
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Production Assetization:
    /// - Legacy(手動Window導線): 生成した Scene の GameObject から Mesh/Material/Texture/Anim/Prefab を Assets に保存
    /// - Importer導線(ScriptedImporter想定): AssetImportContext に SubAsset として登録し、メインObjectを設定
    ///
    /// ポリシー:
    /// - Maya/Autodesk API 依存なし
    /// - “捨てない” を優先し、保存できないものは Manifest に記録して Import 継続
    /// </summary>
    public static class MayaAssetPipeline
    {
        // ---------- Public: Legacy bridge ----------
        /// <summary>
        /// Legacy導線（EditorWindow等）:
        /// Scene上に生成した root を、Assetsに保存（Mesh/Mat/Texture/Anim/Prefab）します。
        /// ※ Production 推奨は ScriptedImporter だが、比較/デバッグ用に残す。
        /// </summary>
        public static void AssetizeImportedRoot(GameObject importedRoot, MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            if (importedRoot == null)
            {
                log?.Error("AssetizeImportedRoot: importedRoot is null.");
                return;
            }

            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            if (!options.SaveAssets)
            {
                log.Info("AssetizeImportedRoot: SaveAssets=false, nothing to do.");
                return;
            }

            var outFolder = NormalizeAssetsFolder(options.OutputFolder);
            EnsureFolder(outFolder);

            var report = new AssetizeReport();

            // 1) Mesh assets
            if (options.SaveMeshes)
                SaveMeshes_AsAssets(importedRoot, outFolder, report, log);

            // 2) Material assets (and remap renderers)
            if (options.SaveMaterials)
                SaveMaterials_AsAssets(importedRoot, outFolder, report, log);

            // 3) Texture assets
            if (options.SaveTextures)
                SaveTextures_AsAssets(importedRoot, outFolder, report, log);

            // 4) AnimationClip assets
            if (options.SaveAnimationClip)
                SaveAnimationClips_AsAssets(importedRoot, outFolder, options, report, log);

            // 5) Prefab
            if (options.SavePrefab)
                SavePrefab_AsAsset(importedRoot, outFolder, report, log);

            // 6) Manifest
            try
            {
                var manifest = MayaImportedAssetManifest.CreateFrom(importedRoot, scene, options, report, log, sourceHint: scene?.SourcePath ?? "");
                var manifestPath = MakeUniqueAssetPath(Path.Combine(outFolder, $"{SanitizeName(importedRoot.name)}__Manifest.asset").Replace('\\', '/'));
                AssetDatabase.CreateAsset(manifest, manifestPath);
                report.ManifestAssetPath = manifestPath;
                log.Info("Wrote Manifest: " + manifestPath);
            }
            catch (Exception e)
            {
                log.Error("Manifest creation failed: " + e.Message);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.Info($"AssetizeImportedRoot done. Mesh={report.MeshAssets.Count}, Mat={report.MaterialAssets.Count}, Tex={report.TextureAssets.Count}, Anim={report.AnimationClipAssets.Count}, Prefab={report.PrefabAssetPath}");
        }

        // ---------- Public: Importer pipeline ----------
        /// <summary>
        /// ScriptedImporter導線:
        /// - root を ctx の main asset にする
        /// - Mesh/Material/Texture/Anim を sub-asset 化して ctx に登録する
        /// - Manifest を sub-asset として登録する
        /// </summary>
        public static void AssetizeForImporter(
            AssetImportContext ctx,
            GameObject importedRoot,
            MayaSceneData scene,
            MayaImportOptions options,
            MayaImportLog log)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (importedRoot == null)
            {
                log?.Error("AssetizeForImporter: importedRoot is null.");
                return;
            }

            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var report = new AssetizeReport();

            // main object
            ctx.AddObjectToAsset("main", importedRoot);
            ctx.SetMainObject(importedRoot);

            // Mesh
            if (options.SaveMeshes)
                AddMeshes_AsSubAssets(ctx, importedRoot, report, log);

            // Material
            if (options.SaveMaterials)
                AddMaterials_AsSubAssets(ctx, importedRoot, report, log);

            // Texture
            if (options.SaveTextures)
                AddTextures_AsSubAssets(ctx, importedRoot, report, log);

            // Animation
            if (options.SaveAnimationClip)
                AddAnimationClips_AsSubAssets(ctx, importedRoot, options, report, log);

            // Manifest (always add – proof & audit)
            try
            {
                var manifest = MayaImportedAssetManifest.CreateFrom(importedRoot, scene, options, report, log, sourceHint: ctx.assetPath);
                ctx.AddObjectToAsset("manifest", manifest);
            }
            catch (Exception e)
            {
                log.Error("Manifest sub-asset failed: " + e.Message);
            }

            // Register dependencies (best-effort):
            // MayaSceneData に “依存ファイル一覧” が存在する場合のみ拾う（存在しなくてもコンパイルが通るよう Reflection で読む）
            try
            {
                foreach (var dep in EnumerateSceneDependencies_BestEffort(scene))
                {
                    if (string.IsNullOrEmpty(dep)) continue;

                    var unityPath = NormalizeAssetsPath(dep);
                    if (!string.IsNullOrEmpty(unityPath) && unityPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        // Source dependency (reimport trigger)
                        ctx.DependsOnSourceAsset(unityPath);
                    }
                }
            }
            catch
            {
                // best-effort: ignore
            }

            log.Info($"AssetizeForImporter done. SubAssets: Mesh={report.MeshSubAssetIds.Count}, Mat={report.MaterialSubAssetIds.Count}, Tex={report.TextureSubAssetIds.Count}, Anim={report.AnimationClipSubAssetIds.Count}");
        }

        // ---------- Report ----------
        public sealed class AssetizeReport
        {
            public readonly List<string> MeshAssets = new List<string>(128);
            public readonly List<string> MaterialAssets = new List<string>(128);
            public readonly List<string> TextureAssets = new List<string>(128);
            public readonly List<string> AnimationClipAssets = new List<string>(128);
            public string PrefabAssetPath = "";
            public string ManifestAssetPath = "";

            // Importer sub-asset identifiers (for manifest)
            public readonly List<string> MeshSubAssetIds = new List<string>(128);
            public readonly List<string> MaterialSubAssetIds = new List<string>(128);
            public readonly List<string> TextureSubAssetIds = new List<string>(128);
            public readonly List<string> AnimationClipSubAssetIds = new List<string>(128);
        }

        // ---------- Legacy: Mesh ----------
        private static void SaveMeshes_AsAssets(GameObject root, string outFolder, AssetizeReport report, MayaImportLog log)
        {
            var meshes = CollectRuntimeMeshes(root);

            int saved = 0;
            foreach (var m in meshes)
            {
                if (m == null) continue;
                if (AssetDatabase.Contains(m)) continue;

                var path = MakeUniqueAssetPath(Path.Combine(outFolder, $"{SanitizeName(m.name)}.asset").Replace('\\', '/'));
                try
                {
                    AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(m), path);
                    report.MeshAssets.Add(path);
                    saved++;
                }
                catch (Exception e)
                {
                    log.Error("Save mesh failed: " + e.Message);
                }
            }

            if (saved > 0) log.Info($"Saved Mesh assets: {saved}");
        }

        // ---------- Legacy: Material ----------
        private static void SaveMaterials_AsAssets(GameObject root, string outFolder, AssetizeReport report, MayaImportLog log)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var seen = new Dictionary<Material, Material>(ReferenceEqualityComparer<Material>.Instance);

            int saved = 0;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null) continue;

                    if (AssetDatabase.Contains(mat))
                        continue;

                    if (!seen.TryGetValue(mat, out var savedMat))
                    {
                        try
                        {
                            var clone = UnityEngine.Object.Instantiate(mat);
                            clone.name = SanitizeName(mat.name);

                            var path = MakeUniqueAssetPath(Path.Combine(outFolder, $"{clone.name}.mat").Replace('\\', '/'));
                            AssetDatabase.CreateAsset(clone, path);

                            savedMat = clone;
                            seen[mat] = savedMat;

                            report.MaterialAssets.Add(path);
                            saved++;
                        }
                        catch (Exception e)
                        {
                            log.Error("Save material failed: " + e.Message);
                            continue;
                        }
                    }

                    mats[i] = savedMat;
                    changed = true;
                }

                if (changed)
                    r.sharedMaterials = mats;
            }

            if (saved > 0) log.Info($"Saved Material assets: {saved}");
        }

        // ---------- Legacy: Texture ----------
        private static void SaveTextures_AsAssets(GameObject root, string outFolder, AssetizeReport report, MayaImportLog log)
        {
            var mats = CollectAllMaterials(root);
            var texSet = new HashSet<Texture>(ReferenceEqualityComparer<Texture>.Instance);

            foreach (var mat in mats)
            {
                if (mat == null) continue;
                CollectTexturesFromMaterial(mat, texSet);
            }

            int saved = 0;
            foreach (var t in texSet)
            {
                if (t == null) continue;
                if (AssetDatabase.Contains(t)) continue;

                try
                {
                    var clone = UnityEngine.Object.Instantiate(t);
                    clone.name = SanitizeName(t.name);

                    // store as .asset (lossless editor-time, no need to encode png)
                    var path = MakeUniqueAssetPath(Path.Combine(outFolder, $"{clone.name}.asset").Replace('\\', '/'));
                    AssetDatabase.CreateAsset(clone, path);

                    report.TextureAssets.Add(path);
                    saved++;
                }
                catch (Exception e)
                {
                    log.Error("Save Texture failed: " + e.Message);
                }
            }

            if (saved > 0) log.Info($"Saved Texture assets: {saved}");
        }

        // ---------- Legacy: Animation ----------
        private static void SaveAnimationClips_AsAssets(GameObject root, string outFolder, MayaImportOptions options, AssetizeReport report, MayaImportLog log)
        {
            var clips = CollectRuntimeAnimationClips(root, options);
            int saved = 0;

            foreach (var c in clips)
            {
                if (c == null) continue;
                if (AssetDatabase.Contains(c)) continue;

                try
                {
                    var clone = UnityEngine.Object.Instantiate(c);
                    clone.name = SanitizeName(clone.name);

                    var path = MakeUniqueAssetPath(Path.Combine(outFolder, $"{clone.name}.anim").Replace('\\', '/'));
                    AssetDatabase.CreateAsset(clone, path);

                    report.AnimationClipAssets.Add(path);
                    saved++;
                }
                catch (Exception e)
                {
                    log.Error("Save AnimationClip failed: " + e.Message);
                }
            }

            if (saved > 0) log.Info($"Saved AnimationClip assets: {saved}");
        }

        // ---------- Legacy: Prefab ----------
        private static void SavePrefab_AsAsset(GameObject root, string outFolder, AssetizeReport report, MayaImportLog log)
        {
            try
            {
                var prefabPath = MakeUniqueAssetPath(Path.Combine(outFolder, $"{SanitizeName(root.name)}.prefab").Replace('\\', '/'));
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab != null)
                {
                    report.PrefabAssetPath = prefabPath;
                    log.Info("Saved Prefab: " + prefabPath);
                }
                else
                {
                    log.Error("SaveAsPrefabAsset returned null.");
                }
            }
            catch (Exception e)
            {
                log.Error("Save Prefab failed: " + e.Message);
            }
        }

        // ---------- Importer: Mesh ----------
        private static void AddMeshes_AsSubAssets(AssetImportContext ctx, GameObject root, AssetizeReport report, MayaImportLog log)
        {
            var meshes = CollectRuntimeMeshes(root);
            int added = 0;

            foreach (var m in meshes)
            {
                if (m == null) continue;

                var clone = UnityEngine.Object.Instantiate(m);
                clone.name = SanitizeName(clone.name);

                var id = $"mesh__{clone.name}__{added}";
                ctx.AddObjectToAsset(id, clone);

                report.MeshSubAssetIds.Add(id);
                added++;
            }

            log.Info($"Importer sub-assets Mesh added: {added}");
        }

        // ---------- Importer: Material ----------
        private static void AddMaterials_AsSubAssets(AssetImportContext ctx, GameObject root, AssetizeReport report, MayaImportLog log)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var seen = new Dictionary<Material, Material>(ReferenceEqualityComparer<Material>.Instance);

            int added = 0;

            foreach (var r in renderers)
            {
                if (r == null) continue;

                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null) continue;

                    if (!seen.TryGetValue(mat, out var clone))
                    {
                        clone = UnityEngine.Object.Instantiate(mat);
                        clone.name = SanitizeName(clone.name);

                        var id = $"mat__{clone.name}__{added}";
                        ctx.AddObjectToAsset(id, clone);

                        seen[mat] = clone;
                        report.MaterialSubAssetIds.Add(id);
                        added++;
                    }

                    mats[i] = clone;
                    changed = true;
                }

                if (changed)
                    r.sharedMaterials = mats;
            }

            log.Info($"Importer sub-assets Material added: {added}");
        }

        // ---------- Importer: Texture ----------
        private static void AddTextures_AsSubAssets(AssetImportContext ctx, GameObject root, AssetizeReport report, MayaImportLog log)
        {
            var mats = CollectAllMaterials(root);
            var texSet = new HashSet<Texture>(ReferenceEqualityComparer<Texture>.Instance);

            foreach (var mat in mats)
            {
                if (mat == null) continue;
                CollectTexturesFromMaterial(mat, texSet);
            }

            int added = 0;
            foreach (var t in texSet)
            {
                if (t == null) continue;

                // If already a project asset reference, keep reference (dependency is handled elsewhere)
                if (AssetDatabase.Contains(t))
                    continue;

                try
                {
                    var clone = UnityEngine.Object.Instantiate(t);
                    clone.name = SanitizeName(clone.name);

                    var id = $"tex__{clone.name}__{added}";
                    ctx.AddObjectToAsset(id, clone);

                    report.TextureSubAssetIds.Add(id);
                    added++;
                }
                catch (Exception e)
                {
                    log.Error("Importer sub-asset Texture failed: " + e.Message);
                }
            }

            log.Info($"Importer sub-assets Texture added: {added}");
        }

        // ---------- Importer: Animation ----------
        private static void AddAnimationClips_AsSubAssets(AssetImportContext ctx, GameObject root, MayaImportOptions options, AssetizeReport report, MayaImportLog log)
        {
            var clips = CollectRuntimeAnimationClips(root, options);
            int added = 0;

            foreach (var c in clips)
            {
                if (c == null) continue;

                if (AssetDatabase.Contains(c))
                    continue;

                try
                {
                    var clone = UnityEngine.Object.Instantiate(c);
                    clone.name = SanitizeName(clone.name);

                    var id = $"anim__{clone.name}__{added}";
                    ctx.AddObjectToAsset(id, clone);

                    report.AnimationClipSubAssetIds.Add(id);
                    added++;
                }
                catch (Exception e)
                {
                    log.Error("Importer sub-asset AnimationClip failed: " + e.Message);
                }
            }

            log.Info($"Importer sub-assets AnimationClip added: {added}");
        }

        // ---------- Collectors ----------
        private static List<Mesh> CollectRuntimeMeshes(GameObject root)
        {
            var set = new HashSet<Mesh>(ReferenceEqualityComparer<Mesh>.Instance);

            var mfs = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < mfs.Length; i++)
            {
                var mf = mfs[i];
                if (mf == null) continue;
                var m = mf.sharedMesh;
                if (m != null) set.Add(m);
            }

            var sks = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < sks.Length; i++)
            {
                var sk = sks[i];
                if (sk == null) continue;
                var m = sk.sharedMesh;
                if (m != null) set.Add(m);
            }

            return set.ToList();
        }

        private static List<Material> CollectAllMaterials(GameObject root)
        {
            var set = new HashSet<Material>(ReferenceEqualityComparer<Material>.Instance);
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int j = 0; j < mats.Length; j++)
                {
                    var mat = mats[j];
                    if (mat != null) set.Add(mat);
                }
            }

            return set.ToList();
        }

        private static void CollectTexturesFromMaterial(Material mat, HashSet<Texture> outSet)
        {
            if (mat == null || outSet == null) return;
            var shader = mat.shader;
            if (shader == null) return;

            int props = shader.GetPropertyCount();
            for (int i = 0; i < props; i++)
            {
                if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture)
                    continue;

                var name = shader.GetPropertyName(i);
                if (string.IsNullOrEmpty(name)) continue;

                var tex = mat.GetTexture(name);
                if (tex != null) outSet.Add(tex);
            }
        }

        private static List<AnimationClip> CollectRuntimeAnimationClips(GameObject root, MayaImportOptions options)
        {
            var set = new HashSet<AnimationClip>(ReferenceEqualityComparer<AnimationClip>.Instance);

            // NOTE: "Animation" が namespace と衝突するプロジェクトがあるため完全修飾
            var anims = root.GetComponentsInChildren<UnityEngine.Animation>(true);
            for (int i = 0; i < anims.Length; i++)
            {
                var a = anims[i];
                if (a == null) continue;

                foreach (UnityEngine.AnimationState st in a)
                {
                    if (st == null) continue;
                    if (st.clip != null) set.Add(st.clip);
                }

                if (a.clip != null) set.Add(a.clip);
            }

            return set.ToList();
        }

        // ---------- Dependencies (best-effort, reflection) ----------
        private static IEnumerable<string> EnumerateSceneDependencies_BestEffort(MayaSceneData scene)
        {
            if (scene == null) yield break;

            // よくありがちな名前を候補として拾う（存在しない場合でもコンパイルは通る）
            // 例: DependencyFiles / Dependencies / ReferencedFiles / ExternalFiles など
            var type = scene.GetType();
            var names = new[]
            {
                "DependencyFiles",
                "Dependencies",
                "SourceDependencies",
                "ExternalFiles",
                "ReferencedFiles",
                "ReferenceFiles",
            };

            // 1) property 優先
            foreach (var n in names)
            {
                var p = type.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p == null) continue;

                var val = p.GetValue(scene, null);
                foreach (var s in EnumerateStringsFromUnknown(val))
                    yield return s;

                yield break; // 最初に見つかった候補だけ使う
            }

            // 2) field fallback
            foreach (var n in names)
            {
                var f = type.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null) continue;

                var val = f.GetValue(scene);
                foreach (var s in EnumerateStringsFromUnknown(val))
                    yield return s;

                yield break;
            }
        }

        private static IEnumerable<string> EnumerateStringsFromUnknown(object val)
        {
            if (val == null) yield break;

            if (val is string one)
            {
                yield return one;
                yield break;
            }

            if (val is IEnumerable<string> es)
            {
                foreach (var s in es) yield return s;
                yield break;
            }

            if (val is IEnumerable e)
            {
                foreach (var x in e)
                {
                    if (x is string s) yield return s;
                }
            }
        }

        // ---------- Paths / Utils ----------
        private static string NormalizeAssetsFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return "Assets/MayaImporter/Generated";

            folder = folder.Replace('\\', '/');
            if (!folder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                folder = "Assets/MayaImporter/Generated";

            return folder.TrimEnd('/');
        }

        private static string NormalizeAssetsPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            return path.Replace('\\', '/');
        }

        private static void EnsureFolder(string folder)
        {
            folder = (folder ?? "").Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return;

            var parts = folder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            if (!string.Equals(parts[0], "Assets", StringComparison.OrdinalIgnoreCase)) return;

            string cur = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static string MakeUniqueAssetPath(string desired)
        {
            desired = (desired ?? "").Replace('\\', '/');
            if (string.IsNullOrEmpty(desired)) desired = "Assets/MayaImporter/Generated/Unnamed.asset";

            var dir = Path.GetDirectoryName(desired)?.Replace('\\', '/') ?? "Assets";
            var file = Path.GetFileName(desired);
            if (string.IsNullOrEmpty(file)) file = "Unnamed.asset";
            EnsureFolder(dir);

            return AssetDatabase.GenerateUniqueAssetPath($"{dir}/{file}");
        }

        private static string SanitizeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Unnamed";
            s = s.Trim();

            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');

            s = s.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            if (s.Length > 80) s = s.Substring(0, 80);
            if (string.IsNullOrEmpty(s)) s = "Unnamed";
            return s;
        }

        // ---------- ReferenceEqualityComparer ----------
        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
#endif
