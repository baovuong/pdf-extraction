using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using System.Text.RegularExpressions;
using System.Globalization;

namespace VuongIdeas.PdfExtraction
{
    public class CharacterMap
    {
        public string Name { get; set; }
        public Tuple<string, string> CodeSpaceRange { get; set; }
        public IEnumerable<Tuple<string, string, IEnumerable<string>>> BfRange { get; set; }
        public Dictionary<string, string> BfChar { get; set; }

        public CharacterMap()
        {
        }

        public CharacterMap(string name, PdfItem item)
        {
            Name = name;
            var dictionary = (PdfDictionary)item;
            if (!dictionary.Elements.KeyNames.Select(n => n.Value).Contains("/ToUnicode"))
            {
                return;
            }

            var cmapItem = dictionary.Elements["/ToUnicode"];
            if (cmapItem != null && cmapItem is PdfReference)
            {
                cmapItem = ((PdfReference)cmapItem).Value;
            }

            var cmap = ((PdfDictionary)cmapItem).Stream.ToString();

            // space range
            var spaceRangeMatch = Regex.Match(cmap, "begincodespacerange\\s*<(\\w+)>\\s*<(\\w+)>\\s*endcodespacerange", RegexOptions.Singleline);
            if (!spaceRangeMatch.Success)
                CodeSpaceRange = Tuple.Create("0000", "FFFF");
            else
            {
                CodeSpaceRange = Tuple.Create(
                    FromHex(spaceRangeMatch.Groups[1].Value).ToString("X4"), 
                    FromHex(spaceRangeMatch.Groups[2].Value).ToString("X4"));
            }

            // bfrange
            var bfRangeMatch = Regex.Match(cmap, "beginbfrange\\s*(.*?)\\s*endbfrange", RegexOptions.Singleline);
            if (bfRangeMatch.Success)
            {
                // find the rows and split them
                var bfRangeRowRegex = new Regex("<(\\w+)>\\s*<(\\w+)>\\s*([\\[\\]\\w <>]*)");
                BfRange = bfRangeMatch.Groups[1].Value.Split('\n').Select(r => r.Replace("\r", string.Empty)).Select(r =>
                {
                    var match = bfRangeRowRegex.Match(r);
                    if (match.Success)
                    {
                        return Tuple.Create(
                            FromHex(match.Groups[1].Value).ToString("X4"),
                            FromHex(match.Groups[2].Value).ToString("X4"),
                            Regex.Matches(match.Groups[3].Value, "<(\\w+)>")
                            .Cast<Match>()
                            .Select(m => FromHex(m.Groups[1].Value).ToString("X4")));
                    }
                    return null;
                }).Where(t => t != null);
            }

            // bfchar
            var bfCharMatch = Regex.Match(cmap, "beginbfchar\\s*(.+?)\\s*endbfchar", RegexOptions.Singleline);
            if (bfCharMatch.Success)
            {
                var bfCharRowRegex = new Regex("<(\\w+)>\\s*<(\\w+)>");
                BfChar = bfCharMatch.Groups[1].Value.Split('\n').Select(r => r.Replace("\r", string.Empty)).Select(r =>
                {
                    var match = bfCharRowRegex.Match(r);
                    if (match.Success)
                    {
                        return new KeyValuePair<string, string>(
                            FromHex(match.Groups[1].Value).ToString("X4"), 
                            FromHex(match.Groups[2].Value).ToString("X4"));
                    }
                    return new KeyValuePair<string, string>(null, null);
                }).Where(p => p.Key != null && p.Value != null).ToDictionary(p => p.Key, p => p.Value);

            }

        }

        public string Get(string index)
        {
            // TODO work on this 

            // check range first
            var indexValue = int.Parse(index, NumberStyles.HexNumber);
            if (CodeSpaceRange == null || indexValue < FromHex(CodeSpaceRange.Item1) || indexValue > FromHex(CodeSpaceRange.Item2))
            {
                return null;
            }

            // ok, bf range
            var result = BfRange?
                .Where(_ => FromHex(index) >= FromHex(_.Item1) && FromHex(index) <= FromHex(_.Item2))
                .Select(_ => new { Beginning = FromHex(_.Item1), Values = _.Item3 })
                .Select(_ => _.Values.Count() == 1
                    ? ((FromHex(index) - _.Beginning) + FromHex(_.Values.First())).ToString("X4")
                    : FromHex(index) + _.Values.ElementAt(FromHex(index) - _.Beginning))
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(result)) return System.Convert.ToChar(FromHex(result)).ToString();

            // check individual values
            string r;
            return BfChar != null && BfChar.TryGetValue(index.ToUpper(), out r)
                ? Regex.Matches(r, ".{4}")
                    .Cast<Match>()
                    .Select(m => System.Convert.ToChar(FromHex(m.Value)).ToString())
                    .Aggregate((a, b) => a + b) : null;
        }

        public string this[string i]
        {
            get => Get(i);
        }

        public string Convert(string input)
        {
            // each entry should be 2 bytes (4 digits)
            var entries = Regex.Matches(input, ".{4}|.{1,3}")
                .Cast<Match>()
                .Select(m => m.Value);

            // check length of each
            //if (entries.Where(e => e.Length < ).Any())
            //{
            //    // oops bad
            //    return null;
            //}
            return entries.Select(_ => Get(_)).Aggregate((a, b) => a + b);
        }

        private int FromHex(string value) => int.Parse(value, NumberStyles.HexNumber);
    }
}
