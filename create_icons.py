"""Generate icon PNGs for the add-in."""
import struct
import zlib
import os
import math


def _make_png(size, pixels):
    """Encode RGBA pixel grid as PNG."""
    raw_data = b''
    for row in pixels:
        raw_data += b'\x00'
        for r, g, b, a in row:
            raw_data += bytes([r, g, b, a])

    def make_chunk(chunk_type, data):
        chunk = chunk_type + data
        crc = zlib.crc32(chunk) & 0xffffffff
        return struct.pack('>I', len(data)) + chunk + struct.pack('>I', crc)

    png = b'\x89PNG\r\n\x1a\n'
    png += make_chunk(b'IHDR', struct.pack('>IIBBBBB', size, size, 8, 6, 0, 0, 0))
    png += make_chunk(b'IDAT', zlib.compress(raw_data))
    png += make_chunk(b'IEND', b'')
    return png


def create_chat_icon(size, output_path):
    """Blue chat bubble with three white dots."""
    pixels = [[(0, 0, 0, 0)] * size for _ in range(size)]
    blue = (21, 101, 192, 255)
    white = (255, 255, 255, 255)

    # Scale factors relative to 32px base
    s = size / 32.0

    bx1, by1 = int(2 * s), int(3 * s)
    bx2, by2 = size - int(3 * s), size - int(10 * s)
    radius = max(2, int(5 * s))

    for y in range(size):
        for x in range(size):
            in_body = False
            if bx1 + radius <= x <= bx2 - radius and by1 <= y <= by2:
                in_body = True
            elif bx1 <= x <= bx2 and by1 + radius <= y <= by2 - radius:
                in_body = True
            else:
                for cx, cy in [(bx1 + radius, by1 + radius), (bx2 - radius, by1 + radius),
                               (bx1 + radius, by2 - radius), (bx2 - radius, by2 - radius)]:
                    if math.hypot(x - cx, y - cy) <= radius:
                        in_body = True
                        break
            if in_body:
                pixels[y][x] = blue

    # Tail
    tail_tip_x, tail_tip_y = int(7 * s), size - int(4 * s)
    tail_bl, tail_br = int(5 * s), int(13 * s)
    for y in range(by2, tail_tip_y + 1):
        progress = (y - by2) / max(1, tail_tip_y - by2)
        left = tail_bl + (tail_tip_x - tail_bl) * progress
        right = tail_br + (tail_tip_x - tail_br) * progress
        for x in range(int(left), int(right) + 1):
            if 0 <= x < size and 0 <= y < size:
                pixels[y][x] = blue

    # Dots
    dot_y = (by1 + by2) // 2
    dot_r = max(1, int(2 * s))
    spacing = max(3, int(7 * s))
    for dx in [size // 2 - spacing, size // 2, size // 2 + spacing]:
        for y in range(dot_y - dot_r, dot_y + dot_r + 1):
            for x in range(dx - dot_r, dx + dot_r + 1):
                if math.hypot(x - dx, y - dot_y) <= dot_r and 0 <= x < size and 0 <= y < size:
                    pixels[y][x] = white

    with open(output_path, 'wb') as f:
        f.write(_make_png(size, pixels))


def create_gear_icon(size, output_path):
    """Gray gear/cog icon for settings."""
    pixels = [[(0, 0, 0, 0)] * size for _ in range(size)]
    gray = (117, 117, 117, 255)
    cx, cy = size / 2, size / 2
    outer_r = size * 0.42
    inner_r = size * 0.25
    hole_r = size * 0.15
    teeth = 8

    for y in range(size):
        for x in range(size):
            dx, dy = x - cx, y - cy
            dist = math.hypot(dx, dy)
            angle = math.atan2(dy, dx)

            # Gear teeth: modulate radius with a square wave
            tooth_angle = angle * teeth / (2 * math.pi)
            on_tooth = (tooth_angle % 1.0) < 0.5
            r = outer_r if on_tooth else inner_r

            if dist <= r and dist > hole_r:
                pixels[y][x] = gray

    with open(output_path, 'wb') as f:
        f.write(_make_png(size, pixels))


img_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'GISChat', 'Images')
os.makedirs(img_dir, exist_ok=True)

create_chat_icon(32, os.path.join(img_dir, 'chat32.png'))
create_chat_icon(16, os.path.join(img_dir, 'chat16.png'))
create_gear_icon(16, os.path.join(img_dir, 'settings16.png'))

print("Icons created.")
