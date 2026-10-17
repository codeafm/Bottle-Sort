using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BottleSort.Gameplay;
using UnityEditor;
using UnityEngine;

namespace BottleSort.Editor.Vfx
{
    // Генерирует весь арт бутылок и эффектов из контуров макета (Docs/Mockup/Bottle.dc.html).
    // Почему текстуры, а не меши: стекло в макете — это градиенты, полупрозрачные обводки и мягкие блики,
    // их проще и точнее один раз растеризовать, чем строить сетку. Жидкость при этом не запечена:
    // шейдер сам режет её маской inner и рисует слои, поэтому вся бутылка — один квад и один draw call.
    //
    // Из batchmode: -executeMethod BottleSort.Editor.Vfx.BottleArtGenerator.Run
    public static class BottleArtGenerator
    {
        const string ArtDir = "Assets/_Game/Art";
        const string BottlesDir = ArtDir + "/Bottles";
        const string EffectsDir = ArtDir + "/Effects";
        const string MaterialsDir = ArtDir + "/Materials";
        const string ResourcesDir = ArtDir + "/Resources";
        const string ArtAssetPath = ResourcesDir + "/VfxArt.asset";
        const string FiguresDir = ArtDir + "/Figures";
        const string FigureAssetsDir = ResourcesDir + "/Figures";
        public const string BottlePrefabPath = BottlesDir + "/Bottle.prefab";

        // Пикселей на единицу макета: классическая бутылка шириной 100 единиц выходит в 320 px текстуры
        const float BottleScale = 3.2f;
        const float CssToWorld = 2f / 70f;

        sealed class ShapeDef
        {
            public string Id, Outer, Inner, Cap, CapType, Extra = "", Deco = "", Ticks = "", Hl1 = "", Hl2 = "", Hl3 = "";
            public float Top, Bottom;
            public Vector2 Mouth;
        }

        // Контуры один в один из SH в макете
        static readonly ShapeDef[] Shapes =
        {
            new ShapeDef
            {
                Id = BottleShapeIds.Classic,
                Outer = "M30 26 V36 Q30 46 20 52 Q16 55 16 64 V206 Q16 230 40 230 H60 Q84 230 84 206 V64 Q84 55 80 52 Q70 46 70 36 V26 Z",
                Inner = "M34 28 V36 Q34 49 24 55 Q20 58 20 66 V206 Q20 226 40 226 H60 Q80 226 80 206 V66 Q80 58 76 55 Q66 49 66 36 V28 Z",
                Cap = "M26 12 H74 Q79 12 79 17 V23 Q79 28 74 28 H26 Q21 28 21 23 V17 Q21 12 26 12 Z", CapType = "glass",
                Top = 66, Bottom = 226, Hl1 = "M25 72 V204", Hl2 = "M76 76 V114", Hl3 = "M37 30 V42", Mouth = new Vector2(50, 12)
            },
            new ShapeDef
            {
                Id = BottleShapeIds.Cylinder,
                Outer = "M24 14 L18 6 H82 V14 Q78 16 78 20 V214 H92 Q94 214 94 216 V230 Q94 232 92 232 H8 Q6 232 6 230 V216 Q6 214 8 214 H22 V20 Q22 16 24 14 Z",
                Inner = "M26 16 H74 V212 H26 Z",
                Cap = "M8 216 H92 V230 H8 Z", CapType = "thick",
                Ticks = "M26 36 H36 M26 58 H32 M26 80 H36 M26 102 H32 M26 124 H36 M26 146 H32 M26 168 H36 M26 190 H32",
                Top = 36, Bottom = 212, Hl1 = "M30 30 V206", Hl2 = "M70 40 V110", Mouth = new Vector2(50, 6)
            },
            new ShapeDef
            {
                Id = BottleShapeIds.Wine,
                Outer = "M40 16 V62 C40 82 14 88 14 108 V218 Q14 230 26 230 H74 Q86 230 86 218 V108 C86 88 60 82 60 62 V16 Z",
                Inner = "M44 18 V62 C44 86 18 90 18 110 V216 Q18 226 28 226 H72 Q82 226 82 216 V110 C82 90 56 86 56 62 V18 Z",
                Cap = "M37 6 H63 Q65 6 65 8 V16 Q65 18 63 18 H37 Q35 18 35 16 V8 Q35 6 37 6 Z", CapType = "glass",
                Top = 106, Bottom = 226, Hl1 = "M22 118 Q20 170 23 214", Hl2 = "M78 124 V158", Hl3 = "M45 24 V60", Mouth = new Vector2(50, 6)
            },
            new ShapeDef
            {
                Id = BottleShapeIds.Jug,
                Outer = "M36 32 Q32 22 24 20 H76 Q68 22 64 32 V52 C64 66 86 82 86 140 C86 190 76 228 58 230 H42 C24 228 14 190 14 140 C14 82 36 66 36 52 Z",
                Inner = "M40 34 H60 V54 C60 70 82 86 82 140 C82 186 72 224 57 226 H43 C28 224 18 186 18 140 C18 86 40 70 40 54 Z",
                Cap = "", CapType = "glass", Extra = "M83 118 C101 106 100 72 64 62",
                Top = 104, Bottom = 226, Hl1 = "M23 118 Q18 160 27 205", Hl2 = "M76 110 Q80 125 80 140", Hl3 = "M41 38 V54", Mouth = new Vector2(50, 20)
            },
            new ShapeDef
            {
                Id = BottleShapeIds.Teapot,
                Outer = "M38 104 C18 114 12 146 13 172 C14 202 22 220 32 230 H72 C82 220 90 202 91 172 C92 146 86 114 66 104 Z",
                Inner = "M40 110 C23 120 17 148 18 172 C19 199 26 216 35 225 H69 C78 216 85 199 86 172 C87 148 81 120 64 110 Z",
                Cap = "M35 98 C35 76 69 76 69 98 Z M47 81 C44 70 60 70 57 81 Z M33 97 H71 Q73 97 73 99 V104 Q73 106 71 106 H33 Q31 106 31 104 V99 Q31 97 33 97 Z", CapType = "glass",
                Deco = "M17 202 C6 195 2 170 1 146 L-1 124 Q-1 118 4 119 L8 120 C8 146 11 168 19 180 Z",
                Extra = "M88 134 C107 130 108 196 86 207",
                Top = 120, Bottom = 225, Hl1 = "M22 140 Q15 175 26 212", Hl2 = "M80 124 Q86 134 87 150", Hl3 = "M42 92 Q44 84 50 82", Mouth = new Vector2(50, 74)
            },
            new ShapeDef
            {
                Id = BottleShapeIds.Glass,
                Outer = "M14 60 H86 L80 226 Q79.6 230 75 230 H25 Q20.4 230 20 226 Z",
                Inner = "M18 63 H82 L76.6 220 H23.4 Z",
                Cap = "M22 221 H78 L77.6 227 H22.4 Z", CapType = "thick",
                Top = 100, Bottom = 220, Hl1 = "M22 72 L27 214", Hl2 = "M76 76 L73 150", Mouth = new Vector2(50, 60)
            },
            new ShapeDef
            {
                Id = BottleShapeIds.Flask,
                Outer = "M40 18 V92 L8 210 Q4 230 24 230 H76 Q96 230 92 210 L60 92 V18 Z",
                Inner = "M44 20 V93 L12 210 Q9 226 25 226 H75 Q91 226 88 210 L56 93 V20 Z",
                Cap = "M36 10 H64 Q66 10 66 12 V18 Q66 20 64 20 H36 Q34 20 34 18 V12 Q34 10 36 10 Z", CapType = "glass",
                Top = 106, Bottom = 226, Hl1 = "M38 112 L18 206", Hl2 = "M62 112 L74 150", Hl3 = "M45 26 V88", Mouth = new Vector2(50, 10)
            },
            new ShapeDef
            {
                Id = BottleShapeIds.Potion,
                Outer = "M40 40 V133 A48 48 0 1 0 60 133 V40 Z",
                Inner = "M44 46 V137 A44 44 0 1 0 56 137 V46 Z",
                Cap = "M37 22 H63 Q65 22 65 24 V44 Q65 47 62 47 H38 Q35 47 35 44 V24 Q35 22 37 22 Z", CapType = "cork",
                Top = 140, Bottom = 224, Hl1 = "M14 172 Q16 146 34 136", Hl2 = "M85 190 Q83 205 75 214", Hl3 = "M45 52 V128", Mouth = new Vector2(50, 22)
            },
            new ShapeDef
            {
                Id = BottleShapeIds.Jar,
                Outer = "M18 40 H82 V52 Q90 56 90 70 V216 Q90 230 76 230 H24 Q10 230 10 216 V70 Q10 56 18 52 Z",
                Inner = "M22 44 H78 V54 Q86 58 86 72 V214 Q86 226 74 226 H26 Q14 226 14 214 V72 Q14 58 22 54 Z",
                Cap = "M15 24 Q15 22 17 22 H83 Q85 22 85 24 V40 Q85 42 83 42 H17 Q15 42 15 40 Z", CapType = "metal",
                Top = 106, Bottom = 226, Hl1 = "M18 80 V210", Hl2 = "M81 80 V130", Mouth = new Vector2(50, 22)
            },
        };

