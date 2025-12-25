#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MayaImporter.Editor
{
    public static class MayaSmokeScene_RunAllOneShot
    {
        private const string MenuPath = "Tools/Maya Importer/Smoke/RUN ALL (Visual Build+Validate + Standard Build + Frequency Report)";

        [MenuItem(MenuPath)]
        public static void RunAll()
        {
            // 1) Build visual
            MayaSmokeScene_GenerateAndBuild.BuildVisualPipelineSmoke();

            // 2) Validate silently (no dialogs)
            bool pass = MayaSmokeScene_ValidateVisualOutput.ValidateSilently(showDialog: false);

            // 3) Build standard nodeTypes smoke
            MayaSmokeScene_GenerateAndBuild.BuildStandardNodeTypesSmoke();

            // 4) Report frequency (writes Assets/MayaImporter/Reports/...)
            MayaSceneNodeTypeFrequencyReporter.Report();

            Debug.Log(pass
                ? "[MayaImporter] ✅ RUN ALL completed. Visual PASS. Frequency report generated."
                : "[MayaImporter] ❌ RUN ALL completed. Visual FAIL (see console). Frequency report generated.");
        }
    }
}
#endif
