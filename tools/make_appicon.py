"""Remove checkerboard background and produce MAUI app icon assets."""
from __future__ import annotations

import sys
from collections import deque
from pathlib import Path

from PIL import Image


def is_neutral_background(r: int, g: int, b: int) -> bool:
    if abs(r - g) > 10 or abs(g - b) > 10 or abs(r - b) > 10:
        return False
    avg = (r + g + b) / 3
    return 195 <= avg <= 255


def remove_background_flood(im: Image.Image) -> Image.Image:
    px = im.load()
    w, h = im.size
    visited = bytearray(w * h)
    q: deque[tuple[int, int]] = deque()

    def push(x: int, y: int) -> None:
        idx = y * w + x
        if visited[idx]:
            return
        r, g, b, a = px[x, y]
        if a == 0 or not is_neutral_background(r, g, b):
            return
        visited[idx] = 1
        q.append((x, y))

    for x in range(w):
        push(x, 0)
        push(x, h - 1)
    for y in range(h):
        push(0, y)
        push(w - 1, y)

    while q:
        x, y = q.popleft()
        px[x, y] = (px[x, y][0], px[x, y][1], px[x, y][2], 0)
        if x > 0:
            push(x - 1, y)
        if x + 1 < w:
            push(x + 1, y)
        if y > 0:
            push(x, y - 1)
        if y + 1 < h:
            push(x, y + 1)

    return im


def trim_transparent(im: Image.Image, padding: int = 40) -> Image.Image:
    bbox = im.getbbox()
    if bbox is None:
        return im
    left, top, right, bottom = bbox
    left = max(0, left - padding)
    top = max(0, top - padding)
    right = min(im.width, right + padding)
    bottom = min(im.height, bottom + padding)
    cropped = im.crop((left, top, right, bottom))
    side = max(cropped.width, cropped.height)
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    offset = ((side - cropped.width) // 2, (side - cropped.height) // 2)
    canvas.paste(cropped, offset)
    return canvas


def main() -> int:
    if len(sys.argv) < 3:
        print("Usage: make_appicon.py <source.png> <output_dir>")
        return 1

    src = Path(sys.argv[1])
    out_dir = Path(sys.argv[2])
    out_dir.mkdir(parents=True, exist_ok=True)

    im = Image.open(src).convert("RGBA")
    icon = trim_transparent(remove_background_flood(im))
    icon_1024 = icon.resize((1024, 1024), Image.Resampling.LANCZOS)

    fg_path = out_dir / "appiconfg.png"
    icon_1024.save(fg_path, "PNG")

    base = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    base.save(out_dir / "appicon.png", "PNG")

    print(f"Wrote {fg_path} and {out_dir / 'appicon.png'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
