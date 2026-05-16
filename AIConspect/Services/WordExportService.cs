using AIConspect.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO;

namespace AIConspect.Services;

public class WordExportService
{
    /// <summary>
    /// Генерирует .docx файл из результата конспекта и возвращает путь к файлу.
    /// </summary>
    public string Export(ConspectResult result, string studentName)
    {
        var outputDir = GetOutputDirectory(studentName);
        Directory.CreateDirectory(outputDir);

        var safeTitle = SanitizeFileName(result.Title);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        var fileName = $"{safeTitle}_{timestamp}.docx";
        var fullPath = Path.Combine(outputDir, fileName);

        using var doc = WordprocessingDocument.Create(fullPath, WordprocessingDocumentType.Document);

        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        // Добавляем стили
        AddStyleDefinitions(mainPart);

        // Заголовок документа
        body.AppendChild(CreateHeading(result.Title, 1, isDocumentTitle: true));

        // Мета-информация
        body.AppendChild(CreateMetaParagraph(
            $"Режим: {GetModeDisplayName(result.Mode)} | " +
            $"Источник: {result.SourceFileName} | " +
            $"Сформировано: {result.GeneratedAt:dd.MM.yyyy HH:mm}"
        ));

        // Горизонтальный разделитель
        body.AppendChild(CreateSeparatorParagraph());

        // Секции
        foreach (var section in result.Sections)
        {
            body.AppendChild(CreateHeading(section.Heading, section.Level + 1));

            foreach (var paragraph in section.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!string.IsNullOrWhiteSpace(paragraph))
                    body.AppendChild(CreateBodyParagraph(paragraph.Trim()));
            }
        }

        // Нижний колонтитул
        body.AppendChild(CreateFooterParagraph($"Документ сформирован приложением AI-Конспект | {studentName}"));

        mainPart.Document.Save();

        return fullPath;
    }

    // ──────────────── Helpers ────────────────

    private static DocumentFormat.OpenXml.Wordprocessing.Paragraph CreateHeading(string text, int level, bool isDocumentTitle = false)
    {
        var para = new Paragraph();
        var props = new ParagraphProperties();

        var styleId = isDocumentTitle ? "Title" : $"Heading{Math.Min(level, 3)}";
        props.AppendChild(new ParagraphStyleId { Val = styleId });

        if (isDocumentTitle)
            props.AppendChild(new Justification { Val = JustificationValues.Center });

        para.AppendChild(props);
        para.AppendChild(new Run(new Text(text)));
        return para;
    }

    private static DocumentFormat.OpenXml.Wordprocessing.Paragraph CreateBodyParagraph(string text)
    {
        var para = new Paragraph();
        var props = new ParagraphProperties();
        props.AppendChild(new SpacingBetweenLines
        {
            After = "120",
            Line = "276",
            LineRule = LineSpacingRuleValues.Auto
        });
        props.AppendChild(new Justification { Val = JustificationValues.Both });
        para.AppendChild(props);

        var run = new Run();
        var runProps = new RunProperties();
        runProps.AppendChild(new FontSize { Val = "24" }); // 12pt
        run.AppendChild(runProps);
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        para.AppendChild(run);
        return para;
    }

    private static DocumentFormat.OpenXml.Wordprocessing.Paragraph CreateMetaParagraph(string text)
    {
        var para = new Paragraph();
        var props = new ParagraphProperties();
        props.AppendChild(new Justification { Val = JustificationValues.Center });
        para.AppendChild(props);

        var run = new Run();
        var runProps = new RunProperties();
        runProps.AppendChild(new FontSize { Val = "18" }); // 9pt
        runProps.AppendChild(new Color { Val = "888888" });
        runProps.AppendChild(new Italic());
        run.AppendChild(runProps);
        run.AppendChild(new Text(text));
        para.AppendChild(run);
        return para;
    }

    private static DocumentFormat.OpenXml.Wordprocessing.Paragraph CreateSeparatorParagraph()
    {
        var para = new Paragraph();
        var props = new ParagraphProperties();
        var pBorder = new ParagraphBorders();
        pBorder.AppendChild(new BottomBorder
        {
            Val = BorderValues.Single,
            Size = 6,
            Color = "6200EE"
        });
        props.AppendChild(pBorder);
        props.AppendChild(new SpacingBetweenLines { After = "240" });
        para.AppendChild(props);
        return para;
    }

    private static DocumentFormat.OpenXml.Wordprocessing.Paragraph CreateFooterParagraph(string text)
    {
        var para = new Paragraph();
        var props = new ParagraphProperties();
        props.AppendChild(new Justification { Val = JustificationValues.Center });
        var pBorder = new ParagraphBorders();
        pBorder.AppendChild(new TopBorder
        {
            Val = BorderValues.Single,
            Size = 4,
            Color = "CCCCCC"
        });
        props.AppendChild(pBorder);
        props.AppendChild(new SpacingBetweenLines { Before = "240" });
        para.AppendChild(props);

        var run = new Run();
        var runProps = new RunProperties();
        runProps.AppendChild(new FontSize { Val = "16" });
        runProps.AppendChild(new Color { Val = "AAAAAA" });
        run.AppendChild(runProps);
        run.AppendChild(new Text(text));
        para.AppendChild(run);
        return para;
    }

    private static void AddStyleDefinitions(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        styles.AppendChild(CreateStyle("Title", "Title", 36, "1F1F1F", bold: true));
        styles.AppendChild(CreateStyle("Heading1", "Heading1", 28, "6200EE", bold: true));
        styles.AppendChild(CreateStyle("Heading2", "Heading2", 24, "3700B3", bold: true));
        styles.AppendChild(CreateStyle("Heading3", "Heading3", 22, "455A64", bold: false));

        stylesPart.Styles = styles;
        stylesPart.Styles.Save();
    }

    private static DocumentFormat.OpenXml.Wordprocessing.Style CreateStyle(string styleId, string styleName, int fontSize, string color, bool bold)
    {
        var style = new Style { Type = StyleValues.Paragraph, StyleId = styleId };
        style.AppendChild(new StyleName { Val = styleName });

        var runProps = new StyleRunProperties();
        runProps.AppendChild(new FontSize { Val = fontSize.ToString() });
        runProps.AppendChild(new Color { Val = color });
        if (bold) runProps.AppendChild(new Bold());

        style.AppendChild(runProps);
        return style;
    }

    private static string GetOutputDirectory(string studentName) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AI-Конспект",
            SanitizeFileName(studentName)
        );

    private static string SanitizeFileName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars()))
              .Replace(" ", "_")
              .TrimStart('_');

    private static string GetModeDisplayName(ConspectMode mode) => mode switch
    {
        ConspectMode.Summary => "Конспект",
        ConspectMode.Report => "Отчёт",
        ConspectMode.LabWork => "Лабораторная работа",
        _ => "Документ"
    };
}