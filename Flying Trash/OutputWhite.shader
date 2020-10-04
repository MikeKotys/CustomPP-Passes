Shader "Hidden/OuputWhite"
{
    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch
	#pragma multi_compile ___ SHOW_HALF

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

	void FullScreenPass(Varyings varyings, out float4 Color : SV_Target, out float Depth : SV_Depth)
    {
		Color = float4(1, 1, 1, 1);
		Depth = 1;
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            Name "Custom Pass 0"

            ZWrite On
            ZTest Always
			Blend One Zero
            Cull Off

            HLSLPROGRAM
                #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}