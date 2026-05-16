using AIConspect.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AIConspect.Services;

public class AnthropicService
{
    // 🔑 API-ключ (захардкожен для этапа разработки)
    private const string ApiKey = "YOUR_ANTHROPIC_API_KEY_HERE";
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-sonnet-4-20250514";

    private readonly HttpClient _http;

    public AnthropicService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("x-api-key", ApiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<string> GenerateConspectAsync(
        string sourceText,
        ConspectMode mode,
        IProgress<string>? progress = null)
    {
        var systemPrompt = GetSystemPrompt(mode);
        var userPrompt = BuildUserPrompt(sourceText, mode);

        progress?.Report("Отправка запроса к Claude...");

        var requestBody = new
        {
            model = Model,
            max_tokens = 4096,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(ApiUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Ошибка API Anthropic ({response.StatusCode}): {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>();
        progress?.Report("Ответ получен, формирование документа...");

        return result?.Content?.FirstOrDefault()?.Text ?? throw new Exception("Пустой ответ от API");
    }

    private static string GetSystemPrompt(ConspectMode mode) => mode switch
    {
        ConspectMode.Summary => """
            Ты — помощник для студентов. Твоя задача: создать структурированный конспект лекции.
            Формат ответа — строго JSON:
            {
              "title": "Название темы",
              "sections": [
                { "heading": "Заголовок раздела", "body": "Текст раздела", "level": 1 },
                { "heading": "Подраздел", "body": "Текст", "level": 2 }
              ]
            }
            Выдели ключевые понятия, определения, формулы. Пиши по-русски, кратко и чётко.
            ВАЖНО: отвечай ТОЛЬКО валидным JSON без markdown-блоков и пояснений.
            """,

        ConspectMode.Report => """
            Ты — помощник для оформления отчётов. На основе предоставленного материала создай академический отчёт.
            Формат ответа — строго JSON:
            {
              "title": "Название отчёта",
              "sections": [
                { "heading": "Введение", "body": "Текст введения", "level": 1 },
                { "heading": "Основная часть", "body": "Текст", "level": 1 },
                { "heading": "Заключение", "body": "Текст заключения", "level": 1 }
              ]
            }
            Используй академический стиль, структуру: введение, основная часть, заключение.
            ВАЖНО: отвечай ТОЛЬКО валидным JSON без markdown-блоков и пояснений.
            """,

        ConspectMode.LabWork => """
            Ты — помощник для оформления лабораторных работ. Преобразуй материал в оформленную лабораторную работу.
            Формат ответа — строго JSON:
            {
              "title": "Название лабораторной работы",
              "sections": [
                { "heading": "Цель работы", "body": "Текст цели", "level": 1 },
                { "heading": "Теоретическая часть", "body": "Текст", "level": 1 },
                { "heading": "Ход работы", "body": "Описание шагов", "level": 1 },
                { "heading": "Результаты", "body": "Результаты работы", "level": 1 },
                { "heading": "Вывод", "body": "Текст вывода", "level": 1 }
              ]
            }
            ВАЖНО: отвечай ТОЛЬКО валидным JSON без markdown-блоков и пояснений.
            """,

        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static string BuildUserPrompt(string text, ConspectMode mode)
    {
        var action = mode switch
        {
            ConspectMode.Summary => "сделай краткий структурированный конспект",
            ConspectMode.Report => "оформи как академический отчёт",
            ConspectMode.LabWork => "оформи как лабораторную работу",
            _ => "обработай"
        };

        // Ограничиваем текст до ~12 000 слов чтобы не превысить контекст
        var trimmed = text.Length > 40000 ? text[..40000] + "\n\n[текст обрезан]" : text;
        return $"Вот текст материала. Пожалуйста, {action}:\n\n{trimmed}";
    }

    // DTO для десериализации ответа Anthropic
    private record AnthropicResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("content")]
        List<ContentBlock>? Content
    );

    private record ContentBlock(
        [property: System.Text.Json.Serialization.JsonPropertyName("text")]
        string? Text
    );
}