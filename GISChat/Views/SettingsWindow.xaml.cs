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
        GeeProjectBox.Text = s.GeeProject;
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

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = AddinSettings.Instance;
        var oldGeeProject = s.GeeProject;

        s.Provider = SelectedProvider;
        s.ApiKey = ApiKeyBox.Password;
        s.Model = ModelCombo.Text;
        s.Endpoint = EndpointBox.Text.Trim();
        s.GeeProject = GeeProjectBox.Text.Trim();
        s.ConfirmBeforeExecute = ConfirmCheck.IsChecked == true;
        s.ShowGeneratedCode = ShowCodeCheck.IsChecked == true;
        s.Save();

        Logger.Info($"Settings saved: Provider={s.Provider}, Model={s.Model}");

        // If GEE project was just configured, run setup check
        if (!string.IsNullOrWhiteSpace(s.GeeProject) && s.GeeProject != oldGeeProject)
        {
            await CheckGeeSetupAsync(s.GeeProject);
        }

        DialogResult = true;
        Close();
    }

    private async Task CheckGeeSetupAsync(string geeProject)
    {
        // Step 1: Check if earthengine-api is installed
        var checkResult = await PythonExecutor.RunArcPyAsync(
            "try:\n    import ee\n    print('INSTALLED')\nexcept ImportError:\n    print('NOT_INSTALLED')");

        if (checkResult.Output.Contains("NOT_INSTALLED"))
        {
            var install = MessageBox.Show(
                "Google Earth Engine (earthengine-api) is not installed.\n\n" +
                "Install it now? This may take a minute.",
                "GIS Chat — GEE Setup",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (install != MessageBoxResult.Yes)
                return;

            // Use pip in-process (no subprocess!) — sys.executable = ArcGISPro.exe in embedded Python
            var installResult = await PythonExecutor.RunArcPyAsync(
                "from pip._internal.cli.main import main as _pip_main\n" +
                "print('Installing earthengine-api...')\n" +
                "_rc = _pip_main(['install', 'earthengine-api', '--quiet'])\n" +
                "if _rc == 0:\n" +
                "    print('INSTALL_OK')\n" +
                "else:\n" +
                "    print(f'INSTALL_FAILED (exit code {_rc})')");

            if (installResult.Output.Contains("INSTALL_FAILED") || !installResult.Success)
            {
                MessageBox.Show(
                    "Failed to install earthengine-api.\n\n" +
                    "Try manually in ArcGIS Pro's Python Command Prompt:\n" +
                    "  pip install earthengine-api\n\n" +
                    (installResult.Error ?? installResult.Output),
                    "GIS Chat — GEE Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("earthengine-api installed successfully!",
                "GIS Chat — GEE Setup", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Step 2: Check if credentials are valid
        var authResult = await PythonExecutor.RunArcPyAsync(
            "import ee\n" +
            "try:\n" +
            $"    ee.Initialize(project='{geeProject}')\n" +
            "    print('AUTH_OK')\n" +
            "except Exception as ex:\n" +
            "    print(f'AUTH_FAILED: {ex}')");

        if (authResult.Output.Contains("AUTH_OK"))
        {
            MessageBox.Show(
                $"GEE is ready! Connected to project '{geeProject}'.",
                "GIS Chat — GEE Setup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            // ee.Authenticate() opens a browser — safe to run in-process, no subprocess needed
            var auth = MessageBox.Show(
                "GEE credentials not found or expired.\n\n" +
                "Run authentication now? This will open a browser\n" +
                "for Google sign-in (one-time only).",
                "GIS Chat — GEE Setup",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (auth == MessageBoxResult.Yes)
            {
                var authRunResult = await PythonExecutor.RunArcPyAsync(
                    "import ee\n" +
                    "ee.Authenticate()\n" +
                    $"ee.Initialize(project='{geeProject}')\n" +
                    "print('AUTH_OK')");

                if (authRunResult.Output.Contains("AUTH_OK"))
                {
                    MessageBox.Show(
                        $"GEE authenticated and connected to '{geeProject}'!",
                        "GIS Chat — GEE Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Complete the sign-in in your browser, then GEE should work.\n\n" +
                        "If it still fails, run in ArcGIS Pro's Python window:\n" +
                        "  import ee; ee.Authenticate()",
                        "GIS Chat — GEE Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
