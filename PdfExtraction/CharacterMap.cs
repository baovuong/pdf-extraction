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
                CodeSpaceRange = Tuple.Create(spaceRangeMatch.Groups[1].Value, spaceRangeMatch.Groups[2].Value);
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
                            match.Groups[1].Value, 
                            match.Groups[2].Value,
                            Regex.Matches(match.Groups[3].Value, "<(\\w+)>")
                            .Cast<Match>()
                            .Select(m => m.Groups[1].Value));
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
                        return new KeyValuePair<string, string>(match.Groups[1].Value, match.Groups[2].Value);
                    }
                    return new KeyValuePair<string, string>(null, null);
                }).Where(p => p.Key != null && p.Value != null).ToDictionary(p => p.Key, p => p.Value);

            }

        }

        public string Get(string index) 
        {
            // TODO work on thising 

            // check range first
            var indexValue = int.Parse(index, NumberStyles.HexNumber);
            if (indexValue < FromHex(CodeSpaceRange.Item1) || indexValue > FromHex(CodeSpaceRange.Item2))
            {
                return null;
            }

            // ok, bf range
            var result = BfRange
                .Where(_ => FromHex(index) >= FromHex(_.Item1) && FromHex(index) <= FromHex(_.Item2))
                .Select(_ => new { Beginning = FromHex(_.Item1), Values = _.Item3.ToList() })
                .Select(_ => _.Values[FromHex(index)-_.Beginning])
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(result)) return result;

            // check individual values
            string r;
            return BfChar.TryGetValue(index, out r) ? r : null;
        }

        public string this[string i]
        {
            get => Get(i);
        }

        public string Convert(string input)
        {
            // each entry should be 2 bytes (4 digits)

            return null;
        }

        private int FromHex(string value) => int.Parse(value, NumberStyles.HexNumber);
    }
}
