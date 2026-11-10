Shader "Hidden/PixelSaur/WaterRipplePainter"
{
    Properties
    {
        _MainTex ("Previous Frame RT", 2D) = "black" {}
        _UVOffset ("World Shift UV Offset", Vector) = (0, 0, 0, 0)
        _RippleThickness ("Ripple Ring Thickness", Float) = 0.018
        _FadeRate ("Fade Rate Per Second", Float) = 1.2
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
            float4 _UVOffset;
            float _RippleThickness;
            float _FadeRate;
            float _DeltaTime;

            // 支持同时绘制最多 8 圈扩散涟漪: xy = centerUV, z = radiusUV, w = intensity
            int _ActiveRippleCount;
            float4 _RippleRings[8];

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.uv = input.uv;
                // 直接使用 Clip 空间全屏映射，避免 URP Instancing 遗留矩阵干扰
                float2 clipXY = input.uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                clipXY.y = -clipXY.y;
                #endif
                output.positionCS = float4(clipXY, 0.0, 1.0);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. 按世界空间位移补偿采样上一帧历史涟漪并执行衰减
                float2 prevUV = input.uv + _UVOffset.xy;
                half history = 0.0;
                if (prevUV.x >= 0.0 && prevUV.x <= 1.0 && prevUV.y >= 0.0 && prevUV.y <= 1.0)
                {
                    history = tex2D(_MainTex, prevUV).r;
                }
                history = saturate(history - (_FadeRate * _DeltaTime));

                // 2. 叠加所有活跃的同心扩散水波纹环
                float maxRingVal = 0.0;
                float thickness = max(_RippleThickness, 0.002);

                for (int k = 0; k < 8; k++)
                {
                    if (k >= _ActiveRippleCount) break;

                    float4 rData = _RippleRings[k];
                    float2 center = rData.xy;
                    float radius = rData.z;
                    float intensity = rData.w;

                    if (intensity > 0.01)
                    {
                        float dist = distance(input.uv, center);
                        // 外圈主波纹环
                        float ringDist = abs(dist - radius);
                        float ring = saturate(1.0 - (ringDist / thickness));
                        ring = smoothstep(0.0, 1.0, ring) * intensity;

                        // 内圈次级伴随波纹环（更丰富的卡通涟漪层次）
                        float innerRadius = max(0.0, radius * 0.55);
                        float innerDist = abs(dist - innerRadius);
                        float innerRing = saturate(1.0 - (innerDist / (thickness * 0.85)));
                        innerRing = smoothstep(0.0, 1.0, innerRing) * (intensity * 0.85);

                        maxRingVal = max(maxRingVal, max(ring, innerRing));
                    }
                }

                half finalRipple = max(history, maxRingVal);
                return half4(finalRipple, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}
