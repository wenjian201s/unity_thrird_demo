Shader "Gass/NPR Wind Valley Terrain"
{
    Properties
    {
        _GroundTex("Ground Noise", 2D) = "white" {}
        _BaseColor("Dry Grass Ground", Color) = (0.58, 0.43, 0.16, 1)
        _RidgeColor("Sun Ridge", Color) = (0.82, 0.65, 0.25, 1)
        _ValleyColor("Valley Ochre", Color) = (0.42, 0.31, 0.13, 1)
        _ShadowColor("NPR Shadow", Color) = (0.24, 0.22, 0.13, 1)
        _NoiseScale("Noise Scale", Range(0.005, 0.2)) = 0.038
        _ValleyWidth("Valley Width", Range(8, 70)) = 36
        _NprSteps("NPR Steps", Range(2, 5)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "NPRTerrainForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GroundTex);
            SAMPLER(sampler_GroundTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RidgeColor;
                half4 _ValleyColor;
                half4 _ShadowColor;
                float _NoiseScale;
                float _ValleyWidth;
                float _NprSteps;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half light01 = saturate(dot(normalWS, mainLight.direction) * 0.5h + 0.5h);
                half stepped = floor(light01 * _NprSteps) / max(1.0, _NprSteps - 1.0);
                stepped = saturate(stepped);

                float centerX = sin(input.positionWS.z * 0.035) * 16.5;
                float valley = 1.0 - saturate(abs(input.positionWS.x - centerX) / _ValleyWidth);
                valley = smoothstep(0.0, 1.0, valley);
                half noise = SAMPLE_TEXTURE2D(_GroundTex, sampler_GroundTex, input.positionWS.xz * _NoiseScale).r;
                half ridge = saturate(normalWS.y * 0.85h + noise * 0.35h);
                half3 dryGround = lerp(_BaseColor.rgb, _RidgeColor.rgb, ridge);
                half3 ground = lerp(dryGround, _ValleyColor.rgb, valley * 0.55h);
                ground *= lerp(0.88h, 1.12h, noise);
                half3 color = lerp(_ShadowColor.rgb, ground, stepped);
                return half4(color * mainLight.color.rgb, 1.0h);
            }
            ENDHLSL
        }
    }
}
