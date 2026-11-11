Shader "PixelSaur/CreatureHighlight"
{
    Properties
    {
        _BaseColor ("Base Color (主体底色)", Color) = (0.25, 0.85, 0.35, 1.0)
        [HDR] _OutlineColor ("Outline Color (外圈描边高亮色)", Color) = (1.0, 0.15, 0.15, 1.0)
        _OutlineWidth ("Outline Width (描边外圈厚度)", Range(0.0, 0.15)) = 0.05
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // Pass 1: 法线挤出外轮廓 Pass (Inverted Hull Outline)
        Pass
        {
            Name "OutlinePass"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                // 仅当描边宽度 > 0 且颜色 Alpha > 0 时挤出外轮廓，否则压平
                float extrude = (_OutlineWidth > 0.001 && _OutlineColor.a > 0.01) ? _OutlineWidth : 0.0;
                float3 extrudedPos = input.positionOS.xyz + normalize(input.normalOS) * extrude;
                output.positionCS = TransformObjectToHClip(extrudedPos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_OutlineWidth <= 0.001 || _OutlineColor.a <= 0.01)
                {
                    discard;
                }
                return _OutlineColor;
            }
            ENDHLSL
        }

        // Pass 2: 主体表面渲染 Pass (标准漫反射加环境光)
        Pass
        {
            Name "ForwardLitPass"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                Light mainLight = GetMainLight();
                float3 normal = normalize(input.normalWS);
                float NdotL = saturate(dot(normal, mainLight.direction));
                float3 lighting = mainLight.color * (NdotL * 0.75 + 0.25);
                float3 finalRgb = _BaseColor.rgb * lighting;
                return half4(finalRgb, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
