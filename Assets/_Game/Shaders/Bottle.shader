// Бутылка целиком за один проход: свечение, стекло сзади, жидкость по маске, стекло спереди, лёд.
// Координаты внутри — единицы макета (viewBox 100x240), _Area — какой кусок их покрывают текстуры.
// У фигурки спасения стекло спереди рисует отдельный спрайт поверх котика, поэтому здесь его можно выключить.
Shader "BottleSort/Bottle"
{
    Properties
    {
        [NoScaleOffset] _BackTex ("Glass behind liquid", 2D) = "black" {}
        [NoScaleOffset] _FrontTex ("Glass in front", 2D) = "black" {}
        [NoScaleOffset] _MaskTex ("R liquid, G glow, B body, A ice lines", 2D) = "black" {}
        [NoScaleOffset] _GlyphTex ("Glyphs 0-9 and ?", 2D) = "black" {}
        _Color0 ("Layer 0", Color) = (1,1,1,1)
        _Color1 ("Layer 1", Color) = (1,1,1,1)
        _Color2 ("Layer 2", Color) = (1,1,1,1)
        _Color3 ("Layer 3", Color) = (1,1,1,1)
        _Color4 ("Layer 4", Color) = (1,1,1,1)
        _Color5 ("Layer 5", Color) = (1,1,1,1)
        _Color6 ("Layer 6", Color) = (1,1,1,1)
        _Color7 ("Layer 7", Color) = (1,1,1,1)
        _Color8 ("Layer 8", Color) = (1,1,1,1)
        _Color9 ("Layer 9", Color) = (1,1,1,1)
        _Fill ("Fill in layers", Float) = 0
        _Capacity ("Capacity", Float) = 4
        _Hidden ("Hidden layers", Float) = 0
        _LiquidTop ("Liquid top", Float) = 66
        _LiquidBottom ("Liquid bottom", Float) = 226
        _GlowColor ("Glow (a = strength)", Color) = (0,0,0,0)
        _Tint ("Glass tint (a = strength)", Color) = (0,0,0,0)
        _Ice ("Ice", Range(0, 1)) = 0
        _IceTurns ("Ice turns", Float) = 0
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Area ("Texture area in units (x, y, w, h)", Vector) = (-30, -30, 160, 300)
        _ToneX ("Liquid shading from x to x", Vector) = (16, 84, 0, 0)
        _ToneNeck ("Neck shading x0, x1 above y = z, blends to _ToneX by y = w", Vector) = (0, 0, 0, 0)
        _Ghost ("Recipe layers shown pale", Float) = 0
        _FrontAlpha ("Front glass in this pass", Range(0, 1)) = 1
        _Edges0 ("Layer tops 1-4, from liquid bottom", Vector) = (40, 80, 120, 160)
        _Edges1 ("Layer tops 5-8", Vector) = (160, 160, 160, 160)
        _Edges2 ("Layer tops 9-10", Vector) = (160, 160, 160, 160)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BackTex); SAMPLER(sampler_BackTex);
            TEXTURE2D(_FrontTex);
            TEXTURE2D(_MaskTex);
            TEXTURE2D(_GlyphTex); SAMPLER(sampler_GlyphTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color0, _Color1, _Color2, _Color3, _Color4, _Color5, _Color6, _Color7, _Color8, _Color9;
                float _Fill, _Capacity, _Hidden, _LiquidTop, _LiquidBottom;
                half4 _GlowColor, _Tint;
                float _Ice, _IceTurns, _Alpha;
                float4 _Area, _ToneX, _ToneNeck;
                float _Ghost, _FrontAlpha;
                float4 _Edges0, _Edges1, _Edges2;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 units : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.units = float2(_Area.x + v.uv.x * _Area.z, _Area.y + (1.0 - v.uv.y) * _Area.w);
                return o;
            }

            half3 LayerColor(float k)
            {
                half3 c = _Color0.rgb;
                c = k >= 1 ? _Color1.rgb : c;
                c = k >= 2 ? _Color2.rgb : c;
                c = k >= 3 ? _Color3.rgb : c;
                c = k >= 4 ? _Color4.rgb : c;
                c = k >= 5 ? _Color5.rgb : c;
                c = k >= 6 ? _Color6.rgb : c;
                c = k >= 7 ? _Color7.rgb : c;
                c = k >= 8 ? _Color8.rgb : c;
                c = k >= 9 ? _Color9.rgb : c;
                return c;
            }

            // Горизонтальный градиент слоя из макета: тёмный край слева, мягкий блик, плоская середина, край справа темнее
            half3 Tone(half3 c, float x, float y)
            {
                float2 span = _ToneX.xy;
                if (_ToneNeck.w > _ToneNeck.z)
                    span = lerp(_ToneNeck.xy, _ToneX.xy, saturate((y - _ToneNeck.z) / (_ToneNeck.w - _ToneNeck.z)));
                float t = saturate((x - span.x) / (span.y - span.x));
                half3 highlight = lerp(c, 1.0, 0.28);
                if (t < 0.12) return lerp(c * 0.79, c, t / 0.12);
                if (t < 0.26) return lerp(c, highlight, (t - 0.12) / 0.14);
                if (t < 0.42) return lerp(highlight, c, (t - 0.26) / 0.16);
                if (t < 0.8) return c;
                return lerp(c, c * 0.62, (t - 0.8) / 0.2);
            }

            half3 HiddenTone(float x)
            {
                float t = saturate((x - _ToneX.x + 8.0) / (_ToneX.y - _ToneX.x + 16.0));
                return lerp(half3(0.137, 0.169, 0.251), half3(0.373, 0.435, 0.580), 1.0 - abs(t * 2.0 - 1.0));
            }

            float GlyphAlpha(float index, float2 local)
            {
                if (any(local < 0.0) || any(local > 1.0))
                    return 0.0;
                float col = fmod(index, 4.0);
                float row = floor(index / 4.0);
                float2 uv = float2((col + local.x) / 4.0, 1.0 - (row + local.y) / 3.0);
                return SAMPLE_TEXTURE2D_LOD(_GlyphTex, sampler_GlyphTex, uv, 0).a;
            }

            // Слои не обязательно равной высоты: у круглой колбы в шаре они ниже, в горле выше.
            // Высоты верхних краёв слоёв считает BottleView, k-й слой лежит между EdgeAt(k) и EdgeAt(k + 1).
            float EdgeAt(float k)
            {
                float e = 0.0;
                e = k >= 1 ? _Edges0.x : e;
                e = k >= 2 ? _Edges0.y : e;
                e = k >= 3 ? _Edges0.z : e;
                e = k >= 4 ? _Edges0.w : e;
                e = k >= 5 ? _Edges1.x : e;
                e = k >= 6 ? _Edges1.y : e;
                e = k >= 7 ? _Edges1.z : e;
                e = k >= 8 ? _Edges1.w : e;
                e = k >= 9 ? _Edges2.x : e;
                e = k >= 10 ? _Edges2.y : e;
                return e;
            }

            float LayerOf(float d)
            {
                float k = step(_Edges0.x, d) * step(1.5, _Capacity) + step(_Edges0.y, d) * step(2.5, _Capacity)
                        + step(_Edges0.z, d) * step(3.5, _Capacity) + step(_Edges0.w, d) * step(4.5, _Capacity)
                        + step(_Edges1.x, d) * step(5.5, _Capacity) + step(_Edges1.y, d) * step(6.5, _Capacity)
                        + step(_Edges1.z, d) * step(7.5, _Capacity) + step(_Edges1.w, d) * step(8.5, _Capacity)
                        + step(_Edges2.x, d) * step(9.5, _Capacity);
                return k;
            }

            float LevelHeight(float fill)
            {
                float k = floor(fill);
                return lerp(EdgeAt(k), EdgeAt(k + 1.0), fill - k);
            }

            half3 LayerShaded(float k, float d, float x, float level)
            {
                half3 c = Tone(LayerColor(k), x, _LiquidBottom - d);
                float bottom = EdgeAt(k);
                float lh = max(EdgeAt(k + 1.0) - bottom, 0.5);
                // Тонкий свет у верхней кромки слоя и едва заметная тень у нижней — слои читаются, но вода не темнеет ко дну
                float layerTop = min(bottom + lh, level);
                float t = saturate((layerTop - d) / lh);
                float light = t < 0.14 ? lerp(0.12, 0.02, t / 0.14) : lerp(0.02, 0.0, saturate((t - 0.14) / 0.66));
                c = lerp(c, 1.0, light);
                c *= 1.0 - saturate((t - 0.8) / 0.2) * 0.07;

                float hidden = saturate(_Hidden - k);
                if (hidden > 0.0)
                {
                    half3 veil = HiddenTone(x);
                    float2 center = float2(50.0, _LiquidBottom - bottom - 0.5 * lh);
                    float cell = 0.78 * min(lh, 32.0);
                    veil = lerp(veil, 1.0, 0.85 * GlyphAlpha(10.0, (float2(x, _LiquidBottom - d) - center) / cell + 0.5));
                    c = lerp(c, veil, hidden);
                }
                return c;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 u = i.units;
                half4 back = SAMPLE_TEXTURE2D(_BackTex, sampler_BackTex, i.uv);
                half4 front = SAMPLE_TEXTURE2D(_FrontTex, sampler_BackTex, i.uv);
                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_BackTex, i.uv);

                half4 col = back;
                half tintA = _Tint.a * mask.b;
                col = half4(_Tint.rgb * tintA, tintA) + col * (1.0 - tintA);

                float level = LevelHeight(_Fill);
                float d = _LiquidBottom - u.y;
                float fw = max(fwidth(d), 1e-4);
                float toneWidth = (_ToneX.y - _ToneX.x) / 68.0;

                // Рецепт фигурки: будущие слои бледным цветом и тонкой линией по верху каждого
                float ghostTop = LevelHeight(_Ghost);
                if (mask.r > 0.002 && d > level - 1.0 && d < ghostTop + 1.0)
                {
                    float k = LayerOf(d);
                    float inside = saturate((d - level) / fw + 0.5) * saturate((ghostTop - d) / fw + 0.5);
                    float edge = EdgeAt(k + 1.0) - d;
                    float lineA = saturate((0.7 - abs(edge - 0.35)) / fw + 0.5) * 0.2;
                    half ghostA = mask.r * inside * (0.25 + lineA);
                    col = half4(LayerColor(k) * ghostA, ghostA) + col * (1.0 - ghostA);
                }

                if (mask.r > 0.002 && level > 0.01 && d > -1.0 && d < level + 1.0)
                {
                    float k = LayerOf(d);
                    half3 liquid = LayerShaded(k, d, u.x, level);

                    // Сглаживаем границу соседних слоёв, иначе при наклоне она идёт лесенкой
                    float below = d - EdgeAt(k);
                    float above = EdgeAt(k + 1.0) - d;
                    if (above < fw && k + 1.0 < _Fill)
                        liquid = lerp(liquid, LayerShaded(k + 1.0, d, u.x, level), saturate(0.5 - above / fw));
                    else if (below < fw && k > 0.0)
                        liquid = lerp(liquid, LayerShaded(k - 1.0, d, u.x, level), saturate(0.5 - below / fw));

                    float depth = saturate((level - d) / level);
                    liquid = lerp(liquid, 1.0, 0.08 * saturate(1.0 - depth / 0.3));
                    liquid = lerp(liquid, half3(0.0, 0.0, 0.08), 0.04 * saturate((depth - 0.3) / 0.7));

                    float2 glowR = float2(30.0 * toneWidth, max(level * 0.42, 0.5));
                    float glow = saturate(1.0 - length((u - float2(50.0, _LiquidBottom - level * 0.5)) / glowR));
                    liquid = lerp(liquid, 1.0, 0.1 * glow);

                    float caustic = saturate((1.0 - length((u - float2(50.0, _LiquidBottom - 7.0)) / float2(22.0 * toneWidth, 3.0))) * 3.0);
                    liquid = lerp(liquid, 1.0, 0.1 * caustic * step(10.0, level));

                    float body = mask.r * saturate((level - d) / fw + 0.5) * saturate((d + 0.8) / fw + 0.5);
                    col = half4(liquid * body, body) + col * (1.0 - body);

                    // Ровная поверхность: светлая линия цвета верхнего слоя, без волн
                    half3 surface = lerp(LayerColor(ceil(_Fill) - 1.0), 1.0, 0.5);
                    float surfaceLine = mask.r * 0.6 * saturate((0.7 - abs(d - level)) / fw + 0.5);
                    col = half4(surface * surfaceLine, surfaceLine) + col * (1.0 - surfaceLine);
                }

                col = front * _FrontAlpha + col * (1.0 - front.a * _FrontAlpha);

                if (_Ice > 0.0)
                {
                    half iceA = 0.55 * mask.b * _Ice;
                    col = half4(half3(0.847, 0.957, 1.0) * iceA, iceA) + col * (1.0 - iceA);
                    half lines = 0.9 * mask.a * _Ice;
                    col = half4(half3(0.92, 0.98, 1.0) * lines, lines) + col * (1.0 - lines);

                    float2 center = float2(50.0, (_LiquidTop + _LiquidBottom) * 0.5);
                    float r = length(u - center);
                    float rw = max(fwidth(r), 1e-4);
                    float disc = saturate((18.5 - r) / rw + 0.5) * _Ice;
                    float fill = saturate((15.5 - r) / rw + 0.5);
                    half3 badge = lerp(half3(1.0, 1.0, 1.0), half3(0.184, 0.659, 0.910), fill);

                    float turns = clamp(round(_IceTurns), 0.0, 99.0);
                    float2 local = u - center;
                    float digits;
                    if (turns < 10.0)
                        digits = GlyphAlpha(turns, local / 29.0 + 0.5);
                    else
                        digits = max(GlyphAlpha(floor(turns / 10.0), (local + float2(7.0, 0.0)) / 24.0 + 0.5),
                                     GlyphAlpha(fmod(turns, 10.0), (local - float2(7.0, 0.0)) / 24.0 + 0.5));
                    badge = lerp(badge, 1.0, digits * fill);
                    col = half4(badge * disc, disc) + col * (1.0 - disc);
                }

                // Свечение только снаружи, иначе пустое стекло выглядит налитым цветом подсветки
                half glowA = mask.g * _GlowColor.a * (1.0 - 0.96 * mask.b);
                col = col + half4(_GlowColor.rgb * glowA, glowA) * (1.0 - col.a);

                return col * _Alpha;
            }
            ENDHLSL
        }
    }
}
