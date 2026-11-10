Shader "Hidden/PixelSaur/WaterRipplePainter"
{
    Properties
    {
        _MainTex ("Previous Frame RT", 2D) = "black" {}
        _RippleCenter ("Ripple Center UV", Vector) = (0.5, 0.5, 0, 0)
        _RippleRadius ("Ripple Current Radius UV", Float) = 0.05
        _RippleThickness ("Ripple Ring Thickness", Float) = 0.015
        _RippleIntensity ("Ripple Intensity", Float) = 1.0
        _FadeRate ("Fade Rate Per Second", Float) = 1.5
        _DeltaTime ("Delta Time", Float) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _RippleCenter;
            float _RippleRadius;
            float _RippleThickness;
            float _RippleIntensity;
            float _FadeRate;
            float _DeltaTime;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. 读取上一帧历史涟漪并执行衰减
                half history = tex2D(_MainTex, input.uv).r;
                history = saturate(history - (_FadeRate * _DeltaTime));

                // 2. 绘制当前正在扩散的同心圆环波纹
                float dist = distance(input.uv, _RippleCenter.xy);
                float ringDist = abs(dist - _RippleRadius);
                float ring = saturate(1.0 - (ringDist / max(_RippleThickness, 0.001)));
                ring = smoothstep(0.0, 1.0, ring) * _RippleIntensity;

                half finalRipple = max(history, ring);
                return half4(finalRipple, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}
