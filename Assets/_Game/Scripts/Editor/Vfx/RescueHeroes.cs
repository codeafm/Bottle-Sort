using System;
using System.Collections.Generic;
using BottleSort.Gameplay;
using UnityEngine;

namespace BottleSort.Editor.Vfx
{
    // Герои спасений в том же стиле, что котик: голова-овал, тельце, круг, табличка HELP!.
    // Координаты те же (коробка 100×100, низ круга — (50, 96)), поэтому сидят и прыгают они так же, как котик.
    public static partial class BottleArtGenerator
    {
        sealed class HeroDef
        {
            public string Id;
            public Color Fur, Dark, Belly, Paw;
            // Что рисуется за головой (уши, иголки, хохолок), сама голова, мордочка поверх глаз
            public Action<Raster> Behind;
            public Action<Raster> Head;
            public Action<Raster, bool> Face;
            public Action<Raster> Body;
            public bool PandaEyes;
            public bool Whiskers;
        }

        static readonly Color HeroOutline = Raster.Hex("#4A2E22");

        static readonly HeroDef[] Heroes =
        {
            new HeroDef
            {
                Id = "puppy",
                Fur = Raster.Hex("#F6E6CF"), Dark = Raster.Hex("#9C6B45"), Belly = Raster.Hex("#FFF8EE"), Paw = Raster.Hex("#F6E6CF"),
                Head = r =>
                {
                    HeadOval(r, Raster.Hex("#F6E6CF"));
                    r.Paint(r.Fill(SvgPath.Ellipse(61f, 43f, 9f, 8f)), Raster.Hex("#E2BF93"));
                },
                Behind = r =>
                {
                    foreach (int side in new[] { 1, -1 })
                    {
                        string X(float x) => F(50f + (x - 50f) * side);
                        FillOutlined(r, SvgPath.Parse($"M{X(30)} 28 C{X(19)} 26 {X(14)} 42 {X(17)} 56 C{X(19)} 64 {X(28)} 63 {X(30)} 55 C{X(32)} 45 {X(34)} 34 {X(30)} 28 Z"),
                            Raster.Hex("#9C6B45"));
                    }
                },
                Face = (r, happy) =>
                {
                    r.Paint(r.Fill(SvgPath.Ellipse(50f, 57f, 12f, 8f)), Raster.Hex("#FFF8EE"));
                    if (happy)
                        FillOutlined(r, SvgPath.Parse("M47 60 Q50 70 53 60 Z"), Raster.Hex("#F27B8E"), 1.2f);
                    var nose = SvgPath.Parse("M45.5 51.5 Q50 49.5 54.5 51.5 Q53 56 50 56.5 Q47 56 45.5 51.5 Z");
                    r.Paint(r.Fill(nose), Raster.Hex("#3A2A26"));
                    r.Paint(r.Fill(SvgPath.Ellipse(48.4f, 51.8f, 1.4f, 0.8f)), Color.white, 0.7f);
                },
                Body = r => HeroBody(r, Raster.Hex("#F6E6CF"), Raster.Hex("#FFF8EE")),
            },
            new HeroDef
            {
                Id = "chick",
                Fur = Raster.Hex("#FFD84D"), Dark = Raster.Hex("#F0A92B"), Belly = Raster.Hex("#FFEB9A"), Paw = Raster.Hex("#FFD84D"),
                Head = r =>
                {
                    HeadOval(r, Raster.Hex("#FFD84D"));
                    // Скорлупка на макушке — так цыплёнка не спутать с утёнком
                    FillOutlined(r, SvgPath.Parse("M31 33 C33 18 67 18 69 33 L64 29 L59 34 L54 28 L49 34 L44 28 L39 34 L35 29 Z"), Raster.Hex("#FFFCF2"), 2f);
                    r.Paint(r.Stroke(SvgPath.Parse("M38 24 Q44 21 49 22"), 1.6f), Color.white, 0.8f);
                },
                Face = (r, happy) => Beak(r, happy, 6f),
                Body = r =>
                {
                    HeroBody(r, Raster.Hex("#FFD84D"), Raster.Hex("#FFEB9A"));
                    Wings(r, Raster.Hex("#F6C23A"));
                },
            },
            new HeroDef
            {
                Id = "bunny",
                Fur = Raster.Hex("#F3EEF3"), Dark = Raster.Hex("#C9BFD0"), Belly = Raster.Hex("#FFFFFF"), Paw = Raster.Hex("#F3EEF3"),
                Whiskers = true,
                Behind = r =>
                {
                    foreach (int side in new[] { 1, -1 })
                    {
                        string X(float x) => F(50f + (x - 50f) * side);
                        FillOutlined(r, SvgPath.Parse($"M{X(36)} 29 C{X(31)} 18 {X(32)} 7 {X(38)} 7 C{X(44)} 7 {X(45)} 18 {X(44)} 28 Z"), Raster.Hex("#F3EEF3"));
                        r.Paint(r.Fill(SvgPath.Parse($"M{X(37.5f)} 26 C{X(35)} 18 {X(35.5f)} 11 {X(38.5f)} 11 C{X(41.5f)} 11 {X(42)} 18 {X(41.5f)} 26 Z")), Raster.Hex("#F6B7C3"));
                    }
                },
                Head = r => HeadOval(r, Raster.Hex("#F3EEF3")),
                Face = (r, happy) =>
                {
                    r.Paint(r.Fill(SvgPath.Ellipse(50f, 57f, 10f, 6.5f)), Color.white);
                    if (happy)
                    {
                        FillOutlined(r, SvgPath.Parse("M46 57 Q50 66 54 57 Z"), Raster.Hex("#B8323F"), 1.4f);
                        FillOutlined(r, SvgPath.Parse("M48 57 H52 V61 H48 Z"), Color.white, 0.9f);
                    }
                    var nose = SvgPath.Parse("M47.8 52.5 H52.2 L50 55 Z");
                    r.Paint(r.Fill(nose), Raster.Hex("#F07F98"));
                    r.Paint(r.Stroke(nose, 1f), HeroOutline);
                },
                Body = r => HeroBody(r, Raster.Hex("#F3EEF3"), Color.white),
            },
            new HeroDef
            {
                Id = "penguin",
                Fur = Raster.Hex("#3B4458"), Dark = Raster.Hex("#262C3B"), Belly = Raster.Hex("#FAFBFF"), Paw = Raster.Hex("#3B4458"),
                Head = r =>
                {
                    HeadOval(r, Raster.Hex("#3B4458"));
                    r.Paint(r.Fill(SvgPath.Parse("M50 36 C45 30 31 32 30 46 C29 58 39 64 50 64 C61 64 71 58 70 46 C69 32 55 30 50 36 Z")), Raster.Hex("#FAFBFF"));
                },
                Face = (r, happy) => Beak(r, happy, 5f),
                Body = r =>
                {
                    HeroBody(r, Raster.Hex("#3B4458"), Raster.Hex("#FAFBFF"));
                    Wings(r, Raster.Hex("#2C3344"));
                },
            },
            new HeroDef
            {
                Id = "panda",
                Fur = Raster.Hex("#FAFAF7"), Dark = Raster.Hex("#2F2F35"), Belly = Raster.Hex("#FAFAF7"), Paw = Raster.Hex("#2F2F35"),
                PandaEyes = true,
                Behind = r =>
                {
                    FillOutlined(r, SvgPath.Ellipse(29f, 27f, 8f, 7.5f), Raster.Hex("#2F2F35"));
                    FillOutlined(r, SvgPath.Ellipse(71f, 27f, 8f, 7.5f), Raster.Hex("#2F2F35"));
                },
                Head = r =>
                {
                    HeadOval(r, Raster.Hex("#FAFAF7"));
                    r.Paint(r.Fill(Rotated(SvgPath.Ellipse(0f, 0f, 7.5f, 9f), new Vector2(39f, 47f), 25f)), Raster.Hex("#2F2F35"));
                    r.Paint(r.Fill(Rotated(SvgPath.Ellipse(0f, 0f, 7.5f, 9f), new Vector2(61f, 47f), -25f)), Raster.Hex("#2F2F35"));
                },
                Face = (r, happy) =>
                {
                    if (happy)
                    {
                        var mouth = SvgPath.Parse("M45.5 57 Q50 66 54.5 57 Z");
                        FillOutlined(r, mouth, Raster.Hex("#B8323F"), 1.4f);
                    }
                    var nose = SvgPath.Parse("M46.5 52 Q50 50.5 53.5 52 Q52.5 55 50 55.5 Q47.5 55 46.5 52 Z");
                    r.Paint(r.Fill(nose), Raster.Hex("#2F2F35"));
                },
                Body = r =>
                {
                    HeroBody(r, Raster.Hex("#2F2F35"), Raster.Hex("#FAFAF7"));
                },
            },
            new HeroDef
            {
                Id = "fox",
                Fur = Raster.Hex("#F08A3C"), Dark = Raster.Hex("#3A2A26"), Belly = Raster.Hex("#FFF4E6"), Paw = Raster.Hex("#F08A3C"),
                Whiskers = true,
                Behind = r =>
                {
                    foreach (int side in new[] { 1, -1 })
                    {
                        string X(float x) => F(50f + (x - 50f) * side);
                        FillOutlined(r, SvgPath.Parse($"M{X(26)} 40 L{X(25)} 9 L{X(46)} 27 Z"), Raster.Hex("#F08A3C"));
                        r.Paint(r.Fill(SvgPath.Parse($"M{X(29)} 33 L{X(28.5f)} 16 L{X(41)} 27 Z")), Raster.Hex("#FFF4E6"));
                        r.Paint(r.Fill(SvgPath.Parse($"M{X(25.2f)} 16 L{X(25)} 9 L{X(32)} 15 Z")), Raster.Hex("#3A2A26"));
                    }
                },
                Head = r =>
                {
                    HeadOval(r, Raster.Hex("#F08A3C"));
                    r.Paint(r.Fill(SvgPath.Parse("M25 49 Q36 52 44 58 Q50 54 56 58 Q64 52 75 49 Q72 64 50 65.5 Q28 64 25 49 Z")), Raster.Hex("#FFF4E6"));
                },
                Face = (r, happy) =>
                {
                    if (happy)
                        FillOutlined(r, SvgPath.Parse("M45.5 57 Q50 65 54.5 57 Z"), Raster.Hex("#B8323F"), 1.4f);
                    var nose = SvgPath.Ellipse(50f, 53.5f, 3f, 2.2f);
                    r.Paint(r.Fill(nose), Raster.Hex("#3A2A26"));
                },
                Body = r =>
                {
                    FillOutlined(r, SvgPath.Parse("M64 80 C78 80 82 66 76 58 C74 66 70 70 62 72 Z"), Raster.Hex("#F08A3C"));
                    r.Paint(r.Fill(SvgPath.Parse("M74.6 60.5 C75.5 63 76 64 76 58 C80 64 79 70 74 72 C76 68 76 64 74.6 60.5 Z")), Raster.Hex("#FFF4E6"));
                    HeroBody(r, Raster.Hex("#F08A3C"), Raster.Hex("#FFF4E6"));
                },
            },
            new HeroDef
            {
                Id = "duckling",
                Fur = Raster.Hex("#FFE476"), Dark = Raster.Hex("#F2B33A"), Belly = Raster.Hex("#FFF2B8"), Paw = Raster.Hex("#FFE476"),
                Behind = r => r.Paint(r.Stroke(SvgPath.Parse("M50 25 Q46 15 53 14 Q57 14 55 19"), 3f), Raster.Hex("#F2B33A")),
                Head = r => HeadOval(r, Raster.Hex("#FFE476")),
                Face = (r, happy) =>
                {
                    var bill = happy
                        ? SvgPath.Parse("M40 54 Q50 49 60 54 Q60 58 50 58 Q40 58 40 54 Z M42 58 Q50 66 58 58 Q50 61 42 58 Z")
                        : SvgPath.Parse("M40 54.5 Q50 49 60 54.5 Q61 60 50 60.5 Q39 60 40 54.5 Z");
                    FillOutlined(r, bill, Raster.Hex("#F7A034"), 1.6f);
                    r.Paint(r.Stroke(SvgPath.Parse("M43 56.5 Q50 58 57 56.5"), 1f), HeroOutline, 0.6f);
                    r.Paint(r.Fill(SvgPath.Ellipse(46.5f, 53.2f, 0.9f, 0.7f)), HeroOutline);
                    r.Paint(r.Fill(SvgPath.Ellipse(53.5f, 53.2f, 0.9f, 0.7f)), HeroOutline);
                },
                Body = r =>
                {
                    HeroBody(r, Raster.Hex("#FFE476"), Raster.Hex("#FFF2B8"));
                    Wings(r, Raster.Hex("#F7D15A"));
                },
            },
            new HeroDef
            {
                Id = "hedgehog",
                Fur = Raster.Hex("#F2D7AE"), Dark = Raster.Hex("#7B5236"), Belly = Raster.Hex("#FFF1DA"), Paw = Raster.Hex("#F2D7AE"),
                Behind = r =>
                {
                    // Иголки — зубцы по дуге вокруг верха головы
                    var spikes = new SvgPath();
                    var contour = new SvgPath.Contour { Closed = true };
                    const int teeth = 13;
                    for (int i = 0; i <= teeth * 2; i++)
                    {
                        float a = Mathf.Lerp(Mathf.PI * 1.06f, Mathf.PI * 1.94f, i / (float)(teeth * 2));
                        float radius = i % 2 == 0 ? 1f : 1.34f;
                        contour.Points.Add(new Vector2(50f + Mathf.Cos(a) * 28f * radius, 47f + Mathf.Sin(a) * 23f * radius));
                    }
                    contour.Points.Add(new Vector2(70f, 60f));
                    contour.Points.Add(new Vector2(30f, 60f));
                    spikes.Contours.Add(contour);
                    FillOutlined(r, spikes, Raster.Hex("#7B5236"));
                    r.Paint(r.Stroke(SvgPath.Parse("M34 24 L37 30 M44 18 L45 25 M56 18 L55 25 M66 24 L63 30"), 1.4f), Raster.Hex("#A7774F"));
                },
                Head = r =>
                {
                    FillOutlined(r, SvgPath.Parse("M27 50 C27 36 37 32 50 32 C63 32 73 36 73 50 C73 60 63 65.5 50 65.5 C37 65.5 27 60 27 50 Z"), Raster.Hex("#F2D7AE"));
                    r.Paint(r.Stroke(SvgPath.Parse("M30 38 Q40 30 50 34 Q60 30 70 38"), 2.4f), Raster.Hex("#7B5236"));
                },
                Face = (r, happy) =>
                {
                    if (happy)
                        FillOutlined(r, SvgPath.Parse("M45.5 58 Q50 66 54.5 58 Z"), Raster.Hex("#B8323F"), 1.4f);
                    r.Paint(r.Fill(SvgPath.Ellipse(50f, 54f, 3.4f, 2.8f)), Raster.Hex("#2E2321"));
                    r.Paint(r.Fill(SvgPath.Ellipse(49f, 53f, 1.1f, 0.8f)), Color.white, 0.7f);
                },
                Body = r =>
                {
                    FillOutlined(r, SvgPath.Parse("M32 82 C28 72 34 60 50 60 C66 60 72 72 68 82 Z"), Raster.Hex("#7B5236"));
                    r.Paint(r.Fill(SvgPath.Parse("M42 82 C42 74 45 68 50 68 C55 68 58 74 58 82 Z")), Raster.Hex("#FFF1DA"));
                },
            },
            new HeroDef
            {
                Id = "owlet",
                Fur = Raster.Hex("#B08A6A"), Dark = Raster.Hex("#7A5B44"), Belly = Raster.Hex("#F1E4D2"), Paw = Raster.Hex("#B08A6A"),
                Behind = r =>
                {
                    FillOutlined(r, SvgPath.Parse("M27 34 L25 18 L38 27 Z"), Raster.Hex("#B08A6A"));
                    FillOutlined(r, SvgPath.Parse("M73 34 L75 18 L62 27 Z"), Raster.Hex("#B08A6A"));
                },
                Head = r =>
                {
                    HeadOval(r, Raster.Hex("#B08A6A"));
                    FillOutlined(r, SvgPath.Ellipse(39.5f, 46.5f, 9.5f, 9.5f), Raster.Hex("#F1E4D2"), 1.4f);
                    FillOutlined(r, SvgPath.Ellipse(60.5f, 46.5f, 9.5f, 9.5f), Raster.Hex("#F1E4D2"), 1.4f);
                },
                Face = (r, happy) =>
                {
                    var beak = SvgPath.Parse("M46.8 54 H53.2 L50 60 Z");
                    FillOutlined(r, beak, Raster.Hex("#F5B33E"), 1.2f);
                    if (happy)
                        r.Paint(r.Stroke(SvgPath.Parse("M44 61.5 Q50 64.5 56 61.5"), 1.4f), HeroOutline);
                },
                Body = r =>
                {
                    HeroBody(r, Raster.Hex("#B08A6A"), Raster.Hex("#F1E4D2"));
                    r.Paint(r.Stroke(SvgPath.Parse("M46 73 L48 75 L50 73 L52 75 L54 73 M46 78 L48 80 L50 78 L52 80 L54 78"), 1f), Raster.Hex("#A27E60"));
                    Wings(r, Raster.Hex("#97735A"));
                },
            },
        };

