// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// Assets/MayaImporter/MayaGenerated_ChoiceNode.cs
// NodeType: choice (PhaseC implemented)
// - Decodes selector + input[] indices
// - Best-effort: publish selected scalar as MayaFloatValue (initial snapshot)

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("choice")]
    public sealed class MayaGenerated_ChoiceNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (choice)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private int selectorLocal;
        [SerializeField] private string incomingSelector;

        [SerializeField] private int[] inputIndices;
        [SerializeField] private string[] incomingInputs;

        [Header("Output snapshot (best-effort)")]
        [SerializeField] private int chosenIndex;
        [SerializeField] private float chosenValue;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            selectorLocal = ReadInt(0, ".selector", "selector", ".sel", "sel", ".index", "index");
            incomingSelector = NormalizePlug(FindLastIncomingTo("selector", "sel", "index"));

            var indices = CollectInputIndices();
            indices.Sort();
            inputIndices = indices.ToArray();

            incomingInputs = new string[inputIndices.Length];
            for (int i = 0; i < inputIndices.Length; i++)
                incomingInputs[i] = NormalizePlug(FindIncomingToExact($"input[{inputIndices[i]}]"));

            int sel = ResolveSelector();
            chosenIndex = sel;

            // direct pick
            chosenValue = ResolveInputValue(sel);

            var outVal = GetComponent<MayaImporter.Core.MayaFloatValue>() ?? gameObject.AddComponent<MayaImporter.Core.MayaFloatValue>();
            outVal.valid = true;
            outVal.value = chosenValue;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, selectorLocal={selectorLocal}, incomingSelector={(string.IsNullOrEmpty(incomingSelector) ? "none" : incomingSelector)}, inputs={inputIndices.Length}, chosen={chosenIndex}, value={chosenValue:0.###}");
        }

        private int ResolveSelector()
        {
            // selector connection -> MayaFloatValue -> int
            if (!string.IsNullOrEmpty(incomingSelector))
            {
                float v = ResolveConnectedFloat(incomingSelector, selectorLocal);
                return Mathf.Max(0, Mathf.RoundToInt(v));
            }
            return Mathf.Max(0, selectorLocal);
        }

        private float ResolveInputValue(int idx)
        {
            // 1) input[idx] connection
            var plug = FindIncomingToExact($"input[{idx}]");
            if (!string.IsNullOrEmpty(plug))
                return ResolveConnectedFloat(plug, 0f);

            // 2) local attr
            float local = ReadFloat(float.NaN, $".input[{idx}]", $"input[{idx}]");
            if (!float.IsNaN(local)) return local;

            // 3) if idx is outside known indices, clamp to existing set
            if (inputIndices != null && inputIndices.Length > 0)
            {
                int clamped = Mathf.Clamp(idx, 0, inputIndices.Length - 1);
                int real = inputIndices[clamped];

                var p2 = FindIncomingToExact($"input[{real}]");
                if (!string.IsNullOrEmpty(p2))
                    return ResolveConnectedFloat(p2, 0f);

                float lv = ReadFloat(float.NaN, $".input[{real}]", $"input[{real}]");
                if (!float.IsNaN(lv)) return lv;
            }

            return 0f;
        }

        private List<int> CollectInputIndices()
        {
            var set = new HashSet<int>();

            // attrs
            if (Attributes != null)
            {
                for (int i = 0; i < Attributes.Count; i++)
                {
                    var a = Attributes[i];
                    if (a == null || string.IsNullOrEmpty(a.Key)) continue;

                    if (TryExtractIndex(a.Key, "input", out int idx))
                        set.Add(idx);
                }
            }

            // conns
            if (Connections != null)
            {
                for (int i = 0; i < Connections.Count; i++)
                {
                    var c = Connections[i];
                    if (c == null) continue;
                    if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                        continue;

                    var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                    if (string.IsNullOrEmpty(dstAttr)) continue;

                    if (TryExtractIndex(dstAttr, "input", out int idx))
                        set.Add(idx);
                }
            }

            return new List<int>(set);
        }

        private static bool TryExtractIndex(string s, string baseAttr, out int idx)
        {
            idx = -1;
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(baseAttr)) return false;

            int p = s.IndexOf(baseAttr + "[", StringComparison.Ordinal);
            if (p < 0) p = s.IndexOf("." + baseAttr + "[", StringComparison.Ordinal);
            if (p < 0) return false;

            int lb = s.IndexOf('[', p);
            int rb = s.IndexOf(']', lb + 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            string inner = s.Substring(lb + 1, rb - lb - 1);
            int colon = inner.IndexOf(':');
            if (colon >= 0) inner = inner.Substring(0, colon);

            return int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx);
        }

        private string FindIncomingToExact(string dstAttr)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (string.IsNullOrEmpty(dstAttr)) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dst = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dst)) continue;

                if (string.Equals(dst, dstAttr, StringComparison.Ordinal))
                    return NormalizePlug(c.SrcPlug);
            }
            return null;
        }

        private float ResolveConnectedFloat(string srcPlug, float fallback)
        {
            if (string.IsNullOrEmpty(srcPlug)) return fallback;

            var n = MayaPlugUtil.ExtractNodePart(srcPlug);
            if (string.IsNullOrEmpty(n)) return fallback;

            var tr = MayaNodeLookup.FindTransform(n);
            if (tr == null) return fallback;

            var fvCore = tr.GetComponent<MayaImporter.Core.MayaFloatValue>();
            if (fvCore != null && fvCore.valid) return fvCore.value;

            var fvNodes = tr.GetComponent<MayaImporter.Nodes.MayaFloatValue>();
            if (fvNodes != null && fvNodes.valid) return fvNodes.value;

            return fallback;
        }

        private static string NormalizePlug(string plug)
        {
            if (string.IsNullOrEmpty(plug)) return null;
            plug = plug.Trim();
            if (plug.Length >= 2 && plug[0] == '"' && plug[plug.Length - 1] == '"')
                plug = plug.Substring(1, plug.Length - 2);
            return plug;
        }
    }
}
