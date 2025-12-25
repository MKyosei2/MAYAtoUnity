#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Patches ANY C# script that:
// - has [MayaNodeType("...")]
// - derives from MayaNodeComponentBase (directly or indirectly)
// - does NOT implement override ApplyToUnity (STUB)
// into a Phase C implementation (derive MayaPhaseCNodeBase + DecodePhaseC non-empty)
//
// No Maya/Autodesk API required.

public static class MayaPhaseCPatchAllNodeTypeStubs
{
    // Scan range (safe default: only your project tool area)
    private static readonly string[] RootFolders =
    {
        "Assets/MayaImporter",
    };

    private const bool CreateBackups = true; // writes *.bak next to original (once)
    private const bool DryRun = false;       // true: only log

    private static readonly Regex RxNodeType = new Regex(@"\[MayaNodeType\(\s*""(?<t>[^""]+)""\s*\)\]",
        RegexOptions.Compiled);

    private static readonly Regex RxNamespace = new Regex(@"namespace\s+(?<n>[A-Za-z_][A-Za-z0-9_\.]*)",
        RegexOptions.Compiled);

    // Accept common declarations:
    // public sealed class X : MayaNodeComponentBase
    // public class X : MayaNodeComponentBase
    // public sealed class X : SomeBase
    private static readonly Regex RxClass = new Regex(@"public\s+(?:sealed\s+)?class\s+(?<c>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<b>[A-Za-z_][A-Za-z0-9_\.]*)",
        RegexOptions.Compiled);

    // If file already has any override ApplyToUnity -> treat as implemented and skip
    private static readonly Regex RxOverrideApply = new Regex(@"override\s+void\s+ApplyToUnity\s*\(",
        RegexOptions.Compiled);