        static void HeadOval(Raster r, Color fur) => FillOutlined(r, SvgPath.Ellipse(50f, 44f, 26f, 21.5f), fur);

        static void HeroBody(Raster r, Color fur, Color belly)
        {
            FillOutlined(r, SvgPath.Parse("M34 82 C32 70 38 62 50 62 C62 62 68 70 66 82 Z"), fur);
            r.Paint(r.Fill(SvgPath.Parse("M42 82 C42 74 45 69 50 69 C55 69 58 74 58 82 Z")), belly);
        }

        static void Wings(Raster r, Color color)
        {
            FillOutlined(r, SvgPath.Parse("M36 70 C30 72 29 78 33 81 C36 79 38 75 36 70 Z"), color, 1.6f);
            FillOutlined(r, SvgPath.Parse("M64 70 C70 72 71 78 67 81 C64 79 62 75 64 70 Z"), color, 1.6f);
        }

        // Клювик: испуганный — приоткрыт, весёлый — раскрыт шире
        static void Beak(Raster r, bool happy, float width)
        {
            float half = width * 0.5f;
            var top = SvgPath.Parse(FormattableString.Invariant($"M{50 - half} 54 Q50 49.5 {50 + half} 54 L50 56.5 Z"));
            float inset = happy ? 0.5f : 1f, tip = happy ? 61.5f : 59f;
            var bottom = SvgPath.Parse(FormattableString.Invariant($"M{50 - half + inset} 56 L{50 + half - inset} 56 L50 {tip} Z"));
            if (happy)
                r.Paint(r.Fill(SvgPath.Parse(FormattableString.Invariant($"M{50 - half + 1f} 55.5 L{50 + half - 1f} 55.5 L50 59.5 Z"))), Raster.Hex("#B8323F"));
            FillOutlined(r, bottom, Raster.Hex("#E9862A"), 1.2f);
            FillOutlined(r, top, Raster.Hex("#F7A034"), 1.2f);
        }

