// MAYAIMPORTER_VALIDATION: Registry for preserved/unsupported Maya features
using System.Collections.Generic;
using System.Text;

namespace MayaImporter.Core
{
    public sealed class MayaUnsupportedFeatureEntry
    {
        public string NodeType;
        public int Count;
        public string Handling;
        public string Reason;
    }

    public sealed class MayaUnsupportedFeatureRegistry
    {
        private readonly Dictionary<string, MayaUnsupportedFeatureEntry> entries = new Dictionary<string, MayaUnsupportedFeatureEntry>();

        public void Register(string nodeType, string handling, string reason)
        {
            if (string.IsNullOrEmpty(nodeType)) nodeType = "<unknown>";
            MayaUnsupportedFeatureEntry entry;
            if (!entries.TryGetValue(nodeType, out entry))
            {
                entry = new MayaUnsupportedFeatureEntry
                {
                    NodeType = nodeType,
                    Count = 0,
                    Handling = handling ?? "Preserved metadata",
                    Reason = reason ?? "Unsupported feature"
                };
                entries.Add(nodeType, entry);
            }
            entry.Count++;
        }

        public void RegisterExportUnsupported(MayaUnityExport export)
        {
            if (export == null || export.unsupported == null) return;
            for (int i = 0; i < export.unsupported.Length; i++)
            {
                MayaUnityExportUnsupported unsupported = export.unsupported[i];
                if (unsupported == null) continue;
                Register(unsupported.type, "Preserved metadata", unsupported.reason);
            }
        }

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Unsupported / preserved features");
            sb.AppendLine();
            sb.AppendLine("| Node type | Count | Handling | Reason |");
            sb.AppendLine("|---|---:|---|---|");
            foreach (var pair in entries)
            {
                MayaUnsupportedFeatureEntry e = pair.Value;
                sb.AppendLine("| " + e.NodeType + " | " + e.Count + " | " + e.Handling + " | " + e.Reason + " |");
            }
            if (entries.Count == 0)
                sb.AppendLine("| none | 0 | n/a | n/a |");
            return sb.ToString();
        }
    }
}
