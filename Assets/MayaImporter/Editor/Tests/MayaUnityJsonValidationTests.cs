using MayaImporter.Core;
using NUnit.Framework;

namespace MayaImporter.Editor.Tests
{
    public sealed class MayaUnityJsonValidationTests
    {
        [Test]
        public void SchemaValidatorRejectsOutOfRangeMeshIndices()
        {
            var export = new MayaUnityExport
            {
                schemaVersion = 10,
                meshes = new[]
                {
                    new MayaUnityExportMesh
                    {
                        name = "BadMesh",
                        vertices = new[] { 0f, 0f, 0f },
                        indices = new[] { 0, 1, 2 }
                    }
                }
            };

            MayaSchemaValidationResult result = MayaUnityJsonSchemaValidator.Validate(export);
            Assert.IsFalse(result.Success);
            Assert.Greater(result.Errors.Count, 0);
        }

        [Test]
        public void UnsupportedFeatureRegistryAggregatesExportUnsupportedNodes()
        {
            var export = new MayaUnityExport
            {
                unsupported = new[]
                {
                    new MayaUnityExportUnsupported { name = "ai1", type = "aiStandardSurface", reason = "Arnold shader" },
                    new MayaUnityExportUnsupported { name = "ai2", type = "aiStandardSurface", reason = "Arnold shader" }
                }
            };

            var registry = new MayaUnsupportedFeatureRegistry();
            registry.RegisterExportUnsupported(export);
            string markdown = registry.ToMarkdown();
            StringAssert.Contains("aiStandardSurface", markdown);
            StringAssert.Contains("| aiStandardSurface | 2 |", markdown);
        }
    }
}
