# GIS Chat

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![ArcGIS Pro 3.6+](https://img.shields.io/badge/ArcGIS%20Pro-3.6%2B-00796B.svg)](https://www.esri.com/en-us/arcgis/products/arcgis-pro)

AI-powered chat assistant for ArcGIS Pro. Ask questions in natural language and let the AI execute GIS operations for you — including Google Earth Engine integration.

![GIS Chat Screenshot](docs/screenshot.png)

## Features

- **Natural language GIS operations** -- describe what you want, get it done
- **Map context awareness** -- automatically reads your layers, fields, extent, spatial reference, and raster band info
- **ArcPy code generation & execution** -- generates and runs Python code directly in ArcGIS Pro
- **Google Earth Engine integration** -- query, process, and download GEE data directly from the chat
- **Multi-provider support** -- choose the AI backend that works for you
- **Multi-tool execution** -- handles complex tasks requiring multiple sequential operations
- **Automatic error recovery** -- retries alternative approaches when a tool fails
- **Conversation history management** -- smart truncation with debug JSONL logging

### Supported AI Providers

| Provider | Cost | Setup |
|----------|------|-------|
| **Google Gemini** | Free tier available | [Get API key](https://aistudio.google.com/apikey) |
| **Ollama** | Free (local) | [Install Ollama](https://ollama.com) |
| **Anthropic (Claude)** | Paid | [Get API key](https://console.anthropic.com/settings/keys) |
| **OpenAI (GPT)** | Paid | [Get API key](https://platform.openai.com/api-keys) |
| **OpenAI-compatible** | Varies | Azure OpenAI, LM Studio, vLLM, etc. |

## Quick Start

### Option A: Installer (recommended)

1. Download `GISChat_Setup.exe` from the [latest release](https://github.com/Nagyhoho1234/GISChat/releases/latest)
2. Run the installer -- it will guide you through provider selection and API key setup
3. Open (or restart) ArcGIS Pro
4. Find the **GIS Chat** tab in the ribbon

### Option B: Manual install

1. Download `GISChat.esriAddinX` from the [latest release](https://github.com/Nagyhoho1234/GISChat/releases/latest)
2. Double-click the file to install it
3. Open ArcGIS Pro and go to **GIS Chat** tab > **Settings** to configure your provider and API key

## Google Earth Engine Setup (Optional)

GIS Chat can query, process, and download data from Google Earth Engine directly. To enable:

1. **Install the GEE Python package** -- open the ArcGIS Pro Python Command Prompt (Start Menu > ArcGIS > Python Command Prompt) and run:
   ```
   pip install earthengine-api
   ```
2. **Authenticate** -- in the same prompt (or ArcGIS Pro's Python window), run:
   ```python
   import ee
   ee.Authenticate()
   ```
   This opens a browser for Google sign-in (one-time only).
3. **Configure in GIS Chat** -- go to Settings and enter your GEE project ID (e.g. `my-gee-project`). When you save, GIS Chat will verify the connection.

If you don't have a GEE project, create one at [code.earthengine.google.com](https://code.earthengine.google.com/).

After setup, you can ask things like *"Load recent Sentinel-2 for my study area from GEE"* and the AI will handle the full workflow: querying the catalog, downloading, and adding the data to your map.

## Usage Examples

| You say | GIS Chat does |
|---------|---------------|
| "Buffer the roads layer by 500 meters" | Runs `arcpy.analysis.Buffer` with your parameters |
| "How many features are in the parcels layer?" | Queries `arcpy.management.GetCount` and reports the result |
| "Select buildings within 1 km of the river" | Runs `SelectLayerByLocation` with the right layers |
| "Add a new text field called 'Status' to parcels" | Runs `arcpy.management.AddField` |
| "What coordinate system is this map using?" | Reads map context and answers directly |
| "Export selected features to a shapefile" | Generates and runs the export code |
| "Load recent Sentinel-2 NDVI from GEE for the study area" | Queries GEE, downloads, and adds the raster to the map |
| "Calculate flow accumulation from the DEM" | Runs Spatial Analyst tools and adds the result |

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (x64)
- [ArcGIS Pro 3.6+](https://www.esri.com/en-us/arcgis/products/arcgis-pro) (for the Esri NuGet package)
- Python 3.9+ (for packaging scripts)

### Build

```bash
dotnet restore GISChat.sln
dotnet build GISChat.sln -c Release -p:Platform=x64
```

### Package the add-in

```bash
python package_addin.py --config Release
```

### Build the standalone installer

```bash
pip install pyinstaller
cd installer
pyinstaller --onefile --windowed --add-data "../GISChat/bin/x64/Release/net8.0-windows10.0.19041.0/GISChat.esriAddinX;." GISChatSetup.py
```

## Project Structure

```
GISChat/
├── GISChat/                    # ArcGIS Pro add-in (C#/WPF)
│   ├── Config.daml             # ArcGIS Pro add-in manifest
│   ├── GISChat.csproj          # Project file
│   ├── Images/                 # Toolbar icons
│   ├── Models/
│   │   ├── AddinSettings.cs    # Settings persistence
│   │   └── ChatMessage.cs      # Chat message model
│   ├── Module1.cs              # Add-in module entry point
│   ├── OpenChatButton.cs       # Ribbon button to open chat
│   ├── OpenSettingsButton.cs   # Ribbon button for settings
│   ├── Properties/
│   │   └── AssemblyInfo.cs
│   ├── Services/
│   │   ├── ConnectionChecker.cs # API connectivity check
│   │   ├── LlmProvider.cs      # Provider enum & defaults
│   │   ├── LlmService.cs       # Unified multi-provider LLM client
│   │   ├── Logger.cs            # File logger with GitHub issue builder
│   │   ├── MapContextService.cs # Reads ArcGIS Pro map state
│   │   ├── ModelFetcher.cs      # Dynamic model list fetching
│   │   └── PythonExecutor.cs    # ArcPy code execution bridge
│   └── Views/
│       ├── ChatDockPane.xaml    # Chat panel UI
│       ├── ChatDockPane.xaml.cs
│       ├── ChatDockPaneViewModel.cs
│       ├── MessageTemplateSelector.cs
│       ├── SettingsWindow.xaml  # Settings dialog
│       └── SettingsWindow.xaml.cs
├── GISChat.sln                 # Solution file
├── installer/
│   └── GISChatSetup.py         # Standalone GUI installer
├── package_addin.py            # Packages .esriAddinX from build output
├── create_icons.py             # Generates placeholder icon PNGs
└── LICENSE                     # MIT License
```

## Security

- **API keys are stored locally only** in `%APPDATA%/GISChat/settings.json`
- Keys are never transmitted anywhere except to the selected AI provider's API endpoint
- The `settings.json` file is excluded from version control via `.gitignore`
- All AI requests go directly from your machine to the provider -- no intermediary server

## Citation

If you use GIS Chat in your research, please cite the preprint:

> Fehér, Zs. Z. (2026). GIS Chat: Bridging Natural Language and Desktop GIS Automation with LLM-Powered GIS Plugins. *EarthArXiv preprint, submitted to SoftwareX*. DOI: [10.31223/X54Z09](https://doi.org/10.31223/X54Z09)

```bibtex
@article{feher2026gischat,
  title={GIS Chat: Bridging Natural Language and Desktop GIS Automation with LLM-Powered GIS Plugins},
  author={Feh{\'e}r, Zsolt Zolt{\'a}n},
  year={2026},
  doi={10.31223/X54Z09},
  note={EarthArXiv preprint, submitted to SoftwareX}
}
```

## License

MIT License -- see [LICENSE](LICENSE) for details.

Copyright (c) 2026 Zsolt Zoltan Feher
