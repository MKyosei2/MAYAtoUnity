// Assets/MayaImporter/MbImportSmokeTests.cs
// Editor-only smoke tests for .mb importing.
// NOTE: 特定の内部クラス（MayaMbStringTableDecoder等）に依存しない。

#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Tests.Editor
{
    public class MbImportSmokeTests
    {
        private static string FindAnyMbUnderProject()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

            foreach (var f in Directory.EnumerateFiles(projectRoot, "*.mb", SearchOption.AllDirectories))
            {
                if (f.IndexOf(Path.DirectorySeparatorChar + "Library" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (f.IndexOf(Path.DirectorySeparatorChar + "Temp" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (f.IndexOf(Path.DirectorySeparatorChar + "Logs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                return f;
            }
            return null;
        }

        [Test]
        public void Mb_Parse_DoesNotThrow_AndReturnsScene()
        {
            var mbPath = FindAnyMbUnderProject();
            if (string.IsNullOrEmpty(mbPath))
            {
                Assert.Ignore("No .mb file found in project. Place a sample .mb under Assets/ to enable this test.");
                return;
            }

            var opt = new MayaImportOptions
            {
                // テストでは保存系を切って軽量化
                SaveAssets = false,
                SaveMeshes = false,
                SaveMaterials = false,
                SaveTextures = false,
                SavePrefab = false,
                SaveAnimationClip = false,
                KeepRawStatements = true,
            };

            MayaImportLog log;
            MayaSceneData scene = null;

            Assert.DoesNotThrow(() =>
            {
                scene = MayaImporter.Core.MayaImporter.Parse(mbPath, opt, out log);
            });

            Assert.NotNull(scene);
            Assert.AreEqual(MayaSourceKind.BinaryMb, scene.SourceKind);
        }

        [Test]
        public void Mb_ImportIntoScene_CreatesRootGameObject()
        {
            var mbPath = FindAnyMbUnderProject();
            if (string.IsNullOrEmpty(mbPath))
            {
                Assert.Ignore("No .mb file found in project. Place a sample .mb under Assets/ to enable this test.");
                return;
            }

            var opt = new MayaImportOptions
            {
                SaveAssets = false,
                SaveMeshes = false,
                SaveMaterials = false,
                SaveTextures = false,
                SavePrefab = false,
                SaveAnimationClip = false,
                KeepRawStatements = true,
            };

            GameObject root = null;
            MayaSceneData scene;
            MayaImportLog log;

            Assert.DoesNotThrow(() =>
            {
                root = MayaImporter.Core.MayaImporter.ImportIntoScene(mbPath, opt, out scene, out log);
            });

            Assert.NotNull(root);

            // 後片付け
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
#endif
