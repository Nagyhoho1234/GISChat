using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GISChat.Models;
using GISChat.Services;

namespace GISChat.Views;

public partial class SettingsWindow : Window
{
    private readonly LlmProviderType[] _providers =
    [
        LlmProviderType.Anthropic,
        LlmProviderType.OpenAI,
        LlmProviderType.GoogleGemini,
        LlmProviderType.Ollama,
        LlmProviderType.OpenAICompatible
    ];

    public SettingsWindow()
    {
        InitializeComponent();

        foreach (var p in _providers)
            ProviderCombo.Items.Add(LlmProviderInfo.DisplayName(p));

        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = AddinSettings.Instance;

        var idx = Array.IndexOf(_providers, s.Provider);
        ProviderCombo.SelectedIndex = idx >= 0 ? idx : 0;

        ApiKeyBox.Password = s.ApiKey;
        EndpointBox.Text = s.Endpoint;
        ConfirmCheck.IsChecked = s.ConfirmBeforeExecute;
        ShowCodeCheck.IsChecked = s.ShowGeneratedCode;

        UpdateProviderUI(s.Provider);

        if (!string.IsNullOrEmpty(s.Model))
            ModelCombo.Text = s.Model;
    }

    private LlmProviderType SelectedProvider =>
        ProviderCombo.SelectedIndex >= 0 && ProviderCombo.SelectedIndex < _providers.Length
            ? _providers[ProviderCombo.SelectedIndex]
            : LlmProviderType.Anthropic;

    private void Provider_Changed(object sender, SelectionChangedEventArgs e)
    {
        UpdateProviderUI(SelectedProvider);
    }

    private void UpdateProviderUI(LlmProviderType provider)
    {
        var needsKey = LlmProviderInfo.RequiresApiKey(provider);
        ApiKeyPanel.Visibility = needsKey ? Visibility.Visible : Visibility.Collapsed;
        ApiKeyHelp.Text = LlmProviderInfo.ApiKeyHelp(provider);

        var showEndpoint = provider is LlmProviderType.Ollama or LlmProviderType.OpenAICompatible;
        EndpointPanel.Visibility = showEndpoint ? Visibility.Visible : Visibility.Collapsed;
        EndpointHelp.Text = $"Default: {LlmProviderInfo.DefaultEndpoint(provider)}";

        // Load default models (hardcoded fallback)
        ModelCombo.Items.Clear();
        foreach (var model in LlmProviderInfo.DefaultModels(provider))
            ModelCombo.Items.Add(model);
        if (ModelCombo.Items.Count > 0)
            ModelCombo.SelectedIndex = 0;

        ModelStatus.Text = "Click 'Fetch Models' to load available models from the API";
        ModelStatus.Foreground = System.Windows.Media.Brushes.Gray;
    }

    private async void RefreshModels_Click(object sender, RoutedEventArgs e)
    {
        var provider = SelectedProvider;
        var apiKey = ApiKeyBox.Password;
        var endpoint = EndpointBox.Text.Trim();

        // Validate
        if (LlmProviderInfo.RequiresApiKey(provider) && string.IsNullOrWhiteSpace(apiKey))
        {
            ModelStatus.Text = "Enter an API key first, then click Fetch Models";
            ModelStatus.Foreground = System.Windows.Media.Brushes.OrangeRed;
            return;
        }

        RefreshModelsBtn.IsEnabled = false;
        ModelStatus.Text = "Fetching models...";
        ModelStatus.Foreground = System.Windows.Media.Brushes.Gray;

        try
        {
            var ep = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint;
            var models = await ModelFetcher.FetchModelsAsync(provider, apiKey, ep);

            var currentModel = ModelCombo.Text;
            ModelCombo.Items.Clear();
            foreach (var model in models)
                ModelCombo.Items.Add(model);

            // Try to keep the previously selected model
            if (models.Contains(currentModel))
                ModelCombo.Text = currentModel;
            else if (ModelCombo.Items.Count > 0)
                ModelCombo.SelectedIndex = 0;

            ModelStatus.Text = $"Found {models.Length} models";
            ModelStatus.Foreground = System.Windows.Media.Brushes.Green;

            Logger.Info($"Fetched {models.Length} models for {provider}");
        }
        catch (Exception ex)
        {
            ModelStatus.Text = $"Error: {ex.Message}";
            ModelStatus.Foreground = System.Windows.Media.Brushes.Red;
            Logger.Error("Failed to fetch models", ex);
        }
        finally
        {
            RefreshModelsBtn.IsEnabled = true;
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GISChat", "logs");

        Directory.CreateDirectory(logDir);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = logDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open folder: {ex.Message}", "Error");
        }
    }

    private void ReportIssue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = Logger.BuildGitHubIssueUrl(
                "Bug Report",
                "Please describe what happened:\n\n\n");

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open browser: {ex.Message}", "Error");
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = AddinSettings.Instance;
        s.Provider = SelectedProvider;
        s.ApiKey = ApiKeyBox.Password;
        s.Model = ModelCombo.Text;
        s.Endpoint = EndpointBox.Text.Trim();
        s.ConfirmBeforeExecute = ConfirmCheck.IsChecked == true;
        s.ShowGeneratedCode = ShowCodeCheck.IsChecked == true;
        s.Save();

        Logger.Info($"Settings saved: Provider={s.Provider}, Model={s.Model}");

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
