#!/usr/bin/env python3
"""Generate the monochrome About PNGs: uv run --no-project --with pillow scripts/generate-about-images.py"""

from pathlib import Path
import os

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent.parent
SCALE = 4
FONT_PATHS = (
    Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts/arial.ttf",
    Path("/mnt/c/Windows/Fonts/arial.ttf"),
    Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
    Path("/usr/share/fonts/Adwaita/AdwaitaSans-Regular.ttf"),
)


def menu_window(draw, x, y, width):
    unit = width / 600 * SCALE
    x, y = x * SCALE, y * SCALE
    stroke = round(20 * unit)

    def point(px, py):
        return round(x + px * unit), round(y + py * unit)

    def line(points, weight=20):
        draw.line([point(*p) for p in points], fill="black", width=round(weight * unit), joint="curve")

    def circle(cx, cy, radius, filled=False):
        draw.ellipse((*point(cx - radius, cy - radius), *point(cx + radius, cy + radius)),
                     fill="black" if filled else "white", outline="black", width=stroke)

    draw.rounded_rectangle((*point(0, 0), *point(600, 390)), radius=round(32 * unit),
                           fill="white", outline="black", width=stroke)
    line([(10, 86), (590, 86)])
    line([(174, 86), (174, 380)])
    line([(42, 44), (118, 44)])
    circle(528, 44, 9, filled=True)
    circle(562, 44, 9, filled=True)

    for row, length in ((142, 80), (220, 68), (298, 80)):
        line([(44, row), (44 + length, row)])
    for row, knob in ((150, 350), (232, 476)):
        line([(226, row), (546, row)], weight=16)
        circle(knob, row, 22)
    line([(236, 310), (250, 326), (278, 292)], weight=18)
    line([(322, 310), (494, 310)], weight=18)


def generate(filename, size, symbol_x, symbol_y, symbol_width, title_top, font_size):
    image = Image.new("RGB", (size[0] * SCALE, size[1] * SCALE), "white")
    draw = ImageDraw.Draw(image)
    menu_window(draw, symbol_x, symbol_y, symbol_width)
    font_path = next((path for path in FONT_PATHS if path.is_file()), None)
    if font_path is None:
        raise FileNotFoundError("Install Arial, DejaVu Sans or Adwaita Sans to generate the artwork.")
    font = ImageFont.truetype(str(font_path), font_size * SCALE)
    title = "IrisMenus"
    bounds = draw.textbbox((0, 0), title, font=font)
    draw.text(((image.width - (bounds[2] - bounds[0])) / 2 - bounds[0],
               title_top * SCALE - bounds[1]), title, font=font, fill="black")
    image = image.resize(size, Image.Resampling.LANCZOS)
    destination = ROOT / "About" / filename
    image.save(destination, format="PNG", optimize=True)
    print(f"Generated {destination.relative_to(ROOT)}: {image.width}x{image.height}, {destination.stat().st_size} bytes")


if __name__ == "__main__":
    generate("Preview.png", (1536, 1024), 443, 208, 650, 718, 110)
    generate("ModIcon.png", (1254, 1254), 277, 240, 700, 858, 112)
