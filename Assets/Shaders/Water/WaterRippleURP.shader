Shader "PixelSaur/WaterRippleURP"
{
    Properties
    {
        [Header(MinionsArt Water Colors)]
        _WaterColor ("浅水区颜色 (Shallow Turquoise)", Color) = (0.22, 0.78, 0.88, 0.78)
        _DeepWaterColor ("深水区颜色 (Deep Ocean Blue)", Color) = (0.06, 0.34, 0.68, 0.94)
        _FoamColor ("海岸泡沫颜色 (Shoreline Foam Color)", Color) = (1.0, 1.0, 1.0, 0.95)
        _RippleColor ("交互涟漪颜色 (_RippleColor)", Color) = (1.0, 1.0, 1.0, 1.0)

        [Header(Depth and Shoreline Foam)]
        _DepthMaxDistance ("深浅水过渡深度 (Depth Gradient Dist)", Range(0.05, 3.0)) = 0.42
        _FoamDistance ("岸边/角色泡沫宽度 (Foam Distance)", Range(0.02, 1.2)) = 0.22
        _FoamCutoff ("泡沫裁剪阈值 (Foam Cutoff)", Range(0.1, 0.95)) = 0.52

        [Header(Wave and Noise Animation)]
        _WaveSpeed ("水波流动速度 (Wave Speed)", Range(0.1, 5.0)) = 1.1
        _WaveFrequency ("水波噪点缩放 (Noise Scale)", Range(0.2, 5.0)) = 1.6
        _WaveHeight ("顶点微起伏高度 (严禁过高以防没过岛屿)", Range(0.0, 0.02)) = 0.002

        [Header(Lighting)]
        _Smoothness ("水面高光平滑度 (Smoothness)", Range(0.0, 1.0)) = 0.85
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float4 screenPos : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _WaterColor;
                float4 _DeepWaterColor;
                float4 _FoamColor;
                float4 _RippleColor;
                float _DepthMaxDistance;
                float _FoamDistance;
                float _FoamCutoff;
                float _WaveSpeed;
                float _WaveFrequency;
                float _WaveHeight;
                float _Smoothness;
            CBUFFER_END

            // 由 WaterRippleManager 全局传入的核心变量
            sampler2D _GlobalEffectRT;
            float4 _Position;
            float _OrthographicCamSize;

            // 2D 程序化平滑噪波（用于 MinionsArt 风格海岸泡沫与卡通水波纹，消除平行条纹伪影）
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbmWaterNoise(float2 worldXZ, float time)
            {
                float2 uv1 = worldXZ * _WaveFrequency + float2(time * 0.35, time * 0.25);
                float2 uv2 = worldXZ * (_WaveFrequency * 1.7) - float2(time * 0.28, -time * 0.32);
                float n1 = valueNoise(uv1);
                float n2 = valueNoise(uv2);
                return (n1 * 0.6 + n2 * 0.4);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);

                // 极微弱平滑起伏，严格限制高度以保证绝不没过岛屿沙滩
                float wave = sin(_Time.y * _WaveSpeed + worldPos.x * 1.5) * cos(_Time.y * _WaveSpeed * 0.8 + worldPos.z * 1.5);
                worldPos.y += wave * _WaveHeight;

                output.positionCS = TransformWorldToHClip(worldPos);
                output.worldPos = worldPos;
                output.worldNormal = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ==================== 1. 规范要求的核心 256x256 交互涟漪实现 ====================
                // 先计算 RT 图的采样 UV 值
                float2 uv = input.worldPos.xz - _Position.xz; // 像素点相对于相机中心的距离(获得每个片段在以相机Pos为圆心的坐标系里的坐标值)
                uv = uv / (_OrthographicCamSize * 2.0);       // UV转为 -0.5 ~ 0.5 (矫正Scale)
                uv += 0.5;                                    // UV转为 0 ~ 1 (矫正Offset偏移)

                // 然后采样 RT 图, 并计算 Ripple 颜色值
                float ripples = 0.0;
                if (uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0)
                {
                    ripples = tex2D(_GlobalEffectRT, uv).r;   // 只需要红色值 r
                }

                // 计算 RippleColor
                ripples = step(0.9, ripples * 3.0);           // "ripples * 3" 将计算好的ripples的值中较小部分的值提亮接近1，且对其做裁剪处理去除平滑部分
                float4 ripplesColored = ripples * _RippleColor;// 颜色混合
                // ================================================================================

                // ==================== 2. MinionsArt 风格海水深度渐变与海岸/涉水泡沫 ====================
                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float fragmentEyeDepth = input.screenPos.w;
                float depthDiff = max(0.0, sceneEyeDepth - fragmentEyeDepth);

                // 2D 双向流动水波噪点
                float noise = fbmWaterNoise(input.worldPos.xz, _Time.y * _WaveSpeed);

                // 浅水 -> 深水颜色平滑过渡
                float depthFactor = saturate(depthDiff / max(0.01, _DepthMaxDistance));
                half4 baseWater = lerp(_WaterColor, _DeepWaterColor, depthFactor);

                // 海面卡通波光细胞高光 (Toon Surface Shimmer)
                float surfaceHighlight = step(0.76, noise) * 0.12;
                baseWater.rgb += surfaceHighlight;

                // 海岸线与水中角色交界泡沫 (MinionsArt Shoreline & Intersection Foam)
                float foamLine = 1.0 - saturate(depthDiff / max(0.01, _FoamDistance));
                // 叠加流动正弦与噪点，生成活泼的海岸浪花与涉水白圈
                float shoreWave = sin(_Time.y * (_WaveSpeed * 2.2) - depthDiff * 28.0) * 0.15 + 0.85;
                float modulatedFoam = foamLine * lerp(0.65, 1.35, noise) * shoreWave;
                float foamMask = step(_FoamCutoff, modulatedFoam);
                half4 foamColored = foamMask * _FoamColor;

                // 太阳高光反射
                Light mainLight = GetMainLight();
                float3 viewDir = normalize(GetCameraPositionWS() - input.worldPos);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float spec = pow(saturate(dot(normalize(input.worldNormal), halfDir)), _Smoothness * 96.0 + 16.0) * 0.35;

                // 3. 组合海面底色、岸边泡沫与 256x256 玩家交互涟漪
                half3 waterRGB = baseWater.rgb + mainLight.color * spec;
                half3 withFoamRGB = lerp(waterRGB, foamColored.rgb, foamMask * _FoamColor.a);
                half3 finalRGB = lerp(withFoamRGB, ripplesColored.rgb, ripples);

                half finalAlpha = saturate(max(baseWater.a, max(foamMask * _FoamColor.a, ripples * ripplesColored.a)));

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}
