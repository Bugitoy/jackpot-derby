// The pillar of light over a chest (URP, unlit, additive). Brightest through its middle and soft at its
// edges (view-angle falloff), strongest at the bottom and fading out as it rises, with bright streaks
// that travel upward. Drawn on a cylinder whose UV.y runs bottom (0) to top (1).
Shader "JackpotDerby/LightPillar"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.82, 0.3, 1)
        _Intensity ("Intensity", Float) = 1
        _StreakColor ("Streak Colour", Color) = (1, 0.95, 0.7, 1)
        _StreakCount ("Streaks Around", Float) = 9
        _StreakSpeed ("Streak Speed", Float) = 0.35
        _EdgeSoftness ("Edge Softness", Float) = 1.6
        _HeightFade ("Height Fade", Float) = 1.4
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Pillar"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _StreakColor;
                float _Intensity;
                float _StreakCount;
                float _StreakSpeed;
                float _EdgeSoftness;
                float _HeightFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = i.uv;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(posWS);
                return o;
            }

            float hash11(float n) { return frac(sin(n * 127.1) * 43758.5453); }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);
                float3 v = normalize(i.viewWS);
                // Facing the camera = middle of the pillar (bright); grazing = its edge (soft, fades out).
                float facing = saturate(abs(dot(n, v)));
                float edge = pow(facing, _EdgeSoftness);

                float h = saturate(i.uv.y);
                float fade = pow(1.0 - h, _HeightFade) * lerp(0.35, 1.0, smoothstep(0.0, 0.07, h));

                // Streaks: thin bright columns around the pillar, each broken into dashes rising upward.
                float col = i.uv.x * _StreakCount;
                float id = floor(col);
                float within = frac(col);
                float lineMask = pow(saturate(1.0 - abs(within - 0.5) * 2.0), 6.0);
                float rnd = hash11(id + 3.0);
                float dash = frac(h * (1.5 + rnd * 2.0) - _Time.y * _StreakSpeed * (0.6 + rnd) + rnd);
                float streak = lineMask * smoothstep(0.0, 0.25, dash) * smoothstep(1.0, 0.55, dash) * step(0.25, rnd);

                float3 colr = _Color.rgb * (0.6 + 0.35 * edge) + _StreakColor.rgb * streak * 1.4;
                float a = (0.4 * edge + 0.05 + streak * 0.85) * fade * _Color.a * _Intensity;
                return half4(colr, saturate(a));
            }
            ENDHLSL
        }
    }
}
