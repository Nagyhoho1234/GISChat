using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using GISChat.Models;
using GISChat.Services;

namespace GISChat.Views;

internal class ChatDockPaneViewModel : DockPane
{
    private const string DockPaneId = "GISChat_ChatDockPane";

    private readonly LlmService _llm = new();
    private readonly object _messagesLock = new();

    public ChatDockPaneViewModel()
    {
        BindingOperations.EnableCollectionSynchronization(Messages, _messagesLock);

        var provider = LlmProviderInfo.DisplayName(AddinSettings.Instance.Provider);
        Messages.Add(new ChatMessage(MessageRole.System,
            $"Welcome! Using {provider}.\n" +
            "Ask me to perform GIS tasks. For example:\n" +
            "\"Buffer all roads by 500m\"\n" +
            "\"How many parcels are in the flood zone?\"\n" +
            "\"Export selected features to shapefile\""));

        // Check connection on startup
        _ = CheckConnectionAsync();
    }

    #region Properties

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    private string _userInput = string.Empty;
    public string UserInput
    {
        get => _userInput;
        set => SetProperty(ref _userInput, value);
    }

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    // --- Connection status indicator ---

    private Brush _statusColor = new SolidColorBrush(Color.FromRgb(158, 158, 158)); // grey
    public Brush StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    private string _statusText = "Checking connection...";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    #endregion

    #region Commands

    private RelayCommand? _sendCommand;
    public ICommand SendCommand => _sendCommand ??= new RelayCommand(
        async () => await SendMessageAsync(),
        () => !string.IsNullOrWhiteSpace(UserInput) && !IsProcessing);

    private RelayCommand? _clearCommand;
    public ICommand ClearCommand => _clearCommand ??= new RelayCommand(() =>
    {
        Messages.Clear();
        _llm.ClearHistory();
        var provider = LlmProviderInfo.DisplayName(AddinSettings.Instance.Provider);
        Messages.Add(new ChatMessage(MessageRole.System, $"Chat cleared. Using {provider}."));
    });

    #endregion

    #region Core Logic

    private async Task SendMessageAsync()
    {
        var input = UserInput.Trim();
        if (string.IsNullOrEmpty(input)) return;

        Messages.Add(new ChatMessage(MessageRole.User, input));
        UserInput = string.Empty;
        IsProcessing = true;

        try
        {
            var settings = AddinSettings.Instance;

            if (LlmProviderInfo.RequiresApiKey(settings.Provider)
                && string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                Messages.Add(new ChatMessage(MessageRole.System,
                    $"API key not set for {LlmProviderInfo.DisplayName(settings.Provider)}.\n" +
                    "Click Settings in the GIS Chat tab to configure."));
                return;
            }

            Logger.Info($"User message: {input[..Math.Min(80, input.Length)]}...");

            var mapContext = await MapContextService.GetMapContextAsync();
            var response = await _llm.SendAsync(input, mapContext);

            // Successful response — mark connected
            StatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            StatusText = $"Connected to {LlmProviderInfo.DisplayName(settings.Provider)}";

            await ProcessResponseAsync(response, mapContext);
        }
        catch (Exception ex)
        {
            Logger.Error("SendMessage failed", ex);
            Messages.Add(new ChatMessage(MessageRole.System, $"Error: {ex.Message}"));

            // If conversation history is corrupted (missing tool_result etc.), reset it
            if (ex.Message.Contains("tool_result") || ex.Message.Contains("tool_use"))
            {
                _llm.ClearHistory();
                Messages.Add(new ChatMessage(MessageRole.System, "Conversation history reset due to sync error. You can continue chatting."));
            }

            // Re-check connection on failure
            _ = CheckConnectionAsync();
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task ProcessResponseAsync(LlmResponse response, string mapContext)
    {
        if (response.HasToolCall && response.ToolCall!.Name == "run_arcpy")
        {
            var tc = response.ToolCall;
            var code = tc.GetArg("code");
            var explanation = tc.GetArg("explanation");

            var msg = new ChatMessage(MessageRole.Assistant,
                string.IsNullOrWhiteSpace(explanation) ? response.Text : explanation)
            {
                CodeBlock = AddinSettings.Instance.ShowGeneratedCode ? code : null,
                IsExecuting = true
            };
            Messages.Add(msg);

            string toolResultText;
            if (!AddinSettings.Instance.ConfirmBeforeExecute || await ConfirmExecutionAsync(explanation))
            {
                Logger.Info($"Executing ArcPy: {explanation}");
                var result = await PythonExecutor.RunArcPyAsync(code);
                msg.ExecutionResult = result.ToString();
                msg.IsExecuting = false;
                toolResultText = result.ToString();
                Logger.Info($"ArcPy result: {(result.Success ? "OK" : "FAILED")} - {toolResultText[..Math.Min(100, toolResultText.Length)]}");
            }
            else
            {
                toolResultText = "Cancelled by user.";
                msg.ExecutionResult = toolResultText;
                msg.IsExecuting = false;
            }

            // Always send tool_result back — Anthropic requires it after every tool_use
            var followUp = await _llm.SendToolResultAsync(tc.Id, toolResultText, mapContext);
            if (!string.IsNullOrWhiteSpace(followUp.Text))
                Messages.Add(new ChatMessage(MessageRole.Assistant, followUp.Text));

            RefreshMessages();
            return;
        }

        // Plain text response
        if (!string.IsNullOrWhiteSpace(response.Text))
            Messages.Add(new ChatMessage(MessageRole.Assistant, response.Text));
    }

    private Task<bool> ConfirmExecutionAsync(string explanation)
    {
        var tcs = new TaskCompletionSource<bool>();
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var result = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                $"Execute this GIS operation?\n\n{explanation}",
                "GIS Chat - Confirm Execution",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            tcs.SetResult(result == System.Windows.MessageBoxResult.Yes);
        });
        return tcs.Task;
    }

    private void RefreshMessages()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            CollectionViewSource.GetDefaultView(Messages)?.Refresh();
        });
    }

    #endregion

    #region Connection Status

    public async Task CheckConnectionAsync()
    {
        StatusColor = new SolidColorBrush(Color.FromRgb(158, 158, 158)); // grey
        StatusText = "Checking connection...";

        try
        {
            var (status, message) = await ConnectionChecker.CheckAsync();

            StatusColor = status switch
            {
                ConnectionStatus.Connected => new SolidColorBrush(Color.FromRgb(76, 175, 80)),  // green
                ConnectionStatus.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),      // red
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))                          // grey
            };
            StatusText = message;

            Logger.Info($"Connection check: {status} - {message}");
        }
        catch (Exception ex)
        {
            StatusColor = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            StatusText = $"Check failed: {ex.Message}";
            Logger.Error("Connection check failed", ex);
        }
    }

    #endregion

    #region DockPane

    internal static void Show()
    {
        var pane = FrameworkApplication.DockPaneManager.Find(DockPaneId);
        pane?.Activate();
    }

    protected override Task InitializeAsync() => base.InitializeAsync();

    #endregion
}
