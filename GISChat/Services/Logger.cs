using System.IO;
using System.Collections.Concurrent;

namespace GISChat.Services;

/// <summary>
/// Simple file logger. Writes to %APPDATA%/GISChat/logs/YYYY-MM-DD.log
/// Keeps a rolling buffer of recent entries for error reporting.
/// </summary>
public static class Logger
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GISChat", "logs");

    private static readonly ConcurrentQueue<string> RecentEntries = new();
    private const int MaxRecentEntries = 50;
    private static readonly object WriteLock = new();

    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Error(string message) => Log("ERROR", message);

    public static void Error(string message, Exception ex)
    {
        Log("ERROR", $"{message}\n  Exception: {ex.GetType().Name}: {ex.Message}\n  Stack: {ex.StackTrace}");
    }

    private static void Log(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var entry = $"[{timestamp}] [{level}] {message}";

        // Add to recent buffer
        RecentEntries.Enqueue(entry);
        while (RecentEntries.Count > MaxRecentEntries)
            RecentEntries.TryDequeue(out _);

        // Write to file (fire and forget, never crash on logging)
        try
        {
            lock (WriteLock)
            {
                Directory.CreateDirectory(LogDir);
                var logFile = Path.Combine(LogDir, $"{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(logFile, entry + Environment.NewLine);
            }

            // Cleanup: delete logs older than 14 days
            CleanOldLogs();
        }
        catch { }
    }

    /// <summary>
    /// Get recent log entries for error reporting.
    /// </summary>
    public static string GetRecentLogs(int count = 30)
    {
        var entries = RecentEntries.ToArray();
        var start = Math.Max(0, entries.Length - count);
        return string.Join("\n", entries.Skip(start));
    }

    /// <summary>
    /// Get the full path to today's log file.
    /// </summary>
    public static string GetTodayLogPath()
    {
        return Path.Combine(LogDir, $"{DateTime.Now:yyyy-MM-dd}.log");
    }

    /// <summary>
    /// Build a GitHub issue URL pre-filled with error details.
    /// </summary>
    public static string BuildGitHubIssueUrl(string title, string errorDetails)
    {
        var body = $"""
            ## Error Report

            **GIS Chat Version:** 1.1.0
            **ArcGIS Pro:** {GetArcGISProVersion()}
            **Provider:** {Models.AddinSettings.Instance.Provider}
            **Model:** {Models.AddinSettings.Instance.Model}
            **OS:** {Environment.OSVersion}
            **Date:** {DateTime.Now:yyyy-MM-dd HH:mm}

            ## Description
            {errorDetails}

            ## Recent Logs
            ```
            {GetRecentLogs(20)}
            ```
            """;

        // URL encode for GitHub issue
        var encodedTitle = Uri.EscapeDataString(title);
        var encodedBody = Uri.EscapeDataString(body);

        // This will be the user's GitHub repo - placeholder for now
        return $"https://github.com/Nagyhoho1234/GISChat/issues/new?title={encodedTitle}&body={encodedBody}";
    }

    private static string GetArcGISProVersion()
    {
        try
        {
            var versionFile = @"C:\Program Files\ArcGIS\Pro\ArcGISPro.exe";
            if (File.Exists(versionFile))
            {
                var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(versionFile);
                return vi.FileVersion ?? "Unknown";
            }
        }
        catch { }
        return "Unknown";
    }

    private static DateTime _lastCleanup = DateTime.MinValue;

    private static void CleanOldLogs()
    {
        // Only clean once per session
        if ((DateTime.Now - _lastCleanup).TotalHours < 24) return;
        _lastCleanup = DateTime.Now;

        try
        {
            var cutoff = DateTime.Now.AddDays(-14);
            foreach (var file in Directory.GetFiles(LogDir, "*.log"))
            {
                if (File.GetCreationTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { }
    }
}