        static SvgPath Rotated(SvgPath path, Vector2 center, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            var result = new SvgPath();
            foreach (var contour in path.Contours)
            {
                var c = new SvgPath.Contour { Closed = contour.Closed };
                foreach (var p in contour.Points)
                    c.Points.Add(center + new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos));
                result.Contours.Add(c);
            }
            return result;
        }

        // Глаза как у котика: испуганные круглые с бликами и брови домиком, весёлые — дужки.
        // У панды глаза на чёрных пятнах, поэтому зрачок обведён светлым.
        static void HeroEyes(Raster r, bool happy, bool panda)
        {
            var eyeColor = Raster.Hex("#2B2633");
            if (happy)
            {
                r.Paint(r.Stroke(SvgPath.Parse("M35 47 Q40 40 45 47 M55 47 Q60 40 65 47"), 2.6f), panda ? Color.white : eyeColor);
                return;
            }
            foreach (float x in new[] { 40f, 60f })
            {
                if (panda)
                    r.Paint(r.Fill(SvgPath.Ellipse(x, 46f, 5.6f, 6.8f)), Color.white);
                r.Paint(r.Fill(SvgPath.Ellipse(x, 46f, 5f, 6.2f)), eyeColor);
            }
            foreach (var (x, y, radius) in new[] { (41.8f, 43.6f, 1.9f), (61.8f, 43.6f, 1.9f), (38.6f, 48.6f, 0.9f), (58.6f, 48.6f, 0.9f) })
                r.Paint(r.Fill(SvgPath.Ellipse(x, y, radius, radius)), Color.white);
            r.Paint(r.Stroke(SvgPath.Parse("M33 37.5 L43 34 M57 34 L67 37.5"), 1.8f), panda ? Raster.Hex("#2F2F35") : HeroOutline);
        }

