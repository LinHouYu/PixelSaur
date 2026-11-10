Shader "PixelSaur/WaterRippleURP"
{
    Properties
    {
        [Header(Water Colors)]
        _WaterColor ("水面浅水颜色 (Shallow Color)", Color) = (0.2, 0.65, 0.85, 0.75)
        _DeepWaterColor ("水面深水颜色 (Deep Color)", Color) = (0.05, 0.25, 0.55, 0.95)
        _RippleColor ("涟漪高光颜色 (_RippleColor)", Color) = (1.0, 1.0, 1.0, 1.0)

        [Header(Wave Animation)]
        _WaveSpeed ("水波流动速度", Range(0.1, 5.0)) = 1.2
        _WaveFrequency ("水波纹理频率", Range(0.1, 5.0)) = 1.0
        _WaveHeight ("波浪微起伏高度", Range(0.0, 0.5)) = 0.05

        [Header(Ripple Parameters)]
        _OrthographicCamSize ("正交相机半高 (_OrthographicCamSize)", Float) = 20.0
        _Position ("正交中心点 (_Position)", Vector) = (0, 0, 0, 0)
        _GlobalEffectRT ("涟漪RT贴图 (_GlobalEffectRT 256x256)", 2D) = "black" {}

        [Header(Lighting)]
        _Smoothness ("平滑高光 (Smoothness)", Range(0.0, 1.0)) = 0.85
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : NORMAL;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _WaterColor;
                float4 _DeepWaterColor;
                float4 _RippleColor;
                float _WaveSpeed;
                float _WaveFrequency;
                float _WaveHeight;
                float _Smoothness;
            CBUFFER_END

            // 由 WaterRippleManager 全局传入的核心变量
            sampler2D _GlobalEffectRT;
            float4 _Position;
            float _OrthographicCamSize;

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);

                // 微弱正弦顶点波浪
                float wave = sin(_Time.y * _WaveSpeed + worldPos.x * _WaveFrequency + worldPos.z * _WaveFrequency);
                worldPos.y += wave * _WaveHeight;

                output.positionCS = TransformWorldToHClip(worldPos);
                output.worldPos = worldPos;
                output.worldNormal = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ==================== 规范要求的核心技术实现 ====================
                // 1. 先计算 RT 图的采样 UV 值
                float2 uv = input.worldPos.xz - _Position.xz; // 像素点相对于相机中心的距离(获得每个片段在以相机Pos为圆心的坐标系里的坐标值)
                uv = uv / (_OrthographicCamSize * 2.0);        // UV转为 -0.5 ~ 0.5 (矫正Scale)
                uv += 0.5;                                    // UV转为 0 ~ 1 (矫正Offset偏移)

                // 2. 然后采样 RT 图, 并计算 Ripple 颜色值
                float ripples = 0.0;
                if (uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0)
                {
                    ripples = tex2D(_GlobalEffectRT, uv).r;   // 只需要红色值 r
                }

                // 3. 计算 RippleColor
                ripples = step(0.9, ripples * 3.0);           // "ripples * 3" 将计算好的ripples的值中较小部分的值提亮接近1，且对其做裁剪处理去除平滑部分
                float4 ripplesColored = ripples * _RippleColor;// 颜色混合
                // ===============================================================

                // 基础水体渐变着色
                float waveNoise = sin(_Time.y * (_WaveSpeed * 0.8) + input.worldPos.x * 2.0 + input.worldPos.z * 1.5) * 0.5 + 0.5;
                half4 baseWater = lerp(_DeepWaterColor, _WaterColor, waveNoise * 0.5 + 0.5);

                // 光照计算
                Light mainLight = GetMainLight();
                float3 viewDir = normalize(GetCameraPositionWS() - input.worldPos);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float spec = pow(saturate(dot(normalize(input.worldNormal), halfDir)), _Smoothness * 128.0 + 8.0);

                // 混合水面与涟漪
                half3 finalRGB = lerp(baseWater.rgb + (mainLight.color * spec * 0.5), ripplesColored.rgb, ripples);
                half finalAlpha = saturate(baseWater.a + ripplesColored.a * ripples);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}
