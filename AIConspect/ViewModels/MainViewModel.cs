using AIConspect.Models;
using AIConspect.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows.Input;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AIConspect.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AnthropicService _ai = new();
    private readonly FileExtractorService _extractor = new();
    private readonly WordExportService _wordExport = new();
    private readonly ProfileService _profileService = new();

    // ── Состояние профилей ──
    [ObservableProperty] private ObservableCollection<StudentProfile> profiles = new();
    [ObservableProperty] private StudentProfile? currentProfile;
    [ObservableProperty] private bool isProfileSelectionVisible = true;
    [ObservableProperty] private bool isMainViewVisible = false;
    [ObservableProperty] private string newProfileName = string.Empty;

    // ── Состояние ввода ──
    [ObservableProperty] private string? loadedFilePath;
    [ObservableProperty] private string? loadedFileName;
    [ObservableProperty] private string manualText = string.Empty;
    [ObservableProperty] private string inputMode = "file"; // "file" | "text"
    [ObservableProperty] private ConspectMode selectedMode = ConspectMode.Summary;

    // ── Состояние обработки ──
    [ObservableProperty] private bool isProcessing = false;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool hasResult = false;
    [ObservableProperty] private ConspectResult? lastResult;

    // ── Уведомления ──
    [ObservableProperty] private string? notificationMessage;
    [ObservableProperty] private bool isNotificationVisible = false;
    [ObservableProperty] private bool isError = false;

    public MainViewModel()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var loaded = await _profileService.LoadProfilesAsync();
        foreach (var p in loaded)
            Profiles.Add(p);
    }

    // ──────────────── Профили ────────────────

    [RelayCommand]
    private async Task CreateProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName)) return;

        var profile = await _profileService.CreateProfileAsync(NewProfileName.Trim());
        Profiles.Add(profile);
        NewProfileName = string.Empty;
        SelectProfile(profile);
    }

    [RelayCommand]
    private void SelectProfile(StudentProfile profile)
    {
        CurrentProfile = profile;
        IsProfileSelectionVisible = false;
        IsMainViewVisible = true;
        ResetInput();
    }

    [RelayCommand]
    private void SwitchProfile()
    {
        IsProfileSelectionVisible = true;
        IsMainViewVisible = false;
        CurrentProfile = null;
        ResetInput();
    }

    [RelayCommand]
    private async Task DeleteProfileAsync(StudentProfile profile)
    {
        await _profileService.DeleteProfileAsync(profile.Id);
        Profiles.Remove(profile);
    }

    // ──────────────── Работа с файлом ────────────────

    [RelayCommand]
    private void LoadFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Выберите файл лекции",
            Filter = "Поддерживаемые форматы|*.pdf;*.docx;*.xlsx;*.md;*.txt|" +
                     "PDF|*.pdf|Word|*.docx|Excel|*.xlsx|Markdown|*.md|Текст|*.txt"
        };

        if (dialog.ShowDialog() != true) return;

        LoadedFilePath = dialog.FileName;
        LoadedFileName = Path.GetFileName(dialog.FileName);
        InputMode = "file";
        HasResult = false;
        LastResult = null;
    }

    [RelayCommand]
    private void ClearFile()
    {
        LoadedFilePath = null;
        LoadedFileName = null;
    }

    // ──────────────── Генерация конспекта ────────────────

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (CurrentProfile == null) return;

        // Валидация
        if (InputMode == "file" && string.IsNullOrEmpty(LoadedFilePath))
        {
            ShowNotification("Загрузите файл или введите текст вручную", isError: true);
            return;
        }
        if (InputMode == "text" && string.IsNullOrWhiteSpace(ManualText))
        {
            ShowNotification("Введите текст для обработки", isError: true);
            return;
        }

        IsProcessing = true;
        HasResult = false;
        LastResult = null;

        try
        {
            // 1. Извлекаем текст
            string sourceText;
            string sourceFileName;

            if (InputMode == "file")
            {
                StatusMessage = "Извлечение текста из файла...";
                sourceText = await _extractor.ExtractTextAsync(LoadedFilePath!);
                sourceFileName = LoadedFileName!;
            }
            else
            {
                sourceText = ManualText;
                sourceFileName = "Ручной ввод";
            }

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                ShowNotification("Не удалось извлечь текст из файла", isError: true);
                return;
            }

            // 2. Отправляем в Claude
            var progress = new Progress<string>(msg => StatusMessage = msg);
            var rawJson = await _ai.GenerateConspectAsync(sourceText, SelectedMode, progress);

            // 3. Парсим ответ
            StatusMessage = "Формирование документа Word...";
            var result = ParseConspectJson(rawJson, sourceFileName);

            // 4. Экспортируем в Word
            var outputPath = _wordExport.Export(result, CurrentProfile.Name);
            result.OutputFilePath = outputPath;

            LastResult = result;
            HasResult = true;

            // 5. Сохраняем в историю
            await _profileService.AddHistoryEntryAsync(CurrentProfile.Id, new ConspectHistory
            {
                Title = result.Title,
                SourceFileName = sourceFileName,
                Mode = SelectedMode,
                OutputFilePath = outputPath
            });

            // Обновляем историю в профиле (для UI)
            var updatedProfiles = await _profileService.LoadProfilesAsync();
            var updated = updatedProfiles.FirstOrDefault(p => p.Id == CurrentProfile.Id);
            if (updated != null) CurrentProfile = updated;

            StatusMessage = string.Empty;
            ShowNotification($"Готово! Файл сохранён: {Path.GetFileName(outputPath)}");
        }
        catch (Exception ex)
        {
            StatusMessage = string.Empty;
            ShowNotification($"Ошибка: {ex.Message}", isError: true);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void OpenResult()
    {
        if (LastResult?.OutputFilePath == null) return;
        if (File.Exists(LastResult.OutputFilePath))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = LastResult.OutputFilePath,
                UseShellExecute = true
            });
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (LastResult?.OutputFilePath == null) return;
        var dir = Path.GetDirectoryName(LastResult.OutputFilePath);
        if (dir != null && Directory.Exists(dir))
            System.Diagnostics.Process.Start("explorer.exe", dir);
    }

    [RelayCommand]
    private void OpenHistoryFile(ConspectHistory history)
    {
        if (!File.Exists(history.OutputFilePath))
        {
            ShowNotification("Файл не найден", isError: true);
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = history.OutputFilePath,
            UseShellExecute = true
        });
    }

    // ──────────────── Helpers ────────────────

    private ConspectResult ParseConspectJson(string json, string sourceFileName)
    {
        try
        {
            // Убираем возможные markdown-блоки
            var clean = json.Trim();
            if (clean.StartsWith("```")) clean = clean.Split('\n').Skip(1).Aggregate((a, b) => a + "\n" + b);
            if (clean.EndsWith("```")) clean = clean[..clean.LastIndexOf("```")];
            clean = clean.Trim();

            var doc = JsonSerializer.Deserialize<JsonDocument>(clean)!;
            var root = doc.RootElement;

            var result = new ConspectResult
            {
                IsSuccess = true,
                Mode = SelectedMode,
                SourceFileName = sourceFileName,
                Title = root.GetProperty("title").GetString() ?? "Конспект"
            };

            if (root.TryGetProperty("sections", out var sections))
            {
                foreach (var section in sections.EnumerateArray())
                {
                    result.Sections.Add(new ConspectSection
                    {
                        Heading = section.GetProperty("heading").GetString() ?? "",
                        Body = section.GetProperty("body").GetString() ?? "",
                        Level = section.TryGetProperty("level", out var lvl) ? lvl.GetInt32() : 1
                    });
                }
            }

            return result;
        }
        catch
        {
            // Fallback: если JSON не распарсился — создаём один раздел с сырым текстом
            return new ConspectResult
            {
                IsSuccess = true,
                Mode = SelectedMode,
                SourceFileName = sourceFileName,
                Title = "Конспект",
                Sections = new List<ConspectSection>
                {
                    new() { Heading = "Содержание", Body = json, Level = 1 }
                }
            };
        }
    }

    private void ResetInput()
    {
        LoadedFilePath = null;
        LoadedFileName = null;
        ManualText = string.Empty;
        HasResult = false;
        LastResult = null;
        StatusMessage = string.Empty;
        InputMode = "file";
    }

    private async void ShowNotification(string message, bool isError = false)
    {
        NotificationMessage = message;
        IsError = isError;
        IsNotificationVisible = true;

        await Task.Delay(4000);
        IsNotificationVisible = false;
    }

    // Свойства для биндинга режима
    // Свойства для биндинга режима
    public bool IsSummaryMode
    {
        get => SelectedMode == ConspectMode.Summary;
        set { if (value) { SelectedMode = ConspectMode.Summary; OnPropertyChanged(nameof(IsSummaryMode)); OnPropertyChanged(nameof(IsReportMode)); OnPropertyChanged(nameof(IsLabMode)); } }
    }
    public bool IsReportMode
    {
        get => SelectedMode == ConspectMode.Report;
        set { if (value) { SelectedMode = ConspectMode.Report; OnPropertyChanged(nameof(IsSummaryMode)); OnPropertyChanged(nameof(IsReportMode)); OnPropertyChanged(nameof(IsLabMode)); } }
    }
    public bool IsLabMode
    {
        get => SelectedMode == ConspectMode.LabWork;
        set { if (value) { SelectedMode = ConspectMode.LabWork; OnPropertyChanged(nameof(IsSummaryMode)); OnPropertyChanged(nameof(IsReportMode)); OnPropertyChanged(nameof(IsLabMode)); } }
    }
    public bool IsFileInput
    {
        get => InputMode == "file";
        set { if (value) { InputMode = "file"; OnPropertyChanged(nameof(IsFileInput)); OnPropertyChanged(nameof(IsTextInput)); } }
    }
    public bool IsTextInput
    {
        get => InputMode == "text";
        set { if (value) { InputMode = "text"; OnPropertyChanged(nameof(IsFileInput)); OnPropertyChanged(nameof(IsTextInput)); } }
    }
}