        static void DrawHero(HeroDef hero, string path, bool happy)
        {
            var r = CatRaster(CatArea);
            hero.Body(r);
            hero.Behind?.Invoke(r);
            hero.Head(r);
            float blush = happy ? 0.8f : 0.55f;
            var pink = Raster.Hex("#F4A0A8");
            r.Paint(r.Fill(SvgPath.Ellipse(34.5f, 55f, 5f, 3f)), pink, blush);
            r.Paint(r.Fill(SvgPath.Ellipse(65.5f, 55f, 5f, 3f)), pink, blush);
            HeroEyes(r, happy, hero.PandaEyes);
            if (!happy && hero.Id != "duckling" && hero.Id != "owlet" && hero.Id != "chick" && hero.Id != "penguin")
            {
                var mouth = SvgPath.Ellipse(50f, 60f, 2.2f, 2.6f);
                r.Paint(r.Fill(mouth), Raster.Hex("#8E2A36"));
                r.Paint(r.Stroke(mouth, 1.1f), HeroOutline);
            }
            hero.Face(r, happy);
            if (hero.Whiskers)
                r.Paint(r.Stroke(SvgPath.Parse("M37 56 L25 53.5 M37 58.5 L25 60 M63 56 L75 53.5 M63 58.5 L75 60"), 1f), HeroOutline, 0.6f);
            r.SavePng(path);
        }

