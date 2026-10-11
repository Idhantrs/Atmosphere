Shader "Custom/PlanetUnlit"
{
    Properties
    {
        [Header(Water)]
        _DeepColor ("Deep Water", Color) = (0.08, 0.22, 0.6, 1)
        _ShallowColor ("Shallow Water", Color) = (0.3, 0.8, 0.85, 1)
        _SeaLevel ("Sea Level", Range(0, 1)) = 0.3
        _WaterShine ("Water Sparkle", Range(0, 1)) = 0.5

        [Header(Land)]
        _SandColor ("Sand", Color) = (0.98, 0.9, 0.62, 1)
        _GrassColor ("Grass", Color) = (0.5, 0.82, 0.38, 1)
        _ForestColor ("Forest", Color) = (0.2, 0.52, 0.27, 1)
        _RockColor ("Rock", Color) = (0.6, 0.54, 0.52, 1)
        _SnowColor ("Snow", Color) = (0.97, 0.98, 1, 1)
        _BeachWidth ("Beach Width", Range(0, 0.2)) = 0.04
        _SnowLevel ("Snow Level", Range(0, 1)) = 0.8
        _RockSlope ("Rock On Steep Slopes", Range(0, 1)) = 0.2

        [Header(Look)]
        _Softness ("Soft Cute Lighting", Range(0, 1)) = 0.7
        _RimColor ("Atmosphere Color", Color) = (0.6, 0.85, 1, 1)
        _RimPower ("Atmosphere Size", Range(0.5, 8)) = 3
        _RimStrength ("Atmosphere Strength", Range(0, 2)) = 0.8

        [Header(Set by the Planet script)]
        Radius ("Radius", Float) = 0
        MinHeight ("Min Height", Float) = 0
        MaxHeight ("Max Height", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half _SeaLevel;
                half _WaterShine;
                half4 _SandColor;
                half4 _GrassColor;
                half4 _ForestColor;
                half4 _RockColor;
                half4 _SnowColor;
                half _BeachWidth;
                half _SnowLevel;
                half _RockSlope;
                half _Softness;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                float Radius;
                float MinHeight;
                float MaxHeight;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalOS = IN.normalOS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ---------- How high is this point? (0 = lowest, 1 = highest) ----------
                float3 upOS = normalize(IN.positionOS);
                float dist = length(IN.positionOS);
                float low = Radius + MinHeight;
                float high = Radius + MaxHeight;
                float h = saturate((dist - low) / max(high - low, 0.00001));

                // ---------- How steep is the ground? (0 = flat, bigger = cliff) ----------
                float3 nOS = normalize(IN.normalOS);
                float slope = 1.0 - saturate(dot(nOS, upOS));

                // ---------- Water ----------
                float sea = max(_SeaLevel, 0.0001);
                float depth01 = saturate(h / sea);
                float3 water = lerp(_DeepColor.rgb, _ShallowColor.rgb, pow(depth01, 3.0));

                // ---------- Land ----------
                float aboveSea = h - _SeaLevel;
                float lh = saturate(aboveSea / max(1.0 - _SeaLevel, 0.0001));

                float sandMask = 1.0 - smoothstep(0.0, max(_BeachWidth, 0.0001), aboveSea);
                float rockHeight = smoothstep(_SnowLevel - 0.3, _SnowLevel - 0.05, h);
                float rockSlope = smoothstep(_RockSlope, _RockSlope + 0.15, slope);
                float snowMask = smoothstep(_SnowLevel - 0.05, _SnowLevel + 0.05, h);

                float3 land = lerp(_GrassColor.rgb, _ForestColor.rgb, smoothstep(0.0, 0.5, lh));
                land = lerp(land, _SandColor.rgb, sandMask);
                float rockMask = saturate(rockSlope + rockHeight) * (1.0 - sandMask);
                land = lerp(land, _RockColor.rgb, rockMask);
                land = lerp(land, _SnowColor.rgb, snowMask * (1.0 - rockSlope * 0.7));

                // Soft edge between water and land
                float landMask = smoothstep(_SeaLevel - 0.004, _SeaLevel + 0.004, h);
                float3 color = lerp(water, land, landMask);

                // ---------- Lighting ----------
             float3 upWS = TransformObjectToWorldDir(upOS);
                // Oceans are smooth like glass, land uses the real bumpy normals
                float3 nWS = normalize(lerp(upWS, normalize(IN.normalWS), landMask));
                float3 L = normalize(_MainLightPosition.xyz);
                float ndl = dot(nWS, L);

                // Mix between "realistic" and "soft & puffy" light
                float lit = lerp(saturate(ndl), ndl * 0.5 + 0.5, _Softness);
                lit = smoothstep(0.0, 0.9, lit);

                // Shadows are a cool blue-purple instead of black
                float3 shadowTint = float3(0.55, 0.6, 0.92);
                float3 lightTint = float3(1.06, 1.0, 0.94);
                color *= lerp(shadowTint, lightTint, lit);

                // ---------- Water sparkle ----------
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float spec = pow(saturate(dot(reflect(-L, nWS), V)), 64.0);
                color += spec * _WaterShine * (1.0 - landMask);

                // ---------- Atmosphere glow around the edge ----------
                float fresnel = pow(1.0 - saturate(dot(upWS, V)), _RimPower);
                float sunSide = saturate(dot(upWS, L) * 0.5 + 0.5);
                color += _RimColor.rgb * fresnel * _RimStrength * (0.3 + 0.7 * sunSide);

                return half4(saturate(color), 1);
            }
            ENDHLSL
        }
    }
}