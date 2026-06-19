using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using System;
using System.IO;

namespace AntAbstract.Infrastructure.Services.Pdf
{
    public sealed class PdfAnonymizer : IPdfAnonymizer
    {
        public byte[] AnonymizeMetadata(byte[] pdfBytes, string submissionCode)
        {
            using var inputStream = new MemoryStream(pdfBytes);
            using var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

            document.Info.Author = "";
            document.Info.Creator = "AntAbstract";
            document.Info.Title = submissionCode;
            document.Info.Subject = "";
            document.Info.Keywords = "";

            var keysToRemove = new[] { "/Producer", "/Company", "/Manager" };
            foreach (var key in keysToRemove)
            {
                if (document.Info.Elements.ContainsKey(key))
                    document.Info.Elements.Remove(key);
            }

            using var outputStream = new MemoryStream();
            document.Save(outputStream, false);
            return outputStream.ToArray();
        }
    }
}
