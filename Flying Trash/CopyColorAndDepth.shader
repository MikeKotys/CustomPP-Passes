Shader "Hidden/CopyTrashColorAndDepth"
{
    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch
	#pragma multi_compile ___ SHOW_HALF

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

	TEXTURE2D_X(_ColorBuffer);
	TEXTURE2D_X(_DepthBuffer);

	float _YThreshold = 0.35f;
	float4 _FillColor;
	float _MinSEColorAlpha = 0.23f;


    void FullScreenPass(Varyings varyings, out float4 Color : SV_Target, out float Depth : SV_Depth)
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

        // When sampling RTHandle texture, always use _RTHandleScale.xy to scale your UVs first.
        float2 uv = posInput.positionNDC.xy * _RTHandleScale.xy;
        float4 inputColor = SAMPLE_TEXTURE2D_X_LOD(_ColorBuffer, s_linear_clamp_sampler, uv, 0);

		clip(inputColor.a - 0.01f);

		Color = inputColor;
		Depth = SAMPLE_TEXTURE2D_X_LOD(_DepthBuffer, s_point_clamp_sampler, uv, 0).r;
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            Name "Custom Pass 0"

            ZWrite On
            ZTest LEqual
			Blend SrcAlpha OneMinusSrcAlpha
            Cull Back

            HLSLPROGRAM
                #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}