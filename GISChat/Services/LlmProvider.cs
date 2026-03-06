namespace GISChat.Services;

public enum LlmProviderType
{
    Anthropic,      // Claude API
    OpenAI,         // OpenAI GPT-4o etc.
    GoogleGemini,   // Google Gemini (generous free tier)
    Ollama,         // Local, completely free, no API key
    OpenAICompatible // Azure OpenAI, LM Studio, vLLM, text-generation-webui, etc.
}

public static class LlmProviderInfo
{
    public static string DisplayName(LlmProviderType type) => type switch
    {
        LlmProviderType.Anthropic => "Anthropic (Claude)",
        LlmProviderType.OpenAI => "OpenAI (GPT)",
        LlmProviderType.GoogleGemini => "Google Gemini (free tier available)",
        LlmProviderType.Ollama => "Ollama (local, free)",
        LlmProviderType.OpenAICompatible => "OpenAI-compatible endpoint",
        _ => type.ToString()
    };

    public static string[] DefaultModels(LlmProviderType type) => type switch
    {
        LlmProviderType.Anthropic => ["claude-sonnet-4-6", "claude-haiku-4-5-20251001", "claude-opus-4-6"],
        LlmProviderType.OpenAI => ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "o3-mini"],
        LlmProviderType.GoogleGemini => ["gemini-2.0-flash", "gemini-2.0-pro", "gemini-1.5-flash"],
        LlmProviderType.Ollama => ["llama3.1", "mistral", "codellama", "deepseek-coder-v2"],
        LlmProviderType.OpenAICompatible => ["default"],
        _ => ["default"]
    };

    public static string DefaultEndpoint(LlmProviderType type) => type switch
    {
        LlmProviderType.Anthropic => "https://api.anthropic.com/v1/messages",
        LlmProviderType.OpenAI => "https://api.openai.com/v1/chat/completions",
        LlmProviderType.GoogleGemini => "https://generativelanguage.googleapis.com/v1beta",
        LlmProviderType.Ollama => "http://localhost:11434/v1/chat/completions",
        LlmProviderType.OpenAICompatible => "http://localhost:8080/v1/chat/completions",
        _ => ""
    };

    public static bool RequiresApiKey(LlmProviderType type) => type switch
    {
        LlmProviderType.Ollama => false,
        _ => true
    };

    public static string ApiKeyHelp(LlmProviderType type) => type switch
    {
        LlmProviderType.Anthropic => "Get your key at console.anthropic.com/settings/keys",
        LlmProviderType.OpenAI => "Get your key at platform.openai.com/api-keys",
        LlmProviderType.GoogleGemini => "Get your key at aistudio.google.com/apikey (free!)",
        LlmProviderType.Ollama => "No API key needed - runs locally. Install from ollama.com",
        LlmProviderType.OpenAICompatible => "Enter the API key for your endpoint (if required)",
        _ => ""
    };
}
