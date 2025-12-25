#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Editor
{
    public static class MayaStandardNodeTypesProbe
    {
        private const string AssetPath = "Assets/MayaImporter/Resources/Maya2026_StandardNodeTypes.txt";
        private const string ResourceName = "Maya2026_StandardNodeTypes";

        [MenuItem("Tools/Maya Importer/Coverage/DEBUG/Probe Standard NodeTypes TXT")]
        public static void Probe()
        {
            Debug.Log("=== Probe Standard NodeTypes TXT ===");

            // 1) AssetDatabaseで見えるか
            var taByPath = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
            Debug.Log($"AssetDatabase.LoadAssetAtPath<TextAsset>(\"{AssetPath}\") => {(taByPath != null ? "FOUND" : "NULL")}");

            // 2) Resources.Loadで見えるか
            var taByRes = Resources.Load<TextAsset>(ResourceName);
            Debug.Log($"Resources.Load<TextAsset>(\"{ResourceName}\") => {(taByRes != null ? "FOUND" : "NULL")}");

            // 3) ディスク上にあるか（絶対パス）
            var abs = Path.Combine(Application.dataPath, "MayaImporter/Resources/Maya2026_StandardNodeTypes.txt")
                .Replace("\\", "/");
            Debug.Log($"File.Exists(\"{abs}\") => {File.Exists(abs)}");

            // 4) あなたの MayaStandardNodeTypes.TryGet が取れるか
            MayaStandardNodeTypes.InvalidateCache();
            if (MayaStandardNodeTypes.TryGet(out var set))
            {
                Debug.Log($"MayaStandardNodeTypes.TryGet => OK (count={set.Count})");
            }
            else
            {
                Debug.LogWarning($"MayaStandardNodeTypes.TryGet => FAILED (set={(set == null ? "null" : "not null")} count={(set == null ? -1 : set.Count)})");
            }

            // 5) 行数チェック（念のため）
            if (taByPath != null)
            {
                var lines = taByPath.text.Split('\n');
                Debug.Log($"TextAsset(by path) length={taByPath.text.Length} approxLines={lines.Length}");
            }

            Debug.Log("=== Probe Done ===");
        }

        [MenuItem("Tools/Maya Importer/Coverage/DEBUG/Force Refresh + Reimport Standard NodeTypes TXT")]
        public static void ForceRefreshReimport()
        {
            Debug.Log("=== Force Refresh/Reimport Standard NodeTypes TXT ===");
            AssetDatabase.Refresh();
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
            if (asset != null)
            {
                AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                Debug.Log("Reimported and selected: " + AssetPath);
            }
            else
            {
                Debug.LogWarning("AssetDatabase still cannot find: " + AssetPath);
            }

            MayaStandardNodeTypes.InvalidateCache();
            Debug.Log("=== Force Done ===");
        }
    }
}
#endif
