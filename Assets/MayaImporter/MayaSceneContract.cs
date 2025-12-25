using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase2 Step1:
    /// - Define "done" for Core: no data drop (Raw exists), and graph is self-consistent.
    /// - Provide audit for debugging and CI-like checks.
    /// </summary>
    public static class MayaSceneContract
    {
        public sealed class AuditResult
        {
            public bool HasRawSource;
            public bool HasNodes;
            public bool HasConnections;

            public int NodeCount;
            public int ConnectionCount;
            public int RawStatementCount;

            public int InvalidNodeNameCount;
            public int MissingNodeTypeCount;
            public int InvalidAttributeKeyCount;
            public int DanglingConnectionCount;

            public readonly List<string> Issues = new List<string>(128);

            public bool IsCoreOk =>
                HasRawSource &&
                HasNodes &&
                InvalidNodeNameCount == 0 &&
                MissingNodeTypeCount == 0 &&
                InvalidAttributeKeyCount == 0;
            // Dangling connections are allowed temporarily (some files connect to nodes created later),
            // but we still report them.
        }

        public static AuditResult Audit(MayaSceneData scene)
        {
            var r = new AuditResult();

            if (scene == null)
            {
                r.Issues.Add("Scene is null.");
                return r;
            }

            r.HasRawSource =
                (!string.IsNullOrEmpty(scene.RawAsciiText)) ||
                (scene.RawBinaryBytes != null && scene.RawBinaryBytes.Length > 0);

            r.NodeCount = scene.Nodes != null ? scene.Nodes.Count : 0;
            r.ConnectionCount = scene.Connections != null ? scene.Connections.Count : 0;
            r.RawStatementCount = scene.RawStatements != null ? scene.RawStatements.Count : 0;

            r.HasNodes = r.NodeCount > 0;
            r.HasConnections = r.ConnectionCount > 0;

            // Nodes sanity
            if (scene.Nodes != null)
            {
                foreach (var kv in scene.Nodes)
                {
                    var n = kv.Value;
                    if (n == null)
                    {
                        r.InvalidNodeNameCount++;
                        r.Issues.Add("Null NodeRecord in scene.Nodes.");
                        continue;
                    }

                    if (string.IsNullOrEmpty(n.Name))
                    {
                        r.InvalidNodeNameCount++;
                        r.Issues.Add("NodeRecord.Name is null/empty.");
                    }

                    if (string.IsNullOrEmpty(n.NodeType))
                    {
                        r.MissingNodeTypeCount++;
                        r.Issues.Add($"Node '{n.Name ?? kv.Key}' has empty NodeType.");
                    }

                    // Attributes: keys must start with '.' by our convention (".tx", ".wl[0].w[2]" etc)
                    if (n.Attributes != null)
                    {
                        foreach (var akv in n.Attributes)
                        {
                            var k = akv.Key;
                            if (string.IsNullOrEmpty(k) || k[0] != '.')
                            {
                                r.InvalidAttributeKeyCount++;
                                r.Issues.Add($"Node '{n.Name}': attribute key invalid '{k ?? "(null)"}' (must start with '.').");
                            }
                        }
                    }
                }
            }

            // Connections dangling check (best-effort)
            if (scene.Connections != null && scene.Nodes != null)
            {
                for (int i = 0; i < scene.Connections.Count; i++)
                {
                    var c = scene.Connections[i];
                    if (c == null) continue;

                    var sn = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                    var dn = MayaPlugUtil.ExtractNodePart(c.DstPlug);

                    // If can't parse node part, count as dangling-ish
                    if (string.IsNullOrEmpty(sn) || string.IsNullOrEmpty(dn))
                    {
                        r.DanglingConnectionCount++;
                        r.Issues.Add($"Connection #{i}: cannot extract node part: '{c.SrcPlug}' -> '{c.DstPlug}'.");
                        continue;
                    }

                    // Nodes might be created later in .ma parsing; at Core-final they should exist
                    if (!scene.Nodes.ContainsKey(sn) || !scene.Nodes.ContainsKey(dn))
                    {
                        r.DanglingConnectionCount++;
                        r.Issues.Add($"Connection #{i}: dangling node: '{sn}' -> '{dn}'.");
                    }
                }
            }

            if (!r.HasRawSource)
                r.Issues.Add("Raw source not captured (RawAsciiText/RawBinaryBytes are empty). This breaks 'no data drop' guarantee.");

            if (!r.HasNodes)
                r.Issues.Add("No nodes found.");

            return r;
        }
    }
}
