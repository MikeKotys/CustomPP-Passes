Shader "MCG/Unlit"
{
    Properties
    {
        _UnlitColor("Color", Color) = (1,1,1,1)
        _UnlitColorMap("ColorMap", 2D) = "white" {}
    }

    HLSLINCLUDE

    #pragma target 4.5


    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/UnlitProperties.hlsl"

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" "RenderType" = "HDUnlitShader" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

			Blend One One
			BlendOp Max
			ZWrite Off
			Cull Front
			ZTest Off

            HLSLPROGRAM

            #pragma only_renderers d3d11 playstation xboxone vulkan metal switch

            #pragma multi_compile_instancing

            #define SHADERPASS SHADERPASS_FORWARD_UNLIT

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/ShaderPass/UnlitSharePass.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/VertMesh.hlsl"

			float AttenuationMultiplier;

			float4 ColorMask0;
			float4 ColorMask1;

			PackedVaryingsType Vert(AttributesMesh inputMesh)
			{
				VaryingsType varyingsType;
				varyingsType.vmesh = VertMesh(inputMesh);
				return PackVaryingsType(varyingsType);
			}

			void Frag(PackedVaryingsToPS packedInput, out half4 color0 : SV_Target0, out half4 color1 : SV_Target1)
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(packedInput);
				FragInputs input = UnpackVaryingsMeshToFragInputs(packedInput.vmesh);

				// input.positionSS is SV_Position
				PositionInputs posInput = GetPositionInput(input.positionSS.xy, _ScreenSize.zw, input.positionSS.z, input.positionSS.w, input.positionRWS);

				float2 unlitColorMapUv = TRANSFORM_TEX(input.texCoord0.xy, _UnlitColorMap);
				color0 = SAMPLE_TEXTURE2D(_UnlitColorMap, sampler_UnlitColorMap, unlitColorMapUv) * _UnlitColor;

				color0 = min(1, color0 * AttenuationMultiplier);
				color1 = color0 * ColorMask1;
				color0 = color0 * ColorMask0;
			}

            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
        }
    }
}
