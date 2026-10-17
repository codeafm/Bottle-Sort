"""Рисует фигурки из figures.py в HTML в стиле стекла макета: пустая, наполовину и полная, с котиком.
Запуск: python Tools/Figures/preview.py [папка] — пишет preview.html и detail.html, открыть в браузере.
"""
import os
import re
import sys

import cat
from figures import FIGURES, ORDER

LIQ = {'R': '#C9283A', 'B': '#2D5FC4', 'Y': '#E3B21E', 'G': '#2A9E4A', 'P': '#6E3BB0', 'C': '#1495AA', 'O': '#D9772A',
       'K': '#C73278', 'L': '#85B524', 'N': '#8A5530', 'W': '#D5D9E6', 'D': '#2A2D57'}

CAT_SCALE = 0.9


def hex2rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def rgb2hex(c):
    return "#%02X%02X%02X" % tuple(max(0, min(255, round(v))) for v in c)


def lerp(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def tone_stops(h):
    c = hex2rgb(h)
    hl = lerp(c, (255, 255, 255), 0.28)
    return [(0, rgb2hex(tuple(v * 0.79 for v in c))), (0.12, h), (0.26, rgb2hex(hl)), (0.42, h), (0.8, h),
            (1, rgb2hex(tuple(v * 0.62 for v in c)))]


def sample_path(d):
    """Ломаная пути из M/L/C/Z — для рамки тела хватает."""
    tokens = re.findall(r"[MLCZHVQAmlczhvqa]|-?\d*\.?\d+", d)
    pts, cur, i, cmd = [], (0.0, 0.0), 0, None
    while i < len(tokens):
        t = tokens[i]
        if t.isalpha():
            cmd = t
            i += 1
            if cmd in "Zz":
                continue
        if cmd == 'M' or cmd == 'L':
            cur = (float(tokens[i]), float(tokens[i + 1]))
            pts.append(cur)
            i += 2
        elif cmd == 'C':
            c1 = (float(tokens[i]), float(tokens[i + 1]))
            c2 = (float(tokens[i + 2]), float(tokens[i + 3]))
            end = (float(tokens[i + 4]), float(tokens[i + 5]))
            for k in range(1, 13):
                u = k / 12
                v = 1 - u
                pts.append((v ** 3 * cur[0] + 3 * v * v * u * c1[0] + 3 * v * u * u * c2[0] + u ** 3 * end[0],
                            v ** 3 * cur[1] + 3 * v * v * u * c1[1] + 3 * v * u * u * c2[1] + u ** 3 * end[1]))
            cur = end
            i += 6
        else:
            i += 1
    return pts


def width_at(pts, y):
    xs = []
    n = len(pts)
    for k in range(n):
        a, b = pts[k], pts[(k + 1) % n]
        if (a[1] - y) * (b[1] - y) <= 0 and a[1] != b[1]:
            xs.append(a[0] + (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]))
    return (min(xs), max(xs)) if len(xs) >= 2 else None


