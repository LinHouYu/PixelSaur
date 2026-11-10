Shader "Hidden/PixelSaur/GrassInteractionPainter"
{
    Properties
    {
        _MainTex ("Previous Frame", 2D) = "black" {}
        _StampCenter ("Stamp Center UV", Vector) = (0.5, 0.5, 0, 0)
        _StampRadius ("Stamp Radius UV", Float) = 0.05
        _StampHardness ("Stamp Hardness", Float) = 2.0
        _FadeRate ("Fade Rate Per Second", Float) = 2.0
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
            float _StampRadius;
            float _StampHardness;
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
                // 读取上一帧历史状态
                half history = tex2D(_MainTex, input.uv).r;

                // 历史轨迹自然平滑回弹消退
                history = saturate(history - (_FadeRate * _DeltaTime));

                // 计算当前帧玩家足迹盖章强度
                float dist = distance(input.uv, _StampCenter.xy);
                float stamp = saturate(1.0 - (dist / max(_StampRadius, 0.001)));
                stamp = pow(stamp, _StampHardness);

                // 叠加当前压痕与消退后的历史痕迹
                half finalWeight = max(history, stamp);
                return half4(finalWeight, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}