        const string IceCracks = "M30 90 L42 104 L36 118 M70 120 L58 136 L66 150 M28 170 L44 182 M74 190 L60 204 M46 60 L54 74";

        static readonly (float, Color)[] GlassFill =
        {
            (0f, Raster.Hex("#E6F5FF", 0.34f)), (0.16f, Raster.Hex("#E6F5FF", 0.12f)), (0.5f, Raster.Hex("#E6F5FF", 0.04f)),
            (0.84f, Raster.Hex("#E6F5FF", 0.1f)), (1f, Raster.Hex("#E6F5FF", 0.3f))
        };

        static readonly (float, Color)[] RimFill =
        {
            (0f, Raster.Hex("#BFE3FF", 0.75f)), (0.45f, Raster.Hex("#FFFFFF", 0.85f)), (1f, Raster.Hex("#8FC3EE", 0.7f))
        };

        // В макете толстое донце просто белое; с лёгким градиентом оно читается как стекло, а не как пластик
        static readonly (float, Color)[] ThickFill =
        {
            (0f, Raster.Hex("#D6ECFF", 0.95f)), (0.45f, Raster.Hex("#FFFFFF")), (1f, Raster.Hex("#B8DBF5", 0.95f))
        };

        static readonly (float, Color)[] CorkFill =
        {
            (0f, Raster.Hex("#7A4A22")), (0.4f, Raster.Hex("#E0AE78")), (1f, Raster.Hex("#8E5A2C"))
        };

        static readonly (float, Color)[] MetalFill =
        {
            (0f, Raster.Hex("#8A6A1E")), (0.35f, Raster.Hex("#FFE9A3")), (0.6f, Raster.Hex("#D9A93A")), (1f, Raster.Hex("#7A5A12"))
        };

