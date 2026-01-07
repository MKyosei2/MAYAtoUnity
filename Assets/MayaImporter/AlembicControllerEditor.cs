// Auto-generated  Production implementation (decoded, non-empty).
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Alembic
{
    [MayaNodeType("alembicControllerEditor")]
    [DisallowMultipleComponent]
    public sealed class AlembicControllerEditor : MayaPhaseCNodeBase
    {
        [Header("Decoded (alembicControllerEditor)")]
        [SerializeField] private string uiPreset;
        [SerializeField] private bool showAdvanced;
        [SerializeField] private bool showPaths;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            uiPreset = ReadString("",
                ".uiPreset", "uiPreset",
                ".preset", "preset",
                ".layout", "layout");

            showAdvanced = ReadBool(false, ".showAdvanced", "showAdvanced", ".adv", "adv");
            showPaths = ReadBool(false, ".showPaths", "showPaths", ".sp", "sp");

            SetNotes($"alembicControllerEditor decoded: preset='{uiPreset}', showAdvanced={showAdvanced}, showPaths={showPaths}");
        }
    }
}
