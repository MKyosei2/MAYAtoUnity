// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.IO;

namespace MayaImporter.Core
{
    public sealed class MayaAsciiParser
    {
        public MayaSceneData ParseFile(string path, MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var scene = new MayaSceneData();

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                log.Error($"Failed to read .ma: {ex.Message}");
                scene.SourcePath = path;
                return scene;
            }

            scene.SetRawAscii(path, text);
            ParseTextIntoScene(text, scene, options, log);
            return scene;
        }

        /// <summary>
        /// Parse provided .ma-like text into a new MayaSceneData.
        /// Useful for .mb embedded-ma extraction pipeline.
        /// </summary>
        public MayaSceneData ParseText(string sourceName, string text, MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var scene = new MayaSceneData();
            scene.SetRawAscii(sourceName, text ?? "");
            ParseTextIntoScene(text ?? "", scene, options, log);
            return scene;
        }

        private void ParseTextIntoScene(string text, MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            List<MayaStatement> statements;
            try
            {
                statements = MayaMaTokenizer.SplitStatements(text, log);
            }
            catch (Exception ex)
            {
                log.Error($"SplitStatements failed: {ex.Message}");
                return;
            }

            string currentNode = null;

            for (int s = 0; s < statements.Count; s++)
            {
                var stmt = statements[s];

                var raw = new RawStatement
                {
                    LineStart = stmt.LineStart,
                    LineEnd = stmt.LineEnd,
                    Text = stmt.Text
                };

                List<string> tokens = null;
                try
                {
                    tokens = MayaMaTokenizer.Tokenize(stmt.Text);
                }
                catch (Exception ex)
                {
                    log.Warn($"Tokenize failed at lines {stmt.LineStart}-{stmt.LineEnd}: {ex.Message}. Keeping raw.");
                }

                raw.Tokens = tokens;
                raw.Command = (tokens != null && tokens.Count > 0) ? tokens[0] : null;

                scene.TryAddRawStatement(raw, options);

                if (tokens == null || tokens.Count == 0)
                    continue;

                var cmd = tokens[0];

                switch (cmd)
                {
                    case "createNode":
                        ParseCreateNode(tokens, raw, scene, log, ref currentNode);
                        break;

					case "setAttr":
						ParseSetAttr(tokens, raw, scene, options, log, ref currentNode);
						break;

                    case "connectAttr":
                        ParseConnectAttr(tokens, raw, scene, log);
                        break;

                    case "parent":
                        ParseParent(tokens, raw, scene, log);
                        break;

                    case "currentUnit":
                        ParseCurrentUnit(tokens, scene);
                        break;

                    case "rename":
                        ParseRename(tokens, scene, log, ref currentNode);
                        break;

                    // Category1 structured commands
                    case "fileInfo": ParseFileInfo(tokens, raw, scene); break;
                    case "requires": ParseRequires(tokens, raw, scene); break;
                    case "workspace": ParseWorkspace(tokens, raw, scene); break;
                    case "namespace": ParseNamespace(tokens, raw, scene); break;
                    case "addAttr": ParseAddAttr(tokens, raw, scene, log, currentNode); break;
                    case "deleteAttr": ParseDeleteAttr(tokens, raw, scene, log, currentNode); break;
                    case "lockNode": ParseLockNode(tokens, raw, scene, log); break;
                    case "select": ParseSelect(tokens, raw, scene); break;
                    case "setKeyframe": ParseSetKeyframe(tokens, raw, scene); break;
                    case "setDrivenKeyframe": ParseSetDrivenKeyframe(tokens, raw, scene); break;
                    case "animLayer": ParseAnimLayer(tokens, raw, scene); break;
                    case "connectDynamic": ParseConnectDynamic(tokens, raw, scene); break;
                    case "scriptNode": ParseScriptNode(tokens, raw, scene); break;
                    case "evalDeferred": ParseEvalDeferred(tokens, raw, scene); break;
                    case "expression": ParseExpression(tokens, raw, scene); break;

                    default:
                        break;
                }
            }
        }

        // ============================
        // Core commands
        // ============================

