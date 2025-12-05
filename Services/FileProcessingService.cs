using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.IO;

namespace BetterNotes.Services
{
    public class FileProcessingService
    {
        public MemoryStream CreateWordDocument(string content, string originalFileName)
        {
            var memoryStream = new MemoryStream();

            using (var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document, true))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                // Add title
                var titleParagraph = body.AppendChild(new Paragraph());
                var titleRun = titleParagraph.AppendChild(new Run());
                var titleRunProperties = titleRun.AppendChild(new RunProperties());
                titleRunProperties.AppendChild(new Bold());
                titleRunProperties.AppendChild(new FontSize() { Val = "32" });
                titleRun.AppendChild(new Text($"Analysis Results - {originalFileName}"));

                // Add timestamp
                var timestampParagraph = body.AppendChild(new Paragraph());
                var timestampRun = timestampParagraph.AppendChild(new Run());
                timestampRun.AppendChild(new Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"));

                // Add blank line
                body.AppendChild(new Paragraph());

                // Add content - split by lines and preserve structure
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    var paragraph = body.AppendChild(new Paragraph());
                    var run = paragraph.AppendChild(new Run());
                    run.AppendChild(new Text(line));
                }
            }

            memoryStream.Position = 0;
            return memoryStream;
        }
    }
}