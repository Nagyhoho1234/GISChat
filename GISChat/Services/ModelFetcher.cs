using System.Net.Http;
using System.Text.Json;
using GISChat.Models;

namespace GISChat.Services;

/// <summary>
/// Fetches available models dynamically from the selected AI provider's API.
/// </summary>
public static class ModelFetcher
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// Fetch available models from the provider. Returns model IDs.
    /// Falls back to default hardcoded list on failure.
    /// </summary>
    public static async Task<string[]> FetchModelsAsync(LlmProviderType provider, string apiKey, string? endpoint = null)
    {
        try
        {
            return provider switch
            {
                LlmProviderType.OpenAI => await FetchOpenAIModelsAsync(apiKey, endpoint),
                LlmProviderType.Ollama => await FetchOllamaModelsAsync(endpoint),
                LlmProviderType.OpenAICompatible => await FetchOpenAIModelsAsync(apiKey, endpoint),
                LlmProviderType.GoogleGemini => await FetchGeminiModelsAsync(apiKey),
                LlmProviderType.Anthropic => await FetchAnthropicModelsAsync(apiKey),
                _ => LlmProviderInfo.DefaultModels(provider)
            };
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to fetch models for {provider}: {ex.Message}");
            return LlmProviderInfo.DefaultModels(provider);
        }
    }

    private static async Task<string[]> FetchOpenAIModelsAsync(string apiKey, string? endpoint)
    {
        // OpenAI: GET /v1/models
        var baseUrl = endpoint ?? "https://api.openai.com/v1/chat/completions";
        var modelsUrl = baseUrl.Replace("/chat/completions", "/models");

        var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

        var response = await Http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Logger.Warn($"OpenAI models API returned {response.StatusCode}");
            return LlmProviderInfo.DefaultModels(LlmProviderType.OpenAI);
        }

        var doc = JsonDocument.Parse(json);
        var models = new List<string>();

        foreach (var model in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            var id = model.GetProperty("id").GetString();
            if (id != null)
                models.Add(id);
        }

        // Sort: prioritize known good models, then alphabetical
        var priority = new[] { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "o3-mini", "o1" };
        var sorted = models
            .OrderBy(m => {
                var idx = Array.FindIndex(priority, p => m.StartsWith(p));
                return idx >= 0 ? idx : 100;
            })
            .ThenBy(m => m)
            .ToArray();

        Logger.Info($"Fetched {sorted.Length} models from OpenAI-compatible API");
        return sorted.Length > 0 ? sorted : LlmProviderInfo.DefaultModels(LlmProviderType.OpenAI);
    }

    private static async Task<string[]> FetchOllamaModelsAsync(string? endpoint)
    {
        // Ollama: GET /api/tags (native) or /v1/models (OpenAI-compatible)
        var baseUrl = endpoint?.TrimEnd('/') ?? "http://localhost:11434";

        // Try native Ollama API first
        var tagsUrl = baseUrl.Contains("/v1")
            ? baseUrl.Replace("/v1/chat/completions", "/api/tags")
            : baseUrl + "/api/tags";

        try
        {
            var response = await Http.GetAsync(tagsUrl);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var doc = JsonDocument.Parse(json);
                var models = new List<string>();

                foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
                {
                    var name = model.GetProperty("name").GetString();
                    if (name != null)
                        models.Add(name);
                }

                Logger.Info($"Fetched {models.Count} models from Ollama");
                return models.Count > 0 ? models.ToArray() : LlmProviderInfo.DefaultModels(LlmProviderType.Ollama);
            }
        }
        catch (HttpRequestException)
        {
            Logger.Warn("Ollama not running or not reachable. Is it installed?");
        }

        return LlmProviderInfo.DefaultModels(LlmProviderType.Ollama);
    }

    private static async Task<string[]> FetchGeminiModelsAsync(string apiKey)
    {
        // Gemini: GET /v1beta/models?key=API_KEY
        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";

        var response = await Http.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Logger.Warn($"Gemini models API returned {response.StatusCode}");
            return LlmProviderInfo.DefaultModels(LlmProviderType.GoogleGemini);
        }

        var doc = JsonDocument.Parse(json);
        var models = new List<string>();

        foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
        {
            var name = model.GetProperty("name").GetString();
            if (name == null) continue;

            // Gemini returns "models/gemini-2.0-flash" — strip prefix
            name = name.Replace("models/", "");

            // Only include generateContent-capable models
            if (model.TryGetProperty("supportedGenerationMethods", out var methods))
            {
                var methodList = methods.EnumerateArray().Select(m => m.GetString()).ToList();
                if (methodList.Contains("generateContent"))
                    models.Add(name);
            }
        }

        // Prioritize known good models
        var priority = new[] { "gemini-2.0-flash", "gemini-2.0-pro", "gemini-1.5-flash", "gemini-1.5-pro" };
        var sorted = models
            .OrderBy(m => {
                var idx = Array.FindIndex(priority, p => m.StartsWith(p));
                return idx >= 0 ? idx : 100;
            })
            .ThenBy(m => m)
            .ToArray();

        Logger.Info($"Fetched {sorted.Length} models from Gemini API");
        return sorted.Length > 0 ? sorted : LlmProviderInfo.DefaultModels(LlmProviderType.GoogleGemini);
    }

    private static async Task<string[]> FetchAnthropicModelsAsync(string apiKey)
    {
        // Anthropic: GET /v1/models
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await Http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var doc = JsonDocument.Parse(json);
                var models = new List<string>();

                foreach (var model in doc.RootElement.GetProperty("data").EnumerateArray())
                {
                    var id = model.GetProperty("id").GetString();
                    if (id != null)
                        models.Add(id);
                }

                Logger.Info($"Fetched {models.Count} models from Anthropic API");
                if (models.Count > 0) return models.ToArray();
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Anthropic models fetch failed: {ex.Message}");
        }

        // Fallback to hardcoded
        return LlmProviderInfo.DefaultModels(LlmProviderType.Anthropic);
    }
}
