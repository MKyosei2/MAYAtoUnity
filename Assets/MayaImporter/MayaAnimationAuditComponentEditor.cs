#if UNITY_EDITOR
using UnityEngine;

// ★ 型名衝突回避のため、UnityEditor を global:: で明示
using UE = global::UnityEditor;

namespace MayaImporter.Core
{
    [UE.CustomEditor(typeof(MayaAnimationAuditComponent))]
    public sealed class MayaAnimationAuditComponentEditor : global::UnityEditor.Editor
    {
        private bool _showLimitations = true;
        private bool _showDeterminism = true;

        public override void OnInspectorGUI()
        {
            var a = (MayaAnimationAuditComponent)target;

            UE.EditorGUILayout.LabelField("Maya Animation Audit", UE.EditorStyles.boldLabel);

            // Detected
            UE.EditorGUILayout.Space(6);
            UE.EditorGUILayout.LabelField("Detected", UE.EditorStyles.boldLabel);
            DrawBool("AnimCurves", a.hasAnimCurves);
            DrawBool("Constraints", a.hasConstraints);
            DrawBool("Expressions", a.hasExpressions);

            // Bake
            UE.EditorGUILayout.Space(8);
            UE.EditorGUILayout.LabelField("Bake Result", UE.EditorStyles.boldLabel);

            if (a.bakeFailed)
                UE.EditorGUILayout.HelpBox("Bake failed. Data is preserved (runtime evaluation may still work).", UE.MessageType.Warning);

            if (!a.baked && !a.bakeFailed)
                UE.EditorGUILayout.HelpBox("No bake performed (no anim/constraints/expressions detected).", UE.MessageType.Info);

            UE.EditorGUILayout.LabelField("Clip", string.IsNullOrEmpty(a.bakedClipName) ? "(none)" : a.bakedClipName);
            UE.EditorGUILayout.LabelField("Range", $"{a.bakedStartFrame} .. {a.bakedEndFrame} (fps {a.bakedFps})");
            UE.EditorGUILayout.LabelField("Curves / Keys", $"{a.bakedCurveCount} / {a.bakedKeyCount}");

            // Determinism
            UE.EditorGUILayout.Space(8);
            _showDeterminism = UE.EditorGUILayout.Foldout(_showDeterminism, "Determinism (Stable Hash)", true);
            if (_showDeterminism)
            {
                UE.EditorGUILayout.TextField("Current", a.clipStableHash ?? "");
                UE.EditorGUILayout.TextField("Previous", a.previousClipStableHash ?? "");
                UE.EditorGUILayout.HelpBox(
                    a.determinismOk ? "OK: same hash as previous import (deterministic)" : "Mismatch: hash differs from previous import",
                    a.determinismOk ? UE.MessageType.Info : UE.MessageType.Warning);
            }

            // Limitations
            UE.EditorGUILayout.Space(8);
            _showLimitations = UE.EditorGUILayout.Foldout(_showLimitations, "Limitations / Notes", true);
            if (_showLimitations)
            {
                var rows = a.GetRows();
                if (rows == null || rows.Count == 0)
                {
                    UE.EditorGUILayout.HelpBox("No limitations reported.", UE.MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];

                        UE.EditorGUILayout.BeginVertical("box");
                        UE.EditorGUILayout.LabelField($"{r.severity} : {r.issueKey}", UE.EditorStyles.boldLabel);
                        UE.EditorGUILayout.LabelField("Scope", r.scope);
                        UE.EditorGUILayout.LabelField("Details");
                        UE.EditorGUILayout.TextArea(
                            r.details ?? "",
                            global::UnityEngine.GUILayout.MinHeight(40)); // ★ここが修正点
                        UE.EditorGUILayout.EndVertical();
                    }
                }
            }

            UE.EditorGUILayout.Space(8);
            UE.EditorGUILayout.HelpBox(
                "Tips:\n" +
                "- Curves/Keys が多すぎる場合：changed-only が効いているか確認\n" +
                "- Determinism がMismatchになる場合：評価順/外部依存が変わっていないか確認\n",
                UE.MessageType.None);
        }

        private static void DrawBool(string label, bool v)
        {
            using (new UE.EditorGUI.DisabledScope(true))
            {
                UE.EditorGUILayout.Toggle(label, v);
            }
        }
    }
}
#endif