        static void DrawHeroPaw(HeroDef hero, string path)
        {
            var r = CatRaster(PawArea);
            FillOutlined(r, SvgPath.Ellipse(37f, 84f, 6.5f, 4.8f), hero.Paw, 2f);
            r.Paint(r.Stroke(SvgPath.Parse("M35 82.5 V85.5 M39 82.5 V85.5"), 1f), HeroOutline);
            r.SavePng(path);
        }

        static void DrawHeroSign(HeroDef hero, string path)
        {
            var r = CatRaster(SignArea);
            FillOutlined(r, SvgPath.RoundedRect(18.5f, 19f, 3f, 20f, 1.2f), Raster.Hex("#B07A45"), 1.4f);
            FillOutlined(r, SvgPath.RoundedRect(1f, 1f, 38f, 20f, 4f), Raster.Hex("#FFFDF6"), 2f);
            var red = Raster.Hex("#D63A3A");
            r.Paint(r.Stroke(SvgPath.Parse(HelpLetters), 2.3f), red);
            r.Paint(r.Fill(SvgPath.Ellipse(34.5f, 15.6f, 1.3f, 1.3f)), red);
            FillOutlined(r, SvgPath.Ellipse(20f, 39f, 5.5f, 4.4f), hero.Paw, 2f);
            r.SavePng(path);
        }

