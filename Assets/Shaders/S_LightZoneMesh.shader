Shader "Umbrella/Prototype Light Zone Mesh"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 0.78, 0.22, 1)
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.62
        _EdgeSoftness ("Edge Softness", Range(0.02, 0.8)) = 0.28
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.35
        _NoiseStrength ("Noise Strength", Range(0, 0.25)) = 0.08
        _PulseSpeed ("Pulse Speed", Range(0, 4)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PrototypeLightZone"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _FillAlpha;
                float _EdgeSoftness;
                float _RimAlpha;
                float _NoiseStrength;
                float _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float distanceFromCenter = length(centeredUv);

                float edgeStart = saturate(1.0 - _EdgeSoftness);
                float fill = 1.0 - smoothstep(edgeStart, 1.0, distanceFromCenter);
                float core = 1.0 - smoothstep(0.0, 0.45, distanceFromCenter);
                float rim = 1.0 - smoothstep(0.0, max(0.001, _EdgeSoftness), abs(1.0 - distanceFromCenter));

                float pulse = sin(_Time.y * _PulseSpeed + distanceFromCenter * 8.0) * 0.5 + 0.5;
                float noise = Hash21(input.uv * 18.0 + _Time.y * 0.15);
                float fillAlpha = (fill * 0.58 + core * 0.26) * _FillAlpha;
                float rimAlpha = rim * _RimAlpha;
                float textureAlpha = saturate(fillAlpha + rimAlpha);
                textureAlpha *= lerp(1.0, noise * 0.65 + pulse * 0.35, _NoiseStrength);

                half3 color = _Tint.rgb * (1.0 + core * 0.25 + rim * 0.2);
                half alpha = saturate(textureAlpha);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
