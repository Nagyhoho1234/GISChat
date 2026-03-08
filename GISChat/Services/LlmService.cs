using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GISChat.Models;

namespace GISChat.Services;

/// <summary>
/// Unified LLM service that routes to the correct provider API.
/// </summary>
public class LlmService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    private const string BaseSystemPrompt = """
        You are a GIS assistant embedded in ArcGIS Pro. You help users perform geospatial tasks
        using natural language. You have access to the current map state (layers, extent, spatial reference).

        When the user asks you to perform a GIS operation, call the run_arcpy function/tool to generate
        and execute Python/ArcPy code.

        Guidelines for generated ArcPy code:
        - The current project: aprx = arcpy.mp.ArcGISProject('CURRENT')
        - The active map: m = aprx.activeMap
        - Always use full tool paths: arcpy.analysis.Buffer, arcpy.management.AddField, etc.
        - For layer references, use the layer name as shown in the map context
        - Store results in the project's default geodatabase: arcpy.env.workspace = aprx.defaultGeodatabase
        - Use descriptive output names
        - Print results/counts so the user gets feedback
        - Handle errors with try/except and print useful messages
        - Always end with a print() statement summarizing what was done
        - IMPORTANT: Always work within the current ArcGIS Pro project. Never launch a new ArcGIS Pro instance
          or create a new project. Use aprx.activeMap or create new maps within the current project.
        - CRITICAL: In ArcGIS Pro, sys.executable points to ArcGISPro.exe, NOT python.exe.
          NEVER use subprocess.run([sys.executable, ...]) or subprocess.Popen with sys.executable — it launches
          a new ArcGIS Pro instance. If you need to pip install a package, use in-process pip:
          from pip._internal.cli.main import main as _pip; _pip(['install', 'package_name'])

        If the user asks a question that doesn't require code execution, just answer with text.
        If you're unsure which layer the user means, ask for clarification.
        If a task seems destructive (deleting data), warn the user and ask for confirmation.

        IMPORTANT — Error recovery:
        When a tool execution returns an error, DO NOT just report the error to the user.
        Instead, automatically try an alternative approach. For example:
        - If adding a Living Atlas layer fails, try adding it via a direct service URL instead
        - If a geoprocessing tool fails, try an alternative tool or workaround
        - If a layer name is not found, list available layers and pick the closest match
        - If a projection fails, try a different transformation or coordinate system
        Only report failure to the user after you have exhausted reasonable alternatives (at least 2-3 attempts).
        """;

    private static string BuildSystemPrompt()
    {
        var sb = new StringBuilder(BaseSystemPrompt);
        var geeProject = Models.AddinSettings.Instance.GeeProject;
        if (!string.IsNullOrWhiteSpace(geeProject))
        {
            sb.AppendLine();
            sb.AppendLine($"""
                Google Earth Engine (GEE) Integration:
                - The user has GEE configured with project: '{geeProject}'
                - To use GEE in code:
                  1. import ee
                  2. ee.Initialize(project='{geeProject}')
                - OAuth credentials are cached on the system (~/.config/earthengine/)
                - If ee.Initialize() fails with auth errors, tell the user to run ee.Authenticate()
                  in ArcGIS Pro's Python window first (opens browser for Google sign-in, one-time only)

                CRITICAL GEE DOWNLOAD RULES -- follow this EXACT pattern every time:
                1. NEVER call getDownloadURL without handling the 50 MB limit. NEVER degrade resolution.
                2. Use native scale: S1 GRD=10, S2 B2/B3/B4/B8=10m, B5-B8A/B11/B12=20m, B1/B9/B10=60m.
                3. ALWAYS use this adaptive tiled download with automatic retry:
                """);
            // Python code template — use plain string (no interpolation) to avoid C# brace conflicts
            sb.AppendLine(@"
```python
import ee, os, math, urllib.request
from osgeo import gdal
gdal.UseExceptions()
gdal.SetConfigOption('CPL_LOG', 'NUL')  # suppress noisy TIFF warnings
ee.Initialize(project='" + geeProject + @"')
# ... build image ...
SCALE = 10  # native resolution
region = [lon_min, lat_min, lon_max, lat_max]
out_dir = os.path.join(os.path.expanduser('~'), 'Documents', 'ProjectName')
os.makedirs(out_dir, exist_ok=True)

def download_tiled(image, region, scale, out_dir, grid=1):
    lon_min, lat_min, lon_max, lat_max = region
    lat_step = (lat_max - lat_min) / grid
    lon_step = (lon_max - lon_min) / grid
    tile_paths = []
    for r in range(grid):
        for c in range(grid):
            tile_region = ee.Geometry.Rectangle([
                lon_min + c*lon_step, lat_min + r*lat_step,
                lon_min + (c+1)*lon_step, lat_min + (r+1)*lat_step])
            tile_path = os.path.join(out_dir, f'tile_{r}_{c}.tif')
            try:
                url = image.getDownloadURL({'scale': scale, 'region': tile_region,
                      'format': 'GEO_TIFF', 'filePerBand': False})
                urllib.request.urlretrieve(url, tile_path)
                tile_paths.append(tile_path)
                sz = os.path.getsize(tile_path) / 1e6
                print(f'[OK] tile_{r}_{c}.tif ({sz:.1f} MB)')
            except Exception as e:
                err = str(e)
                if 'must be less than or equal to' in err or '400' in err or 'Bad Request' in err:
                    print(f'[RETRY] Grid {grid}x{grid} too coarse -> {grid*2}x{grid*2}')
                    for p in tile_paths:
                        if os.path.exists(p): os.remove(p)
                    return download_tiled(image, region, scale, out_dir, grid*2)
                raise
    return tile_paths

tile_paths = download_tiled(image, region, SCALE, out_dir)
merged = os.path.join(out_dir, 'result.tif')
gdal.Warp(merged, tile_paths)  # just merge, no extra options
for p in tile_paths: os.remove(p)
print(f'[DONE] {merged}')
# Add to ArcGIS Pro map:
m = arcpy.mp.ArcGISProject('CURRENT').activeMap
m.addDataFromPath(merged)
```

4. After downloading, the merged GeoTIFF is added to the active ArcGIS Pro map automatically.

IMPORTANT GDAL rules:
- Always call gdal.UseExceptions() and gdal.SetConfigOption('CPL_LOG', 'NUL') at the top.
- gdal.Warp(dest, srcs) -- do NOT pass options=['...'] as a list. Use keyword args if needed: gdal.Warp(dest, srcs, creationOptions=['COMPRESS=LZW']).

Other GEE rules:
- Server-side operations (mosaic, clip, compositing) have no size limit
- GEE List.get() only works for index 0-99. For collections >100 images, use .limit(N) or .aggregate_array() + .getInfo()
- Sentinel-1 system:index is the full product name (e.g. S1A_IW_GRDH_...), NOT a date. Use system:time_start (ms since epoch) for dates.
- Use datetime.datetime.fromtimestamp(ts/1000, tz=datetime.timezone.utc) instead of deprecated utcfromtimestamp()
");
        }
        return sb.ToString();
    }

    private static string SystemPrompt => BuildSystemPrompt();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly List<object> _conversationHistory = [];

    /// <summary>Max messages to keep in conversation history before truncating old ones.</summary>
    private const int MaxHistoryLength = 40;

    private static readonly string ConversationLogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GISChat", "logs");

    public void ClearHistory()
    {
        DumpHistoryToDebugLog("clear");
        _conversationHistory.Clear();
    }

    /// <summary>
    /// Remove the last N entries from conversation history (used for error rollback).
    /// </summary>
    public void RollbackHistory(int count = 1)
    {
        for (int i = 0; i < count && _conversationHistory.Count > 0; i++)
            _conversationHistory.RemoveAt(_conversationHistory.Count - 1);
    }

    /// <summary>
    /// Trim conversation history to MaxHistoryLength, saving truncated messages to debug log.
    /// Ensures the first remaining message has role "user" (not a tool_result).
    /// </summary>
    private void TrimHistory()
    {
        if (_conversationHistory.Count <= MaxHistoryLength)
            return;

        var removeCount = _conversationHistory.Count - MaxHistoryLength;

        // Advance past any tool_use/tool_result pairs at the cut boundary
        // to avoid splitting them across the trim point
        while (removeCount < _conversationHistory.Count)
        {
            var entry = JsonSerializer.Serialize(_conversationHistory[removeCount]);
            // If this entry looks like a tool_result (Anthropic) or tool role (OpenAI), include it in the removal
            if (entry.Contains("\"tool_result\"") || entry.Contains("\"role\":\"tool\""))
            {
                removeCount++;
                continue;
            }
            // If it's an assistant message with tool_use, also remove it (its tool_result follows)
            if (entry.Contains("\"tool_use\"") && entry.Contains("\"role\":\"assistant\""))
            {
                removeCount++;
                continue;
            }
            break;
        }

        if (removeCount <= 0 || removeCount >= _conversationHistory.Count)
            return;

        // Save truncated entries to debug log
        var truncated = _conversationHistory.Take(removeCount).ToList();
        DumpHistoryToDebugLog("trim", truncated);

        _conversationHistory.RemoveRange(0, removeCount);
        Logger.Info($"Conversation history trimmed: removed {removeCount} old messages, {_conversationHistory.Count} remaining.");
    }

    /// <summary>
    /// Write conversation history to a JSONL debug file for post-mortem analysis.
    /// </summary>
    private void DumpHistoryToDebugLog(string reason, List<object>? entries = null)
    {
        try
        {
            Directory.CreateDirectory(ConversationLogDir);
            var logFile = Path.Combine(ConversationLogDir, $"conversation_{DateTime.Now:yyyy-MM-dd}.jsonl");
            var items = entries ?? _conversationHistory.ToList();
            if (items.Count == 0) return;

            var record = new
            {
                timestamp = DateTime.Now.ToString("o"),
                reason,
                messageCount = items.Count,
                messages = items
            };
            var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = false });
            File.AppendAllText(logFile, json + Environment.NewLine);
        }
        catch { } // never crash on debug logging
    }

    public async Task<LlmResponse> SendAsync(string userMessage, string mapContext)
    {
        var settings = AddinSettings.Instance;
        var provider = settings.Provider;

        if (LlmProviderInfo.RequiresApiKey(provider) && string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException(
                $"API key not configured. Go to GIS Chat tab → Settings to enter your {LlmProviderInfo.DisplayName(provider)} API key.");

        return provider switch
        {
            LlmProviderType.Anthropic => await SendAnthropicAsync(userMessage, mapContext, settings),
            LlmProviderType.GoogleGemini => await SendGeminiAsync(userMessage, mapContext, settings),
            _ => await SendOpenAICompatibleAsync(userMessage, mapContext, settings) // OpenAI, Ollama, Compatible
        };
    }

    public async Task<LlmResponse> SendToolResultAsync(string toolCallId, string result, string mapContext)
    {
        var settings = AddinSettings.Instance;
        return settings.Provider switch
        {
            LlmProviderType.Anthropic => await SendAnthropicToolResultAsync(toolCallId, result, mapContext, settings),
            LlmProviderType.GoogleGemini => await SendGeminiToolResultAsync(toolCallId, result, mapContext, settings),
            _ => await SendOpenAIToolResultAsync(toolCallId, result, mapContext, settings)
        };
    }

    public async Task<LlmResponse> SendToolResultsAsync(List<(string id, string result)> toolResults, string mapContext)
    {
        var settings = AddinSettings.Instance;
        return settings.Provider switch
        {
            LlmProviderType.Anthropic => await SendAnthropicToolResultsAsync(toolResults, mapContext, settings),
            // For non-Anthropic providers, send results sequentially (they don't batch)
            _ => await SendToolResultAsync(toolResults.Last().id, toolResults.Last().result, mapContext)
        };
    }

    // ---- Anthropic (Claude) ----

    private async Task<LlmResponse> SendAnthropicAsync(string userMessage, string mapContext, AddinSettings settings)
    {
        _conversationHistory.Add(new { role = "user", content = userMessage });
        TrimHistory();

        var body = new
        {
            model = settings.Model,
            max_tokens = settings.MaxTokens,
            system = SystemPrompt + "\n\nCurrent ArcGIS Pro state:\n" + mapContext,
            messages = _conversationHistory,
            tools = GetAnthropicTools()
        };

        var request = new HttpRequestMessage(HttpMethod.Post, settings.EffectiveEndpoint);
        request.Headers.Add("x-api-key", settings.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic API error ({response.StatusCode}): {json}");

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Save to history
        _conversationHistory.Add(JsonSerializer.Deserialize<object>(json.Contains("\"content\"")
            ? JsonSerializer.Serialize(new { role = "assistant", content = root.GetProperty("content") })
            : "{\"role\":\"assistant\",\"content\":\"\"}"));

        return ParseAnthropicResponse(root);
    }

    private async Task<LlmResponse> SendAnthropicToolResultAsync(string toolCallId, string result, string mapContext, AddinSettings settings)
        => await SendAnthropicToolResultsAsync([(toolCallId, result)], mapContext, settings);

    private async Task<LlmResponse> SendAnthropicToolResultsAsync(List<(string id, string result)> toolResults, string mapContext, AddinSettings settings)
    {
        var resultBlocks = toolResults.Select(tr => (object)new { type = "tool_result", tool_use_id = tr.id, content = tr.result }).ToArray();
        _conversationHistory.Add(new
        {
            role = "user",
            content = resultBlocks
        });

        var body = new
        {
            model = settings.Model,
            max_tokens = settings.MaxTokens,
            system = SystemPrompt + "\n\nCurrent ArcGIS Pro state:\n" + mapContext,
            messages = _conversationHistory,
            tools = GetAnthropicTools()
        };

        var request = new HttpRequestMessage(HttpMethod.Post, settings.EffectiveEndpoint);
        request.Headers.Add("x-api-key", settings.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic API error ({response.StatusCode}): {json}");

        var doc = JsonDocument.Parse(json);
        _conversationHistory.Add(JsonSerializer.Deserialize<object>(
            JsonSerializer.Serialize(new { role = "assistant", content = doc.RootElement.GetProperty("content") })));

        return ParseAnthropicResponse(doc.RootElement);
    }

    private static LlmResponse ParseAnthropicResponse(JsonElement root)
    {
        var resp = new LlmResponse();
        foreach (var block in root.GetProperty("content").EnumerateArray())
        {
            var type = block.GetProperty("type").GetString();
            if (type == "text")
                resp.Text += block.GetProperty("text").GetString();
            else if (type == "tool_use")
            {
                resp.ToolCalls.Add(new ToolCallInfo
                {
                    Id = block.GetProperty("id").GetString() ?? "",
                    Name = block.GetProperty("name").GetString() ?? "",
                    Arguments = block.GetProperty("input")
                });
            }
        }
        return resp;
    }

    private static object[] GetAnthropicTools() => [
        new {
            name = "run_arcpy",
            description = "Execute ArcPy (Python) code in ArcGIS Pro's Python environment.",
            input_schema = JsonSerializer.Deserialize<JsonElement>("""
                {
                    "type": "object",
                    "properties": {
                        "code": { "type": "string", "description": "ArcPy/Python code to execute" },
                        "explanation": { "type": "string", "description": "Brief explanation of what this code does" }
                    },
                    "required": ["code", "explanation"]
                }
            """)
        }
    ];

    // ---- OpenAI / Ollama / OpenAI-compatible ----

    private async Task<LlmResponse> SendOpenAICompatibleAsync(string userMessage, string mapContext, AddinSettings settings)
    {
        _conversationHistory.Add(new { role = "user", content = userMessage });
        TrimHistory();

        var messages = new List<object>
        {
            new { role = "system", content = SystemPrompt + "\n\nCurrent ArcGIS Pro state:\n" + mapContext }
        };
        messages.AddRange(_conversationHistory);

        var body = new Dictionary<string, object>
        {
            ["model"] = settings.Model,
            ["messages"] = messages,
            ["max_tokens"] = settings.MaxTokens,
            ["tools"] = GetOpenAITools()
        };

        var request = new HttpRequestMessage(HttpMethod.Post, settings.EffectiveEndpoint);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"API error ({response.StatusCode}): {json}");

        var doc = JsonDocument.Parse(json);
        var choice = doc.RootElement.GetProperty("choices")[0].GetProperty("message");

        // Save assistant message to history
        _conversationHistory.Add(JsonSerializer.Deserialize<object>(choice.GetRawText()));

        return ParseOpenAIResponse(choice);
    }

    private async Task<LlmResponse> SendOpenAIToolResultAsync(string toolCallId, string result, string mapContext, AddinSettings settings)
    {
        _conversationHistory.Add(new { role = "tool", tool_call_id = toolCallId, content = result });

        var messages = new List<object>
        {
            new { role = "system", content = SystemPrompt + "\n\nCurrent ArcGIS Pro state:\n" + mapContext }
        };
        messages.AddRange(_conversationHistory);

        var body = new Dictionary<string, object>
        {
            ["model"] = settings.Model,
            ["messages"] = messages,
            ["max_tokens"] = settings.MaxTokens,
            ["tools"] = GetOpenAITools()
        };

        var request = new HttpRequestMessage(HttpMethod.Post, settings.EffectiveEndpoint);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"API error ({response.StatusCode}): {json}");

        var doc = JsonDocument.Parse(json);
        var choice = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
        _conversationHistory.Add(JsonSerializer.Deserialize<object>(choice.GetRawText()));

        return ParseOpenAIResponse(choice);
    }

    private static LlmResponse ParseOpenAIResponse(JsonElement message)
    {
        var resp = new LlmResponse();

        if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            resp.Text = content.GetString() ?? "";

        if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
        {
            var tc = toolCalls[0];
            var fn = tc.GetProperty("function");
            resp.ToolCalls.Add(new ToolCallInfo
            {
                Id = tc.GetProperty("id").GetString() ?? "",
                Name = fn.GetProperty("name").GetString() ?? "",
                Arguments = JsonDocument.Parse(fn.GetProperty("arguments").GetString() ?? "{}").RootElement
            });
        }

        return resp;
    }

    private static object[] GetOpenAITools() => [
        new {
            type = "function",
            function = new {
                name = "run_arcpy",
                description = "Execute ArcPy (Python) code in ArcGIS Pro's Python environment.",
                parameters = JsonSerializer.Deserialize<JsonElement>("""
                    {
                        "type": "object",
                        "properties": {
                            "code": { "type": "string", "description": "ArcPy/Python code to execute" },
                            "explanation": { "type": "string", "description": "Brief explanation of what this code does" }
                        },
                        "required": ["code", "explanation"]
                    }
                """)
            }
        }
    ];

    // ---- Google Gemini ----

    private async Task<LlmResponse> SendGeminiAsync(string userMessage, string mapContext, AddinSettings settings)
    {
        _conversationHistory.Add(new { role = "user", parts = new[] { new { text = userMessage } } });
        TrimHistory();

        var url = $"{settings.EffectiveEndpoint}/models/{settings.Model}:generateContent?key={settings.ApiKey}";

        var contents = new List<object>
        {
            new { role = "user", parts = new[] { new { text = SystemPrompt + "\n\nCurrent ArcGIS Pro state:\n" + mapContext } } },
            new { role = "model", parts = new[] { new { text = "Understood. I'm ready to help with GIS tasks." } } }
        };
        contents.AddRange(_conversationHistory);

        var body = new Dictionary<string, object>
        {
            ["contents"] = contents,
            ["tools"] = new[] {
                new {
                    function_declarations = new[] {
                        new {
                            name = "run_arcpy",
                            description = "Execute ArcPy (Python) code in ArcGIS Pro's Python environment.",
                            parameters = JsonSerializer.Deserialize<JsonElement>("""
                                {
                                    "type": "object",
                                    "properties": {
                                        "code": { "type": "string", "description": "ArcPy/Python code to execute" },
                                        "explanation": { "type": "string", "description": "Brief explanation of what this code does" }
                                    },
                                    "required": ["code", "explanation"]
                                }
                            """)
                        }
                    }
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Gemini API error ({response.StatusCode}): {json}");

        var doc = JsonDocument.Parse(json);
        var parts = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts");

        // Save to history
        _conversationHistory.Add(new
        {
            role = "model",
            parts = JsonSerializer.Deserialize<object>(parts.GetRawText())
        });

        return ParseGeminiResponse(parts);
    }

    private async Task<LlmResponse> SendGeminiToolResultAsync(string toolCallId, string result, string mapContext, AddinSettings settings)
    {
        _conversationHistory.Add(new
        {
            role = "user",
            parts = new[] { new {
                functionResponse = new {
                    name = "run_arcpy",
                    response = new { result }
                }
            }}
        });

        var url = $"{settings.EffectiveEndpoint}/models/{settings.Model}:generateContent?key={settings.ApiKey}";

        var contents = new List<object>
        {
            new { role = "user", parts = new[] { new { text = SystemPrompt + "\n\nCurrent ArcGIS Pro state:\n" + mapContext } } },
            new { role = "model", parts = new[] { new { text = "Understood." } } }
        };
        contents.AddRange(_conversationHistory);

        var body = new Dictionary<string, object> { ["contents"] = contents };

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Gemini API error ({response.StatusCode}): {json}");

        var doc = JsonDocument.Parse(json);
        var parts = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts");

        _conversationHistory.Add(new
        {
            role = "model",
            parts = JsonSerializer.Deserialize<object>(parts.GetRawText())
        });

        return ParseGeminiResponse(parts);
    }

    private static LlmResponse ParseGeminiResponse(JsonElement parts)
    {
        var resp = new LlmResponse();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text))
                resp.Text += text.GetString();
            if (part.TryGetProperty("functionCall", out var fc))
            {
                resp.ToolCalls.Add(new ToolCallInfo
                {
                    Id = "gemini_" + Guid.NewGuid().ToString("N")[..8],
                    Name = fc.GetProperty("name").GetString() ?? "",
                    Arguments = fc.GetProperty("args")
                });
            }
        }
        return resp;
    }
}

/// <summary>
/// Unified response from any LLM provider.
/// </summary>
public class LlmResponse
{
    public string Text { get; set; } = "";
    public List<ToolCallInfo> ToolCalls { get; set; } = [];
    public ToolCallInfo? ToolCall => ToolCalls.Count > 0 ? ToolCalls[0] : null;
    public bool HasToolCall => ToolCalls.Count > 0;
}

public class ToolCallInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public JsonElement? Arguments { get; set; }

    public string GetArg(string key)
    {
        if (Arguments?.TryGetProperty(key, out var val) == true)
            return val.GetString() ?? "";
        return "";
    }
}
