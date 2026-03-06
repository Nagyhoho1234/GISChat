"""Generate simple placeholder icon PNGs for the add-in."""
import struct
import zlib
import os

def create_png(width, height, color_rgb, output_path):
    """Create a minimal solid-color PNG file."""
    r, g, b = color_rgb

    # Build raw image data (RGBA, with filter byte 0 per row)
    raw_data = b''
    for y in range(height):
        raw_data += b'\x00'  # filter: none
        for x in range(width):
            raw_data += bytes([r, g, b, 255])

    def make_chunk(chunk_type, data):
        chunk = chunk_type + data
        crc = zlib.crc32(chunk) & 0xffffffff
        return struct.pack('>I', len(data)) + chunk + struct.pack('>I', crc)

    # PNG signature
    png = b'\x89PNG\r\n\x1a\n'

    # IHDR
    ihdr_data = struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)  # 8-bit RGBA
    png += make_chunk(b'IHDR', ihdr_data)

    # IDAT
    compressed = zlib.compress(raw_data)
    png += make_chunk(b'IDAT', compressed)

    # IEND
    png += make_chunk(b'IEND', b'')

    with open(output_path, 'wb') as f:
        f.write(png)

img_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'GISChat', 'Images')
os.makedirs(img_dir, exist_ok=True)

# Chat icons (blue)
create_png(32, 32, (21, 101, 192), os.path.join(img_dir, 'chat32.png'))
create_png(16, 16, (21, 101, 192), os.path.join(img_dir, 'chat16.png'))

# Settings icon (gray)
create_png(16, 16, (117, 117, 117), os.path.join(img_dir, 'settings16.png'))

print("Icons created.")
