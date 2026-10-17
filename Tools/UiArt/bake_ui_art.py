"""Растеризует UI-спрайты из SVG-фигур макета (Docs/Mockup) в Assets/_Game/Art/UI.

Свой маленький растеризатор, чтобы не тащить cairo: path (M L H V Q C A Z и относительные),
polyline, polygon, rect с rx, circle, line, заливка nonzero, обводка с круглыми концами.
Запуск: python Tools/UiArt/bake_ui_art.py  (нужны numpy и pillow)
"""
import math
import os
import re
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "_Game", "Art", "UI")
SS = 4


def hex_rgba(color, alpha=1.0):
    color = color.lstrip("#")
    r, g, b = (int(color[i:i + 2], 16) / 255 for i in (0, 2, 4))
    return np.array([r, g, b, alpha], dtype=np.float32)


# Геометрия: всё переводим в ломаные.

def _flatten_quad(p0, p1, p2, steps=12):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1])
            for t in (i / steps for i in range(1, steps + 1))]


def _flatten_cubic(p0, p1, p2, p3, steps=16):
    pts = []
    for i in range(1, steps + 1):
        t = i / steps
        u = 1 - t
        pts.append((u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0],
                    u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1]))
    return pts


def _flatten_arc(p0, rx, ry, phi_deg, large, sweep, p1):
    if rx == 0 or ry == 0:
        return [p1]
    phi = math.radians(phi_deg)
    cos_p, sin_p = math.cos(phi), math.sin(phi)
    dx, dy = (p0[0] - p1[0]) / 2, (p0[1] - p1[1]) / 2
    x1p = cos_p * dx + sin_p * dy
    y1p = -sin_p * dx + cos_p * dy
    rx, ry = abs(rx), abs(ry)
    lam = (x1p ** 2) / (rx ** 2) + (y1p ** 2) / (ry ** 2)
    if lam > 1:
        rx *= math.sqrt(lam)
        ry *= math.sqrt(lam)
    num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p
    den = rx * rx * y1p * y1p + ry * ry * x1p * x1p
    coef = math.sqrt(max(num / den, 0)) if den else 0
    if large == sweep:
        coef = -coef
    cxp = coef * rx * y1p / ry
    cyp = -coef * ry * x1p / rx
    cx = cos_p * cxp - sin_p * cyp + (p0[0] + p1[0]) / 2
    cy = sin_p * cxp + cos_p * cyp + (p0[1] + p1[1]) / 2

    def angle(ux, uy, vx, vy):
        a = math.atan2(ux * vy - uy * vx, ux * vx + uy * vy)
        return a

    th1 = angle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry)
    dth = angle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry)
    if not sweep and dth > 0:
        dth -= 2 * math.pi
    elif sweep and dth < 0:
        dth += 2 * math.pi
    steps = max(4, int(abs(dth) / (math.pi / 24)))
    pts = []
    for i in range(1, steps + 1):
        t = th1 + dth * i / steps
        x = rx * math.cos(t)
        y = ry * math.sin(t)
        pts.append((cos_p * x - sin_p * y + cx, sin_p * x + cos_p * y + cy))
    return pts


