using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            var textObjectRegex = new Regex("BT(.*?)ET", RegexOptions.Singleline);

            var textObjectStrings = textObjectRegex.Matches(result.ToString())
                .Cast<Match>()
                .Select(m => m.Groups[1].Value);

            var processed = textObjectStrings.Select(s => ProcessTextObject(s));

            return result.ToString();
        }

        private static void HandleObject(PdfDocument document, PdfObject value, HashSet<int> seenObjectNumbers, StringBuilder result)
        {
            if (value is PdfDictionary)
                HandleDictionary(document, (PdfDictionary)value, seenObjectNumbers, result);
            else if (value is PdfArray)
                HandleArray(document, (PdfArray)value, seenObjectNumbers, result);
        }

        private static void HandleDictionary(PdfDocument document, PdfDictionary value, HashSet<int> seenObjectNumbers, StringBuilder target)
        {
            
            if (value.Stream != null)
            {
                // parse through the stream
                target.Append(Encoding.Default.GetString(value.Stream.UnfilteredValue));
                
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
            if (item is PdfObject) HandleObject(document, (PdfObject)item, seenObjectNumbers, target);
            else if (item is PdfReference) HandleReference(document, (PdfReference)item, seenObjectNumbers, target);
            else if (item is PdfString) HandleString((PdfString)item, target);
            else if (item is PdfLiteral) HandleLiteral((PdfLiteral)item, target);
            else if (item is PdfName) HandleName((PdfName)item, target);
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

        private static string ProcessTextObject(string input)
        {
            var result = new StringBuilder();
            var tokens = input.Split(null).Where(t => !string.IsNullOrEmpty(t));
            var parameters = new Stack<string>();
            foreach (var token in tokens)
            {
                switch (token.ToUpper())
                {
                    case "TJ":
                        result.Append(ShowTextObjectProcessing(parameters));
                        break;
                    case "TF":
                        // font things
                        // TODO implement this
                        EmptyTextObjectProcessing(parameters);
                        break;
                    case "TD":
                    case "TM":
                    case "GS":
                    case "G":
                        EmptyTextObjectProcessing(parameters);
                        break;
                    default:
                        parameters.Push(token);
                        break;
                }
            }
            return null;
        }

        private static void EmptyTextObjectProcessing(Stack<string> parameters)
        {
            // empty the stack
            parameters.Clear();
        }

        private static string ShowTextObjectProcessing(Stack<string> parameters)
        {
            var result = new StringBuilder();
            while (parameters.Any())
            {
                result.Insert(0, parameters.Pop());
            }

            // Tj
            return result.ToString();
        }
    }
}
