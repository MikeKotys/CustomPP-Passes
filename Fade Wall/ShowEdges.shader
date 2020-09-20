Shader "FullScreen/ShowEdges"
{
    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"
    //#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"

    // The PositionInputs struct allow you to retrieve a lot of useful information for your fullScreenShader:
    // struct PositionInputs
    // {
    //     float3 positionWS;  // World space position (could be camera-relative)
    //     float2 positionNDC; // Normalized screen coordinates within the viewport    : [0, 1) (with the half-pixel offset)
    //     uint2  positionSS;  // Screen space pixel coordinates                       : [0, NumPixels)
    //     uint2  tileCoord;   // Screen tile coordinates                              : [0, NumTiles)
    //     float  deviceDepth; // Depth from the depth buffer                          : [0, 1] (typically reversed)
    //     float  linearDepth; // View space Z coordinate                              : [Near, Far]
    // };

    // To sample custom buffers, you have access to these functions:
    // But be careful, on most platforms you can't sample to the bound color buffer. It means that you
    // can't use the SampleCustomColor when the pass color buffer is set to custom (and same for camera the buffer).
    // float4 SampleCustomColor(float2 uv);
    // float4 LoadCustomColor(uint2 pixelCoords);
    // float LoadCustomDepth(uint2 pixelCoords);
    // float SampleCustomDepth(float2 uv);

    // There are also a lot of utility function you can use inside Common.hlsl and Color.hlsl,
    // you can check them out in the source code of the core SRP package.

	TEXTURE2D_X(_DepthBuffer);
    float _EdgeDetectThreshold;
	float3 _EdgeColor;
    float _EdgeRadius;
	
    //float SampleClampedDepth(float2 uv) { return SampleCameraDepth(clamp(uv, _ScreenSize.zw, 1 - _ScreenSize.zw)).r; }

    float EdgeDetect(float2 uv, float depthThreshold, float normalThreshold)
    {
        normalThreshold *= _EdgeDetectThreshold;
        depthThreshold *= _EdgeDetectThreshold;
        float halfScaleFloor = floor(_EdgeRadius * 0.5);
        float halfScaleCeil = ceil(_EdgeRadius * 0.5);
    
        // Compute uv position to fetch depth informations
        float2 bottomLeftUV = uv - float2(_ScreenSize.zw.x, _ScreenSize.zw.y) * halfScaleFloor;
        float2 topRightUV = uv + float2(_ScreenSize.zw.x, _ScreenSize.zw.y) * halfScaleCeil;
        float2 bottomRightUV = uv + float2(_ScreenSize.zw.x * halfScaleCeil, -_ScreenSize.zw.y * halfScaleFloor);
        float2 topLeftUV = uv + float2(-_ScreenSize.zw.x * halfScaleFloor, _ScreenSize.zw.y * halfScaleCeil);
    
        // Depth from camera buffer
        //float depth0 = SampleClampedDepth(bottomLeftUV);
        //float depth1 = SampleClampedDepth(topRightUV);
        //float depth2 = SampleClampedDepth(bottomRightUV);
        //float depth3 = SampleClampedDepth(topLeftUV);
		float depth0 = SAMPLE_TEXTURE2D_X_LOD(_DepthBuffer, s_point_clamp_sampler, bottomLeftUV, 0).r;
		float depth1 = SAMPLE_TEXTURE2D_X_LOD(_DepthBuffer, s_point_clamp_sampler, topRightUV, 0).r;
		float depth2 = SAMPLE_TEXTURE2D_X_LOD(_DepthBuffer, s_point_clamp_sampler, bottomRightUV, 0).r;
		float depth3 = SAMPLE_TEXTURE2D_X_LOD(_DepthBuffer, s_point_clamp_sampler, topLeftUV, 0).r;

        float depthDerivative0 = depth1 - depth0;
        float depthDerivative1 = depth3 - depth2;
    
        float edgeDepth = sqrt(pow(depthDerivative0, 2) + pow(depthDerivative1, 2)) * 100;

        float newDepthThreshold = depthThreshold * depth0;
        edgeDepth = edgeDepth > newDepthThreshold ? 1 : 0;
    
		return edgeDepth;
		//// Normals extracted from the camera normal buffer
  //      NormalData normalData0, normalData1, normalData2, normalData3;
  //      DecodeFromNormalBuffer(_ScreenSize.xy * bottomLeftUV, normalData0);
  //      DecodeFromNormalBuffer(_ScreenSize.xy * topRightUV, normalData1);
  //      DecodeFromNormalBuffer(_ScreenSize.xy * bottomRightUV, normalData2);
  //      DecodeFromNormalBuffer(_ScreenSize.xy * topLeftUV, normalData3);
  //  
  //      float3 normalFiniteDifference0 = normalData1.normalWS - normalData0.normalWS;
  //      float3 normalFiniteDifference1 = normalData3.normalWS - normalData2.normalWS;
  //  
  //      float edgeNormal = sqrt(dot(normalFiniteDifference0, normalFiniteDifference0) + dot(normalFiniteDifference1, normalFiniteDifference1));
  //      edgeNormal = edgeNormal > normalThreshold ? 1 : 0;

  //      // Combined
  //      return max(edgeDepth, edgeNormal);
    }


	float _InvOpacity;
	float _YThreshold = 0.35f;


    float4 Compositing(Varyings varyings) : SV_Target
    {
		float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
		float2 uv = posInput.positionNDC.xy * _RTHandleScale.xy;

		float3 edgeDetectColor = EdgeDetect(uv, 2, 1) * _EdgeColor;

        // Remove the edge detect effect between the sky and objects when the object is inside the sphere
        edgeDetectColor *= depth != UNITY_RAW_FAR_CLIP_VALUE;

		if (posInput.positionNDC.y < _YThreshold)
			return float4(0, 0, 0, 0);

		float4 color = float4(edgeDetectColor, depth) * _InvOpacity;
		color *= color;
		color *= color;
		return float4(color.rgb, max(color.r, max(color.g, color.b)));
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            Name "Compositing"

            ZWrite Off
            ZTest Always
			Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
                #pragma fragment Compositing
            ENDHLSL
        }
    }
    Fallback Off
}
