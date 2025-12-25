// Assets/MayaImporter/Editor/MayaPhase1Finalize_OpaqueGenerated.cs
// One-shot: rewrite remaining "Decoded (opaque)" MayaGenerated_*.cs to inherit MayaPhaseCOpaqueRuntimeNodeBase.
// This eliminates the last "same script again" loop for generated stubs.
//
// Fix note:
// - Avoids verbatim interpolated multi-line string pitfalls that caused "Newline in constant" etc.
// - Generates safe C# output with explicit line building.

#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class MayaPhase1Finalize_OpaqueGenerated
{
    [MenuItem("Tools/Maya Importer/Phase1/One-shot: Finalize Remaining Opaque Generated Nodes")]
    public static void FinalizeOpaqueGenerated()
    {
        string root = Path.Combine(Application.dataPath, "MayaImporter");
        if (!Directory.Exists(root))
        {
            Debug.LogError($"[MayaImporter] Folder not found: {root}");
            return;
        }

        int scanned = 0;
        int rewritten = 0;
        int skipped = 0;
        int failed = 0;

        var files = Directory.GetFiles(root, "MayaGenerated_*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            scanned++;

            string text;
            try { text = File.ReadAllText(file); }
            catch (Exception e)
            {
                failed++;
                Debug.LogError($"[MayaImporter] Read failed: {file}\n{e}");
                continue;
            }

            // Only target the remaining "Decoded (opaque)" stubs
            if (!text.Contains("Decoded (opaque)") && !text.Contains("opaque decoded") && !text.Contains("opaque decode"))
            {
                skipped++;
                continue;
            }

            // Must currently inherit MayaPhaseCNodeBase
            if (!Regex.IsMatch(text, @":\s*MayaPhaseCNodeBase\b"))
            {
                skipped++;
                continue;
            }

            // Extract nodeType
            var mType = Regex.Match(text, @"\[MayaNodeType\(""(?<t>[^""]+)""\)\]");
            if (!mType.Success)
            {
                skipped++;
                continue;
            }
            string nodeType = mType.Groups["t"].Value;

            // Extract class name (allow optional 'partial')
            var mClass = Regex.Match(
                text,
                @"public\s+sealed\s+(?:partial\s+)?class\s+(?<c>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*MayaPhaseCNodeBase\b"
            );
            if (!mClass.Success)
            {
                skipped++;
                continue;
            }
            string className = mClass.Groups["c"].Value;

            // Extract namespace
            var mNs = Regex.Match(text, @"namespace\s+(?<n>[A-Za-z_][A-Za-z0-9_.]*)\s*\{");
            string ns = mNs.Success ? mNs.Groups["n"].Value : "MayaImporter.Generated";

            // Preserve initial comment block if present
            string header = "";
            var mHeader = Regex.Match(text, @"\A(?<h>(?:\s*//.*\R)+)", RegexOptions.Multiline);
            if (mHeader.Success) header = mHeader.Groups["h"].Value.TrimEnd();

            string headerLine = string.IsNullOrEmpty(header)
                ? $"// Assets/MayaImporter/{Path.GetFileName(file)}"
                : header;

            // Build new file content safely
            var sb = new StringBuilder(2048);
            sb.AppendLine(headerLine);
            sb.AppendLine("// AUTO-FINALIZED (One-shot):");
            sb.AppendLine("// - Was: Phase C \"Decoded (opaque)\" stub");
            sb.AppendLine("// - Now: inherits MayaPhaseCOpaqueRuntimeNodeBase");
            sb.AppendLine("//   -> always adds MayaOpaqueNodeRuntime + attribute preview (portfolio-visible)");
            sb.AppendLine("// - No Maya/API required.");
            sb.AppendLine();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using MayaImporter;");
            sb.AppendLine("using MayaImporter.Core;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine("    [DisallowMultipleComponent]");
            sb.AppendLine($"    [MayaNodeType(\"{EscapeForCSharpString(nodeType)}\")]");
            sb.AppendLine($"    public sealed class {className} : MayaPhaseCOpaqueRuntimeNodeBase");
            sb.AppendLine("    {");
            sb.AppendLine("        // Intentionally empty:");
            sb.AppendLine("        // DecodePhaseC is provided by MayaPhaseCOpaqueRuntimeNodeBase.");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            try
            {
                File.WriteAllText(file, sb.ToString());
                rewritten++;
            }
            catch (Exception e)
            {
                failed++;
                Debug.LogError($"[MayaImporter] Write failed: {file}\n{e}");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[MayaImporter] Finalize opaque generated done. scanned={scanned}, rewritten={rewritten}, skipped={skipped}, failed={failed}");
    }

    private static string EscapeForCSharpString(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        // Minimal escaping (quotes and backslashes)
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
#endif