def parse_path(d):
    tokens = re.findall(r"[MmLlHhVvQqCcAaZzSsTt]|-?\d*\.?\d+(?:e-?\d+)?", d)
    subpaths, cur, i = [], [], 0
    pos = (0.0, 0.0)
    start = pos
    cmd = None
    last_ctrl = None

    def num():
        nonlocal i
        v = float(tokens[i])
        i += 1
        return v

    while i < len(tokens):
        t = tokens[i]
        if re.match(r"[A-Za-z]", t):
            cmd = t
            i += 1
            if cmd in "Zz":
                if cur:
                    cur.append(start)
                    subpaths.append((cur, True))
                cur = []
                pos = start
                continue
        rel = cmd.islower()
        c = cmd.upper()
        ox, oy = (pos if rel else (0.0, 0.0))
        if c == "M":
            if cur:
                subpaths.append((cur, False))
            pos = (ox + num(), oy + num())
            start = pos
            cur = [pos]
            cmd = "l" if rel else "L"
        elif c == "L":
            pos = (ox + num(), oy + num())
            cur.append(pos)
        elif c == "H":
            pos = ((pos[0] if rel else 0) + num(), pos[1])
            cur.append(pos)
        elif c == "V":
            pos = (pos[0], (pos[1] if rel else 0) + num())
            cur.append(pos)
        elif c == "Q":
            p1 = (ox + num(), oy + num())
            p2 = (ox + num(), oy + num())
            cur += _flatten_quad(pos, p1, p2)
            last_ctrl = p1
            pos = p2
        elif c == "T":
            p1 = (2 * pos[0] - last_ctrl[0], 2 * pos[1] - last_ctrl[1]) if last_ctrl else pos
            p2 = (ox + num(), oy + num())
            cur += _flatten_quad(pos, p1, p2)
            last_ctrl = p1
            pos = p2
        elif c == "C":
            p1 = (ox + num(), oy + num())
            p2 = (ox + num(), oy + num())
            p3 = (ox + num(), oy + num())
            cur += _flatten_cubic(pos, p1, p2, p3)
            last_ctrl = p2
            pos = p3
        elif c == "S":
            p1 = (2 * pos[0] - last_ctrl[0], 2 * pos[1] - last_ctrl[1]) if last_ctrl else pos
            p2 = (ox + num(), oy + num())
            p3 = (ox + num(), oy + num())
            cur += _flatten_cubic(pos, p1, p2, p3)
            last_ctrl = p2
            pos = p3
        elif c == "A":
            rx, ry, phi = num(), num(), num()
            large, sweep = int(num()), int(num())
            p1 = (ox + num(), oy + num())
            cur += _flatten_arc(pos, rx, ry, phi, large, sweep, p1)
            pos = p1
        if c not in "QTCS":
            last_ctrl = None
    if cur:
        subpaths.append((cur, False))
    return subpaths


def poly(points, closed):
    nums = [float(v) for v in re.findall(r"-?\d*\.?\d+", points)]
    pts = list(zip(nums[0::2], nums[1::2]))
    if closed:
        pts.append(pts[0])
    return [(pts, closed)]


def rect(x, y, w, h, rx=0):
    if rx <= 0:
        return [([(x, y), (x + w, y), (x + w, y + h), (x, y + h), (x, y)], True)]
    r = min(rx, w / 2, h / 2)
    d = (f"M{x + r} {y} H{x + w - r} A{r} {r} 0 0 1 {x + w} {y + r} V{y + h - r} "
         f"A{r} {r} 0 0 1 {x + w - r} {y + h} H{x + r} A{r} {r} 0 0 1 {x} {y + h - r} "
         f"V{y + r} A{r} {r} 0 0 1 {x + r} {y} Z")
    return parse_path(d)


def circle(cx, cy, r):
    steps = 72
    pts = [(cx + r * math.cos(2 * math.pi * i / steps), cy + r * math.sin(2 * math.pi * i / steps))
           for i in range(steps + 1)]
    return [(pts, True)]


def line(x1, y1, x2, y2):
    return [([(x1, y1), (x2, y2)], False)]


# Растр с суперсэмплингом SS×SS.

