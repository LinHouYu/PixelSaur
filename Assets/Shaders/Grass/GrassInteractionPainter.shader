Shader "Hidden/PixelSaur/GrassInteractionPainter"
{
    Properties
    {
        _MainTex ("Previous Frame", 2D) = "black" {}
        _StampCenter ("Stamp Center UV", Vector) = (0.5, 0.5, 0, 0)
        _UVOffset ("World Shift UV Offset", Vector) = (0, 0, 0, 0)
        _StampRadius ("Stamp Radius UV", Float) = 0.08
        _StampHardness ("Stamp Hardness", Float) = 1.5
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
            float4 _StampCenter;
            float4 _UVOffset;
            float _StampRadius;
            float _StampHardness;
            float _FadeRate;
            float _DeltaTime;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.uv = input.uv;
                // 直接使用全屏 Clip 空间映射，彻底规避 URP RenderMeshInstanced 遗留的矩阵干扰
                float2 clipXY = input.uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                clipXY.y = -clipXY.y;
                #endif
                output.positionCS = float4(clipXY, 0.0, 1.0);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. 根据相机/中心点位移偏移采样上一帧历史轨迹（确保压草轨迹固定在世界空间）
                float2 prevUV = input.uv + _UVOffset.xy;
                half4 historySample = half4(0, 0.5, 0.5, 1);
                if (prevUV.x >= 0.0 && prevUV.x <= 1.0 && prevUV.y >= 0.0 && prevUV.y <= 1.0)
                {
                    historySample = tex2D(_MainTex, prevUV);
                }

                // 历史压痕权重自然衰减回弹
                half historyWeight = saturate(historySample.r - (_FadeRate * _DeltaTime));
                float2 historyDir = historySample.gb * 2.0 - 1.0;

                // 2. 计算当前帧玩家足迹盖章强度与向外推开的方向
                float2 diff = input.uv - _StampCenter.xy;
                float dist = length(diff);
                float2 pushDir = (dist > 0.0001) ? (diff / dist) : float2(0.0, 1.0);

                float stamp = saturate(1.0 - (dist / max(_StampRadius, 0.001)));
                stamp = pow(stamp, max(0.2, _StampHardness));

                // 3. 混合最新踩踏与历史轨迹
                half finalWeight = max(historyWeight, stamp);
                float2 finalDir = (stamp >= historyWeight) ? pushDir : historyDir;
                if (length(finalDir) > 0.001)
                {
                    finalDir = normalize(finalDir);
                }

                return half4(finalWeight, finalDir.x * 0.5 + 0.5, finalDir.y * 0.5 + 0.5, 1.0);
            }
            ENDHLSL
        }
    }
}
