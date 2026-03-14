using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using UglyToad.PdfPig;

namespace BBF.Services;

public static class DocumentTextExtractor
{
    /// <summary>
    /// Extracts text content from a file based on its extension.
    /// Returns null if the file type is not supported for text extraction.
    /// </summary>
    public static string? ExtractText(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            return ext switch
            {
                // Plain text files — read directly
                ".txt" or ".md" or ".csv" or ".log" or ".json" or ".xml"
                    or ".yaml" or ".yml" or ".ini" or ".conf" or ".cfg"
                    or ".html" or ".css" or ".js"
                    => ReadTextFile(filePath),

                // PDF
                ".pdf" => ExtractPdfText(filePath),

                // Word documents
                ".docx" => ExtractDocxText(filePath),

                // Excel spreadsheets
                ".xlsx" => ExtractXlsxText(filePath),

                _ => null
            };
        }
        catch
        {
            // If extraction fails for any reason, return null gracefully
            return null;
        }
    }

    private static string? ReadTextFile(string filePath)
    {
        var text = File.ReadAllText(filePath);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? ExtractPdfText(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        var sb = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        var text = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? ExtractDocxText(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return null;

        var sb = new StringBuilder();
        foreach (var para in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            var text = para.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
                sb.AppendLine(text);
        }

        var result = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static string? ExtractXlsxText(string filePath)
    {
        using var doc = SpreadsheetDocument.Open(filePath, false);
        var workbookPart = doc.WorkbookPart;
        if (workbookPart is null) return null;

        var sb = new StringBuilder();
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

        foreach (var worksheetPart in workbookPart.WorksheetParts)
        {
            var sheetData = worksheetPart.Worksheet?.GetFirstChild<SheetData>();
            if (sheetData is null) continue;

            foreach (var row in sheetData.Elements<Row>())
            {
                var cells = new List<string>();
                foreach (var cell in row.Elements<Cell>())
                {
                    var value = GetCellValue(cell, sharedStrings);
                    if (!string.IsNullOrEmpty(value))
                        cells.Add(value);
                }
                if (cells.Count > 0)
                    sb.AppendLine(string.Join("\t", cells));
            }
        }

        var result = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static string GetCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        var value = cell.CellValue?.Text ?? string.Empty;

        if (cell.DataType?.Value == CellValues.SharedString && sharedStrings is not null)
        {
            if (int.TryParse(value, out var index))
                return sharedStrings.ElementAt(index).InnerText;
        }

        return value;
    }
}
