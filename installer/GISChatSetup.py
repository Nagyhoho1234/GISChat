"""
GIS Chat Installer
Installs the GIS Chat add-in for ArcGIS Pro.

Copyright (c) 2026 Zsolt Zoltán Fehér - GIS Expert
Licensed under the MIT License. See LICENSE file for details.
"""
import tkinter as tk
from tkinter import ttk, messagebox
import os
import sys
import shutil
import json


PROVIDERS = [
    {
        "id": "Anthropic",
        "name": "Anthropic (Claude)",
        "models": ["claude-sonnet-4-6", "claude-haiku-4-5-20251001", "claude-opus-4-6"],
        "endpoint": "https://api.anthropic.com/v1/messages",
        "needs_key": True,
        "key_help": "Get your key at console.anthropic.com/settings/keys",
    },
    {
        "id": "OpenAI",
        "name": "OpenAI (GPT)",
        "models": ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "o3-mini"],
        "endpoint": "https://api.openai.com/v1/chat/completions",
        "needs_key": True,
        "key_help": "Get your key at platform.openai.com/api-keys",
    },
    {
        "id": "GoogleGemini",
        "name": "Google Gemini (free tier available)",
        "models": ["gemini-2.0-flash", "gemini-2.0-pro", "gemini-1.5-flash"],
        "endpoint": "https://generativelanguage.googleapis.com/v1beta",
        "needs_key": True,
        "key_help": "Get your FREE key at aistudio.google.com/apikey",
    },
    {
        "id": "Ollama",
        "name": "Ollama (local, completely free)",
        "models": ["llama3.1", "mistral", "codellama", "deepseek-coder-v2"],
        "endpoint": "http://localhost:11434/v1/chat/completions",
        "needs_key": False,
        "key_help": "No API key needed! Install Ollama from ollama.com",
    },
    {
        "id": "OpenAICompatible",
        "name": "OpenAI-compatible (Azure, LM Studio, vLLM...)",
        "models": ["default"],
        "endpoint": "http://localhost:8080/v1/chat/completions",
        "needs_key": True,
        "key_help": "Enter the API key for your endpoint (if required)",
    },
]


def get_resource_path(filename):
    if hasattr(sys, '_MEIPASS'):
        return os.path.join(sys._MEIPASS, filename)
    return os.path.join(os.path.dirname(os.path.abspath(__file__)), filename)


def detect_arcgis_pro():
    pro_path = r"C:\Program Files\ArcGIS\Pro"
    if os.path.isdir(pro_path):
        return True, pro_path
    return False, None


def get_addins_folder():
    """Get the real Documents folder (respects OneDrive/folder redirection)."""
    try:
        import ctypes, ctypes.wintypes
        buf = ctypes.create_unicode_buffer(ctypes.wintypes.MAX_PATH)
        ctypes.windll.shell32.SHGetFolderPathW(None, 5, None, 0, buf)  # CSIDL_PERSONAL
        docs = buf.value
    except Exception:
        docs = os.path.join(os.environ.get('USERPROFILE', ''), 'Documents')
    return os.path.join(docs, 'ArcGIS', 'AddIns', 'ArcGIS Pro')


def get_settings_folder():
    return os.path.join(os.environ.get('APPDATA', ''), 'GISChat')


