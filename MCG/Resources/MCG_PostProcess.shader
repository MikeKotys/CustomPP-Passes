Shader "Hidden/MCG_PostProcess"
{
	HLSLINCLUDE

	#pragma target 4.5
	#pragma only_renderers d3d11 playstation xboxone vulkan metal switch
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

	#pragma multi_compile ___ LUT_ZERO_LUMA
	#pragma multi_compile ___ LUT_ONE
	#pragma multi_compile ___ LUT_ONE_LUMA
	#pragma multi_compile ___ LUT_TWO
	#pragma multi_compile ___ LUT_TWO_LUMA
	#pragma multi_compile ___ LUT_THREE
	#pragma multi_compile ___ LUT_THREE_LUMA
	#pragma multi_compile ___ LUT_FOUR
	#pragma multi_compile ___ LUT_FOUR_LUMA
	#pragma multi_compile ___ LUT_FIVE
	#pragma multi_compile ___ LUT_FIVE_LUMA
	#pragma multi_compile ___ LUT_SIX
	#pragma multi_compile ___ LUT_SIX_LUMA
	#pragma multi_compile ___ LUT_SEVEN
	#pragma multi_compile ___ LUT_SEVEN_LUMA
	#pragma multi_compile ___ LUT_DEFAULT
	#pragma multi_compile ___ VISION_CONES_ENABLED

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

	TEXTURE2D_X(_InputTexture);
	TEXTURE2D(_Attenuation1RT);
	TEXTURE2D(_Attenuation2RT);

	float OverrideSecondaryLCGs = 1;

	sampler3D _LUT3D0;
	float _LUT_Scale0;
	float _LUT_Offset0;
	float _LUT_Strength0;
	float LUMA_Sensitivity0;

	sampler3D _LUT3D1;
	float _LUT_Scale1;
	float _LUT_Offset1;
	float _LUT_Strength1;
	float LUMA_Sensitivity1;

	sampler3D _LUT3D2;
	float _LUT_Scale2;
	float _LUT_Offset2;
	float _LUT_Strength2;
	float LUMA_Sensitivity2;

	sampler3D _LUT3D3;
	float _LUT_Scale3;
	float _LUT_Offset3;
	float _LUT_Strength3;
	float LUMA_Sensitivity3;

	sampler3D _LUT3D4;
	float _LUT_Scale4;
	float _LUT_Offset4;
	float _LUT_Strength4;
	float LUMA_Sensitivity4;

	sampler3D _LUT3D5;
	float _LUT_Scale5;
	float _LUT_Offset5;
	float _LUT_Strength5;
	float LUMA_Sensitivity5;

	sampler3D _LUT3D6;
	float _LUT_Scale6;
	float _LUT_Offset6;
	float _LUT_Strength6;
	float LUMA_Sensitivity6;

	sampler3D _LUT3D7;
	float _LUT_Scale7;
	float _LUT_Offset7;
	float _LUT_Strength7;
	float LUMA_Sensitivity7;

	sampler3D _LUT3DVISION;
	float _LUT_ScaleVISION;
	float _LUT_OffsetVISION;
	float _LUT3DVISION_POWER;

	sampler3D _LUT3Default;
	float _LUT_ScaleDefault;
	float _LUT_OffsetDefault;
	float _LUT_StrengthDefault;

	float4 CustomPostProcess(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		uint2 positionSS = input.texcoord * _ScreenSize.xy;
		float4 result = (float4)0;
		float4 col = LOAD_TEXTURE2D_X(_InputTexture, positionSS);
		float4 atten = SAMPLE_TEXTURE2D(_Attenuation1RT, s_trilinear_clamp_sampler, input.texcoord).rgba;

		// 'maxColor' helps preserve HDR colors.
		float maxColor = max(1, max(max(col.r, col.g), col.b));	// make sure it's above 1 to prevent black screen.
		float invMaxColor = 1 / maxColor;
		col *= invMaxColor;

		float luma = dot(col, float3(0.299f, 0.587f, 0.114f));


		float4 lut = lerp(col.rgba, tex3D(_LUT3D0, col.rgb * _LUT_Scale0 + _LUT_Offset0), _LUT_Strength0) * OverrideSecondaryLCGs;
#ifdef LUT_ZERO_LUMA
		atten.r = saturate(atten.r - luma * LUMA_Sensitivity0);
#endif
		result += lut * atten.r;


		float fade = (1 - atten.r);	// how much influence the current LUT has left when all superior LUTs are excluded.
#ifdef LUT_ONE
		lut = lerp(col.rgba, tex3D(_LUT3D1, col.rgb * _LUT_Scale1 + _LUT_Offset1), _LUT_Strength1) * OverrideSecondaryLCGs;
#ifdef LUT_ONE_LUMA
		atten.g = saturate(atten.g - luma * LUMA_Sensitivity1);
#endif
		result += lut * atten.g * fade;
		fade *= (1 - atten.g);
#endif
#ifdef LUT_TWO
		lut = lerp(col.rgba, tex3D(_LUT3D2, col.rgb * _LUT_Scale2 + _LUT_Offset2), _LUT_Strength2) * OverrideSecondaryLCGs;
#ifdef LUT_TWO_LUMA
		atten.b = saturate(atten.b - luma * LUMA_Sensitivity2);
#endif
		result += lut * atten.b * fade;
		fade *= (1 - atten.b);
#endif
#ifdef LUT_THREE
		lut = lerp(col.rgba, tex3D(_LUT3D3, col.rgb * _LUT_Scale3 + _LUT_Offset3), _LUT_Strength3) * OverrideSecondaryLCGs;
#ifdef LUT_THREE_LUMA
		atten.a = saturate(atten.a - luma * LUMA_Sensitivity3);
#endif
		result += lut * atten.a * fade;
		fade *= (1 - atten.a);
#endif
#ifdef LUT_FOUR
		atten = SAMPLE_TEXTURE2D(_Attenuation2RT, s_trilinear_clamp_sampler, input.texcoord).rgba;
		lut = lerp(col.rgba, tex3D(_LUT3D4, col.rgb * _LUT_Scale4 + _LUT_Offset4), _LUT_Strength4) * OverrideSecondaryLCGs;
#ifdef LUT_FOUR_LUMA
		atten.r = saturate(atten.r - luma * LUMA_Sensitivity4);
#endif
		result += lut * atten.r * fade;
		fade *= (1 - atten.r);
#endif
#ifdef LUT_FIVE
		lut = lerp(col.rgba, tex3D(_LUT3D5, col.rgb * _LUT_Scale5 + _LUT_Offset5), _LUT_Strength5) * OverrideSecondaryLCGs;
#ifdef LUT_FIVE_LUMA
		atten.g = saturate(atten.g - luma * LUMA_Sensitivity5);
#endif
		result += lut * atten.g * fade;
		fade *= (1 - atten.g);
#endif
#ifdef LUT_SIX
		lut = lerp(col.rgba, tex3D(_LUT3D6, col.rgb * _LUT_Scale6 + _LUT_Offset6), _LUT_Strength6) * OverrideSecondaryLCGs;
#ifdef LUT_SIX_LUMA
		atten.b = saturate(atten.b - luma * LUMA_Sensitivity6);
#endif
		result += lut * atten.b * fade;
		fade *= (1 - atten.b);
#endif
#ifdef LUT_SEVEN
		lut = lerp(col.rgba, tex3D(_LUT3D7, col.rgb * _LUT_Scale7 + _LUT_Offset7), _LUT_Strength7) * OverrideSecondaryLCGs;
#ifdef LUT_SEVEN_LUMA
		atten.a = saturate(atten.a - luma * LUMA_Sensitivity7);
#endif
		result += lut * atten.a * fade;
#endif
#ifdef LUT_DEFAULT
		fade *= (1 - atten.a);
		lut = lerp(col.rgba, tex3D(_LUT3Default, col.rgb * _LUT_ScaleDefault + _LUT_OffsetDefault), _LUT_StrengthDefault);
		result += lut * fade;
#endif


#ifdef VISION_CONES_ENABLED
		fixed visionConeAlpha = tex2D(_VisionConeTex, i.uv);
#endif

		result.rgb += col.rgb * fade;

#ifdef VISION_CONES_ENABLED
		result.rgb = lerp(result.rgba,
			lerp(result.rgba,
				tex3D(_LUT3DVISION, result.rgb * _LUT_ScaleVISION + _LUT_OffsetVISION), visionConeAlpha), _LUT3DVISION_POWER);
#endif

		result *= maxColor;

		return result;
	}

	ENDHLSL
	SubShader

	{
		Pass
		{
			Name "MCG_PostProcess"
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