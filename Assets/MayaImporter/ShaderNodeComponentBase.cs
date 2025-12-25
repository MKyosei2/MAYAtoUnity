using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Shader
{
    /// <summary>
    /// Shader network tools (HypershadeToShaderGraphConverter etc.) expect:
    /// - Attributes : Dictionary<string, object>
    /// - InputConnections / OutputConnections
    /// This class keeps Phase-A rule: [MayaNodeType] nodes can still inherit MayaNodeComponentBase.
    ///
    /// NOTE:
    /// MayaNodeComponentBase already has "Attributes" (List<SerializedAttribute>).
    /// This class intentionally hides it with "new" to preserve the old shader-network API.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class ShaderNodeComponentBase : MayaNodeComponentBase
    {
        [Header("ShaderNode (Legacy Network API)")]
        public string MayaNodeType;

        // Hide MayaNodeComponentBase.Attributes (lossless token list) with a dictionary API expected by legacy tools.
        public new Dictionary<string, object> Attributes = new Dictionary<string, object>(StringComparer.Ordinal);

        [Header("ShaderNode Connections (Resolved Graph)")]
        public Dictionary<string, ShaderNodeComponentBase> InputConnections =
            new Dictionary<string, ShaderNodeComponentBase>(StringComparer.Ordinal);

        public Dictionary<string, List<ShaderNodeComponentBase>> OutputConnections =
            new Dictionary<string, List<ShaderNodeComponentBase>>(StringComparer.Ordinal);

        public override void InitializeFromRecord(NodeRecord rec, List<ConnectionRecord> allConnections)
        {
            base.InitializeFromRecord(rec, allConnections);

            MayaNodeType = NodeType;

            // Best-effort: convert lossless tokens into object dictionary for shader tools.
            Attributes.Clear();

            for (int i = 0; i < base.Attributes.Count; i++)
            {
                var a = base.Attributes[i];
                if (a == null) continue;

                var key = a.Key ?? "";
                if (string.IsNullOrEmpty(key)) continue;

                var value = ConvertTokensToObject(a.TypeName, a.Tokens);

                // Store both ".attr" and "attr" when possible.
                Attributes[key] = value;

                if (key.StartsWith(".", StringComparison.Ordinal))
                {
                    var noDot = key.Substring(1);
                    if (!string.IsNullOrEmpty(noDot) && !Attributes.ContainsKey(noDot))
                        Attributes[noDot] = value;
                }
            }
        }

        private static object ConvertTokensToObject(string typeName, List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0) return null;

            // Try vector-ish first.
            if (TryParseFloat(tokens, 2, out var f2))
                return new Vector2(f2[0], f2[1]);

            if (TryParseFloat(tokens, 3, out var f3))
                return new Vector3(f3[0], f3[1], f3[2]);

            if (TryParseFloat(tokens, 4, out var f4))
                return new Vector4(f4[0], f4[1], f4[2], f4[3]);

            // Single value
            if (tokens.Count == 1)
            {
                var s = tokens[0] ?? "";

                // bool-ish
                var sl = s.Trim().ToLowerInvariant();
                if (sl == "true" || sl == "yes" || sl == "on") return true;
                if (sl == "false" || sl == "no" || sl == "off") return false;

                // int/float
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
                    return iv;

                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv))
                    return fv;

                // strip quotes if present
                if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
                    return s.Substring(1, s.Length - 2);

                return s;
            }

            // Fallback: keep as string list
            return new List<string>(tokens);
        }

        private static bool TryParseFloat(List<string> tokens, int count, out float[] values)
        {
            values = null;
            if (tokens == null || tokens.Count != count) return false;

            var arr = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (!float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out arr[i]))
                    return false;
            }

            values = arr;
            return true;
        }
    }

    /// <summary>
    /// Fallback component used by shader-network tools when the resolved node type
    /// is not a ShaderNodeComponentBase-derived component.
    /// </summary>
    public sealed class UnknownShaderNodeComponent : ShaderNodeComponentBase
    {
    }
}
