Shader "Custom/AtmosphereVol"
{
    Properties
    {
        // Red channel is the gas den
        _Density("Density", 3D) = "" {}
        // Opaqueness of gas
        _DensityScale("Density Scale", Float) = 20
        // Steps for raymarching
        _Steps("Steps", Float) = 64
        
        _Col0 ("Species 0 Color", Color) = (1,1,1,1)
        _Col1 ("Species 1 Color", Color) = (0,1,1,1)
        _Col2 ("Species 2 Color", Color) = (1,0,0,1)
        _Col3 ("Species 3 Color", Color) = (1,1,0,1)
        _Absorb ("Species Absorption", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        
        Pass
        {
            Name "Volume"
            Tags { "LightMode"="UniversalForward" }
            // Render state
            Cull Front
            ZWrite Off
            ZTest Always
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            
            TEXTURE3D(_Density);
            SAMPLER(sampler_Density);

            CBUFFER_START(UnityPerMaterial)
                float _DensityScale;
                float _Steps;
                float4 _Col0, _Col1, _Col2, _Col3;
                float4 _Absorb;
            CBUFFER_END

            // Vert shader inp
            struct Attributes {float4 positionOS : POSITION;};
            // Frag shader inp
            struct Varyings {float4 positionHCS : SV_POSITION; float3 positionOS : TEXCOORD0;};

            // Converts vertex to screen position
            // And passes obj space positions
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            // ray orgin, ray dir, out ray enter, out ray exits
            // finds whre all axises meet he tray
            bool HitBox(float3 ro, float3 rd, out float t0, out float t1)
            {
                float3 inv = 1.0 / (rd + 1e-6); // So that it is not zero
                float3 tA = (float3(0,0,0) - ro) * inv;
                float3 tB = (float3(1,1,1) - ro) * inv;
                float3 tmin = min(tA, tB);
                float3 tmax = max(tA, tB);
                t0 = max(max(tmin.y, tmin.x), tmin.z);
                t1 = min(min(tmax.y, tmax.x), tmax.z);
                t0 = max(t0, 0);
                return t1 > t0;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 ro = TransformWorldToObject(_WorldSpaceCameraPos) + 0.5;
                float3 rd = normalize(IN.positionOS + 0.5 - ro);

                float2 uv = IN.positionHCS.xy / _ScaledScreenParams.xy;
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);

                float3 posWS = TransformObjectToWorld(IN.positionOS);
                float3 worldRd = normalize(posWS - _WorldSpaceCameraPos);
                float3 camFwd = -UNITY_MATRIX_V[2].xyz;
                float sceneDist = sceneEyeDepth / dot(worldRd, camFwd);

                float3 hitWS = _WorldSpaceCameraPos + worldRd * sceneDist;
                float3 hitOS = TransformWorldToObject(hitWS) + 0.5;
                float tScene = dot(hitOS - ro, rd);

                float3 light = 0;
                float T = 1;
                float t0, t1;

                if (HitBox(ro, rd, t0, t1))
                {
                    t1 = min(t1, tScene);

                    int steps = (int)_Steps;
                    float stepLen = (t1 - t0) / steps;
                    for (int s = 0; s < steps; s++)
                    {
                        float3 pos = ro + rd * (t0 + (s+0.5) * stepLen);
                        float4 d = SAMPLE_TEXTURE3D_LOD(_Density, sampler_Density, pos, 0);
                        float4 w = d * _Absorb;
                        float wSum = w.x + w.y + w.z + w.w;
                        float sigma = wSum * _DensityScale;
                        float3 col = (w.x * _Col0.rgb + w.y * _Col1.rgb + w.z * _Col2.rgb + w.w * _Col3.rgb)
                        / max(wSum, 1e-5);

                        float a = 1 - exp(-sigma * stepLen);
                        light += T * a * col;
                        T *= 1 -a;
                        if (T < 0.01) break;
                    }
                }
                return half4(light, 1 - T);
            }
            
            ENDHLSL
            
        }
    }
}