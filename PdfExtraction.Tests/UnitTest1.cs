using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VuongIdeas.PdfExtraction;

namespace PdfExtraction.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            Assert.IsTrue("Resources\\Pdf-sample.pdf".ExtractText().Contains("Adobe Acrobat PDF Files"));
        }

        [TestMethod]
        public void TestMethod2()
        {
            Assert.IsTrue("Resources\\pdf.pdf".ExtractText().Contains(""));
        }
    }
}
