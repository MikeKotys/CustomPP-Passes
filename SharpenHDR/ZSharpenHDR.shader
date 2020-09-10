Shader "Hidden/Shader/ZSharpenHDR"
{
	HLSLINCLUDE

	#pragma target 4.5
	#pragma only_renderers d3d11 playstation xboxone vulkan metal switch
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/FXAA.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"

	struct Attributes
	{
		uint vertexID : SV_VertexID;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};

	struct Varyings
	{
		float4 positionCS : SV_POSITION;
		float2 texcoord   : TEXCOORD0;
		UNITY_VERTEX_OUTPUT_STEREO
	};

	Varyings Vert(Attributes input)
	{
		Varyings output;
		UNITY_SETUP_INSTANCE_ID(input);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
		
		output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
		output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
		return output;
	}

	// List of properties to control your post process effect
	#define lumacoeff        float3(0.2558, 0.6511, 0.0931)
	#define sharp_clamp    0.165  //[0.000 to 1.000] Limits maximum amount of sharpening a pixel recieves - Default is 0.035
	#define offset_bias 1.0  //[0.0 to 6.0]
	uniform float _SharpenAmount = 1.0;

	TEXTURE2D_X(_InputTexture);
	TEXTURE2D(_BlurredTex);

	uniform float4 _MainTex_TexelSize;

	float4 CustomPostProcess(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		uint2 positionSS = input.texcoord * _ScreenSize.xy;
		float4 colorInput = LOAD_TEXTURE2D_X(_InputTexture, positionSS);
		//return float4(lerp(outColor, Luminance(outColor).xxx, _Intensity), 1);

		float3 ori = colorInput.rgb;

		// -- Combining the strength and luma multipliers --
		float3 sharp_strength_luma = (lumacoeff * _SharpenAmount); //I'll be combining even more multipliers with it later on

		// -- Gaussian filter --
		//   [ .25, .50, .25]     [ 1 , 2 , 1 ]
		//   [ .50,   1, .50]  =  [ 2 , 4 , 2 ]
		//   [ .25, .50, .25]     [ 1 , 2 , 1 ]
		float px = _MainTex_TexelSize.x;//1.0/
		float py = _MainTex_TexelSize.y;

		float4 blur_ori = SAMPLE_TEXTURE2D(_BlurredTex, s_trilinear_clamp_sampler, input.texcoord).rgba;

		// -- Calculate the sharpening --
		float3 sharp = ori - blur_ori;  //Subtracting the blurred image from the original image

		// -- Adjust strength of the sharpening and clamp it--
		float4 sharp_strength_luma_clamp = float4(sharp_strength_luma * (0.5 / sharp_clamp), 0.5); //Roll part of the clamp into the dot

		//Calculate the luma, adjust the strength, scale up and clamp
		float sharp_luma = clamp((dot(float4(sharp, 1.0), sharp_strength_luma_clamp)), 0.0, 1.0);
		sharp_luma = (sharp_clamp * 2.0) * sharp_luma - sharp_clamp; //scale down

		// -- Combining the values to get the final sharpened pixel	--
		colorInput.rgb = colorInput.rgb + sharp_luma;    // Add the sharpening to the input color.

		return colorInput;
	}

	ENDHLSL
	SubShader

	{
		Pass
		{
			Name "SharpenHDR"
			ZWrite Off
			ZTest Always
			Blend Off
			Cull Off
			HLSLPROGRAM
			#pragma fragment CustomPostProcess
			#pragma vertex Vert

			ENDHLSL
		}

	}

	Fallback Off

}