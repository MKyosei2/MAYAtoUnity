// Assets/MayaImporter/CoverageReportTests.cs
// Editor-only: NodeTypeレジストリ周りのスモークテスト（標準一覧が無い場合はIgnore）

#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MayaImporter.EditorTools;

namespace MayaImporter.Tests.Editor
{
    public class CoverageReportTests
    {
        [Test]
        public void RegistrySnapshot_Build_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                var snap = MayaNodeTypeRegistryCache.GetOrBuild(forceRebuild: true);
                Assert.NotNull(snap);
                Assert.NotNull(snap.AllNodeTypes);
            });
        }

        [Test]
        public void StandardNodeTypes_TextAsset_ExistsOrIgnore()
        {
            var ta = Resources.Load<TextAsset>("Maya2026_StandardNodeTypes");
            if (ta == null)
            {
                Assert.Ignore("Resources/Maya2026_StandardNodeTypes.txt not found. Ignore standard coverage test.");
                return;
            }

            Assert.IsNotEmpty(ta.text);
        }

        [Test]
        public void MissingStandardNodeTypes_List_IsStableOrIgnore()
        {
            var ta = Resources.Load<TextAsset>("Maya2026_StandardNodeTypes");
            if (ta == null)
            {
                Assert.Ignore("Resources/Maya2026_StandardNodeTypes.txt not found. Ignore missing-list test.");
                return;
            }

            Assert.DoesNotThrow(() =>
            {
                var missing = MayaNodeTypeRegistryCache.GetMissingStandardNodeTypes(forceRebuild: true);
                Assert.NotNull(missing);
            });
        }
    }
}
#endif
