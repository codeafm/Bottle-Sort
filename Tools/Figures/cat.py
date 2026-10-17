"""Котик для превью: задняя половина круга, котик, передняя половина и табличка HELP! в коробке 100×100.
У круга и котика опорная точка — низ круга (50, 96), табличка вращается вокруг лапы.
В игре их рисует BottleArtGenerator теми же путями."""
import math

OUTLINE = "#4A2E22"
FUR = "#F5C08A"
FUR_DARK = "#E0954C"
CREAM = "#FFF4E2"
PINK = "#F4A0A8"
NOSE = "#EE7A88"
EYE = "#2B2633"
RING_RED = "#E5484D"
RING_WHITE = "#FFF8F0"

RING_C = (50.0, 82.0)
RING_R = (40.0, 14.0)
HOLE_R = (22.0, 6.5)
HOLE_C = (50.0, 79.0)


def ellipse(cx, cy, rx, ry):
    return f"M{cx - rx:.2f} {cy:.2f} A{rx} {ry} 0 1 0 {cx + rx:.2f} {cy:.2f} A{rx} {ry} 0 1 0 {cx - rx:.2f} {cy:.2f} Z"


def ring_sector_path(a0, a1, steps=24):
    """Кусок круга между внешним краем и дыркой, углы a0..a1 в радианах (0 — вправо, y вниз)."""
    pts = []
    for i in range(steps + 1):
        a = a0 + (a1 - a0) * i / steps
        pts.append((RING_C[0] + RING_R[0] * math.cos(a), RING_C[1] + RING_R[1] * math.sin(a)))
    for i in range(steps, -1, -1):
        a = a0 + (a1 - a0) * i / steps
        pts.append((HOLE_C[0] + HOLE_R[0] * math.cos(a), HOLE_C[1] + HOLE_R[1] * math.sin(a)))
    return "M" + " L".join(f"{x:.2f} {y:.2f}" for x, y in pts) + " Z"


def ring_svg(front):
    # Задняя половина — верх круга (углы pi..2pi), передняя — низ (0..pi)
    a_start, a_end = (0.0, math.pi) if front else (math.pi, 2 * math.pi)
    parts = []
    n = 4
    for k in range(n):
        a0 = a_start + (a_end - a_start) * k / n
        a1 = a_start + (a_end - a_start) * (k + 1) / n
        color = RING_RED if (k + (0 if front else 1)) % 2 == 0 else RING_WHITE
        parts.append(f'<path d="{ring_sector_path(a0, a1)}" fill="{color}"/>')
    band = ring_sector_path(a_start, a_end, 48)
    parts.append(f'<path d="{band}" fill="none" stroke="{OUTLINE}" stroke-width="2.2" stroke-linejoin="round"/>')
    if front:
        parts.append(f'<path d="M22 86 Q50 96 78 86" fill="none" stroke="#FFFFFF" stroke-opacity="0.55" stroke-width="2" stroke-linecap="round"/>')
    return "".join(parts)


