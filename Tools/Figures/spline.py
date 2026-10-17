"""Гладкие контуры по опорным точкам: сплайн Catmull-Rom в кубические кривые SVG-пути.

Точка — (x, y) или (x, y, 'c') для острого угла: касательная в ней нулевая с обеих сторон.
"""


def fmt(v):
    s = f"{v:.1f}"
    if s.endswith(".0"):
        s = s[:-2]
    if s == "-0":
        s = "0"
    return s


def catmull_closed(points, tension=0.5):
    pts = [(p[0], p[1], len(p) > 2 and p[2] == 'c') for p in points]
    n = len(pts)

    def tangent(i):
        x, y, corner = pts[i]
        if corner:
            return (0.0, 0.0)
        px, py, _ = pts[(i - 1) % n]
        nx, ny, _ = pts[(i + 1) % n]
        return ((nx - px) * tension, (ny - py) * tension)

    out = [f"M{fmt(pts[0][0])} {fmt(pts[0][1])}"]
    for i in range(n):
        j = (i + 1) % n
        x0, y0, _ = pts[i]
        x1, y1, _ = pts[j]
        t0 = tangent(i)
        t1 = tangent(j)
        c1 = (x0 + t0[0] / 3.0, y0 + t0[1] / 3.0)
        c2 = (x1 - t1[0] / 3.0, y1 - t1[1] / 3.0)
        if t0 == (0.0, 0.0) and t1 == (0.0, 0.0):
            out.append(f"L{fmt(x1)} {fmt(y1)}")
        else:
            out.append(f"C{fmt(c1[0])} {fmt(c1[1])} {fmt(c2[0])} {fmt(c2[1])} {fmt(x1)} {fmt(y1)}")
    out.append("Z")
    return " ".join(out)


def catmull_open(points, tension=0.5):
    pts = [(p[0], p[1]) for p in points]
    n = len(pts)

    def tangent(i):
        if i == 0:
            return ((pts[1][0] - pts[0][0]) * tension * 2, (pts[1][1] - pts[0][1]) * tension * 2)
        if i == n - 1:
            return ((pts[-1][0] - pts[-2][0]) * tension * 2, (pts[-1][1] - pts[-2][1]) * tension * 2)
        return ((pts[i + 1][0] - pts[i - 1][0]) * tension, (pts[i + 1][1] - pts[i - 1][1]) * tension)

    out = [f"M{fmt(pts[0][0])} {fmt(pts[0][1])}"]
    for i in range(n - 1):
        x0, y0 = pts[i]
        x1, y1 = pts[i + 1]
        t0, t1 = tangent(i), tangent(i + 1)
        out.append(f"C{fmt(x0 + t0[0] / 3)} {fmt(y0 + t0[1] / 3)} {fmt(x1 - t1[0] / 3)} {fmt(y1 - t1[1] / 3)} {fmt(x1)} {fmt(y1)}")
    return " ".join(out)


def mirror_body(left, center_x=100.0):
    """left — точки от левого верха горлышка вниз до середины дна (последняя лежит на оси).
    Правая половина достраивается зеркально."""
    right = []
    for p in reversed(left[:-1]):
        corner = len(p) > 2 and p[2] == 'c'
        q = (2 * center_x - p[0], p[1])
        right.append(q + ('c',) if corner else q)
    return list(left) + right


def mirror_path_points(pts, center_x=100.0):
    return [(2 * center_x - p[0], p[1]) + tuple(p[2:]) for p in pts]


def ellipse(cx, cy, rx, ry):
    return (f"M{fmt(cx - rx)} {fmt(cy)} A{fmt(rx)} {fmt(ry)} 0 1 0 {fmt(cx + rx)} {fmt(cy)} "
            f"A{fmt(rx)} {fmt(ry)} 0 1 0 {fmt(cx - rx)} {fmt(cy)} Z")


def circle(cx, cy, r):
    return ellipse(cx, cy, r, r)
