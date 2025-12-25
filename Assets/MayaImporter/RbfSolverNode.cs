using System;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Nodes
{
    [MayaNodeType("rbfSolver")]
    [DisallowMultipleComponent]
    public sealed class RbfSolverNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (rbfSolver) - summary")]
        [SerializeField] private float radius = 1f;
        [SerializeField] private float falloff = 1f;
        [SerializeField] private int inputCountHint;
        [SerializeField] private int targetCountHint;

        // Optional: show likely driver plugs
        [SerializeField] private string inputIncomingPlug;
        [SerializeField] private string outputDrivenPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            radius = ReadFloat(1f, ".radius", "radius", ".r", "r");
            falloff = ReadFloat(1f, ".falloff", "falloff", ".fo", "fo");

            inputCountHint = ReadInt(0, ".inputCount", "inputCount", ".ic", "ic");
            targetCountHint = ReadInt(0, ".targetCount", "targetCount", ".tc", "tc");

            // Many setups are connection-driven; keep a hint for inspection
            inputIncomingPlug = FindLastIncomingTo("input", "in", "i");
            // output attr names vary; we can at least show if something is sourcing from us
            outputDrivenPlug = FindLastOutgoingFromThisNode();

            if (inputCountHint == 0 || targetCountHint == 0)
            {
                // Best-effort inference from raw attribute keys
                int inC = 0, tgC = 0;
                if (Attributes != null)
                {
                    for (int i = 0; i < Attributes.Count; i++)
                    {
                        var a = Attributes[i];
                        if (a == null || string.IsNullOrEmpty(a.Key)) continue;
                        if (a.Key.StartsWith(".input[", StringComparison.Ordinal) || a.Key.StartsWith("input[", StringComparison.Ordinal)) inC++;
                        if (a.Key.StartsWith(".target[", StringComparison.Ordinal) || a.Key.StartsWith("target[", StringComparison.Ordinal)) tgC++;
                    }
                }
                if (inputCountHint == 0) inputCountHint = inC;
                if (targetCountHint == 0) targetCountHint = tgC;
            }

            SetNotes(
                $"rbfSolver decoded: radius={radius}, falloff={falloff}, inputCountHint={inputCountHint}, targetCountHint={targetCountHint}, " +
                $"inputIncoming={(string.IsNullOrEmpty(inputIncomingPlug) ? "none" : inputIncomingPlug)}, " +
                $"outgoingSample={(string.IsNullOrEmpty(outputDrivenPlug) ? "none" : outputDrivenPlug)}. " +
                $"(no runtime solve; raw attrs+connections preserved)"
            );
        }

        private string FindLastOutgoingFromThisNode()
        {
            if (Connections == null || Connections.Count == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;
                if (c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Source &&
                    c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Both)
                    continue;

                return c.DstPlug;
            }
            return null;
        }
    }
}
