// Для спрайтов и частиц из Art/Generated: текстуры уже премультиплицированы.
// _Additive = 1 превращает тот же материал в аддитивный (альфа обнуляется, цвет просто прибавляется).
Shader "BottleSort/UnlitPremultiplied"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        _Additive ("Additive", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Additive;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 c = tex * half4(i.color.rgb * i.color.a, i.color.a);
                c.a *= 1.0 - _Additive;
                return c;
            }
            ENDHLSL
        }
    }
}
