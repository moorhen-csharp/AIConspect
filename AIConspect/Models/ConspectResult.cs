namespace AIConspect.Models;

public class ConspectResult
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = string.Empty;
    public ConspectMode Mode { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    // Структурированные секции для Word-документа
    public string Title { get; set; } = string.Empty;
    public List<ConspectSection> Sections { get; set; } = new();
}

public class ConspectSection
{
    public string Heading { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int Level { get; set; } = 1; // 1 = H1, 2 = H2
}