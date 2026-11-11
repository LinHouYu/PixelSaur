Shader "PixelSaur/DinoRangeIndicator"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color (变色环与填充色)", Color) = (1.0, 0.2, 0.2, 0.85)
        _InnerAlpha ("Inner Fill Alpha (内圈底色透明度)", Range(0.0, 1.0)) = 0.16
        _RingWidth ("Ring Width (外环宽度)", Range(0.01, 0.25)) = 0.05
        _SoftEdge ("Edge Smoothness (抗锯齿柔边)", Range(0.001, 0.05)) = 0.012
    }
    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent+60" 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "RangeIndicatorPass"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -3, -3 // 深度向前偏移，杜绝地表穿模

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _InnerAlpha;
                float _RingWidth;
                float _SoftEdge;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 计算距离圆心的归一化极半径 (0 ~ 1)
                float2 centeredUV = (input.uv - 0.5) * 2.0;
                float dist = length(centeredUV);

                // 外边缘平滑裁剪 (dist <= 1.0)
                float outerFade = 1.0 - smoothstep(1.0 - _SoftEdge, 1.0, dist);
                if (outerFade <= 0.001)
                {
                    discard;
                }

                // 外边缘高亮环
                float ringInner = 1.0 - _RingWidth;
                float isRing = smoothstep(ringInner - _SoftEdge, ringInner, dist);

                // 内圈半透明填充 + 外环高亮
                float finalAlpha = lerp(_InnerAlpha, 1.0, isRing) * outerFade * _BaseColor.a;

                return half4(_BaseColor.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
