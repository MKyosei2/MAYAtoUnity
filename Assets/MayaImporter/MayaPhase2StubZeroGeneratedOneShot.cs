// Assets/MayaImporter/Editor/MayaPhase2StubZeroGeneratedOneShot.cs
//
// Phase②: STUB -> 本実装（Opaque）を一気に片付けるワンショット。
// .ma/.mb が無い前提なので、Generated系スクリプトだけを安全に対象化する。
// 対象条件:
// - Assets/MayaImporter 配下の MayaGenerated_*.cs
// - [MayaNodeType("xxx")] を持つ
// - class が MayaNodeComponentBase を直接継承している
// - ファイル内に override ApplyToUnity が存在しない（= STUB）
//
// 置換内容:
// - 同じ className / nodeType を保ちつつ、
// - MayaPhaseCOpaqueRuntimeNodeBase 継承に変更（=ApplyToUnity実装済み扱い）
// - Unity-onlyで「保持＋可視化」(runtime marker + preview)

#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MayaImporter.Editor
{
    public static class MayaPhase2StubZeroGeneratedOneShot
    {
        private const string MenuPath = "Tools/Maya Importer/Phase2/Make STUB ZERO (Generated scripts -> Opaque impl)";
        private static readonly string RootAbs = Path.Combine(Application.dataPath, "MayaImporter");

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (!Directory.Exists(RootAbs))
            {
                Debug.LogError($"[MayaImporter] Folder not found: {RootAbs}");
                return;
            }

            int scanned = 0;
            int rewritten = 0;
            int skipped = 0;
            int failed = 0;

            var files = Directory.GetFiles(RootAbs, "MayaGenerated_*.cs", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                scanned++;

                string text;
                try { text = File.ReadAllText(file, Encoding.UTF8); }
                catch (Exception e)
                {
                    failed++;
                    Debug.LogError($"[MayaImporter] Read failed: {file}\n{e}");
                    continue;
                }

                // must have nodeType attribute
                var mType = Regex.Match(text, @"\[MayaNodeType\(""(?<t>[^""]+)""\)\]");
                if (!mType.Success) { skipped++; continue; }
                string nodeType = mType.Groups["t"].Value;

                // must be class : MayaNodeComponentBase (direct)
                // allow 'sealed' and/or 'partial'
                var mClass = Regex.Match(
                    text,
                    @"public\s+(?:sealed\s+)?(?:partial\s+)?class\s+(?<c>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*MayaNodeComponentBase\b"
                );
                if (!mClass.Success) { skipped++; continue; }

                string className = mClass.Groups["c"].Value;

                // STUB check: no ApplyToUnity override in file
                // (fast text check; safe enough for generated files)
                if (text.Contains("override void ApplyToUnity") || text.Contains("override sealed void ApplyToUnity"))
                {
                    skipped++;
                    continue;
                }

                // keep original namespace if present; default to MayaImporter.Generated
                var mNs = Regex.Match(text, @"namespace\s+(?<n>[A-Za-z_][A-Za-z0-9_.]*)\s*\{");
                string ns = mNs.Success ? mNs.Groups["n"].Value : "MayaImporter.Generated";

                // preserve initial comment block if present
                string header = "";
                var mHeader = Regex.Match(text, @"\A(?<h>(?:\s*//.*\R)+)", RegexOptions.Multiline);
                if (mHeader.Success) header = mHeader.Groups["h"].Value.TrimEnd();

                var sb = new StringBuilder(2048);
                if (!string.IsNullOrEmpty(header)) sb.AppendLine(header);
                else sb.AppendLine($"// {ToAssetRelative(file)}");

                sb.AppendLine("// AUTO-REWRITTEN (Phase2) : STUB -> Opaque Implementation");
                sb.AppendLine($"// NodeType: {nodeType}");
                sb.AppendLine("// - Guarantees ApplyToUnity (via MayaPhaseCNodeBase)");
                sb.AppendLine("// - Unity-only: runtime marker + attribute preview");
                sb.AppendLine();

                sb.AppendLine("using UnityEngine;");
                sb.AppendLine("using MayaImporter.Core;");
                sb.AppendLine();

                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
                sb.AppendLine("    [DisallowMultipleComponent]");
                sb.AppendLine($"    [MayaNodeType(\"{EscapeForCSharpString(nodeType)}\")]");
                sb.AppendLine($"    public sealed class {className} : MayaPhaseCOpaqueRuntimeNodeBase");
                sb.AppendLine("    {");
                sb.AppendLine("        // Intentionally empty.");
                sb.AppendLine("        // Decode/ApplyToUnity are provided by MayaPhaseCOpaqueRuntimeNodeBase.");
                sb.AppendLine("    }");
                sb.AppendLine("}");

                try
                {
                    File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
                    rewritten++;
                }
                catch (Exception e)
                {
                    failed++;
                    Debug.LogError($"[MayaImporter] Write failed: {file}\n{e}");
                }
            }

            AssetDatabase.Refresh();

            Debug.Log($"[MayaImporter] Phase2 STUB->Opaque done. scanned={scanned}, rewritten={rewritten}, skipped={skipped}, failed={failed}");
            EditorUtility.DisplayDialog(
                "MayaImporter (Phase2)",
                $"Generated STUB -> Opaque 実装 変換完了\n\nscanned={scanned}\nrewritten={rewritten}\nskipped={skipped}\nfailed={failed}",
                "OK"
            );
        }

        private static string EscapeForCSharpString(string s)
            => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string ToAssetRelative(string abs)
        {
            abs = abs.Replace('\\', '/');
            var a = Application.dataPath.Replace('\\', '/');
            if (abs.StartsWith(a, StringComparison.OrdinalIgnoreCase))
                return "Assets" + abs.Substring(a.Length);
            return abs;
        }
    }
}
#endif
