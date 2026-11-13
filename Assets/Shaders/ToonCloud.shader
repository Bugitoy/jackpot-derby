// Cartoon cloud (URP): the cloud model's painted texture with soft two-tone shading from the sun
// (bright tops, cool blue undersides), a light rim, and no fog so the clouds stay crisp and white.
Shader "JackpotDerby/ToonCloud"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Colour", Color) = (0.72, 0.80, 0.96, 1)
        _ShadeSoftness ("Shade Softness", Range(0.01, 1)) = 0.35
        _ShadeOffset ("Shade Offset", Range(-1, 1)) = -0.1
        _RimColor ("Rim Colour", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ToonCloud"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint, _ShadeColor, _RimColor;
                float _ShadeSoftness, _ShadeOffset, _RimPower, _RimStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag (Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);
                Light sun = GetMainLight();
                float ndl = dot(n, sun.direction);
                // Two tones with a soft edge (cartoon shading), slightly favouring the lit side.
                float lit = smoothstep(_ShadeOffset - _ShadeSoftness, _ShadeOffset + _ShadeSoftness, ndl);
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half3 col = tex * _Tint.rgb * lerp(_ShadeColor.rgb, half3(1, 1, 1), lit);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float rim = pow(1.0 - saturate(dot(n, viewDir)), _RimPower) * _RimStrength;
                col += _RimColor.rgb * rim;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
