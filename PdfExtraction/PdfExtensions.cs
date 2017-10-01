using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VuongIdeas.PdfExtraction
{
    public static class PdfExtensions
    {
        public static string ExtractText(this string path)
        {
            using (var document = PdfReader.Open(path, PdfDocumentOpenMode.ReadOnly))
            {
                return document.ExtractText();
            }
        }

        public static string ExtractText(this PdfDocument document)
        {
            var seenObjectNumbers = new HashSet<int>();
            var result = new StringBuilder();
            var objects = document.Internals.GetAllObjects();
            foreach (var o in objects)
            {
                HandleObject(document, o, seenObjectNumbers, result);
            }
            return result.ToString();
        }

        private static void HandleObject(PdfDocument document, PdfObject value, HashSet<int> seenObjectNumbers, StringBuilder result)
        {
            if (value.GetType() == typeof(PdfDictionary))
                HandleDictionary(document, (PdfDictionary)value, seenObjectNumbers, result);
            else if (value.GetType() == typeof(PdfArray))
                HandleArray(document, (PdfArray)value, seenObjectNumbers, result);
        }

        private static void HandleDictionary(PdfDocument document, PdfDictionary value, HashSet<int> seenObjectNumbers, StringBuilder target)
        {
            
            if (value.Stream != null)
            {

            }
            else
            {
                var elements = value.Elements.Values;
                foreach (var element in elements)
                {
                    HandleItem(document, element, seenObjectNumbers, target);
                }
            }
        }
        
        private static void HandleArray(PdfDocument document, PdfArray array, HashSet<int> seenObjectNumbers, StringBuilder target)
        {
            foreach (var element in array)
            {
                HandleItem(document, element, seenObjectNumbers, target);
            }
        }
        private static void HandleItem(PdfDocument document, PdfItem item, HashSet<int> seenObjectNumbers, StringBuilder target)
        {
            if (item.GetType() == typeof(PdfObject)) HandleObject(document, (PdfObject)item, seenObjectNumbers, target);
            else if (item.GetType() == typeof(PdfReference)) HandleReference(document, (PdfReference)item, seenObjectNumbers, target);
            else if (item.GetType() == typeof(PdfString)) HandleString((PdfString)item, target);
            else if (item.GetType() == typeof(PdfLiteral)) HandleLiteral((PdfLiteral)item, target);
            else if (item.GetType() == typeof(PdfName)) HandleName((PdfName)item, target);
            //else if (item.GetType() == typeof(PdfInteger)) HandleInteger((PdfInteger)item, target);
            //else if (item.GetType() == typeof(PdfReal)) HandleReal((PdfReal)item, target);
            //else if (item.GetType() == typeof(PdfBoolean)) HandleBoolean((PdfBoolean)item, target);
        }

        private static void HandleReal(PdfReal item, StringBuilder target) { target.Append(item.Value); }
        private static void HandleInteger(PdfInteger item, StringBuilder target) { target.Append(item.Value); }
        private static void HandleName(PdfName item, StringBuilder target) { target.Append(item.Value); }
        private static void HandleNumber(PdfNumber item, StringBuilder target) { target.Append(item); }
        private static void HandleLiteral(PdfLiteral item, StringBuilder target) { target.Append(item.Value); }
        private static void HandleBoolean(PdfBoolean item, StringBuilder target) { target.Append(item.Value); }

        private static void HandleString(PdfString text, StringBuilder target)
        {
            if (text.HexLiteral)
            {
                target.Append(text);
            }
            else
            {
                target.Append(text);
            }
        }

        private static void HandleReference(PdfDocument document, PdfReference reference, HashSet<int> seenObjectNumbers, StringBuilder target)
        {
            if (!seenObjectNumbers.Contains(reference.ObjectNumber))
            {
                seenObjectNumbers.Add(reference.ObjectNumber);
                HandleObject(document, reference.Value, seenObjectNumbers, target);
            }
        }
    }
}
