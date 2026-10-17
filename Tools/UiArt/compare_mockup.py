"""Склейки «макет | Unity | разница» для экранов из Docs/Mockup/png и Logs/MockupCompare.

Снимки Unity делает PlayMode-тест MockupCapture. Запуск: python Tools/UiArt/compare_mockup.py [имя ...]
Печатает среднее расхождение по экрану (0 — совпадает, 255 — всё наоборот) и по полосам высоты,
чтобы было видно, где именно съехало.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MOCK = os.path.join(ROOT, "Docs", "Mockup", "png")
UNITY = os.path.join(ROOT, "Logs", "MockupCompare")
OUT = os.path.join(ROOT, "Docs", "Screens", "compare")
BANDS = 8
SCALE = 0.5


def compare(name):
    mock = Image.open(os.path.join(MOCK, name + ".png")).convert("RGB")
    unity = Image.open(os.path.join(UNITY, name + ".png")).convert("RGB")
    if unity.size != mock.size:
        unity = unity.resize(mock.size, Image.LANCZOS)

    a = np.asarray(mock, dtype=np.int16)
    b = np.asarray(unity, dtype=np.int16)
    diff = np.abs(a - b).mean(axis=2)
    bands = [diff[i * diff.shape[0] // BANDS:(i + 1) * diff.shape[0] // BANDS].mean() for i in range(BANDS)]

    heat = np.clip(diff * 3, 0, 255).astype(np.uint8)
    heat_img = Image.fromarray(np.stack([heat, heat // 3, heat // 4], axis=2), "RGB")

    w, h = int(mock.width * SCALE), int(mock.height * SCALE)
    sheet = Image.new("RGB", (w * 3 + 40, h + 50), (10, 12, 30))
    draw = ImageDraw.Draw(sheet)
    for i, (title, img) in enumerate((("mockup", mock), ("Unity", unity), ("diff x3", heat_img))):
        x = i * (w + 20)
        sheet.paste(img.resize((w, h), Image.LANCZOS), (x, 50))
        draw.text((x + 10, 15), title, fill=(230, 235, 255))
    sheet.save(os.path.join(OUT, name + ".jpg"), quality=82)
    return diff.mean(), bands


def main():
    names = sys.argv[1:] or sorted(os.path.splitext(f)[0] for f in os.listdir(UNITY) if f.endswith(".png"))
    for name in names:
        if not os.path.exists(os.path.join(MOCK, name + ".png")):
            continue
        total, bands = compare(name)
        print(f"{name:22s} {total:6.1f}   " + " ".join(f"{v:5.1f}" for v in bands))


if __name__ == "__main__":
    main()
