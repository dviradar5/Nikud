using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Nikud
{
    internal sealed class Reading
    {
        internal string Text;
        internal string Meaning;
    }

    internal sealed class Word
    {
        internal int Start;
        internal string Original;
        internal List<Reading> Readings;
        internal int Selected;
    }

    internal static class Engine
    {
        private static readonly Regex Hebrew = new Regex("[א-ת](?:[א-ת\\u0591-\\u05bd\\u05bf\\u05c1\\u05c2\\u05c4\\u05c5\\u05c7]|['\"׳״](?=[א-ת]))*");
        private static readonly Regex Marks = new Regex(@"[\u05b0-\u05bd\u05bf\u05c1\u05c2\u05c7]");
        internal static bool Available { get { return File.Exists(Executable); } }

        private static string Executable
        {
            get { return Path.Combine(Path.GetDirectoryName(typeof(Engine).Assembly.Location), "NikudEngine", "NikudEngine.exe"); }
        }

        internal static async Task<List<Word>> VocalizeAsync(string text)
        {
            if (!Hebrew.IsMatch(text)) throw new InvalidOperationException("Select at least one Hebrew word.");
            if (!Available) throw new InvalidOperationException("Extract the entire Nikud ZIP and keep the NikudEngine folder beside Nikud.exe.");
            var body = new JavaScriptSerializer().Serialize(new { text });
            var start = new ProcessStartInfo(Executable)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var diagnostics = process.StandardError.ReadToEndAsync();
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(body);
                    await process.StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                }
                finally { process.StandardInput.Close(); }
                if (!await Task.Run(() => process.WaitForExit(25000)).ConfigureAwait(false))
                {
                    if (!process.HasExited) process.Kill();
                    throw new InvalidOperationException("Nikud took too long. Try a shorter selection.");
                }
                await diagnostics.ConfigureAwait(false);
                if (process.ExitCode != 0) throw new InvalidOperationException("The offline vocalizer could not start or finish. Try extracting the ZIP again.");
                return ParseWords(text, await output.ConfigureAwait(false));
            }
        }

        internal static List<Word> ParseWords(string text, string result)
        {
            var spans = Hebrew.Matches(text).Cast<Match>().ToArray();
            var reply = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(result);
            object error;
            if (reply.TryGetValue("error", out error)) throw new InvalidOperationException((string)error);
            var data = Objects(reply["words"]).ToArray();
            if (data.Length != spans.Length)
                throw new InvalidOperationException("The result did not align with the original words. Try again.");
            var words = new List<Word>();
            for (int i = 0; i < spans.Length; i++)
            {
                var span = spans[i];
                var word = new Word { Start = span.Index, Original = span.Value, Readings = new List<Reading>(), Selected = -1 };
                int preferred = (int)data[i]["preferred"];
                string preferredText = null;
                int readingIndex = 0;
                foreach (var option in Objects(data[i]["readings"]))
                {
                    var pointed = ((string)option["text"]).Normalize();
                    if (readingIndex++ == preferred) preferredText = pointed;
                    if (Strip(pointed) == Strip(span.Value) && word.Readings.All(item => item.Text != pointed))
                        word.Readings.Add(new Reading { Text = pointed, Meaning = (string)option["meaning"] });
                }
                if (word.Readings.Count == 1) word.Selected = 0;
                else if (spans.Length > 1) word.Selected = word.Readings.FindIndex(item => item.Text == preferredText);
                words.Add(word);
            }
            return words;
        }

        private static IEnumerable<Dictionary<string, object>> Objects(object value)
        {
            return ((IEnumerable)value).Cast<Dictionary<string, object>>();
        }

        internal static string Render(string source, List<Word> words)
        {
            var output = new StringBuilder(source);
            for (int i = words.Count - 1; i >= 0; i--)
            {
                var word = words[i];
                if (word.Selected >= 0)
                    output.Remove(word.Start, word.Original.Length).Insert(word.Start, word.Readings[word.Selected].Text);
            }
            return output.ToString();
        }

        private static string Strip(string value)
        {
            return Marks.Replace(value.Normalize(NormalizationForm.FormD), "").Normalize();
        }
    }
}
