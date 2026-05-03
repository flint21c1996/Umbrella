Shader "Umbrella/Prototype Light Shaft"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 0.78, 0.22, 1)
        _BeamAlpha ("Beam Alpha", Range(0, 1)) = 0.36
        _CoreAlpha ("Core Alpha", Range(0, 1)) = 0.42
        _NoiseStrength ("Noise Strength", Range(0, 0.25)) = 0.08
        _PulseSpeed ("Pulse Speed", Range(0, 4)) = 0.7
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
            Name "PrototypeLightShaft"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _BeamAlpha;
                float _CoreAlpha;
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
                p = frac(p * float2(243.31, 127.13));
                p += dot(p, p + 31.17);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float across = abs(input.uv.x * 2.0 - 1.0);
                float widthFade = 1.0 - smoothstep(0.1, 1.0, across);
                float core = pow(saturate(1.0 - across), 5.0);
                float sourceFade = smoothstep(0.0, 0.08, input.uv.y);
                float targetFade = 1.0 - smoothstep(0.82, 1.0, input.uv.y);

                float streak = sin(input.uv.y * 18.0 - _Time.y * _PulseSpeed) * 0.5 + 0.5;
                float noise = Hash21(input.uv * 20.0 + _Time.y * 0.2);
                float grain = lerp(1.0, noise * 0.55 + streak * 0.45, _NoiseStrength);

                float alpha = (widthFade * _BeamAlpha + core * _CoreAlpha) * sourceFade * targetFade * grain;
                half3 color = _Tint.rgb * (1.15 + core * 0.75);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