class InstallerApp:
    def __init__(self):
        self.root = tk.Tk()
        self.root.title("GIS Chat - Installer")
        self.root.geometry("580x560")
        self.root.resizable(False, False)
        self.root.configure(bg='white')

        self.root.update_idletasks()
        x = (self.root.winfo_screenwidth() // 2) - 290
        y = (self.root.winfo_screenheight() // 2) - 280
        self.root.geometry(f"580x560+{x}+{y}")

        # Variables
        self.provider_var = tk.StringVar()
        self.api_key_var = tk.StringVar()
        self.model_var = tk.StringVar()
        self.endpoint_var = tk.StringVar()
        self.confirm_var = tk.BooleanVar(value=True)
        self.show_code_var = tk.BooleanVar(value=True)

        self.pages = []
        self.create_pages()
        self.show_page(0)

    def create_pages(self):
        # --- Page 0: Welcome ---
        page0 = tk.Frame(self.root, bg='white')
        self.pages.append(page0)

        banner = tk.Frame(page0, bg='#1565C0', height=80)
        banner.pack(fill='x')
        banner.pack_propagate(False)
        tk.Label(banner, text="GIS Chat", font=('Segoe UI', 24, 'bold'),
                 fg='white', bg='#1565C0').pack(pady=15)

        content = tk.Frame(page0, bg='white', padx=30, pady=20)
        content.pack(fill='both', expand=True)

        tk.Label(content, text="AI-Powered Chat Assistant for ArcGIS Pro",
                 font=('Segoe UI', 13, 'bold'), bg='white', fg='#333').pack(anchor='w', pady=(0, 15))

        tk.Label(content, text=(
            "GIS Chat adds an intelligent chat panel to ArcGIS Pro that can "
            "understand natural language commands and execute GIS operations.\n\n"
            "Features:\n"
            "  \u2022 Natural language GIS task execution\n"
            "  \u2022 Automatic map context awareness (layers, fields, extent)\n"
            "  \u2022 ArcPy code generation and execution\n"
            "  \u2022 Geoprocessing tool integration\n\n"
            "Supports multiple AI providers:\n"
            "  Claude (Anthropic) \u2022 GPT (OpenAI) \u2022 Gemini (Google, free!)\n"
            "  Ollama (local, free!) \u2022 Any OpenAI-compatible endpoint"
        ), font=('Segoe UI', 10), bg='white', fg='#555',
                 justify='left', wraplength=500).pack(anchor='w')

        tk.Label(content, text="\u00a9 2026 Zsolt Zolt\u00e1n Feh\u00e9r - GIS Expert  |  MIT License",
                 font=('Segoe UI', 9), bg='white', fg='#888').pack(anchor='w', pady=(12, 0))

        pro_found, pro_path = detect_arcgis_pro()
        status_text = f"\u2713 ArcGIS Pro detected at {pro_path}" if pro_found else "\u26a0 ArcGIS Pro not found at default location"
        status_color = '#2E7D32' if pro_found else '#E65100'
        tk.Label(content, text=status_text, font=('Segoe UI', 9),
                 bg='white', fg=status_color).pack(anchor='w', pady=(8, 0))

        nav0 = tk.Frame(page0, bg='white', padx=30, pady=15)
        nav0.pack(fill='x', side='bottom')
        ttk.Button(nav0, text="Next >", command=lambda: self.show_page(1)).pack(side='right')
        ttk.Button(nav0, text="Cancel", command=self.root.destroy).pack(side='right', padx=(0, 8))

        # --- Page 1: Provider & Configuration ---
        page1 = tk.Frame(self.root, bg='white')
        self.pages.append(page1)

        banner1 = tk.Frame(page1, bg='#1565C0', height=50)
        banner1.pack(fill='x')
        banner1.pack_propagate(False)
        tk.Label(banner1, text="Configuration", font=('Segoe UI', 16, 'bold'),
                 fg='white', bg='#1565C0').pack(pady=8)

        content1 = tk.Frame(page1, bg='white', padx=30, pady=15)
        content1.pack(fill='both', expand=True)

        # Provider selection
        tk.Label(content1, text="AI Provider:", font=('Segoe UI', 10, 'bold'),
                 bg='white', fg='#333').pack(anchor='w', pady=(0, 4))
        self.provider_combo = ttk.Combobox(content1, textvariable=self.provider_var,
                                            state='readonly', width=50)
        self.provider_combo['values'] = [p['name'] for p in PROVIDERS]
        self.provider_combo.current(0)
        self.provider_combo.pack(anchor='w', pady=(0, 10))
        self.provider_combo.bind('<<ComboboxSelected>>', self._on_provider_change)

        # API Key
        self.api_key_frame = tk.Frame(content1, bg='white')
        self.api_key_frame.pack(anchor='w', fill='x', pady=(0, 10))
        tk.Label(self.api_key_frame, text="API Key (optional - can set later):",
                 font=('Segoe UI', 10, 'bold'), bg='white', fg='#333').pack(anchor='w', pady=(0, 4))
        self.api_key_entry = ttk.Entry(self.api_key_frame, textvariable=self.api_key_var,
                                        show='*', width=55)
        self.api_key_entry.pack(anchor='w', pady=(0, 2))
        self.api_key_help = tk.Label(self.api_key_frame, text="", font=('Segoe UI', 8),
                                      bg='white', fg='#888')
        self.api_key_help.pack(anchor='w')

        # Model
        tk.Label(content1, text="Model:", font=('Segoe UI', 10, 'bold'),
                 bg='white', fg='#333').pack(anchor='w', pady=(0, 4))
        self.model_combo = ttk.Combobox(content1, textvariable=self.model_var, width=40)
        self.model_combo.pack(anchor='w', pady=(0, 10))

        # Endpoint (for Ollama / Custom)
        self.endpoint_frame = tk.Frame(content1, bg='white')
        self.endpoint_frame.pack(anchor='w', fill='x', pady=(0, 10))
        tk.Label(self.endpoint_frame, text="Endpoint URL:",
                 font=('Segoe UI', 10, 'bold'), bg='white', fg='#333').pack(anchor='w', pady=(0, 4))
        self.endpoint_entry = ttk.Entry(self.endpoint_frame, textvariable=self.endpoint_var, width=55)
        self.endpoint_entry.pack(anchor='w')

        # Options
        tk.Label(content1, text="Options:", font=('Segoe UI', 10, 'bold'),
                 bg='white', fg='#333').pack(anchor='w', pady=(0, 4))
        ttk.Checkbutton(content1, text="Ask for confirmation before executing code",
                       variable=self.confirm_var).pack(anchor='w')
        ttk.Checkbutton(content1, text="Show generated code in chat",
                       variable=self.show_code_var).pack(anchor='w')

        nav1 = tk.Frame(page1, bg='white', padx=30, pady=15)
        nav1.pack(fill='x', side='bottom')
        ttk.Button(nav1, text="Install", command=self.do_install).pack(side='right')
        ttk.Button(nav1, text="< Back", command=lambda: self.show_page(0)).pack(side='right', padx=(0, 8))
        ttk.Button(nav1, text="Cancel", command=self.root.destroy).pack(side='right', padx=(0, 8))

        # Initialize UI for first provider
        self._on_provider_change()

        # --- Page 2: Installing / Done ---
        page2 = tk.Frame(self.root, bg='white')
        self.pages.append(page2)

        banner2 = tk.Frame(page2, bg='#1565C0', height=50)
        banner2.pack(fill='x')
        banner2.pack_propagate(False)
        self.done_label = tk.Label(banner2, text="Installing...", font=('Segoe UI', 16, 'bold'),
                                   fg='white', bg='#1565C0')
        self.done_label.pack(pady=8)

        self.result_frame = tk.Frame(page2, bg='white', padx=30, pady=20)
        self.result_frame.pack(fill='both', expand=True)

        self.progress = ttk.Progressbar(self.result_frame, mode='indeterminate', length=400)
        self.progress.pack(pady=20)

        self.status_text = tk.Text(self.result_frame, height=14, width=65,
                                    font=('Consolas', 9), state='disabled',
                                    bg='#F5F5F5', relief='flat')
        self.status_text.pack(fill='both', expand=True)

        nav2 = tk.Frame(page2, bg='white', padx=30, pady=15)
        nav2.pack(fill='x', side='bottom')
        self.finish_btn = ttk.Button(nav2, text="Finish", command=self.root.destroy, state='disabled')
        self.finish_btn.pack(side='right')

    def _get_provider(self):
        idx = self.provider_combo.current()
        return PROVIDERS[idx] if 0 <= idx < len(PROVIDERS) else PROVIDERS[0]

    def _on_provider_change(self, event=None):
        p = self._get_provider()

        # API key
        if p['needs_key']:
            self.api_key_frame.pack(anchor='w', fill='x', pady=(0, 10))
        else:
            self.api_key_frame.pack_forget()
        self.api_key_help.config(text=p['key_help'])

        # Models
        self.model_combo['values'] = p['models']
        if p['models']:
            self.model_combo.current(0)

        # Endpoint
        if p['id'] in ('Ollama', 'OpenAICompatible'):
            self.endpoint_frame.pack(anchor='w', fill='x', pady=(0, 10))
            self.endpoint_var.set(p['endpoint'])
        else:
            self.endpoint_frame.pack_forget()
            self.endpoint_var.set('')

    def show_page(self, idx):
        for page in self.pages:
            page.pack_forget()
        self.pages[idx].pack(fill='both', expand=True)

    def log(self, msg):
        self.status_text.configure(state='normal')
        self.status_text.insert('end', msg + '\n')
        self.status_text.see('end')
        self.status_text.configure(state='disabled')
        self.root.update()

    def do_install(self):
        self.show_page(2)
        self.progress.start(15)
        self.root.after(100, self._run_install)

    def _run_install(self):
        success = True
        provider = self._get_provider()
        try:
            addins_dir = get_addins_folder()
            self.log(f"Creating add-ins folder: {addins_dir}")
            os.makedirs(addins_dir, exist_ok=True)
            self.log("  OK")

            addin_src = get_resource_path('GISChat.esriAddinX')
            if not os.path.exists(addin_src):
                addin_src = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..',
                                         'GISChat', 'bin', 'x64', 'Debug',
                                         'net8.0-windows10.0.19041.0', 'GISChat.esriAddinX')
            if not os.path.exists(addin_src):
                self.log(f"  ERROR: Cannot find GISChat.esriAddinX")
                success = False
            else:
                dest = os.path.join(addins_dir, 'GISChat.esriAddinX')
                self.log(f"Installing add-in: {dest}")
                shutil.copy2(addin_src, dest)
                self.log(f"  OK ({os.path.getsize(dest):,} bytes)")

            settings_dir = get_settings_folder()
            self.log(f"Creating settings: {settings_dir}")
            os.makedirs(settings_dir, exist_ok=True)

            settings = {
                "Provider": provider['id'],
                "ApiKey": self.api_key_var.get().strip(),
                "Model": self.model_var.get(),
                "Endpoint": self.endpoint_var.get().strip(),
                "MaxTokens": 4096,
                "ConfirmBeforeExecute": self.confirm_var.get(),
                "ShowGeneratedCode": self.show_code_var.get()
            }
            settings_path = os.path.join(settings_dir, 'settings.json')
            with open(settings_path, 'w') as f:
                json.dump(settings, f, indent=2)

            self.log(f"  Provider: {provider['name']}")
            self.log(f"  Model: {settings['Model']}")
            if settings['ApiKey']:
                self.log("  API key: saved")
            elif provider['needs_key']:
                self.log("  API key: not set (configure later in ArcGIS Pro)")
            else:
                self.log("  API key: not required")
            self.log("  OK")

            self.log("")
            self.log("Verifying installation...")
            addin_path = os.path.join(addins_dir, 'GISChat.esriAddinX')
            if os.path.exists(addin_path):
                self.log(f"  Add-in: OK ({os.path.getsize(addin_path):,} bytes)")
            else:
                self.log("  Add-in: MISSING")
                success = False
            self.log(f"  Settings: {'OK' if os.path.exists(settings_path) else 'MISSING'}")

        except Exception as e:
            self.log(f"\nERROR: {e}")
            success = False

        self.progress.stop()

        if success:
            self.log("")
            self.log("Installation complete!")
            self.log("")
            self.log("Next steps:")
            self.log("  1. Open (or restart) ArcGIS Pro")
            self.log("  2. Look for the 'GIS Chat' tab in the ribbon")
            self.log("  3. Click Settings to configure your API key (if not set)")
            self.log("  4. Click 'GIS Chat' to open the chat panel")
            self.done_label.config(text="Installation Complete!")
        else:
            self.log("\nInstallation encountered errors.")
            self.done_label.config(text="Installation Failed")

        self.finish_btn.configure(state='normal')

    def run(self):
        self.root.mainloop()


if __name__ == '__main__':
    app = InstallerApp()
    app.run()
