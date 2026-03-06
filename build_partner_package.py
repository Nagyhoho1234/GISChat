"""
Build a partner distribution package.
Assembles: installer exe + addin + LICENSE + QUICK_START.md into a zip.
"""
import os
import zipfile
import argparse

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

parser = argparse.ArgumentParser(description="Build partner distribution package")
parser.add_argument('--config', choices=['Debug', 'Release'], default='Release',
                    help='Build configuration (default: Release)')
args = parser.parse_args()

BUILD_DIR = os.path.join(SCRIPT_DIR, 'GISChat', 'bin', 'x64', args.config,
                         'net8.0-windows10.0.19041.0')
INSTALLER_DIR = os.path.join(SCRIPT_DIR, 'installer', 'dist')
OUTPUT_ZIP = os.path.join(SCRIPT_DIR, 'GISChat_Partner_Package.zip')

QUICK_START_CONTENT = """\
# GIS Chat - Quick Start Guide

## Installation

### Option A: Run the Installer (recommended)
1. Double-click **GISChat_Setup.exe**
2. Follow the setup wizard to pick your AI provider and enter your API key
3. Open (or restart) ArcGIS Pro
4. Find the **GIS Chat** tab in the ribbon

### Option B: Manual Install
1. Double-click **GISChat.esriAddinX** to install the add-in
2. Open ArcGIS Pro > GIS Chat tab > Settings to configure your provider

## Free AI Options

### Google Gemini (cloud, free tier)
1. Go to https://aistudio.google.com/apikey
2. Create a free API key
3. Select "Google Gemini" as provider in GIS Chat settings
4. Paste your key and pick a model (gemini-2.0-flash recommended)

### Ollama (local, completely free)
1. Install Ollama from https://ollama.com
2. Open a terminal and run: ollama pull llama3.1
3. Select "Ollama" as provider in GIS Chat settings
4. No API key needed!

## Usage
1. Click the **GIS Chat** button to open the chat panel
2. Type a GIS task in plain language, e.g. "Buffer the roads layer by 100 meters"
3. Review the generated code and click Confirm to execute

## Troubleshooting
- Logs are stored in %APPDATA%/GISChat/logs/
- Settings are in %APPDATA%/GISChat/settings.json
- Report issues: https://github.com/Nagyhoho1234/GISChat/issues
"""

files_to_pack = []

# Installer exe
installer_exe = os.path.join(INSTALLER_DIR, 'GISChat_Setup.exe')
if os.path.exists(installer_exe):
    files_to_pack.append((installer_exe, 'GISChat_Setup.exe'))
else:
    print(f"WARNING: Installer not found at {installer_exe}")

# Add-in
addin = os.path.join(BUILD_DIR, 'GISChat.esriAddinX')
if os.path.exists(addin):
    files_to_pack.append((addin, 'GISChat.esriAddinX'))
else:
    print(f"WARNING: Add-in not found at {addin}")

# LICENSE
license_file = os.path.join(SCRIPT_DIR, 'LICENSE')
if os.path.exists(license_file):
    files_to_pack.append((license_file, 'LICENSE'))

if not files_to_pack:
    print("ERROR: No files found to package. Build the project first.")
    exit(1)

with zipfile.ZipFile(OUTPUT_ZIP, 'w', zipfile.ZIP_DEFLATED) as zf:
    for src, arc_name in files_to_pack:
        print(f"  Adding: {arc_name}")
        zf.write(src, arc_name)
    print("  Adding: QUICK_START.md")
    zf.writestr('QUICK_START.md', QUICK_START_CONTENT)

print(f"\nPartner package: {OUTPUT_ZIP} ({os.path.getsize(OUTPUT_ZIP):,} bytes)")