class Canvas:
    def __init__(self, w, h, view=None):
        self.w, self.h = w, h
        vx, vy, vw, vh = view or (0, 0, w, h)
        self.scale = w / vw
        self.off = (vx, vy)
        self.rgba = np.zeros((h, w, 4), dtype=np.float32)
        ys = (np.arange(h * SS) + 0.5) / SS
        xs = (np.arange(w * SS) + 0.5) / SS
        self.sx, self.sy = np.meshgrid(xs / self.scale + vx, ys / self.scale + vy)

    def _down(self, mask):
        return mask.reshape(self.h, SS, self.w, SS).mean(axis=(1, 3))

    def fill_mask(self, subpaths):
        wind = np.zeros(self.sx.shape, dtype=np.int32)
        for pts, _ in subpaths:
            for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]):
                if y0 == y1:
                    continue
                up = y0 < y1
                ya, yb = (y0, y1) if up else (y1, y0)
                inside = (self.sy >= ya) & (self.sy < yb)
                xint = x0 + (self.sy - y0) * (x1 - x0) / (y1 - y0)
                hit = inside & (self.sx < xint)
                wind += np.where(hit, 1 if up else -1, 0)
        return self._down((wind != 0).astype(np.float32))

    def stroke_mask(self, subpaths, width):
        hw = width / 2
        best = np.full(self.sx.shape, np.inf, dtype=np.float32)
        for pts, _ in subpaths:
            if len(pts) == 1:
                pts = pts * 2
            for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
                dx, dy = x1 - x0, y1 - y0
                ll = dx * dx + dy * dy
                if ll == 0:
                    t = 0
                else:
                    t = np.clip(((self.sx - x0) * dx + (self.sy - y0) * dy) / ll, 0, 1)
                px = x0 + t * dx
                py = y0 + t * dy
                dist = np.hypot(self.sx - px, self.sy - py)
                np.minimum(best, dist, out=best)
        return self._down((best <= hw).astype(np.float32))

    def paint(self, mask, color):
        if callable(color):
            col = color(self)
        else:
            col = np.broadcast_to(np.asarray(color, dtype=np.float32), (self.h, self.w, 4))
        a = col[..., 3] * mask
        out_a = a + self.rgba[..., 3] * (1 - a)
        safe = np.where(out_a > 0, out_a, 1)
        for ch in range(3):
            self.rgba[..., ch] = (col[..., ch] * a + self.rgba[..., ch] * self.rgba[..., 3] * (1 - a)) / safe
        self.rgba[..., 3] = out_a

    def fill(self, shape, color):
        self.paint(self.fill_mask(shape), color)

    def stroke(self, shape, color, width):
        self.paint(self.stroke_mask(shape, width), color)

    def pixel_coords(self):
        ys = (np.arange(self.h) + 0.5) / self.scale + self.off[1]
        xs = (np.arange(self.w) + 0.5) / self.scale + self.off[0]
        return np.meshgrid(xs, ys)

    def save(self, name):
        img = np.clip(self.rgba * 255 + 0.5, 0, 255).astype(np.uint8)
        Image.fromarray(img, "RGBA").save(os.path.join(OUT, name))
        print("baked", name)


def radial(stops, cx, cy, r):
    def f(cv):
        x, y = cv.pixel_coords()
        t = np.clip(np.hypot(x - cx, y - cy) / r, 0, 1)
        return gradient(t, stops)
    return f


def linear_y(stops, y0, y1):
    def f(cv):
        _, y = cv.pixel_coords()
        return gradient(np.clip((y - y0) / (y1 - y0), 0, 1), stops)
    return f


def linear_x(stops, x0, x1):
    def f(cv):
        x, _ = cv.pixel_coords()
        return gradient(np.clip((x - x0) / (x1 - x0), 0, 1), stops)
    return f


def gradient(t, stops):
    out = np.zeros(t.shape + (4,), dtype=np.float32)
    offs = [s[0] for s in stops]
    cols = [s[1] for s in stops]
    for ch in range(4):
        out[..., ch] = np.interp(t, offs, [c[ch] for c in cols])
    return out


def blur(cv, radius):
    # Мягкая тень/свечение: несколько проходов box-фильтра по альфе.
    a = cv.rgba[..., 3].copy()
    k = max(1, int(radius))
    for _ in range(3):
        pad = np.pad(a, k, mode="constant")
        c = np.cumsum(np.cumsum(pad, axis=0), axis=1)
        c = np.pad(c, ((1, 0), (1, 0)))
        s = 2 * k + 1
        a = (c[s:, s:] - c[:-s, s:] - c[s:, :-s] + c[:-s, :-s]) / (s * s)
    return a


# Иконки: viewBox 24, белые, цвет задаётся в Unity.

WHITE = hex_rgba("#FFFFFF")

