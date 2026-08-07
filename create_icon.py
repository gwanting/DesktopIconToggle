from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
source = ROOT / "assets" / "GHide.png"
output = ROOT / "assets" / "GHide.ico"
sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]

with Image.open(source) as image:
    rgba = image.convert("RGBA")
    if rgba.width != rgba.height:
        raise ValueError("Icon source must be square")
    rgba.save(output, format="ICO", sizes=sizes, bitmap_format="png")

print(f"Created {output} with {len(sizes)} sizes")