        static void ImportHeroSprites(float ppu)
        {
            foreach (var hero in Heroes)
            {
                ImportSprite($"{EffectsDir}/{hero.Id}.png", ppu, PivotIn(CatArea, CatPivot));
                ImportSprite($"{EffectsDir}/{hero.Id}_happy.png", ppu, PivotIn(CatArea, CatPivot));
                ImportSprite($"{EffectsDir}/{hero.Id}_paw.png", ppu, PivotIn(PawArea, CatPivot));
                ImportSprite($"{EffectsDir}/{hero.Id}_sign.png", ppu, PivotIn(SignArea, SignPivot));
            }
        }

        static void AssignHeroSprites(VfxArt art)
        {
            var heroes = new List<RescueHeroArt>();
            foreach (var hero in Heroes)
                heroes.Add(new RescueHeroArt
                {
                    id = hero.Id,
                    sprite = LoadSprite(hero.Id),
                    happySprite = LoadSprite(hero.Id + "_happy"),
                    pawSprite = LoadSprite(hero.Id + "_paw"),
                    signSprite = LoadSprite(hero.Id + "_sign")
                });
            art.heroes = heroes.ToArray();
        }

        static void DrawHeroSprites()
        {
            foreach (var hero in Heroes)
            {
                DrawHero(hero, $"{EffectsDir}/{hero.Id}.png", false);
                DrawHero(hero, $"{EffectsDir}/{hero.Id}_happy.png", true);
                DrawHeroPaw(hero, $"{EffectsDir}/{hero.Id}_paw.png");
                DrawHeroSign(hero, $"{EffectsDir}/{hero.Id}_sign.png");
            }
        }
    }
}
