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

    internal sealed class MarkChange
    {
        internal int Start;
        internal int Length;
        internal string Text;
    }

    internal sealed class Word
    {
        internal int Start;
        internal string Original;
        internal List<Reading> Readings;
        internal int Selected;
        internal Compound Compound;
    }

    internal sealed class Compound
    {
        internal string[] Parts;
        internal string Meaning;
        internal bool Selected = true;
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
            var spans = Hebrew.Matches(text).Cast<Match>().ToArray();
            if (spans.Length == 0) throw new InvalidOperationException("Select at least one Hebrew word.");
            if (IsRemoval(text))
            {
                return spans.Select(span => new Word
                {
                    Start = span.Index,
                    Original = span.Value,
                    Readings = new List<Reading> { new Reading { Text = Marks.Replace(span.Value, ""), Meaning = "" } },
                    Selected = 0,
                }).ToList();
            }
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

        internal static bool IsRemoval(string text)
        {
            var spans = Hebrew.Matches(text).Cast<Match>().ToArray();
            return spans.Length > 0 && spans.All(span => Marks.IsMatch(span.Value));
        }

        internal static bool SameSpelling(string left, string right) { return Strip(left) == Strip(right); }

        internal static List<MarkChange> MarkChanges(string original, string replacement)
        {
            var changes = new List<MarkChange>();
            int before = 0, after = 0;
            while (before < original.Length && after < replacement.Length)
            {
                char letter = original[before++];
                if (letter != replacement[after++]) return null;
                if (letter < 'א' || letter > 'ת') continue;
                int start = before, next = after;
                while (before < original.Length && Marks.IsMatch(original[before].ToString())) before++;
                while (after < replacement.Length && Marks.IsMatch(replacement[after].ToString())) after++;
                string marks = replacement.Substring(next, after - next);
                if (marks != original.Substring(start, before - start))
                    changes.Add(new MarkChange { Start = start, Length = before - start, Text = marks });
            }
            return before == original.Length && after == replacement.Length ? changes : null;
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
                Reading preferredReading = null;
                int readingIndex = 0;
                foreach (var option in Objects(data[i]["readings"]))
                {
                    var pointed = ((string)option["text"]).Normalize();
                    var meaning = (string)option["meaning"];
                    object grammaticalType;
                    if (option.TryGetValue("pos", out grammaticalType) && !string.IsNullOrEmpty((string)grammaticalType))
                        meaning = string.IsNullOrEmpty(meaning) ? (string)grammaticalType : meaning + ", " + grammaticalType;
                    bool isPreferred = readingIndex++ == preferred;
                    if (Strip(pointed) != Strip(span.Value)) continue;
                    var reading = word.Readings.Find(item => item.Text == pointed && item.Meaning == meaning);
                    if (reading == null)
                    {
                        reading = new Reading { Text = pointed, Meaning = meaning };
                        word.Readings.Add(reading);
                    }
                    if (isPreferred) preferredReading = reading;
                }
                if (word.Readings.Count == 1) word.Selected = 0;
                else if (spans.Length > 1) word.Selected = word.Readings.IndexOf(preferredReading);
                object compound;
                if (i + 1 < spans.Length && data[i].TryGetValue("compound", out compound))
                {
                    var entry = (Dictionary<string, object>)compound;
                    var parts = ((IEnumerable)entry["parts"]).Cast<string>().Select(part => part.Normalize()).ToArray();
                    if (parts.Length == 2 && Strip(parts[0]) == Strip(span.Value) && Strip(parts[1]) == Strip(spans[i + 1].Value))
                    {
                        object phraseType;
                        string label = (string)entry["meaning"];
                        if (entry.TryGetValue("pos", out phraseType)) label += ", " + phraseType;
                        word.Compound = new Compound { Parts = parts, Meaning = label };
                    }
                }
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
                string pointed = word.Selected >= 0 ? word.Readings[word.Selected].Text : word.Original;
                if (word.Compound != null && word.Compound.Selected)
                    pointed = word.Compound.Parts[0];
                else if (i > 0 && words[i - 1].Compound != null && words[i - 1].Compound.Selected)
                    pointed = words[i - 1].Compound.Parts[1];
                output.Remove(word.Start, word.Original.Length).Insert(word.Start, pointed);
            }
            return output.ToString();
        }

        private static string Strip(string value)
        {
            return Marks.Replace(value.Normalize(NormalizationForm.FormD), "").Normalize();
        }
    }
}
