// Струя: сужается книзу, поперёк — тот же трёхтоновый градиент, что у жидкости, вдоль бегут мягкие блики.
// uv.x поперёк квада (струя занимает середину, остальное — свечение), uv.y = 0 у горлышка, 1 у поверхности.
Shader "BottleSort/PourStream"
{
    Properties
    {
        _Color ("Color", Color) = (0.79, 0.16, 0.23, 1)
        _Length ("Length in world units", Float) = 2
        _Grow ("Grow from top", Range(0, 1)) = 1
        _Cut ("Cut from top", Range(0, 1)) = 0
        _Width ("Stream share of quad width", Range(0.1, 1)) = 0.34
        _FlowPeriod ("Highlight period, world", Float) = 0.69
        _FlowSpeed ("Highlight speed, world/s", Float) = 2.45
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Length, _Grow, _Cut, _Width, _FlowPeriod, _FlowSpeed;
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

            half4 frag(Varyings i) : SV_Target
            {
                float v = i.uv.y;
                if (v > _Grow || v < _Cut)
                    return 0;

                // От полной ширины у горлышка до половины у поверхности, как clip-path в макете
                float halfW = _Width * 0.5 * lerp(1.0, 0.52, v);
                float x = i.uv.x - 0.5;
                float edge = abs(x) - halfW;
                float aa = max(fwidth(i.uv.x), 1e-4);
                float body = saturate(0.5 - edge / aa);

                float across = saturate(x / (halfW * 2.0) + 0.5);
                half3 c = _Color.rgb;
                half3 light = lerp(c, 1.0, 0.28);
                half3 dark = c * 0.62;
                half3 tone = across < 0.32 ? lerp(dark, light, across / 0.32)
                           : across < 0.62 ? lerp(light, c, (across - 0.32) / 0.3)
                           : lerp(c, dark, (across - 0.62) / 0.38);

                float phase = frac((v * _Length - _Time.y * _FlowSpeed) / _FlowPeriod);
                float shine = phase < 0.21 ? phase / 0.21 : saturate(1.0 - (phase - 0.21) / 0.29);
                tone = lerp(tone, 1.0, 0.45 * shine);

                // Мягкое свечение вокруг струи цветом жидкости
                float glowFalloff = saturate(1.0 - max(edge, 0.0) / max(0.5 - halfW, 1e-3));
                float glow = 0.45 * glowFalloff * glowFalloff * (1.0 - body);

                // Кончик у горлышка и у поверхности не обрывается резко
                float ends = saturate((v - _Cut) * _Length / 0.04) * saturate((_Grow - v) * _Length / 0.04 + 0.3);

                half4 result = half4(tone * body, body) + half4(c * glow, glow) * (1.0 - body);
                return result * ends * _Color.a;
            }
            ENDHLSL
        }
    }
}
