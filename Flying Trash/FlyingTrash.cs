using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using System;
using FadeableWall;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FlyingTrash
{
	public class FlyingTrash : CustomPass
	{
		public LayerMask TrashLayer = 1; // Layer mask Default enabled

		RTHandle ColorBuffer;
		RTHandle DepthBuffer;
		ShaderTagId[] NormalShaderTags;

		MaterialPropertyBlock ShaderProperties;

		// To make sure the shader ends up in the build, we keep it's reference in the custom pass
		[SerializeField, HideInInspector]
		Shader CopyShader;
		Material CopyMat;

		protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
		{//#colreg(darkorange);
			name = "Flying Trash";

			NormalShaderTags = new ShaderTagId[]
			{
				new ShaderTagId("Forward"),
				new ShaderTagId("ForwardOnly"),
				new ShaderTagId("SRPDefaultUnlit"),
			};
			ShaderProperties = new MaterialPropertyBlock();

			ColorBuffer = RTHandles.Alloc(
				Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
				colorFormat: GraphicsFormat.R16G16B16A16_SFloat, useDynamicScale: true, name: "Flying Trash Color Buffer");

			DepthBuffer = RTHandles.Alloc(
				Vector2.one, TextureXR.slices, depthBufferBits: DepthBits.Depth24, dimension: TextureXR.dimension,
				colorFormat: GraphicsFormat.R16_UInt, useDynamicScale: true, name: "Flying Trash Depth Buffer");

			CopyShader = Shader.Find("Hidden/CopyTrashColorAndDepth");
			CopyMat = CoreUtils.CreateEngineMaterial(CopyShader);
		}//#endcolreg



		protected override void AggregateCullingParameters(ref ScriptableCullingParameters cullingParameters, HDCamera hdCamera)
		{
			cullingParameters.cullingMask |= (uint)(int)TrashLayer;
		}


		protected override void Execute(ScriptableRenderContext renderContext,
			CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResult)
		{//#colreg(darkpurple);
			// Render blockers - objects that will prevent trash from rendering on top of them.
			CoreUtils.SetRenderTarget(cmd, DepthBuffer, DepthBuffer, ClearFlag.All);

			foreach (var mesh in FlyingTrashSystem.AllMeshes)
				mesh.RenderMesh(cmd);   //#color(purple);

			var stateBlock = new RenderStateBlock(RenderStateMask.Depth)
			{
				depthState = new DepthState(writeEnabled: true, compareFunction: CompareFunction.LessEqual),
				// We disable the stencil when the depth is overwritten but we don't write to it, to prevent writing to the stencil.
				stencilState = new StencilState(false),
			};
			RendererListDesc filteredMeshes = new RendererListDesc(NormalShaderTags, cullingResult, hdCamera.camera)
			{
				// We need the lighting render configuration to support rendering lit objects
				rendererConfiguration = PerObjectData.LightProbe | PerObjectData.LightProbeProxyVolume
				                                                 | PerObjectData.Lightmaps | PerObjectData.ShadowMask,
				renderQueueRange = RenderQueueRange.all,
				sortingCriteria = SortingCriteria.BackToFront,
				excludeObjectMotionVectors = false,
				stateBlock = stateBlock,
				layerMask = TrashLayer,
			};

			CoreUtils.SetRenderTarget(cmd, ColorBuffer, DepthBuffer, ClearFlag.None, clearColor: new Color(0, 0, 0, 0));
			HDUtils.DrawRendererList(renderContext, cmd, RendererList.Create(filteredMeshes));

			ShaderProperties.SetTexture("_ColorBuffer", ColorBuffer);
			ShaderProperties.SetTexture("_DepthBuffer", DepthBuffer);
			SetCameraRenderTarget(cmd);
			CoreUtils.DrawFullScreen(cmd, CopyMat, ShaderProperties, shaderPassId: 0);
		}//#endcolreg



		protected override void Cleanup()
		{//#colreg(black);
			if (ColorBuffer != null)
				ColorBuffer.Release();
			if (DepthBuffer != null)
				DepthBuffer.Release();
		}//#endcolreg
	}
}