ICONS = {
    "home": (2.4, [parse_path("M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"), poly("9 22 9 12 15 12 15 22", False)]),
    "settings": (2.2, [circle(12, 12, 3), parse_path(
        "M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 "
        "1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06"
        "a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 "
        "2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 "
        "0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 "
        "1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21"
        "a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z")]),
    "back": (2.6, [line(19, 12, 5, 12), poly("12 19 5 12 12 5", False)]),
    "undo": (2.6, [poly("1 4 1 10 7 10", False), parse_path("M3.51 15a9 9 0 1 0 2.13-9.36L1 10")]),
    "restart": (2.6, [poly("23 4 23 10 17 10", False), parse_path("M20.49 15a9 9 0 1 1-2.12-9.36L23 10")]),
    "hint": (2.4, [parse_path("M9 18h6"), parse_path("M10 22h4"), parse_path("M12 2a7 7 0 0 0-4 12.7V17h8v-2.3A7 7 0 0 0 12 2z")]),
    "add_bottle": (2.4, [parse_path("M9 2h6"), parse_path("M10 2v5l-3 3v10a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V10l-3-3V2"),
                         line(12, 12, 12, 18), line(9, 15, 15, 15)]),
    "map": (2.2, [poly("1 6 1 22 8 18 16 22 23 18 23 2 16 6 8 2 1 6", True), line(8, 2, 8, 18), line(16, 6, 16, 22)]),
    "bag": (2.2, [parse_path("M6 2L3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"), line(3, 6, 21, 6), parse_path("M16 10a4 4 0 0 1-8 0")]),
    "gift": (2.2, [poly("20 12 20 22 4 22 4 12", False), rect(2, 7, 20, 5), line(12, 22, 12, 7),
                   parse_path("M12 7H7.5a2.5 2.5 0 0 1 0-5C11 2 12 7 12 7z"), parse_path("M12 7h4.5a2.5 2.5 0 0 0 0-5C13 2 12 7 12 7z")]),
    "calendar": (2.2, [rect(3, 4, 18, 18, 2), line(16, 2, 16, 6), line(8, 2, 8, 6), line(3, 10, 21, 10), poly("9 15 11 17 15 13", False)]),
    "wheel": (2.0, [circle(12, 12, 9), circle(12, 12, 2), line(12, 3, 12, 10), line(12, 14, 12, 21), line(3, 12, 10, 12),
                    line(14, 12, 21, 12), line(5.6, 5.6, 10.6, 10.6), line(13.4, 13.4, 18.4, 18.4)]),
    "video": (2.4, [poly("23 7 16 12 23 17 23 7", True), rect(1, 5, 15, 14, 2)]),
    "lock": (2.4, [rect(4, 11, 16, 11, 2), parse_path("M8 11V7a4 4 0 0 1 8 0v4")]),
    "arrow_right": (3.0, [line(5, 12, 19, 12), poly("12 5 19 12 12 19", False)]),
    "plus": (3.5, [line(12, 5, 12, 19), line(5, 12, 19, 12)]),
    "check": (4.0, [poly("20 6 9 17 4 12", False)]),
    "close": (3.0, [line(18, 6, 6, 18), line(6, 6, 18, 18)]),
    "music": (2.2, [parse_path("M9 18V5l12-2v13"), circle(6, 18, 3), circle(18, 16, 3)]),
    "sound": (2.2, [poly("11 5 6 9 2 9 2 15 6 15 11 19 11 5", True), parse_path("M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07")]),
    "vibration": (2.2, [rect(8, 5, 8, 14, 1.5), parse_path("M3 8l2 2-2 2 2 2-2 2"), parse_path("M21 8l-2 2 2 2-2 2 2 2")]),
    "bell": (2.2, [parse_path("M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"), parse_path("M13.73 21a2 2 0 0 1-3.46 0")]),
    "globe": (2.2, [circle(12, 12, 10), line(2, 12, 22, 12),
                    parse_path("M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z")]),
}

FILLED_ICONS = {
    "play": parse_path("M6 3L20 12L6 21Z"),
}

STAR = "12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"
HAND = ("M10 9.5V4a2 2 0 0 0-4 0v10l-1.6-1.6a2 2 0 0 0-2.8 2.8l3.6 3.6C6.7 20.3 8.4 22 11.2 22H14a8 8 0 0 0 8-8v-3"
        "a2 2 0 0 0-4 0v-1a2 2 0 0 0-4 0v-1a2 2 0 0 0-4 0z")
CROWN = "2 17 4 5 9 10 12 2 15 10 20 5 22 17 2 17"


def bake_icons(size=96):
    pad = 1.5  # запас под толстую обводку на краях
    view = (-pad, -pad, 24 + 2 * pad, 24 + 2 * pad)
    for name, (width, shapes) in ICONS.items():
        cv = Canvas(size, size, view)
        for shape in shapes:
            cv.stroke(shape, WHITE, width)
        cv.save(f"icon_{name}.png")
    for name, shape in FILLED_ICONS.items():
        cv = Canvas(size, size, view)
        cv.fill(shape, WHITE)
        cv.stroke(shape, WHITE, 1.5)
        cv.save(f"icon_{name}.png")


