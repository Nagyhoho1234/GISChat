using System.IO;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace GISChat.Services;

public static class PythonExecutor
{
    /// <summary>
    /// Execute arbitrary ArcPy code inside ArcGIS Pro's Python environment.
    /// Returns the printed output or error messages.
    /// </summary>
    public static async Task<ExecutionResult> RunArcPyAsync(string code)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "GISChat");
        Directory.CreateDirectory(tempDir);

        var scriptPath = Path.Combine(tempDir, $"gischat_{Guid.NewGuid():N}.py");
        var outputPath = Path.Combine(tempDir, $"gischat_output_{Guid.NewGuid():N}.txt");

        // Wrap the user code in error handling that writes output to a file
        var wrappedCode = string.Join("\n",
            "import arcpy",
            "import sys",
            "import io",
            "import traceback",
            "",
            "# Capture all print output",
            "_gischat_output = io.StringIO()",
            "_gischat_old_stdout = sys.stdout",
            "sys.stdout = _gischat_output",
            "",
            "try:",
            IndentCode(code, "    "),
            "except Exception as _gischat_e:",
            "    print(f\"ERROR: {type(_gischat_e).__name__}: {_gischat_e}\")",
            "    print(traceback.format_exc())",
            "finally:",
            "    sys.stdout = _gischat_old_stdout",
            $"    with open(r'{outputPath}', 'w', encoding='utf-8') as _gischat_f:",
            "        _gischat_f.write(_gischat_output.getvalue())");

        try
        {
            File.WriteAllText(scriptPath, wrappedCode);

            // Execute via the CalculateValue geoprocessing tool which can run Python expressions
            var parameters = Geoprocessing.MakeValueArray(
                $"exec(open(r'{scriptPath}', encoding='utf-8').read())"
            );

            var gpResult = await Geoprocessing.ExecuteToolAsync("management.CalculateValue", parameters,
                null, null, null, GPExecuteToolFlags.None);

            // Read captured output
            string output = "";
            if (File.Exists(outputPath))
            {
                output = File.ReadAllText(outputPath);
            }

            // Also collect GP messages
            var gpMessages = string.Join("\n",
                gpResult.Messages?.Select(m => m.Text) ?? []);

            if (gpResult.IsFailed)
            {
                return new ExecutionResult
                {
                    Success = false,
                    Output = output,
                    Error = gpMessages
                };
            }

            return new ExecutionResult
            {
                Success = !output.Contains("ERROR:"),
                Output = output,
                Error = output.Contains("ERROR:") ? output : null
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Output = "",
                Error = $"Failed to execute Python: {ex.Message}"
            };
        }
        finally
        {
            // Cleanup temp files
            TryDelete(scriptPath);
            TryDelete(outputPath);
        }
    }

    /// <summary>
    /// Run a specific geoprocessing tool by name with positional parameters.
    /// </summary>
    public static async Task<ExecutionResult> RunGeoprocessingToolAsync(
        string toolName, string[] parameters)
    {
        try
        {
            var gpParams = Geoprocessing.MakeValueArray(parameters.Cast<object>().ToArray());
            var gpResult = await Geoprocessing.ExecuteToolAsync(toolName, gpParams);

            var messages = string.Join("\n",
                gpResult.Messages?.Select(m => m.Text) ?? []);

            return new ExecutionResult
            {
                Success = !gpResult.IsFailed,
                Output = messages,
                Error = gpResult.IsFailed ? messages : null
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Output = "",
                Error = $"Geoprocessing error: {ex.Message}"
            };
        }
    }

    private static string IndentCode(string code, string indent)
    {
        var lines = code.Split('\n');
        return string.Join("\n", lines.Select(l => indent + l));
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }
}

public class ExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string? Error { get; set; }

    public override string ToString()
    {
        if (Success)
            return string.IsNullOrEmpty(Output) ? "Completed successfully." : Output.Trim();
        return $"Error: {Error ?? "Unknown error"}\n{Output}".Trim();
    }
}
