// Daytime sky for the arena (URP skybox): an atmosphere gradient (deep blue overhead, pale near the
// horizon), a sun disk with a soft glow, and drifting clouds lit from the sun's side with bright
// edges. The horizon colour matches the scene fog, so distant buildings fade into the sky.
Shader "JackpotDerby/StylizedSky"
{
    Properties
    {
        [Header(Atmosphere)]
        _ZenithColor ("Zenith", Color) = (0.24, 0.46, 0.84, 1)
        _HorizonColor ("Horizon", Color) = (0.76, 0.85, 0.93, 1)
        _GroundColor ("Below Horizon", Color) = (0.62, 0.66, 0.70, 1)
        _GradientPower ("Gradient Power", Range(0.1, 2)) = 0.55
        _HorizonHaze ("Horizon Haze", Range(0, 1)) = 0.55
        _HazeFalloff ("Haze Falloff", Range(1, 40)) = 12
        _Bands ("Cartoon Bands (0 = smooth)", Range(0, 8)) = 0
        _BandSoftness ("Band Edge Softness", Range(0.01, 0.5)) = 0.15

        [Header(Cartoon Sun)]
        [Toggle] _ToonSun ("Cartoon Sun (flat disk + ring)", Float) = 0
        _SunRingColor ("Sun Ring Colour", Color) = (1, 0.95, 0.7, 1)

        [Header(Sun)]
        _SunDir ("Sun Direction (world, towards sun)", Vector) = (0.4, 0.3, 0.6, 0)
        _SunColor ("Sun Colour", Color) = (1, 0.92, 0.78, 1)
        _SunSize ("Sun Size (1 - cos radius)", Range(0.0001, 0.005)) = 0.0006
        _SunIntensity ("Sun Disk Intensity", Range(1, 30)) = 10
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 0.7

        [Header(Clouds)]
        _CloudLit ("Cloud Lit Colour", Color) = (1, 0.99, 0.96, 1)
        _CloudShade ("Cloud Shade Colour", Color) = (0.60, 0.67, 0.78, 1)
        _CloudScale ("Cloud Scale", Float) = 1.6
        _CloudSpeed ("Cloud Speed", Float) = 0.012
        _CloudCoverage ("Coverage", Range(0, 1)) = 0.48
        _CloudSoftness ("Softness", Range(0.01, 0.5)) = 0.22
        _CloudOpacity ("Opacity", Range(0, 1)) = 0.95
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor;
                float _GradientPower, _HorizonHaze, _HazeFalloff, _Bands, _BandSoftness, _ToonSun;
                half4 _SunRingColor;
                float4 _SunDir;
                half4 _SunColor;
                float _SunSize, _SunIntensity, _GlowIntensity;
                half4 _CloudLit, _CloudShade;
                float _CloudScale, _CloudSpeed, _CloudCoverage, _CloudSoftness, _CloudOpacity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
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
                for (int k = 0; k < 5; k++) { s += a * ValueNoise(p); p = p * 2.07 + 13.7; a *= 0.5; }
                return s;
            }

            // Cloud density at a point on the cloud layer (domain-warped noise for billowy shapes).
            float CloudDensity (float2 uv)
            {
                float warp = Fbm(uv * 0.5 + 3.1);
                float n = Fbm(uv + warp * 0.9);
                return smoothstep(_CloudCoverage, _CloudCoverage + _CloudSoftness, n);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 sun = normalize(_SunDir.xyz);
                float cosSun = dot(d, sun);

                // Atmosphere: horizon to zenith, slightly warmer toward the sun.
                float up = saturate(h);
                float g = pow(up, _GradientPower);
                // Cartoon: the gradient steps in a few soft-edged bands, like a painted backdrop.
                if (_Bands > 0.5)
                {
                    float b = g * _Bands;
                    g = (floor(b) + smoothstep(0.5 - _BandSoftness, 0.5 + _BandSoftness, frac(b))) / _Bands;
                }
                float3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, g);
                float towardSun = saturate(cosSun * 0.5 + 0.5);
                sky = lerp(sky, sky * _SunColor.rgb * 1.08, towardSun * towardSun * 0.35);
                sky = lerp(sky, _GroundColor.rgb, saturate(-h * 6.0));

                // Sun glow (wide halo + tighter bloom around the disk).
                float glow = pow(saturate(cosSun), 8.0) * 0.18 + pow(saturate(cosSun), 120.0) * 0.45
                           + pow(saturate(cosSun), 1600.0) * 1.2;
                sky += _SunColor.rgb * glow * _GlowIntensity;

                // Clouds on a flat layer overhead, drifting slowly.
                float cloud = 0.0;
                if (h > 0.0)
                {
                    float2 uv = d.xz / (h + 0.12) * _CloudScale + _Time.y * _CloudSpeed * float2(1.0, 0.35);
                    cloud = CloudDensity(uv);
                    // Self-shadowing: denser toward the sun = darker underside.
                    float2 toSun = normalize(sun.xz + 1e-4) * 0.12;
                    float shadow = CloudDensity(uv + toSun);
                    float lit = saturate(1.0 - (shadow - cloud) * 1.6 - shadow * 0.35);
                    float3 cloudCol = lerp(_CloudShade.rgb, _CloudLit.rgb, lit);
                    // Bright silver edges when looking toward the sun.
                    cloudCol += _SunColor.rgb * pow(saturate(cosSun), 8.0) * (1.0 - cloud) * 0.9;
                    float fade = smoothstep(0.0, 0.22, h);   // clouds thin out toward the horizon
                    cloud *= fade * _CloudOpacity;
                    sky = lerp(sky, cloudCol, cloud);
                }

                // Sun disk, hidden behind thick cloud.
                float disk;
                if (_ToonSun > 0.5)
                {
                    // Flat, crisp disk with a pale ring around it.
                    disk = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.9, cosSun);
                    float ring = smoothstep(1.0 - _SunSize * 3.2, 1.0 - _SunSize * 3.0, cosSun) * (1.0 - disk);
                    sky = lerp(sky, _SunRingColor.rgb, ring * 0.35 * (1.0 - cloud));
                    sky = lerp(sky, _SunColor.rgb * _SunIntensity, disk * (1.0 - cloud * 0.9));
                }
                else
                {
                    disk = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, cosSun);
                    sky += _SunColor.rgb * disk * _SunIntensity * (1.0 - cloud * 0.9);
                }

                // Haze band at the horizon, matching the fog colour.
                float haze = exp(-abs(h) * _HazeFalloff) * _HorizonHaze;
                sky = lerp(sky, _HorizonColor.rgb, haze * (1.0 - disk));

                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