def bake_stars():
    view = (0, 0, 24, 24)
    star = poly(STAR, True)
    cv = Canvas(96, 96, view)
    cv.fill(star, hex_rgba("#FFC83D"))
    cv.stroke(star, hex_rgba("#7A3F00"), 1.5)
    cv.save("star_on.png")
    cv = Canvas(96, 96, view)
    cv.fill(star, hex_rgba("#FFFFFF", 0.25))
    cv.stroke(star, hex_rgba("#7A3F00"), 1.5)
    cv.save("star_off.png")
    # Большая звезда экрана победы: светлая обводка, свечение запекаем сразу.
    view = (-4, -4, 32, 32)
    cv = Canvas(256, 256, view)
    cv.fill(star, hex_rgba("#FFC83D"))
    glow = blur(cv, 14)
    cv.rgba[..., :3] = 0
    cv.rgba[..., 3] = 0
    cv.paint(glow * 0.85, hex_rgba("#FFC83D"))
    cv.fill(star, hex_rgba("#FFC83D"))
    cv.stroke(star, hex_rgba("#FFF3B0"), 1.2)
    cv.save("star_big.png")


def bake_hand():
    view = (-3, -3, 30, 30)
    cv = Canvas(160, 160, view)
    hand = parse_path(HAND)
    shadow = Canvas(160, 160, view)
    shadow.fill([( [(x, y + 4 * 24 / 46) for x, y in pts], c) for pts, c in hand], hex_rgba("#000000"))
    cv.paint(blur(shadow, 5) * 0.45, hex_rgba("#000000"))
    cv.fill(hand, WHITE)
    cv.stroke(hand, hex_rgba("#1B2550"), 1.3)
    cv.save("hand.png")


def bake_crown():
    cv = Canvas(96, 80, (0, 0, 24, 20))
    shape = poly(CROWN, True)
    cv.fill(shape, hex_rgba("#FFD957"))
    cv.stroke(shape, hex_rgba("#7A3F00"), 1.4)
    cv.save("crown.png")


# Примитивы для панелей и кнопок.

def bake_primitives():
    # Белые 9-slice: радиус 64 px, в Unity border 64; радиус меняется через Pixels Per Unit Multiplier.
    cv = Canvas(160, 160)
    cv.fill(rect(0, 0, 160, 160, 64), WHITE)
    cv.save("rounded.png")
    cv = Canvas(160, 160)
    cv.stroke(rect(3, 3, 154, 154, 61), WHITE, 6)
    cv.save("rounded_line.png")

    cv = Canvas(128, 128)
    cv.fill(circle(64, 64, 63), WHITE)
    cv.save("circle.png")
    cv = Canvas(128, 128)
    cv.stroke(circle(64, 64, 58), WHITE, 9)
    cv.save("ring.png")

    cv = Canvas(128, 128)
    x, y = cv.pixel_coords()
    d = np.clip(np.hypot(x - 64, y - 64) / 64, 0, 1)
    cv.rgba[..., :3] = 1
    cv.rgba[..., 3] = (1 - d) ** 2.2
    cv.save("glow.png")

    # Полоса блика: прозрачный — белый — прозрачный.
    cv = Canvas(64, 8)
    x, _ = cv.pixel_coords()
    cv.rgba[..., :3] = 1
    cv.rgba[..., 3] = np.clip(1 - np.abs(x - 32) / 32, 0, 1) ** 1.5
    cv.save("shine.png")

    # Стеклянная полка: 9-slice по горизонтали.
    cv = Canvas(96, 32)
    shelf = rect(0, 0, 96, 32, 16)
    cv.fill(shelf, linear_y([(0, hex_rgba("#FFFFFF", 0.5)), (0.45, hex_rgba("#C8E6FF", 0.18)),
                             (1, hex_rgba("#FFFFFF", 0.06))], 0, 32))
    cv.stroke([([(10, 1.2), (86, 1.2)], False)], hex_rgba("#FFFFFF", 0.75), 2.4)
    cv.save("shelf.png")

    # Монета (radial-gradient из макета) и тёмная полоска снизу.
    cv = Canvas(96, 96)
    coin = circle(48, 48, 46)
    cv.fill(coin, radial([(0, hex_rgba("#FFF5BF")), (0.45, hex_rgba("#FFC83D")), (1, hex_rgba("#D97F00"))], 33, 29, 66))
    inner = Canvas(96, 96)
    inner.fill(circle(48, 42, 46), WHITE)
    rim = cv.fill_mask(coin) * (1 - inner.rgba[..., 3])
    cv.paint(rim, hex_rgba("#8C4600", 0.55))
    cv.save("coin.png")

    # Конус лучей для экрана победы.
    cv = Canvas(512, 512)
    x, y = cv.pixel_coords()
    ang = (np.degrees(np.arctan2(y - 256, x - 256)) + 360) % 24
    on = np.clip(np.minimum(ang, 10 - ang) * 2 + 0.5, 0, 1) * (ang < 10.5)
    r = np.hypot(x - 256, y - 256) / 256
    cv.rgba[..., :3] = hex_rgba("#FFD66E")[:3]
    cv.rgba[..., 3] = on * 0.16 * np.clip((1 - r) * 3, 0, 1)
    cv.save("rays.png")


