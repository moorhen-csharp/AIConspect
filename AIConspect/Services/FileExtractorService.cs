using DocumentFormat.OpenXml.Packaging;
using Markdig;
using System.IO;
using System.Text;

namespace AIConspect.Services;

public class FileExtractorService
{
    public static readonly string[] SupportedExtensions =
        { ".pdf", ".docx", ".xlsx", ".md", ".txt" };

    /// <summary>Извлекает текст из файла в зависимости от расширения.</summary>
    public async Task<string> ExtractTextAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".pdf" => await Task.Run(() => ExtractFromPdf(filePath)),
            ".docx" => await Task.Run(() => ExtractFromDocx(filePath)),
            ".xlsx" => await Task.Run(() => ExtractFromXlsx(filePath)),
            ".md" => await ExtractFromMarkdown(filePath),
            ".txt" => await File.ReadAllTextAsync(filePath),
            _ => throw new NotSupportedException($"Формат '{ext}' не поддерживается.")
        };
    }

    // ──────────────── PDF ────────────────
    private string ExtractFromPdf(string path)
    {
        // NOTE: спроектировано без внешней PDF-библиотеки в рамках текущего изменения.
        // В рабочем приложении подключите подходящую библиотеку (PdfPig / iText / Pdfium) и реализуйте извлечение текста.
        return string.Empty; // TODO: реализовать извлечение текста из PDF
    }

    // ──────────────── DOCX ────────────────
    private string ExtractFromDocx(string path)
    {
        var sb = new StringBuilder();

        using var doc = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return string.Empty;

        foreach (var para in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            sb.AppendLine(para.InnerText);
        }

        return sb.ToString().Trim();
    }

    // ──────────────── XLSX ────────────────
    private string ExtractFromXlsx(string path)
    {
        var sb = new StringBuilder();
        var workbook = new ClosedXML.Excel.XLWorkbook(path);

        foreach (var ws in workbook.Worksheets)
        {
            sb.AppendLine($"=== Лист: {ws.Name} ===");

            foreach (var row in ws.RowsUsed())
            {
                var cells = row.CellsUsed()
                    .Select(c => c.GetValue<string>())
                    .Where(v => !string.IsNullOrWhiteSpace(v));
                sb.AppendLine(string.Join("\t", cells));
            }

            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    // ──────────────── Markdown ────────────────
    private async Task<string> ExtractFromMarkdown(string path)
    {
        var md = await File.ReadAllTextAsync(path);
        // Конвертируем MD → plain text (убираем разметку)
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var html = Markdown.ToHtml(md, pipeline);

        // Простая очистка от HTML тегов
        var plain = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        plain = System.Text.RegularExpressions.Regex.Replace(plain, @"\s{2,}", " ");
        return plain.Trim();
    }

    /// <summary>Возвращает отображаемое имя расширения для UI.</summary>
    public static string GetFormatDisplayName(string extension) => extension.ToLower() switch
    {
        ".pdf" => "PDF",
        ".docx" => "Word",
        ".xlsx" => "Excel",
        ".md" => "Markdown",
        ".txt" => "Текстовый файл",
        _ => extension.TrimStart('.').ToUpper()
    };
}