using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Reports limitations for Maya dynamics/FX nodes in Unity.
    /// Policy: preserve raw data and warn (not blocker).
    /// </summary>
    public static class MayaDynamicsFxLimitationsReporter
    {
        [Serializable]
        public sealed class DynamicsFxLimitationRow
        {
            public string Scope;      // "DynamicsFx"
            public string NodeName;
            public string NodeType;
            public string IssueKey;
            public string Severity;   // Info / Warn
            public string Details;
        }

        public static List<DynamicsFxLimitationRow> Collect(MayaSceneData scene)
        {
            var list = new List<DynamicsFxLimitationRow>(64);
            if (scene == null || scene.Nodes == null) return list;

            foreach (var kv in scene.Nodes)
            {
                var n = kv.Value;
                if (n == null) continue;

                var t = n.NodeType ?? "";
                if (string.IsNullOrEmpty(t)) continue;

                if (IsDynamicsNodeType(t))
                {
                    list.Add(new DynamicsFxLimitationRow
                    {
                        Scope = "DynamicsFx",
                        NodeName = n.Name,
                        NodeType = n.NodeType,
                        IssueKey = "MayaDynamics_FxNode",
                        Severity = "Warn",
                        Details = "Dynamics/FX node detected (nucleus/particle/fluid/cloth/hair etc). Unity requires dedicated simulation/runtime or baked caches to match Maya. Raw data preserved."
                    });
                }
            }

            return list;
        }

        private static bool IsDynamicsNodeType(string t)
        {
            if (t.Equals("nucleus", StringComparison.OrdinalIgnoreCase)) return true;
            if (t.IndexOf("nParticle", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("particle", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("fluid", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("nCloth", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("cloth", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("rigid", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("field", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("turbulence", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("gravity", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }
    }
}