def bake_nodes():
    s = 160
    c = s / 2
    # Пройден: золотой.
    cv = Canvas(s, s)
    cv.fill(circle(c, c, c - 1), hex_rgba("#FFF1B8"))
    cv.fill(circle(c, c, c - 9), radial([(0, hex_rgba("#FFF0A0")), (0.45, hex_rgba("#FFC83D")), (1, hex_rgba("#E08200"))], s * 0.35, s * 0.3, s * 0.82))
    cv.save("node_done.png")
    # Текущий: зелёный, белая обводка.
    cv = Canvas(s, s)
    cv.fill(circle(c, c, c - 1), WHITE)
    cv.fill(circle(c, c, c - 9), radial([(0, hex_rgba("#D4FF9E")), (0.45, hex_rgba("#5BDB33")), (1, hex_rgba("#138A1E"))], s * 0.35, s * 0.3, s * 0.82))
    cv.save("node_current.png")
    # Закрыт.
    cv = Canvas(s, s)
    cv.fill(circle(c, c, c - 1), hex_rgba("#AAC8FF", 0.4))
    cv.fill(circle(c, c, c - 8), hex_rgba("#0C1C50", 0.85))
    cv.save("node_locked.png")
    # Босс: ромб.
    cv = Canvas(s, s)
    half = s / 2 - 4
    diamond = parse_path(f"M{c} {c - half} L{c + half} {c} L{c} {c + half} L{c - half} {c} Z")
    cv.fill(diamond, hex_rgba("#E9B8FF"))
    inner = parse_path(f"M{c} {c - half + 11} L{c + half - 11} {c} L{c} {c + half - 11} L{c - half + 11} {c} Z")
    cv.fill(inner, linear_x([(0, hex_rgba("#B06BFF")), (1, hex_rgba("#5A1FB0"))], 0, s))
    cv.save("node_boss.png")


# Бутылка-заглушка: classic и cylinder из Bottle.dc.html.

BOTTLES = {
    "classic": dict(
        outer="M30 26 V36 Q30 46 20 52 Q16 55 16 64 V206 Q16 230 40 230 H60 Q84 230 84 206 V64 Q84 55 80 52 Q70 46 70 36 V26 Z",
        inner="M34 28 V36 Q34 49 24 55 Q20 58 20 66 V206 Q20 226 40 226 H60 Q80 226 80 206 V66 Q80 58 76 55 Q66 49 66 36 V28 Z",
        cap="M26 12 H74 Q79 12 79 17 V23 Q79 28 74 28 H26 Q21 28 21 23 V17 Q21 12 26 12 Z",
        hl=[("M25 72 V204", 0.5, 5), ("M76 76 V114", 0.28, 2.2), ("M37 30 V42", 0.45, 2)]),
    "cylinder": dict(
        outer="M24 14 L18 6 H82 V14 Q78 16 78 20 V214 H92 Q94 214 94 216 V230 Q94 232 92 232 H8 Q6 232 6 230 V216 Q6 214 8 214 H22 V20 Q22 16 24 14 Z",
        inner="M26 16 H74 V212 H26 Z",
        cap="M8 216 H92 V230 H8 Z",
        hl=[("M30 30 V206", 0.5, 5), ("M70 40 V110", 0.28, 2.2)],
        ticks="M26 36 H36 M26 58 H32 M26 80 H36 M26 102 H32 M26 124 H36 M26 146 H32 M26 168 H36 M26 190 H32"),
}

GLASS = [(0, hex_rgba("#E6F5FF", 0.34)), (0.16, hex_rgba("#E6F5FF", 0.12)), (0.5, hex_rgba("#E6F5FF", 0.04)),
         (0.84, hex_rgba("#E6F5FF", 0.10)), (1, hex_rgba("#E6F5FF", 0.30))]


