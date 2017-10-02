using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VuongIdeas.PdfExtraction;

namespace PdfExtraction.Tests
{
    [TestFixture]
    public class Acceptance
    {
        [Test] public void PdfValidation1() => PdfValidationTemplate("pdf-sample.pdf", "Adobe");
        [Test] public void PdfValidation2() => PdfValidationTemplate("PDF- test.pdf", "Test File");
        [Test] public void PdfValidation3() => PdfValidationTemplate("pdf.pdf", "The pdf995 suite of products");

        public void PdfValidationTemplate(string path, string text)
        {
            Assert.IsTrue(Path.Combine("Resources", path).ExtractText().Contains(text));
        }


        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            var dir = Path.GetDirectoryName(typeof(Acceptance).Assembly.Location);
            Directory.SetCurrentDirectory(dir);
        }
    }
}
