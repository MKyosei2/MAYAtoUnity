// MAYAIMPORTER_PATCH_V4_FIXED: compile fixes for provenance + deterministic .mb
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MayaImporter.Core
{
    public sealed class MayaSceneData
    {
        // NOTE: schema bump (.mb text recovery metadata)
        public const int CurrentSchemaVersion = 8;

        public int SchemaVersion = CurrentSchemaVersion;

        public string SourcePath;

        public MayaSourceKind SourceKind = MayaSourceKind.Unknown;

        public string RawAsciiText;

        public byte[] RawBinaryBytes;

        public string RawSha256;

        /// <summary>
        /// Units from currentUnit (linear/angle/time)
        /// </summary>
        public readonly Dictionary<string, string> SceneUnits = new Dictionary<string, string>(StringComparer.Ordinal);

        public readonly Dictionary<string, NodeRecord> Nodes = new Dictionary<string, NodeRecord>(StringComparer.Ordinal);

        public readonly List<ConnectionRecord> Connections = new List<ConnectionRecord>();

        /// <summary>
        /// Optional: keep all original statements for debugging/audit.
        /// </summary>
        public readonly List<RawStatement> RawStatements = new List<RawStatement>();

        /// <summary>
        /// Parse path flags (so audit doesn't depend on RawStatements being enabled).
        /// </summary>
        public bool MbEmbeddedAsciiParsed;
        public bool MbUsedChunkPlaceholders;

        /// <summary>
        /// Optional: .mb IFF chunk index + extracted strings.
        /// </summary>
        public MayaBinaryIndex MbIndex;

        /// <summary>
        /// Optional: Deterministic hints extracted from .mb (Unity-only, no Maya/Autodesk API).
        /// These are additive debug/assist data and never replace the raw source-of-truth bytes.
        /// </summary>
        public readonly List<string> MbStringTable = new List<string>(2048);

        /// <summary>
        /// Optional: Mesh-related chunk hints extracted from .mb (additive).
        /// </summary>
        public readonly List<MayaMbMeshHint> MbMeshHints = new List<MayaMbMeshHint>(128);

        // ============================
        // Raw statement retention helpers (capped)
        // ============================

        /// <summary>
        /// Adds a raw statement if enabled and within caps.
        /// Designed to avoid unbounded memory usage.
        /// </summary>
        public bool TryAddRawStatement(RawStatement stmt, MayaImportOptions options)
        {
            if (stmt == null) return false;
            options ??= new MayaImportOptions();
            if (!options.KeepRawStatements) return false;

            var max = options.RawStatementsMaxEntries;
            if (max <= 0) max = 50_000;

            if (RawStatements.Count >= max)
                return false;

            RawStatements.Add(stmt);
            return true;
        }

        /// <summary>
        /// Adds a setAttr statement to a node if enabled and within caps.
        /// </summary>
        public bool TryAddSetAttrStatement(NodeRecord node, RawStatement stmt, MayaImportOptions options)
        {
            if (node == null || stmt == null) return false;
            options ??= new MayaImportOptions();
            if (!options.KeepRawStatements) return false;

            var maxPerNode = options.SetAttrStatementsMaxPerNode;
            if (maxPerNode <= 0) maxPerNode = 256;

	            // SetAttrStatements is always initialized (and is readonly) on NodeRecord.
	            // We just cap additions here to avoid unbounded growth.
	            if (node.SetAttrStatements.Count >= maxPerNode)
                return false;

            node.SetAttrStatements.Add(stmt);
            return true;
        }


        // ============================
        // .mb Embedded ASCII extraction (new)
        // ============================

        /// <summary>
        /// If extractor found command-like text inside .mb, it is stored here (capped).
        /// </summary>
        public string MbExtractedAsciiText;

        /// <summary>
        /// Approx statement count (by ';') for extracted text.
        /// </summary>
        public int MbExtractedAsciiStatementCount;

        /// <summary>
        /// Confidence score (higher means more command-like).
        /// </summary>
        public int MbExtractedAsciiConfidence;

        /// <summary>
        /// Optional: statement count reconstructed from null-terminated strings.
        /// </summary>
        public int MbNullTerminatedStatementCount;

        /// <summary>
        /// Optional: confidence score for null-terminated reconstruction.
        /// </summary>
        public int MbNullTerminatedScore;

        // ============================
        // Structured (Category1)
        // ============================

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

        // ============================
        // Helpers
        // ============================

        public Dictionary<string, int> CountNodeTypes()
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in Nodes)
            {
                var t = kv.Value.NodeType ?? "unknownType";
                map.TryGetValue(t, out var c);
                map[t] = c + 1;
            }
            return map;
        }

        public NodeRecord GetOrCreateNode(string nodeName, string nodeType = null)
        {
            if (string.IsNullOrEmpty(nodeName))
                throw new ArgumentException("nodeName is null/empty");

            if (!Nodes.TryGetValue(nodeName, out var n))
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

        /// <summary>
        /// Best-effort provenance setter (does not downgrade existing provenance).
        /// Used for audit/proof: tracks whether a node was recovered from ASCII commands,
        /// embedded .mb ASCII, deterministic string-table enumeration, chunk placeholders, etc.
        /// </summary>
        public void MarkProvenance(string nodeName, MayaNodeProvenance provenance, string detail = null)
        {
            if (string.IsNullOrEmpty(nodeName)) return;
            if (provenance == MayaNodeProvenance.Unknown) return;

            NodeRecord n;
            try { n = GetOrCreateNode(nodeName); }
            catch { return; }
            if (n == null) return;

            // Don't downgrade provenance; only upgrade from Unknown, or from deterministic to stronger evidence.
            if (n.Provenance == MayaNodeProvenance.Unknown ||
                (n.Provenance == MayaNodeProvenance.MbDeterministicStringTable && provenance != MayaNodeProvenance.MbDeterministicStringTable))
            {
                n.Provenance = provenance;
            }

            if (!string.IsNullOrEmpty(detail) && string.IsNullOrEmpty(n.ProvenanceDetail))
                n.ProvenanceDetail = detail;
        }

        public void SetRawAscii(string sourcePath, string text)
        {
            SourcePath = sourcePath;
            SourceKind = MayaSourceKind.AsciiMa;
            RawAsciiText = text;
            RawBinaryBytes = null;
            RawSha256 = ComputeSha256Hex(text != null ? Encoding.UTF8.GetBytes(text) : Array.Empty<byte>());
        }

        public void SetRawBinary(string sourcePath, byte[] bytes)
        {
            SourcePath = sourcePath;
            SourceKind = MayaSourceKind.BinaryMb;
            RawBinaryBytes = bytes;
            RawAsciiText = null;
            RawSha256 = ComputeSha256Hex(bytes ?? Array.Empty<byte>());
        }

        private static string ComputeSha256Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes ?? Array.Empty<byte>());
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }

    public enum MayaSourceKind
    {
        Unknown = 0,
        AsciiMa = 1,
        BinaryMb = 2
    }

    /// <summary>
    /// Provenance of a NodeRecord (Unity-only, no Maya/Autodesk API).
    /// Used for audit/proof: whether the node was recovered from ASCII commands,
    /// extracted from .mb evidence, deterministic string table enumeration, or placeholders.
    /// </summary>
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

        /// <summary>
        /// How this node was discovered/recovered in a Unity-only pipeline (for audits).
        /// </summary>
        public MayaNodeProvenance Provenance = MayaNodeProvenance.Unknown;

        /// <summary>
        /// Additional provenance detail (best-effort), e.g. 'createNode', 'stringTable', 'chunkIndex'.
        /// </summary>
        public string ProvenanceDetail;

        /// <summary>
        /// setAttr values: key is ".attr" (as written in .ma), value stores tokens + parsed typed value (best-effort).
        /// </summary>
        public readonly Dictionary<string, RawAttributeValue> Attributes =
            new Dictionary<string, RawAttributeValue>(StringComparer.Ordinal);

        /// <summary>
        /// Keep raw setAttr statements that occurred under this node (useful for debugging).
        /// </summary>
        public readonly List<RawStatement> SetAttrStatements = new List<RawStatement>();

        // ===== Category1: per-node structured =====
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

        /// <summary>
        /// Tokenized form; may be null when tokenization failed.
        /// </summary>
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

        public bool HasParsedValue => Kind != MayaAttrValueKind.Tokens && ParsedValue != null;

        public bool TryGetBool(out bool v)
        {
            if (Kind == MayaAttrValueKind.Bool && ParsedValue is bool b) { v = b; return true; }
            v = default; return false;
        }

        public bool TryGetInt(out int v)
        {
            if (Kind == MayaAttrValueKind.Int && ParsedValue is int i) { v = i; return true; }
            v = default; return false;
        }

        public bool TryGetFloat(out float v)
        {
            if (Kind == MayaAttrValueKind.Float && ParsedValue is float f) { v = f; return true; }
            v = default; return false;
        }

        public bool TryGetFloatArray(out float[] v)
        {
            if ((Kind == MayaAttrValueKind.Vector2 || Kind == MayaAttrValueKind.Vector3 || Kind == MayaAttrValueKind.Vector4 || Kind == MayaAttrValueKind.Matrix4x4 || Kind == MayaAttrValueKind.FloatArray)
                && ParsedValue is float[] a)
            { v = a; return true; }
            v = default; return false;
        }

        public bool TryGetIntArray(out int[] v)
        {
            if (Kind == MayaAttrValueKind.IntArray && ParsedValue is int[] a) { v = a; return true; }
            v = default; return false;
        }

        public bool TryGetStringArray(out string[] v)
        {
            if (Kind == MayaAttrValueKind.StringArray && ParsedValue is string[] a) { v = a; return true; }
            v = default; return false;
        }
    }

    // =========================================================
    // Category1 Structured Command Records
    // =========================================================

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