def bake_bottles():
    w, h = 200, 480
    view = (0, 0, 100, 240)
    for name, b in BOTTLES.items():
        outer = parse_path(b["outer"])
        inner = parse_path(b["inner"])
        cap = parse_path(b["cap"])

        back = Canvas(w, h, view)
        back.fill(outer, linear_x(GLASS, 16, 84))
        back.save(f"bottle_{name}_back.png")

        front = Canvas(w, h, view)
        front.stroke(inner, hex_rgba("#FFFFFF", 0.35), 1.4)
        front.stroke(outer, hex_rgba("#DCEBFA", 0.8), 2.6)
        if "ticks" in b:
            front.stroke(parse_path(b["ticks"]), hex_rgba("#FFFFFF", 0.55), 1.4)
        for d, alpha, width in b["hl"]:
            front.stroke(parse_path(d), hex_rgba("#FFFFFF", alpha), width)
        front.fill(cap, linear_x([(0, hex_rgba("#BFE3FF", 0.75)), (0.45, hex_rgba("#FFFFFF", 0.85)), (1, hex_rgba("#8FC3EE", 0.7))], 21, 79))
        front.stroke(cap, hex_rgba("#FFFFFF", 0.9), 1.2)
        front.save(f"bottle_{name}_front.png")

        mask = Canvas(w, h, view)
        mask.fill(inner, WHITE)
        mask.save(f"bottle_{name}_mask.png")

        glow = Canvas(w, h, view)
        glow.fill(outer, WHITE)
        a = blur(glow, 9)
        glow.rgba[..., :3] = 1
        glow.rgba[..., 3] = np.clip(a * 1.6, 0, 1)
        glow.save(f"bottle_{name}_glow.png")

    # Блик и тени слоя жидкости поверх всех слоёв сразу (как lg-* в макете).
    cv = Canvas(64, 4)
    x, _ = cv.pixel_coords()
    t = x / 64
    stops_dark = np.interp(t, [0, 0.12, 0.42, 0.8, 1], [0.22, 0, 0, 0, 0.38])
    stops_light = np.interp(t, [0.12, 0.26, 0.42], [0, 0.32, 0])
    cv.rgba[..., :3] = np.where((stops_light > 0)[..., None], 1.0, 0.0)
    cv.rgba[..., 3] = np.maximum(stops_dark, stops_light)
    cv.save("liquid_shade.png")


# Мягкая тень под панели и кнопки (box-shadow из макета): 9-slice, граница 128 px.
# Внутренний прямоугольник отстоит от края на 64 px, столько же занимает размытие.
def bake_shadow():
    cv = Canvas(256, 256)
    cv.fill(rect(64, 64, 128, 128, 32), WHITE)
    a = blur(cv, 22)
    cv.rgba[..., :3] = 0
    cv.rgba[..., 3] = np.clip(a, 0, 1)
    cv.save("shadow.png")


# Хвост ленты «ОТЛИЧНО!» с вырезом, clip-path из Win.dc.html; правый — зеркальный.
def bake_ribbon_tail():
    cv = Canvas(100, 100)
    cv.fill(poly("0 0 100 0 100 100 0 100 30 50", True), WHITE)
    cv.save("ribbon_tail.png")


# Внутренняя тень снизу (inset 0 -4px 0): серп между скруглённым прямоугольником и им же, поднятым на 12 px.
# 9-slice с границей 64, как rounded.png, поэтому радиус и толщина задаются одним множителем.
def bake_inset_bottom():
    cv = Canvas(160, 160)
    full = cv.fill_mask(rect(0, 0, 160, 160, 64))
    raised = cv.fill_mask(rect(0, -12, 160, 160, 64))
    cv.rgba[..., :3] = 1
    cv.rgba[..., 3] = np.clip(full - raised, 0, 1)
    cv.save("inset_bottom.png")


def main():
    os.makedirs(OUT, exist_ok=True)
    only = set(sys.argv[1:])
    steps = [("icons", bake_icons), ("stars", bake_stars), ("hand", bake_hand), ("crown", bake_crown),
             ("primitives", bake_primitives), ("shadow", bake_shadow), ("ribbon", bake_ribbon_tail), ("inset", bake_inset_bottom), ("nodes", bake_nodes), ("bottles", bake_bottles)]
    for name, fn in steps:
        if not only or name in only:
            fn()


if __name__ == "__main__":
    main()
