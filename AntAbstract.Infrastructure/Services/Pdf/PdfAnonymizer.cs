using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AntAbstract.Infrastructure.Services.Pdf
{
    public sealed class PdfAnonymizer : IPdfAnonymizer
    {
        private static readonly Regex InfoValuePattern = new(
            @"(/(?:Author|Creator|Title|Subject|Keywords|Producer|Company|Manager))\s*\(([^)]*)\)",
            RegexOptions.Compiled);

        public byte[] AnonymizeMetadata(byte[] pdfBytes, string submissionCode)
        {
            var text = Encoding.Latin1.GetString(pdfBytes);

            var replacements = new Dictionary<string, string>
            {
                { "/Author", "" },
                { "/Creator", "AntAbstract" },
                { "/Title", submissionCode },
                { "/Subject", "" },
                { "/Keywords", "" },
                { "/Producer", "AntAbstract" },
                { "/Company", "" },
                { "/Manager", "" }
            };

            var result = InfoValuePattern.Replace(text, match =>
            {
                var key = match.Groups[1].Value;
                if (replacements.TryGetValue(key, out var replacement))
                    return $"{key} ({replacement})";
                return match.Value;
            });

            return Encoding.Latin1.GetBytes(result);
        }
    }
}
