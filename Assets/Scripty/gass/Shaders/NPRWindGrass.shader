Shader "Gass/NPR Wind Grass"
{
    Properties
    {
        _BaseColor("Base Wheat", Color) = (0.67, 0.49, 0.16, 1)
        _TipColor("Sunlit Tip", Color) = (1.0, 0.83, 0.30, 1)
        _ShadowColor("NPR Shadow", Color) = (0.30, 0.25, 0.11, 1)
        _RimColor("Dry Rim", Color) = (1.0, 0.92, 0.52, 1)
        _WindTex("Wind Flow Texture", 2D) = "gray" {}
        _WindDirection("Wind Direction", Vector) = (1, 0, 0.28, 0)
        _WindStrength("Wind Strength", Range(0, 2)) = 0.58
        _WindSpeed("Wind Speed", Range(0, 6)) = 1.65
        _WindScale("Wind Scale", Range(0.001, 0.15)) = 0.035
        _GustStrength("Gust Strength", Range(0, 2)) = 0.42
        _GustScale("Gust Scale", Range(0, 8)) = 2.35
        _BladeLean("Blade Lean", Range(0, 1)) = 0.34
        _NprSteps("NPR Steps", Range(2, 5)) = 3
        _GassWindTime("Wind Time", Float) = 0
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
            Name "NPRGrassForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_WindTex);
            SAMPLER(sampler_WindTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _ShadowColor;
                half4 _RimColor;
                float4 _WindDirection;
                float _WindStrength;
                float _WindSpeed;
                float _WindScale;
                float _GustStrength;
                float _GustScale;
                float _BladeLean;
                float _NprSteps;
                float _GassWindTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half2 uv : TEXCOORD2;
                half height01 : TEXCOORD3;
                half bladeNoise : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7))) * 43758.5453123);
            }

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                half height01 = saturate(input.uv.y);
                float2 windUv = worldPos.xz * _WindScale + normalize(_WindDirection.xz + 0.0001) * (_GassWindTime * _WindSpeed * 0.08);
                float2 windSample = SAMPLE_TEXTURE2D_LOD(_WindTex, sampler_WindTex, windUv, 0).rg * 2.0 - 1.0;
                float stripe = sin((worldPos.x * 0.13 + worldPos.z * 0.19) * _GustScale + _GassWindTime * _WindSpeed * 2.2);
                float gust = stripe * 0.5 + 0.5;
                float3 windDirection = normalize(float3(_WindDirection.x, 0.0, _WindDirection.z) + float3(windSample.x, 0.0, windSample.y) * 0.45 + 0.0001);
                float bendMask = height01 * height01;
                float bend = bendMask * (_WindStrength + gust * _GustStrength) * lerp(0.65, 1.1, input.color.a);
                worldPos += windDirection * bend;
                worldPos.y -= bend * bendMask * _BladeLean;

                output.positionWS = worldPos;
                output.positionHCS = TransformWorldToHClip(worldPos);
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.uv = input.uv;
                output.height01 = height01;
                output.bladeNoise = Hash21(floor(worldPos.xz * 0.35));
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half light01 = saturate(abs(dot(normalWS, mainLight.direction)) * 0.92 + 0.08);
                half stepped = floor(light01 * _NprSteps) / max(1.0, _NprSteps - 1.0);
                stepped = saturate(stepped);

                half3 wheat = lerp(_BaseColor.rgb, _TipColor.rgb, saturate(input.height01 * 1.08));
                wheat *= lerp(0.86h, 1.12h, input.bladeNoise);
                half3 color = lerp(_ShadowColor.rgb, wheat, stepped);
                half windBand = sin((input.positionWS.x * 0.11h + input.positionWS.z * 0.19h) * _GustScale + _GassWindTime * _WindSpeed * 2.35h) * 0.5h + 0.5h;
                color = lerp(color * 0.92h, color * 1.14h, smoothstep(0.38h, 0.92h, windBand) * input.height01);

                half3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                half rim = pow(1.0h - saturate(abs(dot(viewDirection, normalWS))), 3.0h);
                color += _RimColor.rgb * rim * 0.18h;
                color *= mainLight.color.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
