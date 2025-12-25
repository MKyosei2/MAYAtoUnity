#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Editor
{
    public static class MayaSmokeScene_ValidateVisualOutput
    {
        private const string MenuPath = "Tools/Maya Importer/Smoke/Validate VISUAL Smoke Output (Mesh+Material)";

        [MenuItem(MenuPath)]
        public static void ValidateMenu()
        {
            ValidateSilently(showDialog: true);
        }

        public static bool ValidateSilently(bool showDialog)
        {
            var root = global::UnityEngine.GameObject.Find("SMOKE__VisualPipeline");
            if (root == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog(
                        "MayaImporter",
                        "SMOKE__VisualPipeline が見つかりません。\n\n先に\nTools/Maya Importer/Smoke/Build VISUAL Pipeline Smoke Scene\nを実行してください。",
                        "OK");
                }
                Debug.LogError("[MayaImporter] VISUAL Smoke: root not found (SMOKE__VisualPipeline).");
                return false;
            }

            var sb = new StringBuilder();
            int ok = 0, ng = 0;

            var pCube1 = FindDeepChild(root.transform, "pCube1");
            if (pCube1 == null)
            {
                ng++;
                sb.AppendLine("NG: pCube1 が見つかりません");
            }
            else
            {
                var mf = pCube1.GetComponent<global::UnityEngine.MeshFilter>();
                var mr = pCube1.GetComponent<global::UnityEngine.MeshRenderer>();

                if (mf == null || mf.sharedMesh == null)
                {
                    ng++;
                    sb.AppendLine("NG: MeshFilter / sharedMesh がありません（mesh の再構築失敗）");
                }
                else
                {
                    ok++;
                    sb.AppendLine($"OK: Mesh built  vtx={mf.sharedMesh.vertexCount}  subMeshes={mf.sharedMesh.subMeshCount}");
                }

                if (mr == null || mr.sharedMaterials == null || mr.sharedMaterials.Length == 0 || mr.sharedMaterials[0] == null)
                {
                    ng++;
                    sb.AppendLine("NG: MeshRenderer / material がありません（shadingEngine->material 解決失敗）");
                }
                else
                {
                    ok++;
                    sb.AppendLine($"OK: Material assigned  count={mr.sharedMaterials.Length}  mat0='{mr.sharedMaterials[0].name}'");
                }
            }

            var cam = root.GetComponentInChildren<global::UnityEngine.Camera>(true);
            if (cam == null) { ng++; sb.AppendLine("NG: Camera がありません"); }
            else { ok++; sb.AppendLine($"OK: Camera '{cam.name}'"); }

            var light = root.GetComponentInChildren<global::UnityEngine.Light>(true);
            if (light == null) { ng++; sb.AppendLine("NG: Light がありません"); }
            else { ok++; sb.AppendLine($"OK: Light '{light.name}' type={light.type}"); }

            var nodes = root.GetComponentsInChildren<MayaNodeComponentBase>(true);
            int unknown = 0;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] is MayaUnknownNodeComponent || nodes[i] is MayaPlaceholderNode)
                    unknown++;
            }
            sb.AppendLine($"Info: MayaNodeComponentBase={nodes.Length}, Unknown/Placeholder={unknown}");

            var pass = (ng == 0);
            var title = pass ? "✅ VISUAL Smoke: PASS" : "❌ VISUAL Smoke: FAIL";
            sb.Insert(0, $"Result: OK={ok}  NG={ng}\n\n");

            var msg = "[MayaImporter] " + title + "\n" + sb.ToString();
            if (pass) Debug.Log(msg);
            else Debug.LogError(msg);

            if (showDialog)
                EditorUtility.DisplayDialog("MayaImporter", title, sb.ToString(), "OK");

            return pass;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                var r = FindDeepChild(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
#endif
