// Glowing rings on the ground under a chest (URP, unlit, additive), drawn on a flat quad (UV 0..1).
// A soft glow pool, a bright rim with swirling arcs, and ripples that keep expanding outward.
// _Burst (0..1) blasts the ripples out fast and bright when the chest is opened.
Shader "JackpotDerby/ChestRings"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.8, 0.3, 1)
        _Intensity ("Intensity", Float) = 1
        _RippleSpeed ("Ripple Speed", Float) = 0.45
        _SwirlSpeed ("Swirl Speed", Float) = 1.6
        _Burst ("Burst", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Rings"
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
                float _Intensity;
                float _RippleSpeed;
                float _SwirlSpeed;
                float _Burst;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            float ring(float r, float radius, float width) { float d = (r - radius) / width; return exp(-d * d); }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float t = _Time.y;

                // Soft pool of light under the chest.
                float pool = exp(-r * r * 6.0) * 0.45;

                // Bright rim with two arcs swirling round it, plus a fainter inner ring.
                float arcs = pow(saturate(0.5 + 0.5 * sin(ang * 2.0 + t * _SwirlSpeed)), 3.0);
                float rim = ring(r, 0.55, 0.035) * (0.45 + 0.9 * arcs);
                float inner = ring(r, 0.36, 0.02) * (0.3 + 0.5 * pow(saturate(0.5 + 0.5 * sin(-ang * 3.0 + t * _SwirlSpeed * 1.3)), 2.0));

                // Ripples expanding outward and fading; much faster and brighter in a burst.
                float speed = _RippleSpeed * (1.0 + _Burst * 5.0);
                float ripples = 0.0;
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float ph = frac(t * speed + k / 3.0);
                    ripples += ring(r, 0.3 + ph * 0.68, 0.025 + ph * 0.02) * (1.0 - ph) * 0.8;
                }

                float edgeFade = smoothstep(1.0, 0.85, r);
                float a = (pool + rim + inner + ripples) * edgeFade * (1.0 + _Burst * 2.5);
                return half4(_Color.rgb * (1.0 + 0.3 * rim), saturate(a * _Color.a * _Intensity));
            }
            ENDHLSL
        }
    }
}