def figure_svg(fid, f, recipe, fill, mode, scale=1.0):
    pts = sample_path(f["outer"])
    xs = [p[0] for p in pts]
    body_min, body_max = min(xs) + 4, max(xs) - 4
    top, bot = f["top"], f["bot"]
    cap_layers = len(recipe)
    lh = (bot - top) / cap_layers
    level_y = bot - fill * lh
    uid = f"{fid}{mode}"

    defs = [f'''
<linearGradient id="gGlass{uid}" x1="0" y1="0" x2="1" y2="0">
<stop offset="0" stop-color="#E6F5FF" stop-opacity="0.34"/><stop offset="0.16" stop-color="#E6F5FF" stop-opacity="0.12"/>
<stop offset="0.5" stop-color="#E6F5FF" stop-opacity="0.04"/><stop offset="0.84" stop-color="#E6F5FF" stop-opacity="0.1"/>
<stop offset="1" stop-color="#E6F5FF" stop-opacity="0.3"/></linearGradient>
<linearGradient id="gRim{uid}" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#BFE3FF" stop-opacity="0.75"/>
<stop offset="0.45" stop-color="#FFFFFF" stop-opacity="0.85"/><stop offset="1" stop-color="#8FC3EE" stop-opacity="0.7"/></linearGradient>
<radialGradient id="gShadow{uid}" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#000" stop-opacity="0.5"/>
<stop offset="1" stop-color="#000" stop-opacity="0"/></radialGradient>
<mask id="liq{uid}" maskUnits="userSpaceOnUse" x="-40" y="-40" width="280" height="320">
<path d="{f["outer"]}" fill="white"/><path d="{f["outer"]}" fill="none" stroke="black" stroke-width="8"/></mask>
<mask id="band{uid}" maskUnits="userSpaceOnUse" x="-40" y="-40" width="280" height="320">
<path d="{f["outer"]}" fill="none" stroke="white" stroke-width="9.4"/><path d="{f["outer"]}" fill="none" stroke="black" stroke-width="8"/></mask>
<clipPath id="clip{uid}"><path d="{f["outer"]}"/></clipPath>
<mask id="nobody{uid}" maskUnits="userSpaceOnUse" x="-40" y="-40" width="280" height="320">
<rect x="-40" y="-40" width="280" height="320" fill="white"/><path d="{f["outer"]}" fill="black"/></mask>''']
    used = sorted(set(recipe))
    for c in used:
        stops = "".join(f'<stop offset="{o}" stop-color="{col}"/>' for o, col in tone_stops(LIQ[c]))
        defs.append(f'<linearGradient id="lg{c}{uid}" gradientUnits="userSpaceOnUse" x1="{body_min}" y1="0" x2="{body_max}" y2="0">{stops}</linearGradient>')

    g = []
    g.append(f'<ellipse cx="100" cy="234" rx="80" ry="7" fill="url(#gShadow{uid})"/>')
    if f["extra"] and not f["extra_front"]:
        g.append(f'<path d="{f["extra"]}" fill="none" stroke="#E1F2FF" stroke-opacity="0.85" stroke-width="{f["extra_width"]}" stroke-linecap="round" stroke-linejoin="round"/>')
        g.append(f'<path d="{f["extra"]}" fill="none" stroke="#5A8FC8" stroke-opacity="0.55" stroke-width="{f["extra_width"] - 3.5}" stroke-linecap="round" stroke-linejoin="round"/>')
        g.append(f'<path d="{f["extra"]}" fill="none" stroke="#FFFFFF" stroke-opacity="0.75" stroke-width="1.6" stroke-linecap="round"/>')
    if f["deco"]:
        g.append(f'<g mask="url(#nobody{uid})"><path d="{f["deco"]}" fill="url(#gGlass{uid})" stroke="#E1F2FF" stroke-opacity="0.92" stroke-width="2" stroke-linejoin="round"/></g>')
    g.append(f'<path d="{f["outer"]}" fill="url(#gGlass{uid})"/>')

    inner = []
    for k in range(cap_layers):
        y0 = bot - (k + 1) * lh
        color = recipe[k]
        if k < fill:
            inner.append(f'<rect x="-50" y="{y0:.2f}" width="300" height="{lh + 0.6:.2f}" fill="url(#lg{color}{uid})"/>')
        else:
            inner.append(f'<rect x="-50" y="{y0:.2f}" width="300" height="{lh:.2f}" fill="{LIQ[color]}" fill-opacity="0.25"/>')
            inner.append(f'<rect x="-50" y="{y0:.2f}" width="300" height="0.7" fill="{LIQ[color]}" fill-opacity="0.45"/>')
    if fill > 0:
        surf = rgb2hex(lerp(hex2rgb(LIQ[recipe[fill - 1]]), (255, 255, 255), 0.5))
        inner.append(f'<rect x="-50" y="{level_y - 0.7:.2f}" width="300" height="1.4" fill="{surf}" fill-opacity="0.6"/>')
    g.append(f'<g mask="url(#liq{uid})">{"".join(inner)}</g>')

    def at(x, y, s, inner_svg, rot=0):
        return f'<g transform="translate({x} {y}) rotate({rot}) scale({s}) translate(-50 -96)">{inner_svg}</g>'

    if mode != "full":
        cy = level_y
        g.append(at(100, cy, CAT_SCALE, cat.ring_svg(False)))
        g.append(at(100, cy, CAT_SCALE, cat.cat_svg(False)))
        g.append(at(100, cy, CAT_SCALE, cat.ring_svg(True) + cat.paw_svg()))
        sx = 100 + (70 - 50) * CAT_SCALE
        sy = cy + (66 - 96) * CAT_SCALE
        g.append(f'<g transform="translate({sx} {sy}) rotate(14) scale({CAT_SCALE}) translate(-20 -39)">{cat.sign_svg()}</g>')
    else:
        g.append(at(100, level_y, CAT_SCALE, cat.ring_svg(False) + cat.ring_svg(True)))

    if f["extra"] and f["extra_front"]:
        g.append(f'<path d="{f["extra"]}" fill="none" stroke="#E1F2FF" stroke-opacity="0.85" stroke-width="{f["extra_width"]}" stroke-linecap="round" stroke-linejoin="round"/>')
        g.append(f'<path d="{f["extra"]}" fill="none" stroke="#5A8FC8" stroke-opacity="0.55" stroke-width="{f["extra_width"] - 3.5}" stroke-linecap="round" stroke-linejoin="round"/>')
        g.append(f'<path d="{f["extra"]}" fill="none" stroke="#FFFFFF" stroke-opacity="0.75" stroke-width="1.6" stroke-linecap="round"/>')
    g.append(f'<g clip-path="url(#clip{uid})"><rect x="-40" y="-40" width="280" height="320" fill="#FFF" fill-opacity="0.35" mask="url(#band{uid})"/></g>')
    g.append(f'<path d="{f["outer"]}" fill="none" stroke="#DCEBFA" stroke-opacity="0.8" stroke-width="2.6" stroke-linejoin="round"/>')
    if f["emboss"]:
        g.append(f'<path d="{f["emboss"]}" fill="none" stroke="#FFFFFF" stroke-opacity="0.42" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>')
    g.append(f'<path d="{f["hl1"]}" fill="none" stroke="#FFF" stroke-opacity="0.5" stroke-width="5" stroke-linecap="round"/>')
    g.append(f'<path d="{f["hl2"]}" fill="none" stroke="#FFF" stroke-opacity="0.28" stroke-width="2.2" stroke-linecap="round"/>')
    if f["hl3"]:
        g.append(f'<path d="{f["hl3"]}" fill="none" stroke="#FFF" stroke-opacity="0.45" stroke-width="2" stroke-linecap="round"/>')
    g.append(f'<path d="{f["cap"]}" fill="url(#gRim{uid})" stroke="#E1F2FF" stroke-width="1.2"/>')
    if mode == "full":
        g.append(at(100, -6, CAT_SCALE, cat.cat_svg(True)))

    w, h = 260 * scale, 300 * scale
    return (f'<svg viewBox="-30 -30 260 300" width="{w}" height="{h}" overflow="visible">'
            f'<defs>{"".join(defs)}</defs>{"".join(g)}</svg>')


