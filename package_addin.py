"""
Package the ArcGIS Pro .esriAddinX file.
An .esriAddinX is an OPC (Open Packaging Conventions) ZIP containing
the DLL in Install/, Config.daml, images, and OPC metadata.
"""
import zipfile
import os
import shutil
import ctypes.wintypes
import argparse

parser = argparse.ArgumentParser(description="Package GIS Chat add-in")
parser.add_argument('--config', choices=['Debug', 'Release'], default='Debug',
                    help='Build configuration (default: Debug)')
args = parser.parse_args()

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.join(SCRIPT_DIR, 'GISChat')
BUILD_DIR = os.path.join(PROJECT_DIR, 'bin', 'x64', args.config, 'net8.0-windows10.0.19041.0')

# Get actual Documents folder (respects OneDrive/folder redirection)
_buf = ctypes.create_unicode_buffer(ctypes.wintypes.MAX_PATH)
ctypes.windll.shell32.SHGetFolderPathW(None, 5, None, 0, _buf)  # 5 = CSIDL_PERSONAL
ADDIN_DIR = os.path.join(_buf.value, 'ArcGIS', 'AddIns', 'ArcGISPro')
ADDIN_NAME = 'GISChat.esriAddinX'

# Files to include in the add-in package: (source_path, archive_name)
files_to_pack = []

# Main DLL -> Install/ subdirectory
dll_path = os.path.join(BUILD_DIR, 'GISChat.dll')
if os.path.exists(dll_path):
    files_to_pack.append((dll_path, 'Install/GISChat.dll'))
else:
    print(f"ERROR: DLL not found at {dll_path}")
    exit(1)

# Config.daml at root level
daml_path = os.path.join(PROJECT_DIR, 'Config.daml')
files_to_pack.append((daml_path, 'Config.daml'))

# Images at root level
img_dir = os.path.join(PROJECT_DIR, 'Images')
for img in os.listdir(img_dir):
    if img.endswith('.png'):
        files_to_pack.append((os.path.join(img_dir, img), f'Images/{img}'))

# deps.json and pdb -> Install/ subdirectory
deps_path = os.path.join(BUILD_DIR, 'GISChat.deps.json')
if os.path.exists(deps_path):
    files_to_pack.append((deps_path, 'Install/GISChat.deps.json'))

pdb_path = os.path.join(BUILD_DIR, 'GISChat.pdb')
if os.path.exists(pdb_path):
    files_to_pack.append((pdb_path, 'Install/GISChat.pdb'))

# Collect all file extensions for [Content_Types].xml
extensions = set()
for _, arc_name in files_to_pack:
    ext = arc_name.rsplit('.', 1)[-1] if '.' in arc_name else ''
    if ext:
        extensions.add(ext)

# Build [Content_Types].xml (OPC manifest)
content_types_parts = ['<?xml version="1.0" encoding="utf-8"?>']
content_types_parts.append('<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">')
for ext in sorted(extensions):
    content_types_parts.append(f'<Default Extension="{ext}" ContentType="{ext}/{ext}" />')
content_types_parts.append('<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />')
content_types_parts.append('</Types>')
content_types_xml = ''.join(content_types_parts)

# Build _rels/.rels (OPC relationships - minimal, no digital signature)
rels_xml = '<?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships" />'

# Create the .esriAddinX (OPC/ZIP file)
output_path = os.path.join(BUILD_DIR, ADDIN_NAME)
with zipfile.ZipFile(output_path, 'w', zipfile.ZIP_DEFLATED) as zf:
    for src, arc_name in files_to_pack:
        print(f"  Adding: {arc_name}")
        zf.write(src, arc_name)
    # Write OPC metadata
    print("  Adding: [Content_Types].xml")
    zf.writestr('[Content_Types].xml', content_types_xml)
    print("  Adding: _rels/.rels")
    zf.writestr('_rels/.rels', rels_xml)

print(f"\nPackaged: {output_path} ({os.path.getsize(output_path):,} bytes)")

# Copy to ArcGIS Pro add-ins folder
os.makedirs(ADDIN_DIR, exist_ok=True)
dest = os.path.join(ADDIN_DIR, ADDIN_NAME)
shutil.copy2(output_path, dest)
print(f"Installed: {dest}")
print("\nDouble-click the .esriAddinX or restart ArcGIS Pro to load the add-in.")
