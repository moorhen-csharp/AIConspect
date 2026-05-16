using System.Text.Json.Serialization;

namespace AIConspect.Models;

public class StudentProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string AvatarInitials => Name.Length > 0 ? Name[0].ToString().ToUpper() : "?";

    [JsonIgnore]
    public string AvatarColor { get; set; } = "#6200EE";

    public string AvatarColorHex
    {
        get => AvatarColor;
        set => AvatarColor = value;
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<ConspectHistory> History { get; set; } = new();
}

public class ConspectHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public ConspectMode Mode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string OutputFilePath { get; set; } = string.Empty;
}

public enum ConspectMode
{
    Summary,      // Краткая выжимка
    Report,       // Отчёт
    LabWork       // Оформление лабораторной
}