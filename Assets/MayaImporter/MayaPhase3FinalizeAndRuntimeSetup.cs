using System;
using UnityEngine;
using MayaImporter.Runtime;
using MayaImporter.Animation;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase3: "100点" に寄せるための最終処理。
    /// - 全ノードに Proof/Opaque 系コンポーネントを付与（MayaNodeRepresentationFinalizer）
    /// - Runtime側で接続グラフを評価して TRS を更新できるように Root に evaluator を付与
    /// - TRS が接続で駆動されているノードには Inspectable な marker を付与
    /// </summary>
    public static class MayaPhase3FinalizeAndRuntimeSetup
    {
        public static void Run_BestEffort(GameObject importedRoot, MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();
            if (importedRoot == null) return;

            try
            {
                // 1) Attach proof components to every node
                var nodes = importedRoot.GetComponentsInChildren<MayaNodeComponentBase>(true);
                for (int i = 0; i < nodes.Length; i++)
                {
                    var n = nodes[i];
                    if (n == null) continue;
                    MayaNodeRepresentationFinalizer.FinalizeNode(n, options);
                }

                // 2) Attach runtime evaluator (graph)
                var eval = importedRoot.GetComponent<MayaRuntimeGraphEvaluator>();
                if (eval == null) eval = importedRoot.AddComponent<MayaRuntimeGraphEvaluator>();

                // Build bindings now so the prefab has deterministic inspector state.
                eval.Build_BestEffort(options, log);

                log.Info($"[Phase3] Finalized nodes={nodes.Length} evaluator={(eval != null ? "on" : "off")}");
            }
            catch (Exception ex)
            {
                log.Warn($"[Phase3] Finalize/setup failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
