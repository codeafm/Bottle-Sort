Shader "BottleSort/UI/CODEAFM Reveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original CODEAFM artwork", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PartUV ("Part UV", Vector) = (0,0,1,1)
        _EmblemUV ("Emblem UV", Vector) = (0,0,1,1)
        _Reveal ("Reveal", Float) = 1.1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 localPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color;
            float4 _PartUV, _EmblemUV, _ClipRect;
            float _Reveal;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.localPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 original = tex2D(_MainTex, i.uv);
                // The supplied source stays untouched. Its purple chroma separates the emblem
                // from the photographed cream paper, including antialiased edge coverage.
                float chroma = max(0, original.b - min(original.r, original.g));
                float ink = saturate(chroma / .74) * smoothstep(.055, .12, chroma);
                float2 part = (i.uv - _PartUV.xy) / _PartUV.zw;
                float reveal = 1 - smoothstep(_Reveal - .08, _Reveal, part.y);
                // Keep the original AF silhouette and edge coverage, rendered in pure white.
                fixed4 result = fixed4(i.color.rgb, ink * original.a * reveal * i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - .001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