        [MenuItem("BottleSort/VFX/Generate Art")]
        public static void Run()
        {
            var started = DateTime.Now;
            foreach (var dir in new[] { BottlesDir, EffectsDir, MaterialsDir, ResourcesDir, FiguresDir, FigureAssetsDir })
                Directory.CreateDirectory(dir);

            var shapeFiles = new List<(ShapeDef def, Rect outline)>();
            foreach (var def in Shapes)
                shapeFiles.Add((def, DrawBottle(def)));
            var figures = DrawFigures();
            DrawCatSprites();

            DrawGlyphs(EffectsDir + "/glyphs.png");
            DrawLock(EffectsDir + "/lock.png");
            DrawGem(EffectsDir + "/gem.png");
            DrawShelf(EffectsDir + "/shelf.png");
            DrawRipple(EffectsDir + "/ripple.png");
            DrawDrop(EffectsDir + "/drop.png");
            DrawFlash(EffectsDir + "/flash.png");
            DrawSpark(EffectsDir + "/spark.png");
            DrawConfetti(EffectsDir + "/confetti.png");
            AssetDatabase.Refresh();

            foreach (var (def, _) in shapeFiles)
            {
                ImportBottleTexture($"{BottlesDir}/{def.Id}_back.png", true);
                ImportBottleTexture($"{BottlesDir}/{def.Id}_front.png", true);
                ImportBottleTexture($"{BottlesDir}/{def.Id}_mask.png", false);
            }
            ImportGlyphs(EffectsDir + "/glyphs.png");
            ImportSprite(EffectsDir + "/lock.png", 4f / BottleGeometry.UnitSize, new Vector2(0.5f, (206f - 150f) / 122f));
            ImportSprite(EffectsDir + "/gem.png", 8f / BottleGeometry.UnitSize, new Vector2(0.5f, 0.5f));
            ImportSprite(EffectsDir + "/shelf.png", 3f / CssToWorld, new Vector2(0.5f, 46f / 80f), new Vector4(114, 0, 114, 0));
            ImportSprite(EffectsDir + "/ripple.png", 6f / CssToWorld, new Vector2(0.5f, 0.5f));
            ImportSprite(EffectsDir + "/drop.png", 8f / CssToWorld, new Vector2(0.5f, 0.5f));
            ImportSprite(EffectsDir + "/flash.png", 256f / 3f, new Vector2(0.5f, 0.5f));
            ImportSprite(EffectsDir + "/spark.png", 64f, new Vector2(0.5f, 0.5f));
            ImportSprite(EffectsDir + "/confetti.png", 64f, new Vector2(0.5f, 0.5f));
            ImportFigures(figures);
            ImportCatSprites();

            var art = LoadOrCreateArt();

            art.bottleShader = Shader.Find("BottleSort/Bottle");
            art.streamShader = Shader.Find("BottleSort/PourStream");
            art.backgroundShader = Shader.Find("BottleSort/Background");
            art.spriteMaterial = CreateMaterial(MaterialsDir + "/SpritePremultiplied.mat", 0f);
            art.additiveMaterial = CreateMaterial(MaterialsDir + "/SpriteAdditive.mat", 1f);
            art.glyphs = AssetDatabase.LoadAssetAtPath<Texture2D>(EffectsDir + "/glyphs.png");
            art.lockSprite = LoadSprite("lock");
            art.gemSprite = LoadSprite("gem");
            art.shelfSprite = LoadSprite("shelf");
            art.rippleSprite = LoadSprite("ripple");
            art.dropSprite = LoadSprite("drop");
            art.flashSprite = LoadSprite("flash");
            art.sparkSprite = LoadSprite("spark");
            art.confettiSprite = LoadSprite("confetti");
            AssignCatSprites(art);

            var shapes = new List<BottleShapeArt>();
            foreach (var (def, outline) in shapeFiles)
            {
                shapes.Add(new BottleShapeArt
                {
                    id = def.Id,
                    back = AssetDatabase.LoadAssetAtPath<Texture2D>($"{BottlesDir}/{def.Id}_back.png"),
                    front = AssetDatabase.LoadAssetAtPath<Texture2D>($"{BottlesDir}/{def.Id}_front.png"),
                    mask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{BottlesDir}/{def.Id}_mask.png"),
                    liquidTop = def.Top,
                    liquidBottom = def.Bottom,
                    mouth = def.Mouth,
                    outline = outline
                });
            }
            art.shapes = shapes.ToArray();
            EditorUtility.SetDirty(art);
            SaveFigureAssets(figures);
            AssetDatabase.SaveAssets();

            CreateBottlePrefab(art);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BottleArtGenerator] Done in {(DateTime.Now - started).TotalSeconds:0.0}s");
        }

