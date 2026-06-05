// MAYAIMPORTER_PROFILER: Stage timing and cache-stat report support for JSON imports
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MayaImporter.Core
{
    public sealed class MayaImportStageTiming
    {
        public string Name;
        public double Milliseconds;
        public int Warnings;
        public int Errors;
        public string Note;
    }

    public sealed class MayaImportProfile
    {
        public readonly List<MayaImportStageTiming> Stages = new List<MayaImportStageTiming>();
        public MayaImportContextStats CacheStats;
        public string SourcePath;
        public bool Success;

        public double TotalMilliseconds
        {
            get
            {
                double total = 0.0;
                for (int i = 0; i < Stages.Count; i++) total += Stages[i].Milliseconds;
                return total;
            }
        }

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# MAYAtoUnity Import Profile");
            sb.AppendLine();
            sb.AppendLine("Source: `" + SourcePath + "`");
            sb.AppendLine("Success: `" + Success + "`");
            sb.AppendLine();
            sb.AppendLine("| Stage | ms | Warnings | Errors | Note |");
            sb.AppendLine("|---|---:|---:|---:|---|");
            for (int i = 0; i < Stages.Count; i++)
            {
                MayaImportStageTiming s = Stages[i];
                sb.AppendLine("| " + s.Name + " | " + s.Milliseconds.ToString("0.###") + " | " + s.Warnings + " | " + s.Errors + " | " + (s.Note ?? string.Empty) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("## Cache statistics");
            sb.AppendLine();
            sb.AppendLine(CacheStats != null ? CacheStats.ToReportString() : "No import context cache stats were recorded.");
            return sb.ToString();
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"sourcePath\": \"" + Escape(SourcePath) + "\",");
            sb.AppendLine("  \"success\": " + (Success ? "true" : "false") + ",");
            sb.AppendLine("  \"totalMilliseconds\": " + TotalMilliseconds.ToString("0.###") + ",");
            sb.AppendLine("  \"cacheStats\": {");
            if (CacheStats != null)
            {
                sb.AppendLine("    \"transformLookups\": " + CacheStats.TransformLookupCount + ",");
                sb.AppendLine("    \"transformCacheHits\": " + CacheStats.TransformCacheHits + ",");
                sb.AppendLine("    \"transformCacheMisses\": " + CacheStats.TransformCacheMisses + ",");
                sb.AppendLine("    \"componentLookups\": " + CacheStats.ComponentLookupCount + ",");
                sb.AppendLine("    \"componentCacheHits\": " + CacheStats.ComponentCacheHits + ",");
                sb.AppendLine("    \"componentCacheMisses\": " + CacheStats.ComponentCacheMisses);
            }
            sb.AppendLine("  },");
            sb.AppendLine("  \"stages\": [");
            for (int i = 0; i < Stages.Count; i++)
            {
                MayaImportStageTiming s = Stages[i];
                sb.Append("    { \"name\": \"").Append(Escape(s.Name)).Append("\", \"milliseconds\": ").Append(s.Milliseconds.ToString("0.###"))
                  .Append(", \"warnings\": ").Append(s.Warnings).Append(", \"errors\": ").Append(s.Errors)
                  .Append(", \"note\": \"").Append(Escape(s.Note)).Append("\" }");
                if (i + 1 < Stages.Count) sb.Append(',');
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }

    public sealed class MayaImportProfiler
    {
        private readonly MayaImportProfile profile = new MayaImportProfile();
        private Stopwatch stopwatch;
        private string currentName;
        private int startWarnings;
        private int startErrors;

        public MayaImportProfile Profile { get { return profile; } }

        public MayaImportProfiler(string sourcePath)
        {
            profile.SourcePath = sourcePath;
        }

        public void Begin(string name, MayaImportLog log)
        {
            currentName = name;
            startWarnings = log != null ? log.Warnings.Count : 0;
            startErrors = log != null ? log.Errors.Count : 0;
            stopwatch = Stopwatch.StartNew();
        }

        public void End(MayaImportLog log, string note = null)
        {
            if (stopwatch == null) return;
            stopwatch.Stop();
            profile.Stages.Add(new MayaImportStageTiming
            {
                Name = currentName,
                Milliseconds = stopwatch.Elapsed.TotalMilliseconds,
                Warnings = log != null ? Math.Max(0, log.Warnings.Count - startWarnings) : 0,
                Errors = log != null ? Math.Max(0, log.Errors.Count - startErrors) : 0,
                Note = note ?? string.Empty
            });
            stopwatch = null;
            currentName = null;
        }

        public void SetCacheStats(MayaImportContext context)
        {
            profile.CacheStats = context != null ? context.Stats : null;
        }

        public void WriteReports(string directory, MayaImportLog log)
        {
            try
            {
                if (string.IsNullOrEmpty(directory)) directory = "MAYAtoUnity_ImportProfiles";
                Directory.CreateDirectory(directory);
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                File.WriteAllText(Path.Combine(directory, "maya_import_profile_" + stamp + ".md"), profile.ToMarkdown());
                File.WriteAllText(Path.Combine(directory, "maya_import_profile_" + stamp + ".json"), profile.ToJson());
            }
            catch (Exception ex)
            {
                if (log != null) log.Warn("Could not write import profile report: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