def detail(out):
    recipes = {0: "PPPP", 1: "KKKKGGGG"}
    cells = []
    for idx, fid in enumerate(ORDER):
        recipe = recipes[idx % 2]
        cells.append(figure_svg(fid, FIGURES[fid], recipe, len(recipe) // 2 - 1, "d", 1.3))
    html = '<!doctype html><html><head><meta charset="utf-8"><style>body{margin:0;background:radial-gradient(circle at 50% 30%,#0E5266,#062C3A 60%,#021219);} .g{display:flex;flex-wrap:wrap;gap:0;padding:6px}</style></head><body><div class="g">' + "".join(cells) + '</div></body></html>'
    open(out, "w", encoding="utf-8").write(html)


def main(out):
    recipes = {0: "PPPP", 1: "KKKKGGGG"}
    rows = []
    for idx, fid in enumerate(ORDER):
        f = FIGURES[fid]
        recipe = recipes[idx % 2]
        n = len(recipe)
        cells = [figure_svg(fid, f, recipe, 0, "empty", 0.9), figure_svg(fid, f, recipe, n // 2, "half", 0.9),
                 figure_svg(fid, f, recipe, n, "full", 0.9)]
        rows.append(f'<div class="fig"><div class="name">{fid}</div>{"".join(cells)}</div>')
    html = f'''<!doctype html><html><head><meta charset="utf-8"><style>
body {{ margin:0; background: radial-gradient(circle at 50% 30%, #12306E, #071338 60%, #040A22); font-family: sans-serif; }}
.grid {{ display:flex; flex-wrap:wrap; gap:6px; padding:10px; }}
.fig {{ display:flex; align-items:center; background:rgba(255,255,255,0.03); border-radius:12px; padding:4px; }}
.name {{ color:#9cf; writing-mode:vertical-rl; transform:rotate(180deg); font-size:14px; }}
</style></head><body><div class="grid">{"".join(rows)}</div></body></html>'''
    open(out, "w", encoding="utf-8").write(html)


if __name__ == "__main__":
    folder = sys.argv[1] if len(sys.argv) > 1 else "."
    main(os.path.join(folder, "preview.html"))
    detail(os.path.join(folder, "detail.html"))