def cat_svg(happy):
    o = OUTLINE
    s = []
    s.append(f'<path d="M34 82 C32 70 38 62 50 62 C62 62 68 70 66 82 Z" fill="{FUR}" stroke="{o}" stroke-width="2.2"/>')
    s.append(f'<path d="M42 82 C42 74 45 69 50 69 C55 69 58 74 58 82 Z" fill="{CREAM}"/>')
    for sx in (1, -1):
        def X(x): return 50 + (x - 50) * sx
        s.append(f'<path d="M{X(28)} 38 L{X(30)} 12 L{X(47)} 26 Z" fill="{FUR}" stroke="{o}" stroke-width="2.2" stroke-linejoin="round"/>')
        s.append(f'<path d="M{X(31.5)} 32 L{X(32.5)} 18 L{X(42)} 26 Z" fill="{PINK}"/>')
    s.append(f'<path d="{ellipse(50, 44, 26, 21.5)}" fill="{FUR}" stroke="{o}" stroke-width="2.2"/>')
    s.append(f'<path d="M44 24 L46 31 M50 23 V31 M56 24 L54 31" stroke="{FUR_DARK}" stroke-width="2.4" stroke-linecap="round"/>')
    s.append(f'<path d="{ellipse(50, 56, 11, 7.5)}" fill="{CREAM}"/>')
    s.append(f'<path d="{ellipse(34.5, 54, 5, 3)}" fill="{PINK}" fill-opacity="{0.8 if happy else 0.55}"/>')
    s.append(f'<path d="{ellipse(65.5, 54, 5, 3)}" fill="{PINK}" fill-opacity="{0.8 if happy else 0.55}"/>')
    if happy:
        s.append(f'<path d="M35 47 Q40 40 45 47 M55 47 Q60 40 65 47" fill="none" stroke="{EYE}" stroke-width="2.6" stroke-linecap="round"/>')
        s.append(f'<path d="M45 56 Q50 66 55 56 Z" fill="#B8323F" stroke="{o}" stroke-width="1.6" stroke-linejoin="round"/>')
        s.append(f'<path d="M47.5 60.5 Q50 63.5 52.5 60.5" fill="{NOSE}"/>')
    else:
        s.append(f'<path d="{ellipse(40, 46, 5, 6.2)}" fill="{EYE}"/>')
        s.append(f'<path d="{ellipse(60, 46, 5, 6.2)}" fill="{EYE}"/>')
        s.append(f'<circle cx="41.8" cy="43.6" r="1.9" fill="#FFFFFF"/><circle cx="61.8" cy="43.6" r="1.9" fill="#FFFFFF"/>')
        s.append(f'<circle cx="38.6" cy="48.6" r="0.9" fill="#FFFFFF"/><circle cx="58.6" cy="48.6" r="0.9" fill="#FFFFFF"/>')
        s.append(f'<path d="M33 37.5 L43 34 M57 34 L67 37.5" stroke="{o}" stroke-width="1.8" stroke-linecap="round"/>')
        s.append(f'<path d="{ellipse(50, 59, 2.4, 2.8)}" fill="#8E2A36" stroke="{o}" stroke-width="1.2"/>')
    s.append(f'<path d="M47.6 52 H52.4 L50 54.6 Z" fill="{NOSE}" stroke="{o}" stroke-width="1" stroke-linejoin="round"/>')
    s.append(f'<path d="M37 55 L25 52.5 M37 57.5 L25 59" stroke="{o}" stroke-width="1" stroke-linecap="round" opacity="0.7"/>')
    s.append(f'<path d="M63 55 L75 52.5 M63 57.5 L75 59" stroke="{o}" stroke-width="1" stroke-linecap="round" opacity="0.7"/>')
    return "".join(s)


def paw_svg():
    o = OUTLINE
    return (f'<path d="{ellipse(37, 84, 6.5, 4.8)}" fill="{FUR}" stroke="{o}" stroke-width="2"/>'
            f'<path d="M35 82.5 V85.5 M39 82.5 V85.5" stroke="{o}" stroke-width="1" stroke-linecap="round"/>')


def sign_svg():
    """Табличка в своей коробке: доска 0..40 × 0..21, палочка и лапа в (20, 39)."""
    o = OUTLINE
    s = []
    s.append(f'<rect x="18.5" y="19" width="3" height="20" rx="1.2" fill="#B07A45" stroke="{o}" stroke-width="1.4"/>')
    s.append(f'<rect x="1" y="1" width="38" height="20" rx="4" fill="#FFFDF6" stroke="{o}" stroke-width="2"/>')
    letters = ("M5 6 V16 M5 11 H10 M10 6 V16 "
               "M16.5 6 H12.5 V16 H16.5 M12.5 11 H15.5 "
               "M19 6 V16 H23 "
               "M25 16 V6 H28 Q31 6 31 8.75 Q31 11.5 28 11.5 H25 "
               "M34.5 6 V12.5")
    s.append(f'<path d="{letters}" fill="none" stroke="#D63A3A" stroke-width="2.3" stroke-linecap="round" stroke-linejoin="round"/>')
    s.append(f'<circle cx="34.5" cy="15.6" r="1.3" fill="#D63A3A"/>')
    s.append(f'<path d="{ellipse(20, 39, 5.5, 4.4)}" fill="{FUR}" stroke="{o}" stroke-width="2"/>')
    return "".join(s)
