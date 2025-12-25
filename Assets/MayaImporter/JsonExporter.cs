using System;
using System.Collections.Generic;
using System.Text;
using MayaImporter.Core;

namespace MayaImporter.Utils
{
    /// <summary>
    /// MayaSceneData を “Unityのみ環境” で外部共有/検証しやすい JSON に吐く。
    /// Dictionary を含むため JsonUtility ではなく手書きで出力する。
    /// </summary>
    public static class JsonExporter
    {
        public static string ExportScene(MayaSceneData scene, bool pretty = true)
        {
            var sb = new StringBuilder(64 * 1024);
            var w = new Writer(sb, pretty);

            w.ObjStart();

            w.Prop("schemaVersion", scene != null ? scene.SchemaVersion : 0);
            w.Prop("sourcePath", scene?.SourcePath);
            w.Prop("sourceKind", scene?.SourceKind.ToString());
            w.Prop("rawSha256", scene?.RawSha256);

            // units
            w.PropName("sceneUnits");
            w.ObjStart();
            if (scene?.SceneUnits != null)
            {
                bool first = true;
                foreach (var kv in scene.SceneUnits)
                {
                    if (!first) w.Comma();
                    first = false;
                    w.IndentProp(kv.Key);
                    w.Str(kv.Value);
                }
            }
            w.ObjEnd();

            // nodes
            w.PropName("nodes");
            w.ArrStart();
            if (scene?.Nodes != null)
            {
                bool first = true;
                foreach (var kv in scene.Nodes)
                {
                    if (!first) w.Comma();
                    first = false;

                    var n = kv.Value;
                    w.ObjStart();
                    w.Prop("name", n?.Name);
                    w.Prop("type", n?.NodeType);
                    w.Prop("parent", n?.ParentName);
                    w.Prop("uuid", n?.Uuid);

                    w.PropName("attributes");
                    w.ObjStart();
                    if (n?.Attributes != null)
                    {
                        bool aFirst = true;
                        foreach (var akv in n.Attributes)
                        {
                            if (!aFirst) w.Comma();
                            aFirst = false;

                            w.IndentProp(akv.Key);
                            var av = akv.Value;
                            w.ObjStart();
                            w.Prop("typeName", av?.TypeName);
                            w.PropName("tokens");
                            w.ArrStart();
                            if (av?.ValueTokens != null)
                            {
                                for (int i = 0; i < av.ValueTokens.Count; i++)
                                {
                                    if (i > 0) w.Comma();
                                    w.Str(av.ValueTokens[i]);
                                }
                            }
                            w.ArrEnd();
                            w.ObjEnd();
                        }
                    }
                    w.ObjEnd();

                    w.ObjEnd();
                }
            }
            w.ArrEnd();

            // connections
            w.PropName("connections");
            w.ArrStart();
            if (scene?.Connections != null)
            {
                for (int i = 0; i < scene.Connections.Count; i++)
                {
                    var c = scene.Connections[i];
                    if (i > 0) w.Comma();
                    w.ObjStart();
                    w.Prop("src", c?.SrcPlug);
                    w.Prop("dst", c?.DstPlug);
                    w.Prop("force", c != null && c.Force);
                    w.ObjEnd();
                }
            }
            w.ArrEnd();

            w.ObjEnd();
            return sb.ToString();
        }

        private sealed class Writer
        {
            private readonly StringBuilder _sb;
            private readonly bool _pretty;
            private int _indent;
            private bool _needIndent;

            public Writer(StringBuilder sb, bool pretty)
            {
                _sb = sb;
                _pretty = pretty;
            }

            public void ObjStart() { Write("{"); _indent++; NewLine(); }
            public void ObjEnd() { _indent--; NewLine(); Write("}"); }
            public void ArrStart() { Write("["); _indent++; NewLine(); }
            public void ArrEnd() { _indent--; NewLine(); Write("]"); }

            public void Comma() { Write(","); NewLine(); }

            public void PropName(string name)
            {
                Indent();
                Str(name);
                Write(_pretty ? ": " : ":");
            }

            public void Prop(string name, string value)
            {
                PropName(name);
                Str(value);
                CommaMaybe();
            }

            public void Prop(string name, int value)
            {
                PropName(name);
                Write(value.ToString());
                CommaMaybe();
            }

            public void Prop(string name, bool value)
            {
                PropName(name);
                Write(value ? "true" : "false");
                CommaMaybe();
            }

            public void IndentProp(string name)
            {
                Indent();
                Str(name);
                Write(_pretty ? ": " : ":");
            }

            public void Str(string s)
            {
                if (s == null) { Write("null"); return; }
                Write("\"" + Escape(s) + "\"");
            }

            private void CommaMaybe()
            {
                Write(",");
                NewLine();
            }

            private void Write(string s)
            {
                Indent();
                _sb.Append(s);
                _needIndent = false;
            }

            private void Indent()
            {
                if (!_pretty) return;
                if (!_needIndent) return;
                _sb.Append(' ', _indent * 2);
                _needIndent = false;
            }

            private void NewLine()
            {
                if (!_pretty) { _needIndent = false; return; }
                _sb.Append('\n');
                _needIndent = true;
            }

            private static string Escape(string s)
            {
                // 最低限（" と \ と制御文字）
                var sb = new StringBuilder(s.Length + 8);
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    switch (c)
                    {
                        case '\\': sb.Append("\\\\"); break;
                        case '"': sb.Append("\\\""); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20) sb.Append("\\u" + ((int)c).ToString("x4"));
                            else sb.Append(c);
                            break;
                    }
                }
                return sb.ToString();
            }
        }
    }
}
