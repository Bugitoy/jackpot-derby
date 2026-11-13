// Soft, swirling mist for the Bad Luck Storm (URP, unlit, transparent).
// Wall mode: a curtain of drifting noise, thick near the ground and fading out by the top, softened
// where it cuts through the ground or buildings (needs the camera depth texture).
// Ground mode: darkens the ground outside the safe zone, fading in over _EdgeSoftness units past the
// zone edge (the zone rect comes from the global _StormZone set by StormController).
Shader "JackpotDerby/StormMist"
{
    Properties
    {
        _Color ("Mist Colour", Color) = (0.38, 0.34, 0.50, 1)
        _DeepColor ("Deep Colour", Color) = (0.16, 0.14, 0.24, 1)
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 0.4
        _NoiseScale ("Noise Scale", Float) = 0.08
        _ScrollSpeed ("Scroll Speed", Float) = 0.6
        _Height ("Wall Height", Float) = 14
        [Toggle] _GroundMode ("Ground Mode", Float) = 0
        _EdgeSoftness ("Ground Edge Softness", Float) = 10
        _SoftDepth ("Soft Intersection Distance", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Mist"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _DeepColor;
                float _MaxAlpha;
                float _NoiseScale;
                float _ScrollSpeed;
                float _Height;
                float _GroundMode;
                float _EdgeSoftness;
                float _SoftDepth;
            CBUFFER_END

            float4 _StormZone; // xMin, zMin, xMax, zMax of the safe zone (global)

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Hash (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise (float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm (float2 p)
            {
                float a = 0.5, s = 0.0;
                for (int k = 0; k < 4; k++) { s += a * ValueNoise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return s;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y * _ScrollSpeed;
                float3 w = i.positionWS;
                float alpha, n;

                if (_GroundMode > 0.5)
                {
                    float2 outside = max(_StormZone.xy - w.xz, w.xz - _StormZone.zw);
                    float d = length(max(outside, 0.0));
                    if (d <= 0.0) return half4(0, 0, 0, 0);
                    float2 q = w.xz * _NoiseScale;
                    n = Fbm(q + float2(t * 0.3, t * 0.2));
                    alpha = smoothstep(0.0, _EdgeSoftness, d) * (0.6 + 0.4 * n);
                }
                else
                {
                    float along = w.x + w.z;
                    float2 q = float2(along * _NoiseScale, w.y * _NoiseScale * 1.6);
                    float n1 = Fbm(q + float2(t * 0.35, -t * 0.5));
                    float n2 = Fbm(q * 1.9 + float2(-t * 0.6, -t * 0.25) + n1);
                    n = saturate(n1 * 0.6 + n2 * 0.6);
                    float h = saturate(w.y / _Height);
                    // Thick low down, thinning out and gone by the top.
                    float heightFade = smoothstep(0.0, 0.06, h) * (1.0 - smoothstep(0.2, 1.0, h));
                    alpha = heightFade * smoothstep(0.2, 0.75, n + (1.0 - h) * 0.3);

                    // Soft where it meets the ground and buildings.
                    float2 suv = i.screenPos.xy / i.screenPos.w;
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                    float selfDepth = i.screenPos.w;
                    alpha *= saturate((sceneDepth - selfDepth) / _SoftDepth);
                    // Thin out right in front of the camera so driving through it isn't a wall of fog.
                    alpha *= saturate((selfDepth - 1.0) / 6.0);
                }

                half3 col = lerp(_DeepColor.rgb, _Color.rgb, n);
                col = MixFog(col, i.fogFactor);
                return half4(col, saturate(alpha * _MaxAlpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
