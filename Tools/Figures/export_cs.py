"""Пишет Assets/_Game/Scripts/Editor/Vfx/FigureShapes.cs из figures.py.
Запуск: python Tools/Figures/export_cs.py
"""
import os
import sys

from figures import FIGURES, ORDER, RIM

NOTES = {
    "hippo": "Бегемот: узкий лоб над огромными щеками, бугорки глаз и круглые ушки сверху, ноздри и улыбка на стекле",
    "whale": "Кит: капля с покатыми плечами, хвост вверх и вбок, горлышко-фонтанчик",
    "owl": "Сова: груша с перьевыми ушками, лицевой диск и кольца глаз на стекле, крылья и лапки",
    "turtle": "Черепаха: купол панциря, грани только по краю, голова слева, короткие лапы",
    "elephant": "Слон: большие уши по бокам, поднятый хобот — пустая стеклянная трубка поверх тела",
    "fish": "Рыба: круглое тело, большой раздвоенный хвост, губы и плавники, горлышко-плавник",
    "fox": "Лиса: высокие острые уши, пушистый хвост вокруг бока, маска на стекле",
    "bear": "Медведь: тело-бочонок, круглые уши, мордочка и лапы на стекле",
    "rabbit": "Кролик: круглое тело, длинные уши из пустой стеклянной трубки, хвост-помпон",
    "dragon": "Дракон: крылья, рожки, пластины на брюхе, хвост с крючком",
}


def cs_string(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


def wrap(name, value, indent):
    """Длинный путь режем на несколько строк, чтобы файл читался."""
    pad = " " * indent
    if not value:
        return None
    parts = []
    line = ""
    for token in value.split(" "):
        if len(line) + len(token) + 1 > 104 and line:
            parts.append(line + " ")
            line = token
        else:
            line = token if not line else line + " " + token
    parts.append(line)
    if len(parts) == 1:
        return f"{pad}{name} = {cs_string(parts[0])},"
    out = [f"{pad}{name} = {cs_string(parts[0])}"]
    for p in parts[1:-1]:
        out.append(f"{pad}    + {cs_string(p)}")
    out.append(f"{pad}    + {cs_string(parts[-1])},")
    return "\n".join(out)


def main(out):
    lines = [
        "using UnityEngine;",
        "",
        "namespace BottleSort.Editor.Vfx",
        "{",
        "    // Фигурки спасений, BOTTLE_MODELS §5: viewBox 200×240, дно на y = 230, горлышко посередине сверху.",
        "    // Outer — стекло с жидкостью, Deco — стеклянные уши, лапы и хвосты без жидкости, Extra — пустые трубки,",
        "    // Emboss — рельеф на стекле поверх жидкости. Контуры собраны из сплайна по опорным точкам.",
        "    static class FigureShapes",
        "    {",
        "        public sealed class Def",
        "        {",
        "            public string Id, Outer, Deco = \"\", Extra = \"\", Emboss = \"\", Cap = Rim, Hl1 = \"\", Hl2 = \"\", Hl3 = \"\";",
        "            public float ExtraWidth = 9f;",
        "            public bool ExtraInFront;",
        "            public float Top = 84f, Bottom = 226f;",
        "            public Vector2 Mouth = new Vector2(100f, 16f);",
        "        }",
        "",
        f"        public const string Rim = {cs_string(RIM)};",
        "",
        "        public static readonly Def[] All =",
        "        {",
    ]
    for fid in ORDER:
        f = FIGURES[fid]
        lines.append(f"            // {NOTES[fid]}")
        lines.append("            new Def")
        lines.append("            {")
        body = [f'                Id = "{fid}",']
        for key, name in (("outer", "Outer"), ("deco", "Deco"), ("extra", "Extra"), ("emboss", "Emboss")):
            w = wrap(name, f[key], 16)
            if w:
                body.append(w)
        if f["cap"] != RIM:
            body.append(wrap("Cap", f["cap"], 16))
        for key, name in (("hl1", "Hl1"), ("hl2", "Hl2"), ("hl3", "Hl3")):
            w = wrap(name, f[key], 16)
            if w:
                body.append(w)
        if f["extra_width"] != 9:
            body.append(f"                ExtraWidth = {f['extra_width']}f,")
        if f["extra_front"]:
            body.append("                ExtraInFront = true,")
        if f["top"] != 84 or f["bot"] != 226:
            body.append(f"                Top = {f['top']}f, Bottom = {f['bot']}f,")
        body[-1] = body[-1].rstrip(",")
        lines.extend(body)
        lines.append("            },")
    lines[-1] = lines[-1].rstrip(",")
    lines += ["        };", "    }", "}", ""]
    open(out, "w", encoding="utf-8", newline="\n").write("\n".join(lines))


if __name__ == "__main__":
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    default = os.path.join(root, "Assets", "_Game", "Scripts", "Editor", "Vfx", "FigureShapes.cs")
    main(sys.argv[1] if len(sys.argv) > 1 else default)
