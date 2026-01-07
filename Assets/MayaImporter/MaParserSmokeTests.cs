// Assets/MayaImporter/Tests/Editor/MaParserSmokeTests.cs
// EditMode tests (Unity-only; no Maya/Autodesk API)

#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Tests.Editor
{
    public sealed class MaParserSmokeTests
    {
        [Test]
        public void ParseText_MinimalScene_CreatesExpectedNodes()
        {
            var ma = @"
fileInfo ""application"" ""maya"";
currentUnit -l centimeter -a degree -t film;
createNode transform -n ""root"";
    setAttr "".t"" -type ""double3"" 1 2 3 ;
createNode transform -n ""child"" -p ""root"";
    setAttr "".tx"" 4;
";
            var opt = new MayaImportOptions { KeepRawStatements = true, SaveAssets = false };
            var log = new MayaImportLog();

            var parser = new MayaAsciiParser();
            var scene = parser.ParseText("unit-test.ma", ma, opt, log);

            Assert.NotNull(scene);
            Assert.NotNull(scene.Nodes);
            Assert.IsTrue(scene.Nodes.ContainsKey("root"));
            Assert.IsTrue(scene.Nodes.ContainsKey("child"));

            Assert.AreEqual("transform", scene.Nodes["root"].NodeType);
            Assert.AreEqual("root", scene.Nodes["child"].ParentName);

            // Units present
            Assert.IsTrue(scene.SceneUnits.ContainsKey("linear"));
            Assert.AreEqual("centimeter", scene.SceneUnits["linear"]);

            // Raw statements kept
            Assert.NotNull(scene.RawStatements);
            Assert.Greater(scene.RawStatements.Count, 0);
        }

        [Test]
        public void ImportIntoScene_FromTempMa_DoesNotThrowAndBuildsRoot()
        {
            var ma = @"
createNode transform -n ""root"";
createNode transform -n ""child"" -p ""root"";
";
            var temp = Path.Combine(Path.GetTempPath(), "mayaimporter_unittest_min.ma");
            File.WriteAllText(temp, ma);

            var opt = new MayaImportOptions
            {
                KeepRawStatements = true,
                SaveAssets = false,
                SaveMeshes = false,
                SaveMaterials = false,
                SaveTextures = false,
                SavePrefab = false,
                SaveAnimationClip = false
            };

            var root = MayaImporter.Core.MayaImporter.ImportIntoScene(temp, opt, out var scene, out var log);

            Assert.NotNull(root);
            Assert.NotNull(scene);
            Assert.NotNull(log);

            Object.DestroyImmediate(root);
            File.Delete(temp);
        }
    }
}
#endif
