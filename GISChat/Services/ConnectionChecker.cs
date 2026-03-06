using System.Net.Http;
using System.Text;
using System.Text.Json;
using GISChat.Models;

namespace GISChat.Services;

public enum ConnectionStatus
{
    Unknown,    // Not checked yet (grey)
    Connected,  // API reachable and key valid (green)
    Error       // Unreachable or auth failed (red)
}

/// <summary>
/// Lightweight health check — sends a minimal request to verify the API is reachable
/// and the key is valid, without consuming significant tokens.
/// </summary>
public static class ConnectionChecker
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static async Task<(ConnectionStatus Status, string Message)> CheckAsync()
    {
        var settings = AddinSettings.Instance;
        var provider = settings.Provider;

        if (LlmProviderInfo.RequiresApiKey(provider) && string.IsNullOrWhiteSpace(settings.ApiKey))
            return (ConnectionStatus.Error, "No API key configured");

        try
        {
            return provider switch
            {
                LlmProviderType.Anthropic => await CheckAnthropicAsync(settings),
                LlmProviderType.GoogleGemini => await CheckGeminiAsync(settings),
                LlmProviderType.Ollama => await CheckOllamaAsync(settings),
                _ => await CheckOpenAICompatibleAsync(settings)
            };
        }
        catch (TaskCanceledException)
        {
            return (ConnectionStatus.Error, "Connection timed out");
        }
        catch (HttpRequestException ex)
        {
            return (ConnectionStatus.Error, $"Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (ConnectionStatus.Error, ex.Message);
        }
    }

    private static async Task<(ConnectionStatus, string)> CheckAnthropicAsync(AddinSettings settings)
    {
        // Use the models endpoint — cheap, no tokens consumed
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
        request.Headers.Add("x-api-key", settings.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await Http.SendAsync(request);
        if (response.IsSuccessStatusCode)
            return (ConnectionStatus.Connected, $"Connected to Anthropic ({settings.Model})");

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return (ConnectionStatus.Error, "Invalid API key");

        return (ConnectionStatus.Error, $"API returned {(int)response.StatusCode}");
    }

    private static async Task<(ConnectionStatus, string)> CheckOpenAICompatibleAsync(AddinSettings settings)
    {
        var endpoint = settings.EffectiveEndpoint;
        var modelsUrl = endpoint.Replace("/chat/completions", "/models");

        var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");

        var response = await Http.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var name = settings.Provider == LlmProviderType.OpenAI ? "OpenAI" : "API";
            return (ConnectionStatus.Connected, $"Connected to {name} ({settings.Model})");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return (ConnectionStatus.Error, "Invalid API key");

        return (ConnectionStatus.Error, $"API returned {(int)response.StatusCode}");
    }

    private static async Task<(ConnectionStatus, string)> CheckGeminiAsync(AddinSettings settings)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={settings.ApiKey}&pageSize=1";
        var response = await Http.GetAsync(url);

        if (response.IsSuccessStatusCode)
            return (ConnectionStatus.Connected, $"Connected to Gemini ({settings.Model})");

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return (ConnectionStatus.Error, "Invalid API key");

        return (ConnectionStatus.Error, $"Gemini API returned {(int)response.StatusCode}");
    }

    private static async Task<(ConnectionStatus, string)> CheckOllamaAsync(AddinSettings settings)
    {
        var baseUrl = settings.EffectiveEndpoint.TrimEnd('/');
        var tagsUrl = baseUrl.Contains("/v1")
            ? baseUrl.Replace("/v1/chat/completions", "/api/tags")
            : baseUrl + "/api/tags";

        var response = await Http.GetAsync(tagsUrl);
        if (response.IsSuccessStatusCode)
            return (ConnectionStatus.Connected, $"Connected to Ollama ({settings.Model})");

        return (ConnectionStatus.Error, "Ollama not reachable — is it running?");
    }
}
