using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GISChat.Services;

namespace GISChat.Models;

public class AddinSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GISChat", "settings.json");

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LlmProviderType Provider { get; set; } = LlmProviderType.Anthropic;

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-sonnet-4-6";
    public string Endpoint { get; set; } = "";
    public int MaxTokens { get; set; } = 4096;
    public bool ConfirmBeforeExecute { get; set; } = true;
    public bool ShowGeneratedCode { get; set; } = true;

    /// <summary>
    /// Returns the effective endpoint URL, falling back to the provider default.
    /// </summary>
    [JsonIgnore]
    public string EffectiveEndpoint =>
        string.IsNullOrWhiteSpace(Endpoint) ? LlmProviderInfo.DefaultEndpoint(Provider) : Endpoint;

    // Keep backward compat: old "AnthropicApiKey" maps to ApiKey
    public string AnthropicApiKey
    {
        get => Provider == LlmProviderType.Anthropic ? ApiKey : "";
        set { if (!string.IsNullOrEmpty(value) && string.IsNullOrEmpty(ApiKey)) { ApiKey = value; } }
    }

    private static AddinSettings? _instance;
    public static AddinSettings Instance => _instance ??= Load();

    public static AddinSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _instance = JsonSerializer.Deserialize<AddinSettings>(json) ?? new AddinSettings();
                return _instance;
            }
        }
        catch { }

        _instance = new AddinSettings();
        return _instance;
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        });
        File.WriteAllText(SettingsPath, json);
    }
}
