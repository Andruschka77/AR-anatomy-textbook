Shader "AnatomyAR/URP_XRay_Fresnel"
{
    Properties
    {
        [Header(XRay Settings)]
        _XRayColor ("X-Ray Rim Color", Color) = (0.2, 0.7, 1.0, 0.4)
        _FresnelPower ("Fresnel Power", Range(0.5, 6.0)) = 2.5
        _InnerAlpha ("Inner Transparency", Range(0.0, 1.0)) = 0.15
        _RimIntensity ("Rim Intensity", Range(0.5, 5.0)) = 2.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+100" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "XRayPass"
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
                float3 normalWS     : TEXCOORD0;
                float3 viewDirWS    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _XRayColor;
                float _FresnelPower;
                float _InnerAlpha;
                float _RimIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalize(normalInput.normalWS);
                output.viewDirWS = normalize(GetCameraPositionWS() - vertexInput.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);

                // Эффект Френеля: максимум на краях (силуэт) и прозрачность в центре
                float NdotV = saturate(dot(normal, viewDir));
                float fresnel = pow(1.0 - NdotV, _FresnelPower) * _RimIntensity;

                float alpha = saturate(_InnerAlpha + fresnel);
                float3 finalColor = _XRayColor.rgb * (1.0 + fresnel);

                return half4(finalColor, alpha * _XRayColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
