// Assets/MayaImporter/Editor/MayaPhaseCPlaceholderBulkUpgrade.cs
// One-shot bulk upgrade:
// - Finds PhaseC "opaque decoded" scripts (3-line stubs)
// - Rewrites them to inherit MayaPhaseCGenericNodeBase (no override needed)
// - Leaves specialized nodes untouched (those that don't contain "opaque decoded")
//
// Run from menu:
//   MayaImporter/Phase1/Upgrade PhaseC Opaque Nodes (One-shot)

#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class MayaPhaseCPlaceholderBulkUpgrade
{
    [MenuItem("Tools/Maya Importer/Phase1/Upgrade PhaseC Opaque Nodes (One-shot)")]
    public static void Upgrade()
    {
        // Project root MayaImporter folder
        string root = Path.Combine(Application.dataPath, "MayaImporter");
        if (!Directory.Exists(root))
        {
            Debug.LogError($"[MayaImporter] Folder not found: {root}");
            return;
        }

        int scanned = 0;
        int upgraded = 0;
        int skipped = 0;
        int failed = 0;

        var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i].Replace("\\", "/");
            string text;

            try { text = File.ReadAllText(path); }
            catch (Exception e)
            {
                failed++;
                Debug.LogError($"[MayaImporter] Read failed: {path}\n{e}");
                continue;
            }

            scanned++;

            // Only target scripts that:
            // - contain "opaque decoded" (the stub comment + notes)
            // - inherit MayaPhaseCNodeBase
            // - have a MayaNodeType attribute
            if (!text.Contains("opaque decoded") && !text.Contains("opaque decode"))
            {
                skipped++;
                continue;
            }

            if (!Regex.IsMatch(text, @":\s*MayaPhaseCNodeBase\b"))
            {
                skipped++;
                continue;
            }

            // Extract nodeType string
            var mType = Regex.Match(text, @"\[MayaNodeType\(""(?<t>[^""]+)""\)\]");
            if (!mType.Success)
            {
                skipped++;
                continue;
            }
            string nodeType = mType.Groups["t"].Value;

            // Extract class name
            var mClass = Regex.Match(text, @"class\s+(?<c>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*MayaPhaseCNodeBase\b");
            if (!mClass.Success)
            {
                skipped++;
                continue;
            }
            string className = mClass.Groups["c"].Value;

            // Extract namespace (default fallback)
            var mNs = Regex.Match(text, @"namespace\s+(?<n>[A-Za-z_][A-Za-z0-9_.]*)\s*\{");
            string ns = mNs.Success ? mNs.Groups["n"].Value : "MayaImporter.Generated";

            // Try to keep the original header comment block if present
            string header = "";
            var mHeader = Regex.Match(text, @"\A(?<h>(?:\s*//.*\R)+)", RegexOptions.Multiline);
            if (mHeader.Success) header = mHeader.Groups["h"].Value.TrimEnd();

            string newText =
$@"{(string.IsNullOrEmpty(header) ? $"// Assets/MayaImporter/{Path.GetFileName(path)}" : header)}
// AUTO-UPGRADED (One-shot):
// - Was: Phase C opaque 3-line placeholder
// - Now: inherits MayaPhaseCGenericNodeBase (typed decode preview + deterministic notes)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace {ns}
{{
    [DisallowMultipleComponent]
    [MayaNodeType(""{nodeType}"")]
    public sealed class {className} : MayaPhaseCGenericNodeBase
    {{
        // Intentionally empty:
        // Generic decode is handled by MayaPhaseCGenericNodeBase.
        // Raw attrs/conns remain lossless in MayaNodeComponentBase.
    }}
}}
";

            try
            {
                File.WriteAllText(path, newText);
                upgraded++;
            }
            catch (Exception e)
            {
                failed++;
                Debug.LogError($"[MayaImporter] Write failed: {path}\n{e}");
            }
        }

        AssetDatabase.Refresh();

        Debug.Log($"[MayaImporter] PhaseC bulk upgrade finished. scanned={scanned}, upgraded={upgraded}, skipped={skipped}, failed={failed}");
    }
}
#endif
