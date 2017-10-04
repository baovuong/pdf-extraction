using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;

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


            BfRange = new List<Tuple<string, string, IEnumerable<string>>>();

        }

        public string Get(string index) 
        {
            // TODO work on thising 
            return null; 
        }

        public string this[string i]
        {
            get => Get(i);
        }
    }
}
