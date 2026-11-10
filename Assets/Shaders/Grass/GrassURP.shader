Shader "PixelSaur/GrassURP"
{
    Properties
    {
        [Header(Color Settings)]
        _BaseColor("草地底部颜色 (Base Color)", Color) = (0.12, 0.35, 0.08, 1.0)
        _TipColor("草地尖端颜色 (Tip Color)", Color) = (0.45, 0.78, 0.18, 1.0)

        [Header(Wind Simulation)]
        _WindSpeed("风速 (Wind Speed)", Range(0.1, 10.0)) = 2.5
        _WindStrength("风力强度 (Wind Strength)", Range(0.0, 1.5)) = 0.25
        _WindFrequency("风频波长 (Wind Frequency)", Range(0.05, 2.0)) = 0.4

        [Header(Interaction Settings)]
        _InteractionStrength("互动弯折强度 (Bend Strength)", Range(0.0, 3.0)) = 1.2
        _GrassInteractionRT("交互RT贴图 (64x64)", 2D) = "black" {}
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200
        Cull Off // 双面渲染草叶

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float _WindSpeed;
                float _WindStrength;
                float _WindFrequency;
                float _InteractionStrength;
            CBUFFER_END

            // 全局交互数据（由 GrassInteractionManager 设置）
            sampler2D _GrassInteractionRT;
            float4 _GrassInteractionCenter;   // xz 为交互中心
            float _GrassInteractionAreaSize;  // 交互区域边长
            float4 _PlayerWorldPos;           // 玩家实时世界坐标

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float heightFactor = input.uv.y; // 0 为根部，1 为草尖

                // 1. 风力摆动模拟（仅对草叶上半部生效）
                float windWave = sin(_Time.y * _WindSpeed + worldPos.x * _WindFrequency + worldPos.z * _WindFrequency);
                float3 windOffset = float3(windWave, 0, windWave * 0.7) * (_WindStrength * heightFactor);
                worldPos += windOffset;

                // 2. 玩家互动弯折（基于 64x64 交互贴图与玩家距离）
                if (_GrassInteractionAreaSize > 0.001)
                {
                    float2 interactUV = (worldPos.xz - _GrassInteractionCenter.xz) / _GrassInteractionAreaSize + 0.5;
                    if (interactUV.x >= 0.0 && interactUV.x <= 1.0 && interactUV.y >= 0.0 && interactUV.y <= 1.0)
                    {
                        float interactWeight = tex2Dlod(_GrassInteractionRT, float4(interactUV, 0, 0)).r;
                        if (interactWeight > 0.01)
                        {
                            // 计算远离玩家方向的弯折向量
                            float2 awayVec = worldPos.xz - _PlayerWorldPos.xz;
                            float dist = length(awayVec) + 0.001;
                            float2 awayDir = awayVec / dist;

                            // 草尖被踩踏时向外推开并压低
                            worldPos.xz += awayDir * (interactWeight * _InteractionStrength * heightFactor);
                            worldPos.y -= interactWeight * _InteractionStrength * 0.5 * heightFactor;
                        }
                    }
                }

                output.positionCS = TransformWorldToHClip(worldPos);
                output.worldPos = worldPos;
                output.worldNormal = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 上下颜色渐变 (Root to Tip)
                half4 grassColor = lerp(_BaseColor, _TipColor, input.uv.y);

                // 基础漫反射光照
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalize(input.worldNormal), mainLight.direction));
                half3 lighting = (mainLight.color * (NdotL * 0.75 + 0.25));

                return half4(grassColor.rgb * lighting, grassColor.a);
            }
            ENDHLSL
        }
    }
}
