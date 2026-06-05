// MAYAIMPORTER_PATCH_V11: compile-safe scene data model for provenance + deterministic .mb + JSON bridge audits
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MayaImporter.Core
{
    public sealed class MayaSceneData
    {
        public const int CurrentSchemaVersion = 9;

        public int SchemaVersion = CurrentSchemaVersion;
        public string SourcePath;
        public MayaSourceKind SourceKind = MayaSourceKind.Unknown;
        public string RawAsciiText;
        public byte[] RawBinaryBytes;
        public string RawSha256;

        public readonly Dictionary<string, string> SceneUnits = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, NodeRecord> Nodes = new Dictionary<string, NodeRecord>(StringComparer.Ordinal);
        public readonly List<ConnectionRecord> Connections = new List<ConnectionRecord>();
        public readonly List<RawStatement> RawStatements = new List<RawStatement>();

        public bool MbEmbeddedAsciiParsed;
        public bool MbUsedChunkPlaceholders;
        public MayaBinaryIndex MbIndex;
        public readonly List<string> MbStringTable = new List<string>(2048);
        public readonly List<MayaMbMeshHint> MbMeshHints = new List<MayaMbMeshHint>(128);

        public bool TryAddRawStatement(RawStatement stmt, MayaImportOptions options)
        {
            if (stmt == null) return false;
            if (options == null) options = new MayaImportOptions();
            if (!options.KeepRawStatements) return false;

            int max = options.RawStatementsMaxEntries;
            if (max <= 0) max = 50000;
            if (RawStatements.Count >= max) return false;

            RawStatements.Add(stmt);
            return true;
        }

        public bool TryAddSetAttrStatement(NodeRecord node, RawStatement stmt, MayaImportOptions options)
        {
            if (node == null || stmt == null) return false;
            if (options == null) options = new MayaImportOptions();
            if (!options.KeepRawStatements) return false;

            int maxPerNode = options.SetAttrStatementsMaxPerNode;
            if (maxPerNode <= 0) maxPerNode = 256;
            if (node.SetAttrStatements.Count >= maxPerNode) return false;

            node.SetAttrStatements.Add(stmt);
            return true;
        }

        public string MbExtractedAsciiText;
        public int MbExtractedAsciiStatementCount;
        public int MbExtractedAsciiConfidence;
        public int MbNullTerminatedStatementCount;
        public int MbNullTerminatedScore;

        public readonly List<MayaFileInfoEntry> FileInfo = new List<MayaFileInfoEntry>();
        public readonly List<MayaRequiresEntry> Requires = new List<MayaRequiresEntry>();
        public readonly List<MayaWorkspaceRuleEntry> WorkspaceRules = new List<MayaWorkspaceRuleEntry>();
        public readonly List<MayaNamespaceOp> NamespaceOps = new List<MayaNamespaceOp>();

        public readonly List<MayaSetKeyframeCommand> SetKeyframes = new List<MayaSetKeyframeCommand>();
        public readonly List<MayaDrivenKeyframeCommand> DrivenKeyframes = new List<MayaDrivenKeyframeCommand>();
        public readonly List<MayaAnimLayerCommand> AnimLayers = new List<MayaAnimLayerCommand>();
        public readonly List<MayaConnectDynamicCommand> ConnectDynamics = new List<MayaConnectDynamicCommand>();

        public readonly List<MayaScriptNodeCommand> ScriptNodes = new List<MayaScriptNodeCommand>();
        public readonly List<MayaEvalDeferredCommand> EvalDeferred = new List<MayaEvalDeferredCommand>();
        public readonly List<MayaExpressionCommand> Expressions = new List<MayaExpressionCommand>();

        public Dictionary<string, int> CountNodeTypes()
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in Nodes)
            {
                string t = kv.Value != null && kv.Value.NodeType != null ? kv.Value.NodeType : "unknownType";
                int c;
                map.TryGetValue(t, out c);
                map[t] = c + 1;
            }
            return map;
        }

        public NodeRecord GetOrCreateNode(string nodeName, string nodeType = null)
        {
            if (string.IsNullOrEmpty(nodeName)) throw new ArgumentException("nodeName is null/empty");

            NodeRecord n;
            if (!Nodes.TryGetValue(nodeName, out n))
            {
                n = new NodeRecord(nodeName, nodeType ?? "unknown");
                Nodes[nodeName] = n;
            }
            else if (!string.IsNullOrEmpty(nodeType) && (n.NodeType == null || n.NodeType == "unknown"))
            {
                n.NodeType = nodeType;
            }

            return n;
        }

        public void MarkProvenance(string nodeName, MayaNodeProvenance provenance, string detail = null)
        {
            if (string.IsNullOrEmpty(nodeName)) return;
            if (provenance == MayaNodeProvenance.Unknown) return;

            NodeRecord n;
            try { n = GetOrCreateNode(nodeName); }
            catch { return; }
            if (n == null) return;

            if (n.Provenance == MayaNodeProvenance.Unknown ||
                (n.Provenance == MayaNodeProvenance.MbDeterministicStringTable && provenance != MayaNodeProvenance.MbDeterministicStringTable))
            {
                n.Provenance = provenance;
            }

            if (!string.IsNullOrEmpty(detail) && string.IsNullOrEmpty(n.ProvenanceDetail)) n.ProvenanceDetail = detail;
        }

        public void SetRawAscii(string sourcePath, string text)
        {
            SourcePath = sourcePath;
            string ext = !string.IsNullOrEmpty(sourcePath) ? System.IO.Path.GetExtension(sourcePath) : string.Empty;
            SourceKind = string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase) ? MayaSourceKind.ExporterJson : MayaSourceKind.AsciiMa;
            RawAsciiText = text;
            RawBinaryBytes = null;
            RawSha256 = ComputeSha256Hex(text != null ? Encoding.UTF8.GetBytes(text) : new byte[0]);
        }

        public void SetRawBinary(string sourcePath, byte[] bytes)
        {
            SourcePath = sourcePath;
            SourceKind = MayaSourceKind.BinaryMb;
            RawBinaryBytes = bytes;
            RawAsciiText = null;
            RawSha256 = ComputeSha256Hex(bytes ?? new byte[0]);
        }

        private static string ComputeSha256Hex(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes ?? new byte[0]);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }

    public enum MayaSourceKind
    {
        Unknown = 0,
        AsciiMa = 1,
        BinaryMb = 2,
        ExporterJson = 3
    }

    public enum MayaNodeProvenance
    {
        Unknown = 0,
        AsciiCommands = 1,
        MbEmbeddedAscii = 2,
        MbNullTerminatedAscii = 3,
        MbDeterministicStringTable = 4,
        MbChunkPlaceholder = 5,
        MbHeuristic = 6
    }

    public sealed class NodeRecord
    {
        public string Name;
        public string NodeType;
        public string ParentName;
        public string Uuid;
        public MayaNodeProvenance Provenance = MayaNodeProvenance.Unknown;
        public string ProvenanceDetail;
        public readonly Dictionary<string, RawAttributeValue> Attributes = new Dictionary<string, RawAttributeValue>(StringComparer.Ordinal);
        public readonly List<RawStatement> SetAttrStatements = new List<RawStatement>();
        public readonly List<MayaAddAttrCommand> AddAttr = new List<MayaAddAttrCommand>();
        public readonly List<MayaDeleteAttrCommand> DeleteAttr = new List<MayaDeleteAttrCommand>();
        public readonly List<MayaLockNodeCommand> LockOps = new List<MayaLockNodeCommand>();

        public NodeRecord(string name, string nodeType)
        {
            Name = name;
            NodeType = nodeType;
        }
    }

    public sealed class ConnectionRecord
    {
        public string SrcPlug;
        public string DstPlug;
        public bool Force;
        public ConnectionRecord(string src, string dst, bool force = false)
        {
            SrcPlug = src;
            DstPlug = dst;
            Force = force;
        }
    }

    public sealed class RawStatement
    {
        public int LineStart;
        public int LineEnd;
        public string Command;
        public string Text;
        public List<string> Tokens;
    }

    public enum MayaAttrValueKind
    {
        Tokens = 0,
        Bool = 1,
        Int = 2,
        Float = 3,
        Vector2 = 4,
        Vector3 = 5,
        Vector4 = 6,
        Matrix4x4 = 7,
        IntArray = 8,
        FloatArray = 9,
        StringArray = 10
    }

    public sealed class RawAttributeValue
    {
        public string TypeName;
        public readonly List<string> ValueTokens = new List<string>();
        public int? SizeHint;
        public Dictionary<string, string> Flags;
        public MayaAttrValueKind Kind = MayaAttrValueKind.Tokens;
        public object ParsedValue;

        public RawAttributeValue(string typeName, List<string> tokens)
        {
            TypeName = typeName;
            if (tokens != null) ValueTokens.AddRange(tokens);
        }

        public bool HasParsedValue { get { return Kind != MayaAttrValueKind.Tokens && ParsedValue != null; } }

        public bool TryGetBool(out bool v)
        {
            if (Kind == MayaAttrValueKind.Bool && ParsedValue is bool) { v = (bool)ParsedValue; return true; }
            v = false; return false;
        }

        public bool TryGetInt(out int v)
        {
            if (Kind == MayaAttrValueKind.Int && ParsedValue is int) { v = (int)ParsedValue; return true; }
            v = 0; return false;
        }

        public bool TryGetFloat(out float v)
        {
            if (Kind == MayaAttrValueKind.Float && ParsedValue is float) { v = (float)ParsedValue; return true; }
            v = 0f; return false;
        }

        public bool TryGetFloatArray(out float[] v)
        {
            if ((Kind == MayaAttrValueKind.Vector2 || Kind == MayaAttrValueKind.Vector3 || Kind == MayaAttrValueKind.Vector4 || Kind == MayaAttrValueKind.Matrix4x4 || Kind == MayaAttrValueKind.FloatArray)
                && ParsedValue is float[])
            { v = (float[])ParsedValue; return true; }
            v = null; return false;
        }

        public bool TryGetIntArray(out int[] v)
        {
            if (Kind == MayaAttrValueKind.IntArray && ParsedValue is int[]) { v = (int[])ParsedValue; return true; }
            v = null; return false;
        }

        public bool TryGetStringArray(out string[] v)
        {
            if (Kind == MayaAttrValueKind.StringArray && ParsedValue is string[]) { v = (string[])ParsedValue; return true; }
            v = null; return false;
        }
    }

    public sealed class MayaFileInfoEntry
    {
        public int LineStart;
        public int LineEnd;
        public string Key;
        public string Value;
    }

    public sealed class MayaRequiresEntry
    {
        public int LineStart;
        public int LineEnd;
        public string Plugin;
        public string Version;
    }

    public sealed class MayaWorkspaceRuleEntry
    {
        public int LineStart;
        public int LineEnd;
        public string Rule;
        public string Path;
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class MayaNamespaceOp
    {
        public int LineStart;
        public int LineEnd;
        public string Operation;
        public string Name;
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class MayaAddAttrCommand
    {
        public int LineStart;
        public int LineEnd;
        public string TargetNode;
        public string LongName;
        public string ShortName;
        public string NiceName;
        public string Parent;
        public string AttributeType;
        public string DataType;
        public string DefaultValue;
        public string MinValue;
        public string MaxValue;
        public bool? Keyable;
        public bool? ChannelBox;
        public bool? Hidden;
        public bool? Multi;
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class MayaDeleteAttrCommand
    {
        public int LineStart;
        public int LineEnd;
        public string TargetNode;
        public string Attribute;
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class MayaLockNodeCommand
    {
        public int LineStart;
        public int LineEnd;
        public List<string> Targets = new List<string>();
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class MayaSelectCommand
    {
        public int LineStart;
        public int LineEnd;
        public List<string> Targets = new List<string>();
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class MayaSetKeyframeCommand
    {
        public int LineStart;
        public int LineEnd;
        public List<string> Targets = new List<string>();
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class MayaDrivenKeyframeCommand
    {
        public int LineStart;
        public int LineEnd;
        public List<string> Targets = new List<string>();
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class MayaAnimLayerCommand
    {
        public int LineStart;
        public int LineEnd;
        public List<string> Targets = new List<string>();
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class MayaConnectDynamicCommand
    {
        public int LineStart;
        public int LineEnd;
        public List<string> Targets = new List<string>();
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class MayaScriptNodeCommand
    {
        public int LineStart;
        public int LineEnd;
        public string Name;
        public string Script;
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class MayaEvalDeferredCommand
    {
        public int LineStart;
        public int LineEnd;
        public string Code;
    }

    public sealed class MayaExpressionCommand
    {
        public int LineStart;
        public int LineEnd;
        public string Name;
        public string Expression;
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
