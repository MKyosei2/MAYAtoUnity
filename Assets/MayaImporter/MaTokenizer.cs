using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MayaImporter.Parser
{
    public static class MaTokenizer
    {
        public static IEnumerable<string> Tokenize(string path)
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
            var buffer = new StringBuilder();

            while (!reader.EndOfStream)
            {
                char c = (char)reader.Read();

                if (char.IsWhiteSpace(c))
                {
                    Flush(buffer, out var token);
                    if (token != null) yield return token;
                }
                else if (c == ';')
                {
                    Flush(buffer, out var token);
                    if (token != null) yield return token;
                    yield return ";";
                }
                else
                {
                    buffer.Append(c);
                }
            }

            Flush(buffer, out var last);
            if (last != null) yield return last;
        }

        private static void Flush(StringBuilder sb, out string token)
        {
            if (sb.Length > 0)
            {
                token = sb.ToString();
                sb.Clear();
            }
            else token = null;
        }
    }
}