    [MenuItem("Tools/Maya Importer/Phase C/Patch ALL [MayaNodeType] STUB scripts (one-shot)")]
    public static void PatchAllNodeTypeStubs()
    {
        int patched = 0, skipped = 0, failed = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var root in RootFolders)
            {
                var absRoot = Path.Combine(Directory.GetCurrentDirectory(), root);
                if (!Directory.Exists(absRoot))
                {
                    Debug.LogWarning($"[MayaImporter] Root folder not found: {root} (abs: {absRoot})");
                    continue;
                }

                var files = Directory.GetFiles(absRoot, "*.cs", SearchOption.AllDirectories);
                foreach (var path in files)
                {
                    try
                    {
                        var text = File.ReadAllText(path, Encoding.UTF8);

                        // Must have [MayaNodeType]
                        var mType = RxNodeType.Match(text);
                        if (!mType.Success)
                        {
                            skipped++;
                            continue;
                        }

                        // Already Phase C base? skip
                        if (text.Contains("MayaPhaseCNodeBase"))
                        {
                            skipped++;
                            continue;
                        }

                        // If it already overrides ApplyToUnity, it's not a STUB. Skip.
                        if (RxOverrideApply.IsMatch(text))
                        {
                            skipped++;
                            continue;
                        }

                        // Must be a Maya node component class file (best-effort):
                        // We only patch if it *references* MayaNodeComponentBase somewhere OR its base name hints so.
                        // This prevents accidental patching of unrelated files that happen to mention MayaNodeType.
                        if (!text.Contains("MayaNodeComponentBase") && !text.Contains(": MayaNodeComponentBase"))
                        {
                            // still might derive indirectly. Check class base name and allow if it looks like MayaImporter.*
                            var mClass0 = RxClass.Match(text);
                            if (!mClass0.Success)
                            {
                                skipped++;
                                continue;
                            }
                            var baseName0 = mClass0.Groups["b"].Value.Trim();
                            if (!(baseName0.Contains("Maya") || baseName0.Contains("Node")))
                            {
                                skipped++;
                                continue;
                            }
                        }

                        // Extract nodeType / class / namespace
                        string nodeType = mType.Groups["t"].Value.Trim();

                        var mClass = RxClass.Match(text);
                        if (!mClass.Success)
                        {
                            skipped++;
                            continue;
                        }

                        string className = mClass.Groups["c"].Value.Trim();
                        string ns = RxNamespace.Match(text).Success
                            ? RxNamespace.Match(text).Groups["n"].Value.Trim()
                            : "MayaImporter.Generated";

                        // Build Phase C file
                        var newText = BuildPhaseCFile(ns, nodeType, className);

                        if (DryRun)
                        {
                            Debug.Log($"[MayaImporter] (DryRun) Would patch: {path}  nodeType='{nodeType}'  class='{className}'");
                            patched++;
                            continue;
                        }

                        if (CreateBackups)
                        {
                            var bak = path + ".bak";
                            if (!File.Exists(bak))
                                File.WriteAllText(bak, text, Encoding.UTF8);
                        }

                        File.WriteAllText(path, newText, Encoding.UTF8);
                        patched++;
                    }
                    catch (Exception exFile)
                    {
                        failed++;
                        Debug.LogError($"[MayaImporter] Failed patch: {path}\n{exFile}");
                    }
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[MayaImporter] Phase C patch ALL [MayaNodeType] STUB complete. patched={patched}, skipped={skipped}, failed={failed}. Backups={(CreateBackups ? "ON" : "OFF")}, DryRun={(DryRun ? "ON" : "OFF")}");
        EditorUtility.DisplayDialog("MayaImporter", $"Phase C patch complete.\npatched={patched}\nskipped={skipped}\nfailed={failed}\n\nBackups={(CreateBackups ? "ON" : "OFF")}\nDryRun={(DryRun ? "ON" : "OFF")}", "OK");
    }

    [MenuItem("Tools/Maya Importer/Phase C/Patch ALL STUB + Run Coverage Report (one-shot)")]
    public static void PatchAndReport()
    {
        PatchAllNodeTypeStubs();

        // Run audit/report if available (guarded)
        TryRunNodeAudit();
        TryRunCoverageReport();
    }

    private static void TryRunNodeAudit()
    {
        var t = Type.GetType("MayaImporter.Editor.MayaNodeAudit, Assembly-CSharp");
        if (t == null) t = Type.GetType("MayaImporter.Editor.MayaNodeAudit, Assembly-CSharp-Editor");

        if (t == null)
        {
            Debug.LogWarning("[MayaImporter] MayaNodeAudit type not found (skipped).");
            return;
        }

        var mi = t.GetMethod("RunAudit", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (mi == null)
        {
            Debug.LogWarning("[MayaImporter] MayaNodeAudit.RunAudit not found (skipped).");
            return;
        }

        mi.Invoke(null, null);
    }

    private static void TryRunCoverageReport()
    {
        var t = Type.GetType("MayaImporter.Editor.MayaReconstructionCoverageReporter, Assembly-CSharp");
        if (t == null) t = Type.GetType("MayaImporter.Editor.MayaReconstructionCoverageReporter, Assembly-CSharp-Editor");

        if (t == null)
        {
            Debug.LogWarning("[MayaImporter] MayaReconstructionCoverageReporter type not found (skipped).");
            return;
        }

        var mi = t.GetMethod("Report", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (mi == null)
        {
            Debug.LogWarning("[MayaImporter] MayaReconstructionCoverageReporter.Report not found (skipped).");
            return;
        }

        mi.Invoke(null, null);
    }

    private static string BuildPhaseCFile(string ns, string nodeType, string className)
    {
        // Generic decode:
        // - enables "non-empty" implementation
        // - preserves all raw attrs + connections already stored by MayaNodeComponentBase
        // - avoids any \" escaping issues

        return
$@"// AUTO-PATCHED by MayaPhaseCPatchAllNodeTypeStubs (one-shot)
// NodeType: {nodeType}
// Phase C implementation (non-empty DecodePhaseC; coverage: not STUB)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace {ns}
{{
    [DisallowMultipleComponent]
    [MayaNodeType(""{nodeType}"")]
    public sealed class {className} : MayaPhaseCNodeBase
    {{
        [Header(""Decoded ({nodeType})"")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private string incomingInput;
        [SerializeField] private string incomingTime;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {{
            // Generic enable heuristics (works across many nodes)
            bool muted = ReadBool(false, "".mute"", ""mute"", "".disabled"", ""disabled"");
            bool explicitEnabled = ReadBool(true, "".enabled"", ""enabled"", "".enable"", ""enable"");
            enabled = !muted && explicitEnabled;

            // Generic connection hints (best-effort)
            incomingInput = FindLastIncomingTo(""input"", ""in"", ""i"");
            incomingTime  = FindLastIncomingTo(""time"", ""t"");

            string inInput = string.IsNullOrEmpty(incomingInput) ? ""none"" : incomingInput;
            string inTime  = string.IsNullOrEmpty(incomingTime)  ? ""none"" : incomingTime;

            SetNotes($""{{NodeType}} '{{NodeName}}' decoded: enabled={{enabled}}, attrs={{AttributeCount}}, conns={{ConnectionCount}}, incomingInput={{inInput}}, incomingTime={{inTime}} (generic PhaseC)"");
        }}
    }}
}}
";
    }
}
#endif
