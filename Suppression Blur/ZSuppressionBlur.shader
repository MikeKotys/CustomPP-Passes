Shader "Hidden/Shader/ZSuppressionBlur"
{
	HLSLINCLUDE

	#pragma target 4.5
	#pragma only_renderers d3d11 playstation xboxone vulkan metal switch
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/FXAA.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"

	TEXTURE2D_X(_SourceTex);
	TEXTURE2D(_MainTex);
	TEXTURE2D(_Bloom);

	uniform half4 _MainTex_TexelSize;
	uniform half4 _Parameter;

	struct Attributes
	{
		uint vertexID : SV_VertexID;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};

	struct v2f_tap
	{
		float4 pos : SV_POSITION;
		float2 texcoord   : TEXCOORD0;
		float2 uv20 : TEXCOORD1;
		float2 uv21 : TEXCOORD2;
		float2 uv22 : TEXCOORD3;
		float2 uv23 : TEXCOORD4;
		UNITY_VERTEX_OUTPUT_STEREO
	};

	v2f_tap vert4Tap(Attributes input)
	{
		v2f_tap output;
		UNITY_SETUP_INSTANCE_ID(input);
		//UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

		output.pos = GetFullScreenTriangleVertexPosition(input.vertexID);
		output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
		output.uv20 = output.texcoord + _MainTex_TexelSize.xy;
		output.uv21 = output.texcoord + _MainTex_TexelSize.xy * float2(-0.5h, -0.5h);
		output.uv22 = output.texcoord + _MainTex_TexelSize.xy * float2(0.5h, -0.5h);
		output.uv23 = output.texcoord + _MainTex_TexelSize.xy * float2(-0.5h, 0.5h);
		return output;
	}

	float4 fragDownsample(v2f_tap i) : SV_Target
	{
		uint2 positionSS = i.uv20 * _ScreenSize.xy;
		float4 color = LOAD_TEXTURE2D_X(_SourceTex, positionSS);
		positionSS = i.uv21 * _ScreenSize.xy;
		color += LOAD_TEXTURE2D_X(_SourceTex, positionSS);
		positionSS = i.uv22 * _ScreenSize.xy;
		color += LOAD_TEXTURE2D_X(_SourceTex, positionSS);
		positionSS = i.uv23 * _ScreenSize.xy;
		color += LOAD_TEXTURE2D_X(_SourceTex, positionSS);

		return color / 4;
	}

	// weight curves

	static const float curve[7] =
	{
		0.0205, 0.0855, 0.232, 0.324, 0.232, 0.0855, 0.0205
	};// gauss'ish blur weights

	static const float4 curve4[7] =
	{
		half4(0.0205,0.0205,0.0205,0),
		half4(0.0855,0.0855,0.0855,0),
		half4(0.232,0.232,0.232,0),
		half4(0.324,0.324,0.324,1),
		half4(0.232,0.232,0.232,0),
		half4(0.0855,0.0855,0.0855,0),
		half4(0.0205,0.0205,0.0205,0)
	};

	// List of properties to control your post process effect
	struct v2f_withBlurCoords8
	{
		float4 pos : SV_POSITION;
		float4 uv : TEXCOORD0;
		float1 offs : TEXCOORD1;
	};

	v2f_withBlurCoords8 vertBlurHorizontal(Attributes input)
	{
		v2f_withBlurCoords8 output;
		UNITY_SETUP_INSTANCE_ID(input);
		output.pos = GetFullScreenTriangleVertexPosition(input.vertexID);
		output.uv.xy = GetFullScreenTriangleTexCoord(input.vertexID);

		output.uv = float4(output.uv.xy, 1, 1);
		output.offs = _MainTex_TexelSize.xy * float2(1.0, 0.0) * _Parameter.x;

		return output;
	}

	v2f_withBlurCoords8 vertBlurVertical(Attributes input)
	{
		v2f_withBlurCoords8 output;
		UNITY_SETUP_INSTANCE_ID(input);
		output.pos = GetFullScreenTriangleVertexPosition(input.vertexID);
		output.uv.xy = GetFullScreenTriangleTexCoord(input.vertexID);

		output.uv = float4(output.uv.xy, 1, 1);
		output.offs = _MainTex_TexelSize.xy * float2(0.0, 1.0) * _Parameter.x;

		return output;
	}

	float4 fragBlur8(v2f_withBlurCoords8 i) : SV_Target
	{
		float2 uv = i.uv.xy;
		float2 netFilterWidth = i.offs;
		float2 coords = uv - netFilterWidth * 3.0;

		float4 color = 0;
		for (int l = 0; l < 7; l++)
		{
			float4 tap = SAMPLE_TEXTURE2D(_MainTex, s_linear_clamp_sampler, coords);
			color += tap * curve4[l];
			coords += netFilterWidth;
		}
		return color;
	}

	struct v2f_withBlurCoordsSGX
	{
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
		float4 offs[3] : TEXCOORD1;
	};


	v2f_withBlurCoordsSGX vertBlurHorizontalSGX(Attributes v)
	{
		v2f_withBlurCoordsSGX o;
		UNITY_SETUP_INSTANCE_ID(v);
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);

		o.uv = GetFullScreenTriangleTexCoord(v.vertexID);
		half2 netFilterWidth = _MainTex_TexelSize.xy * half2(1.0, 0.0) * _Parameter.x;
		half4 coords = -netFilterWidth.xyxy * 3.0;

		o.offs[0] = o.uv.xyxy + coords * half4(1.0h, 1.0h, -1.0h, -1.0h);
		coords += netFilterWidth.xyxy;
		o.offs[1] = o.uv.xyxy + coords * half4(1.0h, 1.0h, -1.0h, -1.0h);
		coords += netFilterWidth.xyxy;
		o.offs[2] = o.uv.xyxy + coords * half4(1.0h, 1.0h, -1.0h, -1.0h);

		return o;
	}

	v2f_withBlurCoordsSGX vertBlurVerticalSGX(Attributes v)
	{
		v2f_withBlurCoordsSGX o;
		UNITY_SETUP_INSTANCE_ID(v);
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
		o.uv.xy = GetFullScreenTriangleTexCoord(v.vertexID);

		o.uv = half4(o.uv.xy, 1, 1);
		half2 netFilterWidth = _MainTex_TexelSize.xy * half2(0.0, 1.0) * _Parameter.x;
		half4 coords = -netFilterWidth.xyxy * 3.0;

		o.offs[0] = o.uv.xyxy + coords * half4(1.0h, 1.0h, -1.0h, -1.0h);
		coords += netFilterWidth.xyxy;
		o.offs[1] = o.uv.xyxy + coords * half4(1.0h, 1.0h, -1.0h, -1.0h);
		coords += netFilterWidth.xyxy;
		o.offs[2] = o.uv.xyxy + coords * half4(1.0h, 1.0h, -1.0h, -1.0h);

		return o;
	}

	half4 fragBlurSGX(v2f_withBlurCoordsSGX i) : SV_Target
	{
		half2 uv = i.uv.xy;

		float4 color = SAMPLE_TEXTURE2D(_MainTex, s_linear_clamp_sampler, i.uv) * curve4[3];

		for (int l = 0; l < 3; l++)
		{
			float4 tapA = SAMPLE_TEXTURE2D(_MainTex, s_linear_clamp_sampler, i.offs[l].xy);
			float4 tapB = SAMPLE_TEXTURE2D(_MainTex, s_linear_clamp_sampler, i.offs[l].zw);
			color += (tapA + tapB) * curve4[l];
		}

		return color;
	}


	struct v2f_combine
	{
		float4 pos : SV_POSITION;
		half2 uv : TEXCOORD0;
	};

	v2f_combine vertCombine(Attributes v)
	{
		v2f_combine o;
		UNITY_SETUP_INSTANCE_ID(v);
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
		o.uv.xy = GetFullScreenTriangleTexCoord(v.vertexID);

		return o;
	}


	TEXTURE2D(_BlurredTex);
	TEXTURE2D(_SuppressionMap1);
	TEXTURE2D(_SuppressionMapNoise);
	float _SuppressionNoisePower = 0;
	float _BlackNWhitePower = 0;


	float4 fragCombine(v2f_combine i) : SV_Target
	{
		uint2 positionSS = i.uv * _ScreenSize.xy;
		float4 color = LOAD_TEXTURE2D_X(_SourceTex, positionSS);
		float4 blurredColor = SAMPLE_TEXTURE2D(_BlurredTex, s_linear_clamp_sampler, i.uv);
		float suppressionMap1 = SAMPLE_TEXTURE2D(_SuppressionMap1, s_linear_clamp_sampler, i.uv).r;
		float suppressionMapNoise = SAMPLE_TEXTURE2D(_SuppressionMapNoise, s_linear_clamp_sampler, i.uv).r;

		float lerpValue = lerp(0, suppressionMap1, saturate(suppressionMapNoise + _SuppressionNoisePower));

		color = lerp(color, blurredColor, lerpValue);
		float4 luma = dot(color, float3(0.299f, 0.587f, 0.114f)) * .5f;
		color = lerp(color, luma, lerp(0, suppressionMap1, _BlackNWhitePower));

		return color;
	}

	ENDHLSL
	SubShader
	{
		ZTest Always Cull Off ZWrite Off Blend Off

		// 0
		Pass
		{
			Name "ZBlur0"
			HLSLPROGRAM

			#pragma vertex vert4Tap
			#pragma fragment fragDownsample

			ENDHLSL
		}

		// 1
		Pass
		{
			Name "ZBlur1"
			HLSLPROGRAM

			#pragma vertex vertBlurVertical
			#pragma fragment fragBlur8

			ENDHLSL
		}

		// 2
		Pass
		{
			Name "ZBlur2"
			HLSLPROGRAM

			#pragma vertex vertBlurHorizontal
			#pragma fragment fragBlur8

			ENDHLSL
		}

		// alternate blur
		// 3
		Pass
		{
			Name "ZBlur3"
			HLSLPROGRAM

			#pragma vertex vertBlurVerticalSGX
			#pragma fragment fragBlurSGX

			ENDHLSL
		}

		// 4
		Pass
		{
			Name "ZBlur4"
			HLSLPROGRAM

			#pragma vertex vertBlurHorizontalSGX
			#pragma fragment fragBlurSGX

			ENDHLSL
		}

		// 5	Combine
		Pass
		{
			Name "ZBlur5"

			HLSLPROGRAM

			#pragma vertex vertCombine
			#pragma fragment fragCombine

			ENDHLSL
		}

	}

	Fallback Off
}