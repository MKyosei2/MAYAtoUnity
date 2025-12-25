using System.Collections.Generic;
using System.Text;

namespace MayaImporter.Core
{
    /// <summary>
    /// Collects warnings/errors during parsing/import without throwing for non-fatal issues.
    /// </summary>
    public sealed class MayaImportLog
    {
        public readonly List<string> Infos = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool HasErrors => Errors.Count > 0;

        public void Info(string msg) => Infos.Add(msg);
        public void Warn(string msg) => Warnings.Add(msg);
        public void Error(string msg) => Errors.Add(msg);

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (Infos.Count > 0)
            {
                sb.AppendLine("Infos:");
                foreach (var s in Infos) sb.AppendLine("  " + s);
            }
            if (Warnings.Count > 0)
            {
                sb.AppendLine("Warnings:");
                foreach (var s in Warnings) sb.AppendLine("  " + s);
            }
            if (Errors.Count > 0)
            {
                sb.AppendLine("Errors:");
                foreach (var s in Errors) sb.AppendLine("  " + s);
            }
            return sb.ToString();
        }
    }
}