        // Только фигурки спасений и котик, без перерисовки бутылок: -executeMethod BottleSort.Editor.Vfx.BottleArtGenerator.RunFigures
        [MenuItem("BottleSort/VFX/Generate Rescue Figures")]
        public static void RunFigures()
        {
            var started = DateTime.Now;
            foreach (var dir in new[] { EffectsDir, ResourcesDir, FiguresDir, FigureAssetsDir })
                Directory.CreateDirectory(dir);
            var figures = DrawFigures();
            DrawCatSprites();
            AssetDatabase.Refresh();
            ImportFigures(figures);
            ImportCatSprites();
            var art = LoadOrCreateArt();
            AssignCatSprites(art);
            EditorUtility.SetDirty(art);
            SaveFigureAssets(figures);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BottleArtGenerator] Figures done in {(DateTime.Now - started).TotalSeconds:0.0}s");
        }

        static VfxArt LoadOrCreateArt()
        {
            var art = AssetDatabase.LoadAssetAtPath<VfxArt>(ArtAssetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<VfxArt>();
                AssetDatabase.CreateAsset(art, ArtAssetPath);
            }
            return art;
        }

        static Rect DrawBottle(ShapeDef def)
        {
            var area = BottleGeometry.TextureArea;
            int width = Mathf.RoundToInt(area.width * BottleScale), height = Mathf.RoundToInt(area.height * BottleScale);
            Raster NewRaster() => new Raster(width, height, area.position, BottleScale);

            var outer = SvgPath.Parse(def.Outer);
            var inner = SvgPath.Parse(def.Inner);
            var cap = SvgPath.Parse(def.Cap);
            var extra = SvgPath.Parse(def.Extra);
            var deco = SvgPath.Parse(def.Deco);

            var back = NewRaster();
            var shadow = back.Fill(SvgPath.Ellipse(50, 234, 38, 6));
            back.Paint(shadow, Raster.Radial(new Vector2(50, 234), new Vector2(38, 6), (0f, new Color(0, 0, 0, 0.5f)), (1f, new Color(0, 0, 0, 0f))));
            if (!extra.IsEmpty)
                DrawTube(back, extra, 9f);
            if (!deco.IsEmpty)
            {
                back.Paint(back.Fill(deco), BoxGradient(deco, GlassFill));
                back.Paint(back.Stroke(deco, 2f), Raster.Hex("#E1F2FF"), 0.92f);
            }
            var body = back.Fill(outer);
            back.Paint(body, BoxGradient(outer, GlassFill));
            back.SavePng($"{BottlesDir}/{def.Id}_back.png");

            var front = NewRaster();
            front.Paint(front.Stroke(inner, 1.4f), Color.white, 0.35f);
            front.Paint(front.Stroke(outer, 2.6f), Raster.Hex("#DCEBFA"), 0.8f);
            if (!string.IsNullOrEmpty(def.Ticks))
                front.Paint(front.Stroke(SvgPath.Parse(def.Ticks), 1.4f), Color.white, 0.55f);
            // Блики мягкие, чтобы не выглядели белыми пятнами
            front.Paint(front.Stroke(SvgPath.Parse(def.Hl1), 5f, 2.2f), Color.white, 0.5f);
            front.Paint(front.Stroke(SvgPath.Parse(def.Hl2), 2.2f, 1.2f), Color.white, 0.28f);
            front.Paint(front.Stroke(SvgPath.Parse(def.Hl3), 2f, 1f), Color.white, 0.45f);
            if (!cap.IsEmpty)
                DrawCap(front, cap, def.CapType);
            front.SavePng($"{BottlesDir}/{def.Id}_front.png");

            var mask = NewRaster();
            var liquid = mask.Fill(inner);
            var silhouette = Raster.Union(body, mask.Fill(cap), mask.Stroke(extra, 9f), mask.Fill(deco));
            var near = mask.Blur(silhouette, 4.3f);
            var far = mask.Blur(silhouette, 10f);
            var glow = new float[near.Length];
            for (int i = 0; i < glow.Length; i++)
                glow[i] = near[i] + far[i] * 0.8f * (1f - near[i]);
            var iceLines = Raster.Union(mask.Stroke(outer, 3f), Raster.Multiply(mask.Stroke(SvgPath.Parse(IceCracks), 2f), body));
            SaveChannels($"{BottlesDir}/{def.Id}_mask.png", mask, liquid, glow, body, iceLines);

            var outline = outer.Bounds;
            outline = Encapsulate(outline, cap.Bounds, cap.IsEmpty);
            outline = Encapsulate(outline, deco.Bounds, deco.IsEmpty);
            if (!extra.IsEmpty)
            {
                var e = extra.Bounds;
                outline = Encapsulate(outline, Rect.MinMaxRect(e.xMin - 4.5f, e.yMin - 4.5f, e.xMax + 4.5f, e.yMax + 4.5f), false);
            }
            return outline;
        }

        // Ручка, носик или хобот: стеклянная трубка без жидкости
        static void DrawTube(Raster raster, SvgPath tube, float width)
        {
            raster.Paint(raster.Stroke(tube, width), Raster.Hex("#E1F2FF"), 0.85f);
            raster.Paint(raster.Stroke(tube, width - 3.5f), Raster.Hex("#5A8FC8"), 0.55f);
            raster.Paint(raster.Stroke(tube, 1.6f, 0.6f), Color.white, 0.75f);
        }

        static List<BottleShapeArt> DrawFigures()
        {
            var result = new List<BottleShapeArt>();
            foreach (var def in FigureShapes.All)
                result.Add(DrawFigure(def));
            return result;
        }

        // Фигурка в viewBox 200×240 сдвигается на 50 влево: так её середина, как у бутылок, на x = 50.
        // Жидкость — это тело, отступившее от стекла на 4 единицы, внутренний контур отдельно не рисуется.
        static BottleShapeArt DrawFigure(FigureShapes.Def def)
        {
            var area = BottleGeometry.FigureTextureArea;
            int width = Mathf.RoundToInt(area.width * BottleScale), height = Mathf.RoundToInt(area.height * BottleScale);
            Raster NewRaster() => new Raster(width, height, area.position, BottleScale);
            var shift = new Vector2(-50f, 0f);
            SvgPath Path(string d) => SvgPath.Parse(d).Translate(shift);

            var outer = Path(def.Outer);
            var deco = Path(def.Deco);
            var extra = Path(def.Extra);
            var emboss = Path(def.Emboss);
            var cap = Path(def.Cap);

            var back = NewRaster();
            var shadow = back.Fill(SvgPath.Ellipse(50, 234, 80, 7));
            back.Paint(shadow, Raster.Radial(new Vector2(50, 234), new Vector2(80, 7), (0f, new Color(0, 0, 0, 0.5f)), (1f, new Color(0, 0, 0, 0f))));
            if (!extra.IsEmpty && !def.ExtraInFront)
                DrawTube(back, extra, def.ExtraWidth);
            var body = back.Fill(outer);
            if (!deco.IsEmpty)
            {
                // Уши и лапы срастаются с телом: их край внутри тела не рисуем
                back.Paint(Raster.Subtract(back.Fill(deco), body), BoxGradient(deco, GlassFill));
                back.Paint(Raster.Subtract(back.Stroke(deco, 2f), body), Raster.Hex("#E1F2FF"), 0.92f);
            }
            back.Paint(body, BoxGradient(outer, GlassFill));
            back.SavePng($"{FiguresDir}/{def.Id}_back.png");

            var front = NewRaster();
            var wall = front.Stroke(outer, 8f);
            var innerEdge = Raster.Multiply(Raster.Subtract(front.Stroke(outer, 9.4f), wall), body);
            front.Paint(innerEdge, Color.white, 0.35f);
            front.Paint(front.Stroke(outer, 2.6f), Raster.Hex("#DCEBFA"), 0.8f);
            if (!extra.IsEmpty && def.ExtraInFront)
                DrawTube(front, extra, def.ExtraWidth);
            if (!emboss.IsEmpty)
                front.Paint(front.Stroke(emboss, 1.8f, 0.4f), Color.white, 0.42f);
            front.Paint(front.Stroke(Path(def.Hl1), 5f, 2.2f), Color.white, 0.5f);
            front.Paint(front.Stroke(Path(def.Hl2), 2.2f, 1.2f), Color.white, 0.28f);
            front.Paint(front.Stroke(Path(def.Hl3), 2f, 1f), Color.white, 0.45f);
            DrawCap(front, cap, "glass");
            front.SavePng($"{FiguresDir}/{def.Id}_front.png");

            var mask = NewRaster();
            var liquid = Raster.Subtract(body, wall);
            var silhouette = Raster.Union(body, mask.Fill(cap), mask.Stroke(extra, def.ExtraWidth), mask.Fill(deco));
            var near = mask.Blur(silhouette, 4.3f);
            var far = mask.Blur(silhouette, 10f);
            var glow = new float[near.Length];
            for (int i = 0; i < glow.Length; i++)
                glow[i] = near[i] + far[i] * 0.8f * (1f - near[i]);
            SaveChannels($"{FiguresDir}/{def.Id}_mask.png", mask, liquid, glow, body, mask.Stroke(outer, 3f));

            var outline = outer.Bounds;
            outline = Encapsulate(outline, cap.Bounds, cap.IsEmpty);
            outline = Encapsulate(outline, deco.Bounds, deco.IsEmpty);
            if (!extra.IsEmpty)
            {
                var e = extra.Bounds;
                float half = def.ExtraWidth * 0.5f;
                outline = Encapsulate(outline, Rect.MinMaxRect(e.xMin - half, e.yMin - half, e.xMax + half, e.yMax + half), false);
            }

            var bodyBounds = outer.Bounds;
            return new BottleShapeArt
            {
                id = def.Id,
                liquidTop = def.Top,
                liquidBottom = def.Bottom,
                mouth = def.Mouth + shift,
                outline = outline,
                textureArea = area,
                boxWidth = 200f,
                liquidSpan = new Vector2(bodyBounds.xMin + 4f, bodyBounds.xMax - 4f),
                isFigure = true
            };
        }

        static void ImportFigures(List<BottleShapeArt> figures)
        {
            foreach (var figure in figures)
            {
                ImportBottleTexture($"{FiguresDir}/{figure.id}_back.png", true);
                ImportBottleTexture($"{FiguresDir}/{figure.id}_front.png", true);
                ImportBottleTexture($"{FiguresDir}/{figure.id}_mask.png", false);
            }
        }

        static void SaveFigureAssets(List<BottleShapeArt> figures)
        {
            foreach (var figure in figures)
            {
                figure.back = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FiguresDir}/{figure.id}_back.png");
                figure.front = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FiguresDir}/{figure.id}_front.png");
                figure.mask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FiguresDir}/{figure.id}_mask.png");
                string assetPath = $"{FigureAssetsDir}/{figure.id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<FigureArt>(assetPath);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<FigureArt>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                }
                asset.shape = figure;
                EditorUtility.SetDirty(asset);
            }
        }

        // Котик для спасений нарисован в коробке 100×100, низ спасательного круга — точка (50, 96).
        // В фигурке единица котика — 0.9 единицы макета: голова как раз проходит в горлышко.
        const float CatScale = 0.9f;
        static readonly Vector2 CatPivot = new Vector2(50f, 96f);
        static readonly Rect RingArea = new Rect(5f, 60f, 90f, 40f);
        static readonly Rect CatArea = new Rect(15f, 5f, 70f, 95f);
        static readonly Rect PawArea = new Rect(25f, 75f, 30f, 25f);
        static readonly Rect SignArea = new Rect(-5f, -5f, 50f, 50f);
        static readonly Vector2 SignPivot = new Vector2(20f, 39f);
        const string HelpLetters = "M5 6 V16 M5 11 H10 M10 6 V16 M16.5 6 H12.5 V16 H16.5 M12.5 11 H15.5 M19 6 V16 H23 "
            + "M25 16 V6 H28 Q31 6 31 8.75 Q31 11.5 28 11.5 H25 M34.5 6 V12.5";

        static readonly Color CatOutline = Raster.Hex("#4A2E22");
        static readonly Color Fur = Raster.Hex("#F5C08A");
        static readonly Color FurDark = Raster.Hex("#E0954C");
        static readonly Color Cream = Raster.Hex("#FFF4E2");
        static readonly Color Pink = Raster.Hex("#F4A0A8");
        static readonly Color NoseColor = Raster.Hex("#EE7A88");
        static readonly Color EyeColor = Raster.Hex("#2B2633");

        static void DrawCatSprites()
        {
            DrawRing(EffectsDir + "/ring_back.png", false);
            DrawRing(EffectsDir + "/ring_front.png", true);
            DrawCat(EffectsDir + "/cat.png", false);
            DrawCat(EffectsDir + "/cat_happy.png", true);
            DrawPaw(EffectsDir + "/cat_paw.png");
            DrawHelpSign(EffectsDir + "/help_sign.png");
        }

        static void ImportCatSprites()
        {
            float ppu = BottleScale / (CatScale * BottleGeometry.UnitSize);
            ImportSprite(EffectsDir + "/ring_back.png", ppu, PivotIn(RingArea, CatPivot));
            ImportSprite(EffectsDir + "/ring_front.png", ppu, PivotIn(RingArea, CatPivot));
            ImportSprite(EffectsDir + "/cat.png", ppu, PivotIn(CatArea, CatPivot));
            ImportSprite(EffectsDir + "/cat_happy.png", ppu, PivotIn(CatArea, CatPivot));
            ImportSprite(EffectsDir + "/cat_paw.png", ppu, PivotIn(PawArea, CatPivot));
            ImportSprite(EffectsDir + "/help_sign.png", ppu, PivotIn(SignArea, SignPivot));
        }

        static void AssignCatSprites(VfxArt art)
        {
            art.ringBackSprite = LoadSprite("ring_back");
            art.ringFrontSprite = LoadSprite("ring_front");
            art.catSprite = LoadSprite("cat");
            art.catHappySprite = LoadSprite("cat_happy");
            art.catPawSprite = LoadSprite("cat_paw");
            art.helpSignSprite = LoadSprite("help_sign");
        }

        static Vector2 PivotIn(Rect area, Vector2 point) =>
            new Vector2((point.x - area.x) / area.width, (area.yMax - point.y) / area.height);

        static Raster CatRaster(Rect area) =>
            new Raster(Mathf.RoundToInt(area.width * BottleScale), Mathf.RoundToInt(area.height * BottleScale), area.position, BottleScale);

        static void FillOutlined(Raster raster, SvgPath path, Color fill, float outline = 2.2f)
        {
            raster.Paint(raster.Fill(path), fill);
            raster.Paint(raster.Stroke(path, outline), CatOutline);
        }

        static string F(float value) => value.ToString(CultureInfo.InvariantCulture);

        // Кусок спасательного круга между внешним краем и дыркой, углы от a0 до a1 (0 — вправо, y вниз)
        static SvgPath RingBand(float a0, float a1)
        {
            var contour = new SvgPath.Contour { Closed = true };
            const int steps = 32;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)steps);
                contour.Points.Add(new Vector2(50f + 40f * Mathf.Cos(a), 82f + 14f * Mathf.Sin(a)));
            }
            for (int i = steps; i >= 0; i--)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)steps);
                contour.Points.Add(new Vector2(50f + 22f * Mathf.Cos(a), 79f + 6.5f * Mathf.Sin(a)));
            }
            var path = new SvgPath();
            path.Contours.Add(contour);
            return path;
        }

        // Задняя половина круга лежит за котиком, передняя — перед ним
        static void DrawRing(string path, bool front)
        {
            var raster = CatRaster(RingArea);
            float a0 = front ? 0f : Mathf.PI, a1 = front ? Mathf.PI : Mathf.PI * 2f;
            for (int k = 0; k < 4; k++)
            {
                bool red = (k + (front ? 0 : 1)) % 2 == 0;
                var band = RingBand(Mathf.Lerp(a0, a1, k / 4f), Mathf.Lerp(a0, a1, (k + 1) / 4f));
                raster.Paint(raster.Fill(band), Raster.Hex(red ? "#E5484D" : "#FFF8F0"));
            }
            raster.Paint(raster.Stroke(RingBand(a0, a1), 2.2f), CatOutline);
            if (front)
                raster.Paint(raster.Stroke(SvgPath.Parse("M22 86 Q50 96 78 86"), 2f, 0.6f), Color.white, 0.55f);
            raster.SavePng(path);
        }

        static void DrawCat(string path, bool happy)
        {
            var r = CatRaster(CatArea);
            FillOutlined(r, SvgPath.Parse("M34 82 C32 70 38 62 50 62 C62 62 68 70 66 82 Z"), Fur);
            r.Paint(r.Fill(SvgPath.Parse("M42 82 C42 74 45 69 50 69 C55 69 58 74 58 82 Z")), Cream);
            foreach (int side in new[] { 1, -1 })
            {
                string X(float x) => F(50f + (x - 50f) * side);
                FillOutlined(r, SvgPath.Parse($"M{X(28)} 38 L{X(30)} 12 L{X(47)} 26 Z"), Fur);
                r.Paint(r.Fill(SvgPath.Parse($"M{X(31.5f)} 32 L{X(32.5f)} 18 L{X(42)} 26 Z")), Pink);
            }
            FillOutlined(r, SvgPath.Ellipse(50f, 44f, 26f, 21.5f), Fur);
            r.Paint(r.Stroke(SvgPath.Parse("M44 24 L46 31 M50 23 V31 M56 24 L54 31"), 2.4f), FurDark);
            r.Paint(r.Fill(SvgPath.Ellipse(50f, 56f, 11f, 7.5f)), Cream);
            float blush = happy ? 0.8f : 0.55f;
            r.Paint(r.Fill(SvgPath.Ellipse(34.5f, 54f, 5f, 3f)), Pink, blush);
            r.Paint(r.Fill(SvgPath.Ellipse(65.5f, 54f, 5f, 3f)), Pink, blush);

            if (happy)
            {
                r.Paint(r.Stroke(SvgPath.Parse("M35 47 Q40 40 45 47 M55 47 Q60 40 65 47"), 2.6f), EyeColor);
                var mouth = SvgPath.Parse("M45 56 Q50 66 55 56 Z");
                r.Paint(r.Fill(mouth), Raster.Hex("#B8323F"));
                r.Paint(r.Stroke(mouth, 1.6f), CatOutline);
                r.Paint(r.Fill(SvgPath.Parse("M47.5 60.5 Q50 63.5 52.5 60.5 Z")), NoseColor);
            }
            else
            {
                // Испуганный: круглые глаза с бликами, брови домиком, ротик «о»
                r.Paint(r.Fill(SvgPath.Ellipse(40f, 46f, 5f, 6.2f)), EyeColor);
                r.Paint(r.Fill(SvgPath.Ellipse(60f, 46f, 5f, 6.2f)), EyeColor);
                foreach (var (x, y, radius) in new[] { (41.8f, 43.6f, 1.9f), (61.8f, 43.6f, 1.9f), (38.6f, 48.6f, 0.9f), (58.6f, 48.6f, 0.9f) })
                    r.Paint(r.Fill(SvgPath.Ellipse(x, y, radius, radius)), Color.white);
                r.Paint(r.Stroke(SvgPath.Parse("M33 37.5 L43 34 M57 34 L67 37.5"), 1.8f), CatOutline);
                var mouth = SvgPath.Ellipse(50f, 59f, 2.4f, 2.8f);
                r.Paint(r.Fill(mouth), Raster.Hex("#8E2A36"));
                r.Paint(r.Stroke(mouth, 1.2f), CatOutline);
            }
            var nose = SvgPath.Parse("M47.6 52 H52.4 L50 54.6 Z");
            r.Paint(r.Fill(nose), NoseColor);
            r.Paint(r.Stroke(nose, 1f), CatOutline);
            r.Paint(r.Stroke(SvgPath.Parse("M37 55 L25 52.5 M37 57.5 L25 59 M63 55 L75 52.5 M63 57.5 L75 59"), 1f), CatOutline, 0.7f);
            r.SavePng(path);
        }

        // Левая лапа держится за край круга, поверх него
        static void DrawPaw(string path)
        {
            var r = CatRaster(PawArea);
            FillOutlined(r, SvgPath.Ellipse(37f, 84f, 6.5f, 4.8f), Fur, 2f);
            r.Paint(r.Stroke(SvgPath.Parse("M35 82.5 V85.5 M39 82.5 V85.5"), 1f), CatOutline);
            r.SavePng(path);
        }

        // Табличка HELP! на палочке в лапе, вращается вокруг лапы
        static void DrawHelpSign(string path)
        {
            var r = CatRaster(SignArea);
            FillOutlined(r, SvgPath.RoundedRect(18.5f, 19f, 3f, 20f, 1.2f), Raster.Hex("#B07A45"), 1.4f);
            FillOutlined(r, SvgPath.RoundedRect(1f, 1f, 38f, 20f, 4f), Raster.Hex("#FFFDF6"), 2f);
            var red = Raster.Hex("#D63A3A");
            r.Paint(r.Stroke(SvgPath.Parse(HelpLetters), 2.3f), red);
            r.Paint(r.Fill(SvgPath.Ellipse(34.5f, 15.6f, 1.3f, 1.3f)), red);
            FillOutlined(r, SvgPath.Ellipse(20f, 39f, 5.5f, 4.4f), Fur, 2f);
            r.SavePng(path);
        }

        static void DrawCap(Raster raster, SvgPath cap, string type)
        {
            var fill = raster.Fill(cap);
            switch (type)
            {
                case "thick":
                    raster.Paint(fill, BoxGradient(cap, ThickFill));
                    break;
                case "cork":
                    raster.Paint(fill, BoxGradient(cap, CorkFill));
                    raster.Paint(raster.Stroke(cap, 1.2f), Raster.Hex("#5A3312"));
                    break;
                case "metal":
                    raster.Paint(fill, BoxGradient(cap, MetalFill));
                    raster.Paint(raster.Stroke(cap, 1.2f), Raster.Hex("#6A4A0A"));
                    break;
                default:
                    raster.Paint(fill, BoxGradient(cap, RimFill));
                    raster.Paint(raster.Stroke(cap, 1.2f), Raster.Hex("#E1F2FF"));
                    break;
            }
        }

        // Градиент по рамке фигуры, как objectBoundingBox в SVG
        static Func<Vector2, Color> BoxGradient(SvgPath path, (float, Color)[] stops)
        {
            var box = path.Bounds;
            return Raster.LinearX(box.xMin, box.xMax, stops);
        }

        static Rect Encapsulate(Rect a, Rect b, bool skip)
        {
            if (skip)
                return a;
            return Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }

        static void SaveChannels(string path, Raster raster, float[] r, float[] g, float[] b, float[] a)
        {
            int w = raster.Width, h = raster.Height;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                pixels[(h - 1 - y) * w + x] = new Color(Mathf.Clamp01(r[i]), Mathf.Clamp01(g[i]), Mathf.Clamp01(b[i]), Mathf.Clamp01(a[i]));
            }
            Raster.SavePng(path, w, h, pixels);
        }

        // Цифры и «?» для льда и скрытых слоёв: скруглённые штрихи в духе Fredoka, ячейка 130 единиц
        static readonly string[] GlyphPaths =
        {
            "M50 18 A22 32 0 0 1 50 82 A22 32 0 0 1 50 18 Z",
            "M36 32 L54 18 V82",
            "M31 34 Q33 18 50 18 Q69 18 69 35 Q69 49 53 61 L31 82 H71",
            "M32 26 Q40 18 50 18 Q68 18 68 33 Q68 48 50 49 Q70 50 70 66 Q70 82 50 82 Q37 82 30 74",
            "M60 82 V18 L28 62 H74",
            "M68 18 H37 L34 46 Q41 42 50 42 Q71 42 71 62 Q71 82 50 82 Q38 82 30 75",
            "M65 22 Q58 18 50 18 Q30 18 30 52 Q30 82 50 82 Q70 82 70 63 Q70 44 50 44 Q35 44 30 58",
            "M30 18 H70 L44 82",
            "M50 48 Q32 48 32 33 Q32 18 50 18 Q68 18 68 33 Q68 48 50 48 Q30 48 30 65 Q30 82 50 82 Q70 82 70 65 Q70 48 50 48 Z",
            "M35 78 Q42 82 50 82 Q70 82 70 48 Q70 18 50 18 Q30 18 30 37 Q30 56 50 56 Q65 56 70 42",
            "M32 34 Q32 16 50 16 Q68 16 68 32 Q68 44 56 50 Q50 54 50 63",
        };

        static void DrawGlyphs(string path)
        {
            const float cell = 130f;
            var raster = new Raster(256, 192, new Vector2(-15f, -15f), 64f / cell);
            for (int i = 0; i < GlyphPaths.Length; i++)
            {
                var offset = new Vector2(i % 4 * cell, i / 4 * cell);
                var glyph = SvgPath.Parse(GlyphPaths[i]).Translate(offset);
                raster.Paint(raster.Stroke(glyph, 14f), Color.white);
                if (i == 10)
                    raster.Paint(raster.Fill(SvgPath.Ellipse(50f + offset.x, 81f + offset.y, 8f, 8f)), Color.white);
            }
            raster.SavePng(path);
        }

        static void DrawLock(string path)
        {
            var raster = new Raster(432, 488, new Vector2(-4f, 84f), 4f);
            raster.Paint(raster.Stroke(SvgPath.Parse("M2 90 L98 200 M98 90 L2 200"), 5f, 0f, 9f, 4f), Raster.Hex("#B9C3D6"));
            raster.Paint(raster.Stroke(SvgPath.Parse("M39 138 V128 A11 11 0 0 1 61 128 V138"), 5f), Raster.Hex("#D9A93A"));
            var lockBody = SvgPath.RoundedRect(32, 138, 36, 30, 6);
            raster.Paint(raster.Fill(lockBody), BoxGradient(lockBody, MetalFill));
            raster.Paint(raster.Stroke(lockBody, 1.5f), Raster.Hex("#6A4A0A"));
            raster.Paint(raster.Fill(SvgPath.Ellipse(50, 151, 4, 4)), Raster.Hex("#4A3205"));
            raster.Paint(raster.Fill(SvgPath.Parse("M48.5 152 H51.5 V161 H48.5 Z")), Raster.Hex("#4A3205"));
            raster.SavePng(path);
        }

        // Камень цветного замка: светлый, его тонирует цвет замка
        static void DrawGem(string path)
        {
            var raster = new Raster(128, 128, new Vector2(-8f, -8f), 8f);
            var gem = SvgPath.Ellipse(0, 0, 6.5f, 6.5f);
            raster.Paint(raster.Fill(gem), Raster.Radial(new Vector2(-1.5f, -2f), new Vector2(8f, 8f),
                (0f, Color.white), (0.6f, new Color(0.82f, 0.82f, 0.82f)), (1f, new Color(0.5f, 0.5f, 0.5f))));
            raster.Paint(raster.Stroke(gem, 1.2f), new Color(0.25f, 0.18f, 0.05f));
            raster.Paint(raster.Fill(SvgPath.Ellipse(-2f, -2.4f, 1.8f, 1.2f)), Color.white, 0.9f);
            raster.SavePng(path);
        }

        // Полка: 12 px высотой, скругление 6, сверху светлая кромка, под ней тень и голубое свечение.
        // Единицы — px макета, 3 px текстуры на 1 px.
        static void DrawShelf(string path)
        {
            var raster = new Raster(240, 240, new Vector2(-30f, -28f), 3f);
            var bar = SvgPath.RoundedRect(0, 0, 20, 12, 6);
            var barMask = raster.Fill(bar);
            var shadow = raster.Blur(raster.Fill(bar.Translate(new Vector2(0, 12))), 11f);
            raster.Paint(shadow, new Color(0, 0, 0, 1f), 0.35f);
            raster.Paint(raster.Blur(barMask, 11f), new Color(140 / 255f, 200 / 255f, 1f), 0.5f);
            raster.Paint(barMask, Raster.LinearY(0, 12,
                (0f, new Color(1, 1, 1, 0.5f)), (0.45f, new Color(200 / 255f, 230 / 255f, 1f, 0.18f)), (1f, new Color(1, 1, 1, 0.06f))));
            var rim = Raster.Subtract(barMask, raster.Fill(bar.Translate(new Vector2(0, 1.2f))));
            raster.Paint(rim, Color.white, 0.75f);
            raster.SavePng(path);
        }

        static void DrawRipple(string path)
        {
            var raster = new Raster(192, 84, new Vector2(-16f, -7f), 6f);
            raster.Paint(raster.Stroke(SvgPath.Ellipse(0, 0, 12f, 3.5f), 2f), Color.white);
            raster.SavePng(path);
        }

        static void DrawDrop(string path)
        {
            var raster = new Raster(48, 48, new Vector2(-3f, -3f), 8f);
            raster.Paint(raster.Fill(SvgPath.Ellipse(0, 0, 2f, 2f)), Color.white);
            raster.SavePng(path);
        }

        static void DrawFlash(string path)
        {
            var raster = new Raster(256, 256, new Vector2(-1f, -1f), 128f);
            var all = raster.Fill(SvgPath.Ellipse(0, 0, 1f, 1f));
            raster.Paint(all, p =>
            {
                float r = p.magnitude;
                float a = Mathf.Exp(-r * r * 5f) * Mathf.Clamp01((1f - r) * 6f);
                return new Color(1f, 1f, 1f, a);
            });
            raster.SavePng(path);
        }

        static void DrawSpark(string path)
        {
            var raster = new Raster(64, 64, new Vector2(-1f, -1f), 32f);
            var all = raster.Fill(SvgPath.Ellipse(0, 0, 1f, 1f));
            raster.Paint(all, p =>
            {
                float r = p.magnitude;
                float core = Mathf.Exp(-r * r * 18f);
                float rays = Mathf.Exp(-Mathf.Abs(p.x * p.y) * 60f) * Mathf.Clamp01(1f - r);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(core + rays * 0.8f));
            });
            raster.SavePng(path);
        }

        static void DrawConfetti(string path)
        {
            var raster = new Raster(32, 48, Vector2.zero, 4f);
            raster.Paint(raster.Fill(SvgPath.RoundedRect(0.5f, 0.5f, 7f, 11f, 1.4f)), Color.white);
            raster.SavePng(path);
        }

        static void ImportBottleTexture(string path, bool color)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = color;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            SetMobileFormat(importer, TextureImporterFormat.ASTC_4x4);
            importer.SaveAndReimport();
        }

        static void ImportGlyphs(string path)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void ImportSprite(string path, float pixelsPerUnit, Vector2 pivot, Vector4 border = default)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteBorder = border;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            SetMobileFormat(importer, TextureImporterFormat.ASTC_4x4);
            importer.SaveAndReimport();
        }

        static void SetMobileFormat(TextureImporter importer, TextureImporterFormat format)
        {
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.format = format;
                settings.maxTextureSize = 1024;
                importer.SetPlatformTextureSettings(settings);
            }
        }

        static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{EffectsDir}/{name}.png");

        static Material CreateMaterial(string path, float additive)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("BottleSort/UnlitPremultiplied"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("BottleSort/UnlitPremultiplied");
            material.SetFloat("_Additive", additive);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void CreateBottlePrefab(VfxArt art)
        {
            var go = new GameObject("Bottle");
            try
            {
                var view = go.AddComponent<BottleView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("art").objectReferenceValue = art;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(go, BottlePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
