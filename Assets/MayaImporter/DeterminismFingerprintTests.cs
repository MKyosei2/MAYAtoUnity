// Assets/MayaImporter/Tests/Editor/DeterminismFingerprintTests.cs
// EditMode tests (Unity-only; no Maya/Autodesk API)
//
// Verifies that importing the same .ma text twice yields the same Phase-6 fingerprint,
// which is a key "portfolio proof" of determinism.

#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Tests.Editor
{
    public sealed class DeterminismFingerprintTests
    {
        [Test]
        public void SameInput_TwoImports_SameFingerprint()
        {
            var ma = @"
currentUnit -l centimeter -a degree -t film;
createNode transform -n ""root"";
createNode transform -n ""child"" -p ""root"";
setAttr ""child.tx"" 1;
";
            var temp = Path.Combine(Path.GetTempPath(), "mayaimporter_unittest_det.ma");
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

            // Import #1
            var root1 = MayaImporter.Core.MayaImporter.ImportIntoScene(temp, opt, out var scene1, out var log1);
            Assert.NotNull(root1);
            var rep1 = MayaPhase6Verification.BuildAndAttach(root1, scene1, opt, selection: null, log: log1);
            Assert.NotNull(rep1);
            var fp1 = root1.GetComponent<MayaPhase6DeterminismFingerprint>();
            Assert.NotNull(fp1);
            Assert.IsFalse(string.IsNullOrEmpty(fp1.fingerprintSha256));

            // Import #2
            var root2 = MayaImporter.Core.MayaImporter.ImportIntoScene(temp, opt, out var scene2, out var log2);
            Assert.NotNull(root2);
            var rep2 = MayaPhase6Verification.BuildAndAttach(root2, scene2, opt, selection: null, log: log2);
            Assert.NotNull(rep2);
            var fp2 = root2.GetComponent<MayaPhase6DeterminismFingerprint>();
            Assert.NotNull(fp2);
            Assert.IsFalse(string.IsNullOrEmpty(fp2.fingerprintSha256));

            Assert.AreEqual(fp1.rawSha256, fp2.rawSha256);
            Assert.AreEqual(fp1.fingerprintSha256, fp2.fingerprintSha256);

            Object.DestroyImmediate(root1);
            Object.DestroyImmediate(root2);
            File.Delete(temp);
        }
    }
}
#endif
