Shader "PixelSaur/GrassURP"
{
    Properties
    {
        [Header(Color Settings)]
        _BaseColor("草地根部颜色 (Base Color)", Color) = (0.10, 0.36, 0.08, 1.0)
        _TipColor("草地尖端颜色 (Tip Color)", Color) = (0.48, 0.84, 0.18, 1.0)
        _PressedColor("踩踏压弯颜色微调 (Pressed Tint)", Color) = (0.22, 0.52, 0.12, 1.0)

        [Header(Wind Simulation)]
        _WindSpeed("风速 (Wind Speed)", Range(0.1, 10.0)) = 2.5
        _WindStrength("风力强度 (Wind Strength)", Range(0.0, 1.5)) = 0.22
        _WindFrequency("风频波长 (Wind Frequency)", Range(0.05, 3.0)) = 0.6

        [Header(Interaction Settings)]
        _MaxBendAngle("最大受触弯折角度 (Max Bend Angle)", Range(15.0, 75.0)) = 45.0
        _InteractionStrength("互动反应灵敏度 (Touch Sensitivity)", Range(0.2, 2.0)) = 1.0
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
                float interactFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float4 _PressedColor;
                float _WindSpeed;
                float _WindStrength;
                float _WindFrequency;
                float _MaxBendAngle;
                float _InteractionStrength;
            CBUFFER_END

            // 全局交互数据（由 GrassInteractionManager 实时注入）
            sampler2D _GrassInteractionRT;
            float4 _GrassInteractionCenter;   // xz 为交互中心
            float _GrassInteractionAreaSize;  // 交互区域边长
            float4 _PlayerWorldPos;           // 玩家实时世界坐标 (w > 0 表示有效)
            float _PlayerFootprintRadius;     // 玩家踩踏半径

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // 获取单株草根部世界坐标与当前顶点世界坐标
                float3 bladeRootPos = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float heightFactor = input.uv.y; // 0 为草根，1 为草尖

                // 1. 计算 64x64 交互贴图采样 + 实时玩家距离双重保障交互
                float rtWeight = 0.0;
                float2 rtPushDir = float2(0.0, 1.0);

                if (_GrassInteractionAreaSize > 0.01)
                {
                    float2 interactUV = (bladeRootPos.xz - _GrassInteractionCenter.xz) / _GrassInteractionAreaSize + 0.5;
                    if (interactUV.x >= 0.0 && interactUV.x <= 1.0 && interactUV.y >= 0.0 && interactUV.y <= 1.0)
                    {
                        float4 rtSample = tex2Dlod(_GrassInteractionRT, float4(interactUV, 0, 0));
                        rtWeight = rtSample.r;
                        rtPushDir = rtSample.gb * 2.0 - 1.0;
                    }
                }

                // 实时距离场计算（保证角色移动瞬间零延迟排开草丛）
                float directWeight = 0.0;
                float2 directDir = float2(0.0, 1.0);
                if (_PlayerWorldPos.w > 0.1)
                {
                    float2 toBlade = bladeRootPos.xz - _PlayerWorldPos.xz;
                    float dist = length(toBlade);
                    float radius = max(0.4, _PlayerFootprintRadius);
                    directWeight = saturate(1.0 - (dist / radius));
                    directWeight = smoothstep(0.0, 1.0, directWeight);
                    if (dist > 0.0001)
                    {
                        directDir = toBlade / dist;
                    }
                }

                // 融合实时排开与 64x64 RT 持久压痕轨迹
                float interactWeight = max(rtWeight, directWeight);
                float2 awayDir = (directWeight >= rtWeight)
                    ? directDir
                    : (length(rtPushDir) > 0.01 ? normalize(rtPushDir) : directDir);

                // 2. 风力摆动（被踩扁的草风摆减弱）
                float windDamp = saturate(1.0 - interactWeight * 0.8);
                float windWave = sin(_Time.y * _WindSpeed + bladeRootPos.x * _WindFrequency + bladeRootPos.z * _WindFrequency);
                float windWaveZ = cos(_Time.y * (_WindSpeed * 0.8) + bladeRootPos.z * _WindFrequency);
                float3 windOffset = float3(windWave, 0.0, windWaveZ * 0.6) * (_WindStrength * heightFactor * windDamp);
                worldPos += windOffset;
                // 3. 角色接触走过拂动：微微压弯，最低倾角45°，绝不压扁贴地（犹如衣角或身躯走过轻轻碰拂）
                if (interactWeight > 0.001)
                {
                    float maxAngleRad = radians(_MaxBendAngle);
                    float touchFactor = saturate(interactWeight * _InteractionStrength);
                    // 弯折角度随高度渐进（草根 heightFactor=0 不动，草尖最大 45°）
                    float bendAngle = touchFactor * maxAngleRad * heightFactor;

                    float originalRelY = max(0.001, worldPos.y - bladeRootPos.y);
                    float sinAngle = sin(bendAngle);
                    float cosAngle = cos(bendAngle);

                    // 沿着远离角色的方向轻柔拂开，垂直方向保持 cos(angle) 高度（45° 时保留 70.7% 原高）
                    worldPos.xz += awayDir * (originalRelY * sinAngle);
                    worldPos.y = bladeRootPos.y + max(0.05 * heightFactor, originalRelY * cosAngle);
                }

                output.positionCS = TransformWorldToHClip(worldPos);
                output.worldPos = worldPos;
                output.worldNormal = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.interactFactor = saturate(interactWeight);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 根部到尖端渐变，叠加踩踏微调色
                half4 tipCol = lerp(_TipColor, _PressedColor, input.interactFactor * 0.15);
                half4 grassColor = lerp(_BaseColor, tipCol, input.uv.y);

                // 漫反射与环境光照明
                Light mainLight = GetMainLight();
                half NdotL = abs(dot(normalize(input.worldNormal), mainLight.direction));
                half ao = lerp(0.55, 1.0, input.uv.y);
                half3 lighting = mainLight.color * (NdotL * 0.65 + 0.35) * ao;

                return half4(grassColor.rgb * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
