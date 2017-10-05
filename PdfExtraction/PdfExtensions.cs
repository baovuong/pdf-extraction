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
            var fonts = document.Pages.Cast<PdfPage>()
                .SelectMany(p => FindObjects(new string[] { "/Resources", "/Font", "/F*" }, p, true))
                .Select(i => new CharacterMap(i.Item1, i.Item2));
            return Regex.Matches(result.ToString(), "BT\\s(.*?)\\sET", RegexOptions.Singleline)
                .Cast<Match>()
                .Select(m => ProcessTextObject(document, fonts, m.Groups[1].Value))
                .Aggregate((a, b) => a + b);
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

        private static string ProcessTextObject(PdfDocument document, IEnumerable<CharacterMap> fontMappings, string input)
        {
            // this will have to be stack based, but I need to prepare it for the stack
            input = new string[] { "TJ", "Tj", "Tm", "Tf", "Td" }.
                Aggregate(input, (a, b) => Regex.Replace(a, "(?<=[^\\s-])" + b, " " + b + " "));
            
            input = input.Replace(")'", ") '");
            input = input.Replace(")\"", ") \"");

            var parameters = new Stack<string>();
            var result = new StringBuilder();
            var tokens = input.Split(null);
            string mappingIndex = null;
            // here we go baby
            foreach (var t in tokens)
            {
                switch (t)
                {
                    case "'":
                    case "\"":
                    case "Tj":
                        result.Append(ShowTextOp(parameters, fontMappings.Where(m => m.Name == mappingIndex).FirstOrDefault()));
                        break;
                    case "TJ":
                        result.Append(ShowTextGlyphOp(parameters, fontMappings.Where(m => m.Name == mappingIndex).FirstOrDefault()));
                        break;
                    case "Tf":
                        // font things
                        mappingIndex = SelectFontOp(parameters);
                        break;
                    case "Tm":
                    case "Td":
                    case "G":
                    case "Gs":
                        IgnoreTextOp(parameters);
                        break;
                    default:
                        parameters.Push(t);
                        break;
                }
            }

            return result.ToString();
        }
        private static string ShowTextOp(Stack<string> parameters, CharacterMap mapping)
        {
            // check for the following:
            // * literal strings. (ex. (hello) )
            // * hex strings. (ex. (<44> <54> <67>)
            // * identity-h (why) (ex. <0001000500400F40>)
            var result = string.Empty;
            var input = string.Empty;
            while (parameters.Any())
            {
                input = input.Insert(0, " " + parameters.Pop());
            }
            // processing time
            var state = 0;
            var content = string.Empty;
            foreach(var c in input)
            {
                switch (state)
                {
                    case 0:
                        if (c == '(')
                        {
                            state = 1;
                        }
                        else if (c == '<')
                        {
                            state = 2;
                        }
                        break;
                    case 1:
                        if (c == ')')
                        {
                            // process this
                            // literal string
                            result += content;
                            content = string.Empty;
                            state = 0;
                        }
                        else
                        {
                            content += c;
                        }
                        break;
                    case 2:
                        if (c == '>')
                        {
                            // process this hex stuff
                            // check if identity h or just hex string
                            if (content.Length % 4 == 0)
                            {
                                // identity h
                                result += mapping == null ? content : mapping.Convert(content);
                            }
                            else
                            {
                                // regular hex stuff
                            }

                            content = string.Empty;
                            state = 0;
                        }
                        else
                        {
                            content += c;
                        }
                        break;
                    default:
                        break;
                }
            }
            return result;
        }

        private static string ShowTextGlyphOp(Stack<string> parameters, CharacterMap mapping)
        {
            // check for the following:
            // * literal strings. (ex. (hello) )
            // * hex strings. (ex. (<44> <54> <67>)
            // * identity-h (why) (ex. <0001000500400F40>)
            var result = string.Empty;
            var input = string.Empty;
            while (parameters.Any())
            {
                input = input.Insert(0, " " + parameters.Pop());
            }
            // processing time
            var state = 0;
            var content = string.Empty;
            foreach (var c in input)
            {
                switch (state)
                {
                    case 0:
                        if (c == '(')
                        {
                            state = 1;
                        }
                        else if (c == '<')
                        {
                            state = 2;
                        }
                        break;
                    case 1:
                        if (c == ')')
                        {
                            // process this
                            // literal string
                            result += mapping != null ? mapping.Convert(content
                                .Select(_ => Convert.ToByte(_).ToString("X2"))
                                .Aggregate((a, b) => a + b)) : content;
                            content = string.Empty;
                            state = 0;
                        }
                        else
                        {
                            content += c;
                        }
                        break;
                    case 2:
                        if (c == '>')
                        {
                            // process this hex stuff
                            // check if identity h or just hex string
                            if (content.Length % 4 == 0)
                            {
                                // identity h
                                result += mapping == null ? content : mapping.Convert(content);
                            }
                            else
                            {
                                // regular hex stuff
                            }

                            content = string.Empty;
                            state = 0;
                        }
                        else
                        {
                            content += c;
                        }
                        break;
                    default:
                        break;
                }
            }
            return result;
        }

        private static string SelectFontOp(Stack<string> parameters)
        {
            string result = parameters.Pop();
            while (parameters.Any())
            {
                var curr = parameters.Pop();
                if (!string.IsNullOrEmpty(curr))
                {
                    result = curr;
                }
            }
            return result;
        }

        private static void IgnoreTextOp(Stack<string> parameters) => parameters.Clear();

        private static IEnumerable<Tuple<string, PdfItem>> FindObjects(string[] objectHierarchy, PdfItem startingObject, bool followHierarchy)
        {
            var results = new List<Tuple<string, PdfItem>>();
            FindObjects(objectHierarchy, startingObject, followHierarchy, ref results, 0);
            return results;
        }
        private static void FindObjects(string[] objectHierarchy, PdfItem startingObject, bool followHierarchy, ref List<Tuple<string, PdfItem>> results, int Level)
        {
            PdfName[] keyNames = ((PdfDictionary)startingObject).Elements.KeyNames;
            foreach (PdfName keyName in keyNames)
            {
                bool matchFound = false;
                if (!followHierarchy)
                {
                    // We need to check all items for a match, not just the top one
                    for (int i = 0; i < objectHierarchy.Length; i++)
                    {
                        if (keyName.Value == objectHierarchy[i] ||
                            (objectHierarchy[i].Contains("*") &&
                                (keyName.Value.StartsWith(objectHierarchy[i].Substring(0, objectHierarchy[i].IndexOf("*") - 1)) &&
                                keyName.Value.EndsWith(objectHierarchy[i].Substring(objectHierarchy[i].IndexOf("*") + 1)))))
                        {
                            matchFound = true;
                        }
                    }
                }
                else
                {
                    // Check the item in the hierarchy at this level for a match
                    if (Level < objectHierarchy.Length && (keyName.Value == objectHierarchy[Level] ||
                        (objectHierarchy[Level].Contains("*") &&
                                (keyName.Value.StartsWith(objectHierarchy[Level].Substring(0, objectHierarchy[Level].IndexOf("*") - 1)) &&
                                keyName.Value.EndsWith(objectHierarchy[Level].Substring(objectHierarchy[Level].IndexOf("*") + 1))))))
                    {
                        matchFound = true;
                    }
                }

                if (matchFound)
                {
                    PdfItem item = ((PdfDictionary)startingObject).Elements[keyName];
                    if (item != null && item is PdfReference)
                    {
                        item = ((PdfReference)item).Value;
                    }

                    if (Level == objectHierarchy.Length - 1)
                    {
                        // We are at the end of the hierarchy, so this is the target
                        results.Add(Tuple.Create(keyName.Value, item));
                    }
                    else if (!followHierarchy)
                    {
                        // We are returning every matching object so add it
                        results.Add(Tuple.Create(keyName.Value, item));
                    }

                    // Call back to this function to search lower levels
                    Level++;
                    FindObjects(objectHierarchy, item, followHierarchy, ref results, Level);
                    Level--;
                }
            }
            Level--;
        }
    }
}
