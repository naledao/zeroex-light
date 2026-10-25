"""Render the authored SVG and create a native Windows multi-resolution icon.

Run with: py -m pip install pillow cairosvg; py tools/create_icon.py
The application itself has no Python dependency.
"""
from pathlib import Path
from io import BytesIO
import json
import struct

import cairosvg
from PIL import Image, ImageDraw, ImageFont, PngImagePlugin

root = Path(__file__).resolve().parent.parent
assets = root / 'Assets'
svg_path = assets / 'ZeroExLight.svg'
source = svg_path.read_bytes()
sizes = (16, 20, 24, 32, 40, 48, 64, 128, 256)
frames = []
for size in sizes:
    rendered = cairosvg.svg2png(bytestring=source, output_width=size * 4, output_height=size * 4)
    frame = Image.open(BytesIO(rendered)).convert('RGBA').resize((size, size), Image.Resampling.LANCZOS)
    encoded = BytesIO()
    frame.save(encoded, format='PNG')
    frames.append((size, frame, encoded.getvalue()))

# Keep independently rasterized frames, rather than having the ICO writer
# resize a single raster. PNG-compressed entries are native on Windows 11.
header = struct.pack('<HHH', 0, 1, len(frames))
offset = 6 + 16 * len(frames)
directory = bytearray()
payload = bytearray()
for size, _, data in frames:
    directory.extend(struct.pack('<BBBBHHII', size % 256, size % 256, 0, 0, 1, 32, len(data), offset))
    payload.extend(data)
    offset += len(data)
(assets / 'ZeroExLight.ico').write_bytes(header + directory + payload)

metadata = PngImagePlugin.PngInfo()
metadata.add_text('Source', 'Original SVG authored for ZERO EX Light; source: Assets/ZeroExLight.svg')
metadata.add_text('Rendering', 'tools/create_icon.py; CairoSVG, 4x supersampling and Lanczos; no external artwork')
large = Image.open(BytesIO(cairosvg.svg2png(bytestring=source, output_width=512, output_height=512))).convert('RGBA')
large.save(assets / 'ZeroExLight.png', pnginfo=metadata)
provenance = {
    'source': 'ZeroExLight.svg',
    'method': 'Original code-authored vector geometry, rasterized by tools/create_icon.py',
    'purpose': 'Application EXE, window/titlebar and taskbar icon',
    'externalArtwork': False,
    'sizes': sizes,
}
(assets / 'ZeroExLight.provenance.json').write_text(json.dumps(provenance, indent=2), encoding='utf-8')

review = root / '.impeccable' / 'review'
review.mkdir(parents=True, exist_ok=True)
sheet = Image.new('RGB', (900, 400), '#F3F3F3')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 15)
draw.text((24, 14), 'ZERO EX Light / native icon sizes', fill='#242424', font=font)
for index, (_, frame, _) in enumerate(frames[:7]):
    x = 26 + index * 122
    for y, ground in ((50, '#FFFFFF'), (190, '#202020')):
        draw.rectangle((x, y, x + 98, y + 100), fill=ground)
        sheet.paste(frame, (x + (98-frame.width)//2, y + (100-frame.height)//2), frame)
    draw.text((x+22, 318), f'{frame.width} px', fill='#242424', font=font)
draw.text((24, 365), 'Actual pixel sizes on light and dark surfaces; no external artwork.', fill='#555555', font=font)
sheet.save(review / 'icon-sizes.png', pnginfo=metadata)
print('Rendered authored vector and ICO frames:', ', '.join(map(str, sizes)))
