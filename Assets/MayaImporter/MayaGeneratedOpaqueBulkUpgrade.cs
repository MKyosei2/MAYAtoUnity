// Assets/MayaImporter/Editor/MayaGeneratedOpaqueBulkUpgrade.cs
// One-shot: upgrade ALL MayaGenerated_* opaque PhaseC nodes at once.
//
// It rewrites files that match:
// - filename: MayaGenerated_*.cs
// - class inherits : MayaPhaseCNodeBase
// - contains "opaque decoded" marker (generated placeholder)
//
// After rewrite:
// - class inherits MayaPhaseCOpaqueRuntimeNodeBase
// - no per-node DecodePhaseC needed (no more individual edits)

#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class MayaGeneratedOpaqueBulkUpgrade
{
    [MenuItem("Tools/Maya Importer/Phase1/One-shot: Upgrade ALL MayaGenerated Opaque Nodes")]
    public static void UpgradeAll()
    {
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

        var files = Directory.GetFiles(root, "MayaGenerated_*.cs", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            var path = files[i];
            scanned++;

            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e)
            {
                failed++;
                Debug.LogError($"[MayaImporter] Read failed: {path}\n{e}");
                continue;
            }

            // Only rewrite generated opaque placeholders
            if (!text.Contains("opaque decoded"))
            {
                skipped++;
                continue;
            }

            // Must be PhaseC base
            if (!Regex.IsMatch(text, @":\s*MayaPhaseCNodeBase\b"))
            {
                skipped++;
                continue;
            }

            // NodeType
            var mType = Regex.Match(text, @"\[MayaNodeType\(""(?<t>[^""]+)""\)\]");
            if (!mType.Success)
            {
                skipped++;
                continue;
            }
            string nodeType = mType.Groups["t"].Value;

            // Class name
            var mClass = Regex.Match(text, @"public\s+sealed\s+class\s+(?<c>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*MayaPhaseCNodeBase\b");
            if (!mClass.Success)
            {
                skipped++;
                continue;
            }
            string className = mClass.Groups["c"].Value;

            // Namespace (keep original)
            var mNs = Regex.Match(text, @"namespace\s+(?<n>[A-Za-z_][A-Za-z0-9_.]*)\s*\{");
            string ns = mNs.Success ? mNs.Groups["n"].Value : "MayaImporter.Generated";

            // Preserve initial comment block if present
            string header = "";
            var mHeader = Regex.Match(text, @"\A(?<h>(?:\s*//.*\R)+)", RegexOptions.Multiline);
            if (mHeader.Success) header = mHeader.Groups["h"].Value.TrimEnd();

            string newText =
$@"{(string.IsNullOrEmpty(header) ? $"// Assets/MayaImporter/{Path.GetFileName(path)}" : header)}
// AUTO-UPGRADED (One-shot):
// - Was: Phase C opaque placeholder (3-line decode)
// - Now: inherits MayaPhaseCOpaqueRuntimeNodeBase (Unity-side Opaque component + typed summary)
// - No Maya/API required.

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace {ns}
{{
    [DisallowMultipleComponent]
    [MayaNodeType(""{nodeType}"")]
    public sealed class {className} : MayaPhaseCOpaqueRuntimeNodeBase
    {{
        // Intentionally empty:
        // DecodePhaseC is provided by MayaPhaseCOpaqueRuntimeNodeBase.
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
        Debug.Log($"[MayaImporter] One-shot upgrade finished. scanned={scanned}, upgraded={upgraded}, skipped={skipped}, failed={failed}");
    }
}
#endif
