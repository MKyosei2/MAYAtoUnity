// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Text;

namespace MayaImporter.Core
{
    internal static class MayaMaTokenizer
    {
        /// <summary>
        /// Split the whole .ma file into statements, each ending with ';' outside strings/comments.
        /// Comments are removed but line positions are preserved via line counters.
        /// </summary>
        public static List<MayaStatement> SplitStatements(string text, MayaImportLog log)
        {
            var list = new List<MayaStatement>();

            bool inString = false;
            bool inLineComment = false;
            bool inBlockComment = false;

            var sb = new StringBuilder();

            int line = 1;
            int stmtLineStart = 1;
            int stmtLineEnd = 1;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                char next = (i + 1 < text.Length) ? text[i + 1] : '\0';

                if (c == '\n')
                {
                    line++;
                    if (inLineComment) inLineComment = false;
                    // Preserve newlines in statement buffer only if we're not in comments.
                    if (!inLineComment && !inBlockComment) sb.Append(c);
                    continue;
                }

                if (inLineComment)
                {
                    // Ignore until newline
                    continue;
                }

                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++; // consume '/'
                    }
                    continue;
                }

                if (!inString)
                {
                    // start comments
                    if (c == '/' && next == '/')
                    {
                        inLineComment = true;
                        i++; // consume second '/'
                        continue;
                    }
                    if (c == '/' && next == '*')
                    {
                        inBlockComment = true;
                        i++; // consume '*'
                        continue;
                    }
                }

                if (c == '"' && !IsEscaped(text, i))
                {
                    inString = !inString;
                    sb.Append(c);
                    continue;
                }

                if (c == ';' && !inString)
                {
                    var stmtText = sb.ToString().Trim();
                    sb.Length = 0;

                    stmtLineEnd = line;

                    if (!string.IsNullOrWhiteSpace(stmtText))
                    {
                        list.Add(new MayaStatement
                        {
                            Text = stmtText,
                            LineStart = stmtLineStart,
                            LineEnd = stmtLineEnd
                        });
                    }

                    stmtLineStart = line;
                    continue;
                }

                sb.Append(c);
            }

            // trailing (no ';') - keep as raw if non-empty
            var tail = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(tail))
            {
                log?.Warn($"Trailing statement without ';' at end of file (line {stmtLineStart}). Keeping it as raw text.");
                list.Add(new MayaStatement { Text = tail, LineStart = stmtLineStart, LineEnd = line });
            }

            return list;
        }

        public static List<string> Tokenize(string statementText)
        {
            // MEL-like tokenization: spaces separate tokens, quoted strings kept together.
            var tokens = new List<string>();
            var sb = new StringBuilder();
            bool inString = false;

            for (int i = 0; i < statementText.Length; i++)
            {
                char c = statementText[i];

                if (c == '"' && !IsEscaped(statementText, i))
                {
                    inString = !inString;
                    continue; // drop quotes
                }

                if (!inString && char.IsWhiteSpace(c))
                {
                    Flush();
                    continue;
                }

                sb.Append(c);
            }

            Flush();
            return tokens;

            void Flush()
            {
                if (sb.Length == 0) return;
                tokens.Add(Unescape(sb.ToString()));
                sb.Length = 0;
            }
        }

        private static bool IsEscaped(string s, int quoteIndex)
        {
            // count backslashes before quote
            int slashCount = 0;
            for (int i = quoteIndex - 1; i >= 0 && s[i] == '\\'; i--) slashCount++;
            return (slashCount % 2) == 1;
        }

        private static string Unescape(string s)
        {
            // minimal unescape: \" \\ \n \t
            return s
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t");
        }
    }

    internal sealed class MayaStatement
    {
        public string Text;
        public int LineStart;
        public int LineEnd;
    }
}