        private static void ParseCurrentUnit(List<string> tokens, MayaSceneData scene)
        {
            for (int i = 1; i + 1 < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t == "-l" || t == "-a" || t == "-t")
                {
                    var key = t switch { "-l" => "linear", "-a" => "angle", "-t" => "time", _ => t };
                    scene.SceneUnits[key] = tokens[i + 1];
                    i++;
                }
            }
        }

        private static void ParseCreateNode(List<string> tokens, RawStatement raw, MayaSceneData scene, MayaImportLog log, ref string currentNode)
        {
            if (tokens.Count < 2)
            {
                log.Warn($"createNode missing type at lines {raw.LineStart}-{raw.LineEnd}");
                return;
            }

            var nodeType = tokens[1];

            string name = null;
            string parent = null;

            for (int i = 2; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t == "-n" && i + 1 < tokens.Count)
                {
                    name = tokens[i + 1];
                    i++;
                }
                else if (t == "-p" && i + 1 < tokens.Count)
                {
                    parent = tokens[i + 1];
                    i++;
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                log.Warn($"createNode missing -n at lines {raw.LineStart}-{raw.LineEnd}");
                return;
            }

            var n = scene.GetOrCreateNode(name, nodeType);
            // Provenance for audit
            scene.MarkProvenance(name, scene.SourceKind == MayaSourceKind.BinaryMb ? MayaNodeProvenance.MbEmbeddedAscii : MayaNodeProvenance.AsciiCommands, "maCommands");
            if (!string.IsNullOrEmpty(parent))
                n.ParentName = parent;

            currentNode = name;
        }

		private static void ParseSetAttr(List<string> tokens, RawStatement raw, MayaSceneData scene, MayaImportOptions options, MayaImportLog log, ref string currentNode)
        {
            if (tokens.Count < 2) return;

            int i = 1;

            string typeName = null;
            int? sizeHint = null;

            var flags = new Dictionary<string, string>(StringComparer.Ordinal);

            while (i < tokens.Count && IsFlag(tokens[i]))
            {
                var f = tokens[i];

                if (f == "-type" && i + 1 < tokens.Count)
                {
                    typeName = tokens[i + 1];
                    i += 2;
                    continue;
                }

                if (f == "-s" && i + 1 < tokens.Count)
                {
                    if (int.TryParse(tokens[i + 1], out var n)) sizeHint = n;
                    i += 2;
                    continue;
                }

                if ((f == "-k" || f == "-l" || f == "-cb" || f == "-ca" || f == "-cl") && i + 1 < tokens.Count && !IsFlag(tokens[i + 1]))
                {
                    flags[f] = tokens[i + 1];
                    i += 2;
                    continue;
                }

                flags[f] = "true";
                i++;
            }

            if (i >= tokens.Count) return;

            var attrToken = tokens[i];
            i++;

            var values = new List<string>();
            for (; i < tokens.Count; i++)
                values.Add(tokens[i]);

            ResolveSetAttrTarget(attrToken, currentNode, out var targetNode, out var attrKey);

            if (string.IsNullOrEmpty(targetNode))
                return;

            var node = scene.GetOrCreateNode(targetNode);
            scene.TryAddSetAttrStatement(node, raw, options);

            var rav = new RawAttributeValue(typeName, values)
            {
                SizeHint = sizeHint,
                Flags = flags
            };

            if (MayaSetAttrValueParser.TryParse(typeName, values, out var kind, out var parsed))
            {
                rav.Kind = kind;
                rav.ParsedValue = parsed;
            }

            node.Attributes[attrKey] = rav;

            if (currentNode == null && !string.IsNullOrEmpty(targetNode))
                currentNode = targetNode;
        }

        private static void ResolveSetAttrTarget(string attrToken, string currentNode, out string targetNode, out string attrKey)
        {
            targetNode = null;
            attrKey = null;

            if (string.IsNullOrEmpty(attrToken))
                return;

            if (attrToken[0] == '.')
            {
                targetNode = currentNode;
                attrKey = attrToken;
                return;
            }

            var dot = attrToken.IndexOf('.');
            if (dot > 0)
            {
                targetNode = attrToken.Substring(0, dot);
                var rest = attrToken.Substring(dot + 1);
                attrKey = "." + rest;
                return;
            }

            targetNode = currentNode;
            attrKey = attrToken.StartsWith(".") ? attrToken : "." + attrToken;
        }

        private static void ParseConnectAttr(List<string> tokens, RawStatement raw, MayaSceneData scene, MayaImportLog log)
        {
            if (tokens.Count < 3)
            {
                log.Warn($"connectAttr missing args at lines {raw.LineStart}-{raw.LineEnd}");
                return;
            }

            bool force = false;
            int i = 1;
            if (tokens[i] == "-f")
            {
                force = true;
                i++;
            }

            if (i + 1 >= tokens.Count) return;

            var src = tokens[i];
            var dst = tokens[i + 1];

            scene.Connections.Add(new ConnectionRecord(src, dst, force));
        }

        private static void ParseParent(List<string> tokens, RawStatement raw, MayaSceneData scene, MayaImportLog log)
        {
            var args = new List<string>();
            for (int i = 1; i < tokens.Count; i++)
            {
                if (IsFlag(tokens[i]))
                {
                    if (i + 1 < tokens.Count && !IsFlag(tokens[i + 1]))
                        i++;
                    continue;
                }
                args.Add(tokens[i]);
            }

            if (args.Count >= 2)
            {
                var child = args[0];
                var parent = args[1];

                var n = scene.GetOrCreateNode(child);
                n.ParentName = parent;
            }
        }

        private static void ParseRename(List<string> tokens, MayaSceneData scene, MayaImportLog log, ref string currentNode)
        {
            string uuid = null;
            string name = null;

            var args = new List<string>();

            for (int i = 1; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t == "-uid" && i + 1 < tokens.Count)
                {
                    uuid = tokens[i + 1];
                    i++;
                }
                else if (IsFlag(t))
                {
                    if (i + 1 < tokens.Count && !IsFlag(tokens[i + 1]))
                        i++;
                }
                else
                {
                    args.Add(t);
                }
            }

            if (args.Count >= 1)
                name = args[args.Count - 1];

            if (string.IsNullOrEmpty(name))
                return;

            var n = scene.GetOrCreateNode(name);
            if (!string.IsNullOrEmpty(uuid))
                n.Uuid = uuid;

            currentNode = name;
        }

        // ============================
        // Category1 structured commands
        // ============================

        private static void ParseFileInfo(List<string> tokens, RawStatement raw, MayaSceneData scene)
        {
            if (tokens.Count >= 3)
            {
                scene.FileInfo.Add(new MayaFileInfoEntry
                {
                    LineStart = raw.LineStart,
                    LineEnd = raw.LineEnd,
                    Key = tokens[1],
                    Value = tokens[2]
                });
            }
        }

        private static void ParseRequires(List<string> tokens, RawStatement raw, MayaSceneData scene)
        {
            if (tokens.Count >= 3)
            {
                scene.Requires.Add(new MayaRequiresEntry
                {
                    LineStart = raw.LineStart,
                    LineEnd = raw.LineEnd,
                    Plugin = tokens[1],
                    Version = tokens[2]
                });
            }
        }

        private static void ParseWorkspace(List<string> tokens, RawStatement raw, MayaSceneData scene)
        {
            var flags = ParseFlags(tokens, startIndex: 1, out var args);

            var entry = new MayaWorkspaceRuleEntry
            {
                LineStart = raw.LineStart,
                LineEnd = raw.LineEnd,
                Flags = flags
            };

            if (flags.TryGetValue("-fr", out var _))
            {
                if (args.Count >= 2)
                {
                    entry.Rule = args[0];
                    entry.Path = args[1];
                }
            }
            else
            {
                if (args.Count >= 2)
                {
                    entry.Rule = args[0];
                    entry.Path = args[1];
                }
            }

            scene.WorkspaceRules.Add(entry);
        }

        private static void ParseNamespace(List<string> tokens, RawStatement raw, MayaSceneData scene)
        {
            var flags = ParseFlags(tokens, startIndex: 1, out var args);

            string op = null;
            if (flags.ContainsKey("-set")) op = "set";
            else if (flags.ContainsKey("-add")) op = "add";
            else if (flags.ContainsKey("-rm") || flags.ContainsKey("-remove")) op = "remove";
            else if (flags.ContainsKey("-mv") || flags.ContainsKey("-move")) op = "move";
            else if (flags.ContainsKey("-ren") || flags.ContainsKey("-rename")) op = "rename";
            else op = "unknown";

            var name = args.Count > 0 ? args[0] : null;

            scene.NamespaceOps.Add(new MayaNamespaceOp
            {
                LineStart = raw.LineStart,
                LineEnd = raw.LineEnd,
                Operation = op,
                Name = name,
                Flags = flags
            });
        }

        private static void ParseAddAttr(List<string> tokens, RawStatement raw, MayaSceneData scene, MayaImportLog log, string currentNode)
        {
            var flags = ParseFlags(tokens, startIndex: 1, out var args);

            string target = null;
            if (args.Count > 0) target = args[args.Count - 1];
            if (string.IsNullOrEmpty(target)) target = currentNode;

            if (string.IsNullOrEmpty(target))
            {
                log?.Warn($"addAttr without target node at lines {raw.LineStart}-{raw.LineEnd}");
                return;
            }

            var cmd = new MayaAddAttrCommand
            {
                LineStart = raw.LineStart,
                LineEnd = raw.LineEnd,
                TargetNode = target,
                Flags = flags
            };

            flags.TryGetValue("-ln", out cmd.LongName);
            flags.TryGetValue("-sn", out cmd.ShortName);
            flags.TryGetValue("-nn", out cmd.NiceName);
            flags.TryGetValue("-p", out cmd.Parent);
            flags.TryGetValue("-at", out cmd.AttributeType);
            flags.TryGetValue("-dt", out cmd.DataType);
            flags.TryGetValue("-dv", out cmd.DefaultValue);
            flags.TryGetValue("-min", out cmd.MinValue);
            flags.TryGetValue("-max", out cmd.MaxValue);

            cmd.Keyable = TryParseBool(flags, "-k");
            cmd.ChannelBox = TryParseBool(flags, "-cb");
            cmd.Hidden = TryParseBool(flags, "-h");
            cmd.Multi = TryParseBool(flags, "-m");

            var node = scene.GetOrCreateNode(target);
            node.AddAttr.Add(cmd);
        }

        private static void ParseDeleteAttr(List<string> tokens, RawStatement raw, MayaSceneData scene, MayaImportLog log, string currentNode)
        {
            var flags = ParseFlags(tokens, startIndex: 1, out var args);

            string target = null;
            string attr = null;

            if (flags.TryGetValue("-at", out var at))
                attr = at;

            if (args.Count > 0)
            {
                if (args.Count == 1 && string.IsNullOrEmpty(attr))
                {
                    attr = args[0];
                    target = currentNode;
                }
                else
                {
                    target = args[args.Count - 1];
                    if (string.IsNullOrEmpty(attr) && args.Count >= 2)
                        attr = args[0];
                }
            }

            if (string.IsNullOrEmpty(target)) target = currentNode;

            if (string.IsNullOrEmpty(target))
            {
                log?.Warn($"deleteAttr without target at lines {raw.LineStart}-{raw.LineEnd}");
                return;
            }

            var cmd = new MayaDeleteAttrCommand
            {
                LineStart = raw.LineStart,
                LineEnd = raw.LineEnd,
                TargetNode = target,
                Attribute = attr,
                Flags = flags
            };

            var node = scene.GetOrCreateNode(target);
            node.DeleteAttr.Add(cmd);
        }

        private static void ParseLockNode(List<string> tokens, RawStatement raw, MayaSceneData scene, MayaImportLog log)
        {
            var flags = ParseFlags(tokens, startIndex: 1, out var args);

            var cmd = new MayaLockNodeCommand
            {
                LineStart = raw.LineStart,
                LineEnd = raw.LineEnd,
                Flags = flags
            };
            cmd.Targets.AddRange(args);

            for (int i = 0; i < cmd.Targets.Count; i++)
            {
                var n = scene.GetOrCreateNode(cmd.Targets[i]);
                n.LockOps.Add(cmd);
            }
        }

        private static void ParseSelect(List<string> tokens, RawStatement raw, MayaSceneData scene)
        {
            _ = ParseFlags(tokens, startIndex: 1, out _);
        }

        private static void ParseSetKeyframe(List<string> tokens, RawStatement raw, MayaSceneData scene)
        {
            var flags = ParseFlags(tokens, startIndex: 1, out var args);

            var cmd = new MayaSetKeyframeCommand
            {
                LineStart = raw.LineStart,
                LineEnd = raw.LineEnd,
                Flags = flags
            };
            cmd.Targets.AddRange(args);

            scene.SetKeyframes.Add(cmd);
        }

        private static void ParseSetDrivenKeyframe(List<string> tokens, RawStatement raw, MayaSceneData scene)
        {
            var flags = ParseFlags(tokens, startIndex: 1, out var args);

            var cmd = new MayaDrivenKeyframeCommand
            {
                LineStart = raw.LineStart,
                LineEnd = raw.LineEnd,
                Flags = flags
            };
            cmd.Targets.AddRange(args);

            scene.DrivenKeyframes.Add(cmd);
        }

        private static void ParseAnimLayer(List<string> tokens, RawStatement raw, MayaSceneData scene)
        {
            var flags = ParseFlags(tokens, startIndex: 1, out var args);

            var cmd = new MayaAnimLayerCommand
            {
                LineStart = raw.LineStart,
                LineEnd = raw.LineEnd,
                Flags = flags
            };
            cmd.Targets.AddRange(args);

            scene.AnimLayers.Add(cmd);
        }

        private static void ParseConnectDynamic(List<string> tokens, RawStatement raw, MayaSceneData scene)
        {
            var flags = ParseFlags(tokens, startIndex: 1, out var args);

            var cmd = new MayaConnectDynamicCommand
            {
                LineStart = raw.LineStart,
                LineEnd = raw.LineEnd,
                Flags = flags
            };
            cmd.Targets.AddRange(args);

            scene.ConnectDynamics.Add(cmd);
        }

        private static void ParseScriptNode(List<string> tokens, RawStatement raw, MayaSceneData scene)
        {
            var flags = ParseFlags(tokens, startIndex: 1, out var args);

            var cmd = new MayaScriptNodeCommand
            {
                LineStart = raw.LineStart,
                LineEnd = raw.LineEnd,
                Flags = flags
            };

            if (args.Count > 0)
                cmd.Name = args[args.Count - 1];

            if (!flags.TryGetValue("-bs", out cmd.Script))
                if (!flags.TryGetValue("-b", out cmd.Script))
                    if (!flags.TryGetValue("-st", out cmd.Script))
                        flags.TryGetValue("-s", out cmd.Script);

            scene.ScriptNodes.Add(cmd);
        }

        private static void ParseEvalDeferred(List<string> tokens, RawStatement raw, MayaSceneData scene)
        {
            string code = null;
            if (tokens.Count >= 2)
                code = tokens[1];

            scene.EvalDeferred.Add(new MayaEvalDeferredCommand
            {
                LineStart = raw.LineStart,
                LineEnd = raw.LineEnd,
                Code = code
            });
        }

        private static void ParseExpression(List<string> tokens, RawStatement raw, MayaSceneData scene)
        {
            var flags = ParseFlags(tokens, startIndex: 1, out var args);

            var cmd = new MayaExpressionCommand
            {
                LineStart = raw.LineStart,
                LineEnd = raw.LineEnd,
                Flags = flags
            };

            flags.TryGetValue("-n", out cmd.Name);
            flags.TryGetValue("-s", out cmd.Expression);

            if (string.IsNullOrEmpty(cmd.Name) && args.Count > 0)
                cmd.Name = args[0];

            scene.Expressions.Add(cmd);
        }

        // ============================
        // Token utilities
        // ============================

        private static bool IsFlag(string token)
        {
            return !string.IsNullOrEmpty(token) && token.Length >= 2 && token[0] == '-';
        }

        private static Dictionary<string, string> ParseFlags(List<string> tokens, int startIndex, out List<string> args)
        {
            var flags = new Dictionary<string, string>(StringComparer.Ordinal);
            args = new List<string>();

            for (int i = startIndex; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (!IsFlag(t))
                {
                    args.Add(t);
                    continue;
                }

                string value = "true";

                if (i + 1 < tokens.Count && !IsFlag(tokens[i + 1]))
                {
                    value = tokens[i + 1];
                    i++;
                }

                flags[t] = value;
            }

            return flags;
        }

        private static bool? TryParseBool(Dictionary<string, string> flags, string key)
        {
            if (!flags.TryGetValue(key, out var v)) return null;
            if (string.IsNullOrEmpty(v)) return true;

            var s = v.Trim().ToLowerInvariant();
            if (s == "1" || s == "true" || s == "yes" || s == "on") return true;
            if (s == "0" || s == "false" || s == "no" || s == "off") return false;

            return null;
        }
    }
}
