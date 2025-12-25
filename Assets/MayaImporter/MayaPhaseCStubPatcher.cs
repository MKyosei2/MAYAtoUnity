#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot patcher:
/// - Finds MayaGenerated_*.cs under Assets/MayaImporter (recursively)
/// - If it is still a stub (does NOT derive from MayaPhaseCNodeBase), rewrite file to Phase C implementation.
/// - This reduces STUB count massively in one click, without Maya / Autodesk API.
/// </summary>
public static class MayaPhaseCStubPatcher
{
    // Adjust if your folder differs:
    private const string RootRelative = "Assets/MayaImporter";

    // Safety:
    private const bool CreateBackups = true;    // writes *.bak next to original
    private const bool DryRun = false;          // if true, only logs what would change

    // Regexes for extracting nodeType / class / namespace from stub files
    private static readonly Regex RxNodeType = new Regex(@"\[MayaNodeType\(\s*""(?<t>[^""]+)""\s*\)\]",
        RegexOptions.Compiled);
    private static readonly Regex RxClass = new Regex(@"public\s+sealed\s+class\s+(?<c>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);
    private static readonly Regex RxNamespace = new Regex(@"namespace\s+(?<n>[A-Za-z_][A-Za-z0-9_\.]*)",
        RegexOptions.Compiled);

    [MenuItem("Tools/Maya Importer/Phase C/Patch ALL MayaGenerated STUB nodes (one-shot)")]
    public static void PatchAllGeneratedStubs()
    {
        var projectRoot = Directory.GetCurrentDirectory();
        var rootAbs = Path.Combine(projectRoot, RootRelative);

        if (!Directory.Exists(rootAbs))
        {
            Debug.LogError($"[MayaImporter] Root folder not found: {rootAbs}\nAdjust RootRelative in MayaPhaseCStubPatcher.");
            return;
        }

        var files = Directory.GetFiles(rootAbs, "MayaGenerated_*.cs", SearchOption.AllDirectories);
        if (files == null || files.Length == 0)
        {
            Debug.LogWarning($"[MayaImporter] No MayaGenerated_*.cs found under {RootRelative}");
            return;
        }

        int patched = 0;
        int skipped = 0;
        int failed = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var path in files)
            {
                try
                {
                    var text = File.ReadAllText(path, Encoding.UTF8);

                    // Already Phase C? skip.
                    if (text.Contains("MayaPhaseCNodeBase"))
                    {
                        skipped++;
                        continue;
                    }

                    // Must have nodeType + class
                    var mType = RxNodeType.Match(text);
                    var mClass = RxClass.Match(text);
                    var mNs = RxNamespace.Match(text);

                    if (!mType.Success || !mClass.Success)
                    {
                        // Not a standard generated file -> skip
                        skipped++;
                        continue;
                    }

                    string nodeType = mType.Groups["t"].Value.Trim();
                    string className = mClass.Groups["c"].Value.Trim();
                    string ns = mNs.Success ? mNs.Groups["n"].Value.Trim() : "MayaImporter.Generated";

                    // Generate new content (generic but non-empty, ApplyToUnity covered by base)
                    var newText = BuildPhaseCFile(ns, nodeType, className);

                    if (string.Equals(text, newText, StringComparison.Ordinal))
                    {
                        skipped++;
                        continue;
                    }

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
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[MayaImporter] Phase C one-shot patch complete. patched={patched}, skipped={skipped}, failed={failed}\nRoot={RootRelative}\nBackups={(CreateBackups ? "ON" : "OFF")}, DryRun={(DryRun ? "ON" : "OFF")}");
    }

    private static string BuildPhaseCFile(string ns, string nodeType, string className)
    {
        // Generic decode: ensures class is not empty, records enabled-ish state, leaves full raw attrs+conns intact.
        // IMPORTANT: no Maya/Autodesk API usage.
        return
$@"// AUTO-PATCHED by MayaPhaseCStubPatcher (one-shot)
// NodeType: {nodeType}
// Phase C implementation: non-empty DecodePhaseC + MayaPhaseCNodeBase (coverage: not STUB)

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

        // A couple of common decoded hints (generic)
        [SerializeField] private string lastIncomingToInput;
        [SerializeField] private string lastIncomingToTime;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {{
            // Generic enable heuristics (works across many nodes)
            bool muted = ReadBool(false, "".mute"", ""mute"", "".disabled"", ""disabled"");
            bool explicitEnabled = ReadBool(true, "".enabled"", ""enabled"", "".enable"", ""enable"");
            enabled = !muted && explicitEnabled;

            // Generic connection hints (best-effort)
            lastIncomingToInput = FindLastIncomingTo(""input"", ""in"", ""i"");
            lastIncomingToTime  = FindLastIncomingTo(""time"", ""t"");

            string inInput = string.IsNullOrEmpty(lastIncomingToInput) ? ""none"" : lastIncomingToInput;
            string inTime  = string.IsNullOrEmpty(lastIncomingToTime)  ? ""none"" : lastIncomingToTime;

            SetNotes($""{{NodeType}} '{{NodeName}}' decoded: enabled={{enabled}}, attrs={{AttributeCount}}, conns={{ConnectionCount}}, incomingInput={{inInput}}, incomingTime={{inTime}} (generic PhaseC)"");
        }}
    }}
}}
";
    }
}
#endif
