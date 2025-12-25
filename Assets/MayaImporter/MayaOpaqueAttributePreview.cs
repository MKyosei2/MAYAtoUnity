// Assets/MayaImporter/MayaOpaqueAttributePreview.cs
// Inspector-friendly preview of raw Maya attributes (no Maya/API).
// This is intentionally shallow: it shows keys + last token + token count.
// Works for any MayaNodeComponentBase.

using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MayaOpaqueAttributePreview : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public string typeName;
            public string lastToken;
            public int tokenCount;
        }

        [Header("Preview")]
        public int attributeCount;
        public int connectionCount;

        [Tooltip("Max entries kept for inspector preview.")]
        public int maxEntries = 64;

        public List<Entry> entries = new();

        public void BuildFrom(MayaNodeComponentBase node)
        {
            entries.Clear();

            if (node == null)
            {
                attributeCount = 0;
                connectionCount = 0;
                return;
            }

            attributeCount = node.Attributes != null ? node.Attributes.Count : 0;
            connectionCount = node.Connections != null ? node.Connections.Count : 0;

            if (node.Attributes == null) return;

            int limit = Mathf.Clamp(maxEntries, 0, 2048);
            for (int i = 0; i < node.Attributes.Count && entries.Count < limit; i++)
            {
                var a = node.Attributes[i];
                if (a == null) continue;

                var last = PickLastMeaningfulToken(a.Tokens);
                entries.Add(new Entry
                {
                    key = a.Key ?? "",
                    typeName = a.TypeName ?? "",
                    lastToken = last ?? "",
                    tokenCount = a.Tokens != null ? a.Tokens.Count : 0
                });
            }
        }

        private static string PickLastMeaningfulToken(List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0) return null;

            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                var s = (tokens[i] ?? "").Trim();
                if (s.Length == 0) continue;

                // dequote
                if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                    s = s.Substring(1, s.Length - 2);

                // ignore common type markers
                if (s == "-type" || s == "double3" || s == "float3" || s == "double2" || s == "float2")
                    continue;

                return s;
            }

            return null;
        }
    }
}
