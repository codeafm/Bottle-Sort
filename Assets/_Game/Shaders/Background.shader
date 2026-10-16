// Фон без текстур: радиальный градиент, дышащее свечение в центре, редкие мерцающие искорки и виньетка.
// Работает на любом разрешении, пропорции экрана передаёт BackgroundView через _Aspect.
Shader "BottleSort/Background"
{
    Properties
    {
        _Center ("Center color", Color) = (0.071, 0.188, 0.431, 1)
        _Middle ("Middle color", Color) = (0.027, 0.075, 0.220, 1)
        _Edge ("Edge color", Color) = (0.016, 0.039, 0.133, 1)
        _Halo ("Halo color", Color) = (0.227, 0.482, 1, 1)
        _SparkGlow ("Spark glow", Color) = (0.588, 0.745, 1, 0.8)
        _Aspect ("Width / height", Float) = 0.5625
        _SparkDensity ("Spark share of cells", Range(0, 1)) = 0.42
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Opaque" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Center, _Middle, _Edge, _Halo, _SparkGlow;
                float _Aspect, _SparkDensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Всё считаем в «пикселях макета»: ширина экрана всегда 390
                float w = 390.0;
                float h = w / max(_Aspect, 0.1);
                float2 p = float2(i.uv.x * w, (1.0 - i.uv.y) * h);

                float2 radius = float2(0.7071 * w, 1.4142 * 0.65 * h);
                float r = length((p - float2(0.5 * w, 0.35 * h)) / radius);
                half3 col = r < 0.55 ? lerp(_Center.rgb, _Middle.rgb, r / 0.55) : lerp(_Middle.rgb, _Edge.rgb, saturate((r - 0.55) / 0.45));

                float breath = 0.5 - 0.5 * cos(_Time.y * 6.2832 / 9.0);
                float haloR = length(p - float2(0.5 * w, 0.4 * h)) / (260.0 * lerp(1.0, 1.08, breath));
                float halo = 0.14 * saturate(1.0 - haloR / 0.65) * lerp(0.55, 0.85, breath);
                col = lerp(col, _Halo.rgb, halo);

                // Одна искорка на ячейку 78x78, смещение внутри ячейки с отступом, чтобы свечение не резалось
                float cellSize = 78.0;
                float2 cell = floor(p / cellSize);
                float seed = Hash(cell);
                if (seed < _SparkDensity)
                {
                    float2 pos = (cell + 0.2 + 0.6 * float2(Hash(cell + 17.1), Hash(cell + 31.7))) * cellSize;
                    float size = 1.0 + Hash(cell + 5.3);
                    float period = 3.0 + 4.0 * Hash(cell + 9.1);
                    float tw = 0.5 - 0.5 * cos((_Time.y / period + Hash(cell + 2.9)) * 6.2832);
                    float dist = length(p - pos);
                    float core = saturate(size * lerp(0.6, 1.0, tw) - dist + 0.5);
                    float glowS = exp(-dist * dist / 18.0) * _SparkGlow.a;
                    float alpha = lerp(0.1, 0.75, tw);
                    col = lerp(col, _SparkGlow.rgb, glowS * alpha * 0.6);
                    col = lerp(col, 1.0, core * alpha);
                }

                float2 vr = float2(0.7071 * w, 1.4142 * 0.6 * h);
                float vignette = 0.35 * saturate((length((p - float2(0.5 * w, 0.4 * h)) / vr) - 0.55) / 0.45);
                col *= 1.0 - vignette;

                // Лёгкий шум против полос на тёмных градиентах
                col += (Hash(i.uv * 1024.0 + frac(_Time.y)) - 0.5) / 255.0;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
