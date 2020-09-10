using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FadeableWall
{
	public class FadeWall : CustomPass
	{
		public static FadeWall Instance;
#if UNITY_EDITOR
		[Layer]
#endif
		public int FadeWallLayer = 0;
#if UNITY_EDITOR
		[Layer]
#endif
		public int FadeWallFrozenLayer = 0;
#if UNITY_EDITOR
		[Layer]
#endif
		public int ShowEdgesLayer = 0;
#if UNITY_EDITOR
		[Layer]
#endif
		public int ShowEdgesFrozenLayer = 0;

		[Range(0, 1)]
		public float Opacity = 1;

		[Range(.01f, 1)]
		public float MinEdgeAlpha = .23f;

		[Range(.1f, 5)]
		public float EdgeDetectThreshold = 1;
		[Range(1, 6)]
		public int EdgeRadius = 2;
		[ColorUsage(false, true)]
		public Color GlowColor = Color.white;
		[ColorUsage(false, true)]
		public Color InvisColor = Color.white;

		// To make sure the shader will ends up in the build, we keep it's reference in the custom pass
		[SerializeField, HideInInspector]
		Shader CopyShader;
		Material CopyMatNorm;
		Material CopyMatHalf;

		[SerializeField, HideInInspector]
		Shader ShowEdgesShader;
		Material ShowEdgesMat;
		int CompositingPass;

		MaterialPropertyBlock ShaderProperties;
		ShaderTagId[] NormalShaderTags;
		ShaderTagId[] DepthOnlyShaderTags;
		RTHandle FadeWallBuffer;
		RTHandle DepthBuffer;

		public bool IsBusy => isBusy;
		protected bool isBusy = false;

		public float YThreshold = 0.35f;

		protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
		{//#colreg(darkorange);
#if UNITY_EDITOR
			if (!HideWarnings && Instance != this && Instance != null)
				Debug.LogError("More than 1 instance of the '" + nameof(FadeWall) + "' custom pass in the scene!");
			EditorApplication.playModeStateChanged += PlayModeStateChanged;
#endif
			Instance = this;
			SceneManager.sceneLoaded += CleanupStaticVariables;

			CopyShader = Shader.Find("Hidden/CopyColorAndDepth");
			CopyMatNorm = CoreUtils.CreateEngineMaterial(CopyShader);
			CopyMatHalf = CoreUtils.CreateEngineMaterial(CopyShader);
			CopyMatHalf.EnableKeyword("SHOW_HALF");
			CopyMatHalf.SetFloat("_YThreshold", YThreshold);
			CopyMatHalf.SetFloat("_MinEdgeAlpha", MinEdgeAlpha);
			CopyMatHalf.SetColor("_InvisColor", InvisColor);
			ShaderProperties = new MaterialPropertyBlock();


			ShowEdgesShader = Shader.Find("FullScreen/ShowEdges");
			ShowEdgesMat = CoreUtils.CreateEngineMaterial(ShowEdgesShader);
			CompositingPass = ShowEdgesMat.FindPass("Compositing");


			NormalShaderTags = new ShaderTagId[]
			{
				new ShaderTagId("Forward"),
				new ShaderTagId("ForwardOnly"),
				new ShaderTagId("SRPDefaultUnlit"),
			};
			DepthOnlyShaderTags = new ShaderTagId[2]
			{
				new ShaderTagId("DepthOnly"),
				new ShaderTagId("DepthForwardOnly"),
			};

			FadeWallBuffer = RTHandles.Alloc(
				Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
				colorFormat: GraphicsFormat.R16G16B16A16_SFloat, useDynamicScale: true, name: "Fade Wall Color Buffer");

			DepthBuffer = RTHandles.Alloc(
				Vector2.one, TextureXR.slices, depthBufferBits: DepthBits.Depth16, dimension: TextureXR.dimension,
				colorFormat: GraphicsFormat.R16_UInt, useDynamicScale: true, name: "Fade Wall Depth Buffer");

			name = "Fade Walls";
		}//#endcolreg



		protected override void AggregateCullingParameters(ref ScriptableCullingParameters cullingParameters, HDCamera hdCamera)
		{
			//base.AggregateCullingParameters(ref cullingParameters, hdCamera);
			cullingParameters.cullingMask = (uint)LayerMask.GetMask(LayerMask.LayerToName(FadeWallLayer),
				LayerMask.LayerToName(ShowEdgesLayer), LayerMask.LayerToName(ShowEdgesFrozenLayer));
		}

		void RenderFadeWall(ScriptableRenderContext renderContext,
			CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResult, int layer)
		{//#colreg(darkpurple);
			var stateBlock = new RenderStateBlock(RenderStateMask.Depth)
			{
				depthState = new DepthState(writeEnabled: true, compareFunction: CompareFunction.LessEqual),
				// We disable the stencil when the depth is overwritten but we don't write to it, to prevent writing to the stencil.
				stencilState = new StencilState(false),
			};
			var filteredMeshes = new RendererListDesc(NormalShaderTags, cullingResult, hdCamera.camera)
			{
				// We need the lighting render configuration to support rendering lit objects
				rendererConfiguration = PerObjectData.LightProbe | PerObjectData.LightProbeProxyVolume
					| PerObjectData.Lightmaps | PerObjectData.ShadowMask,
				renderQueueRange = RenderQueueRange.all,
				sortingCriteria = SortingCriteria.BackToFront,
				excludeObjectMotionVectors = false,
				stateBlock = stateBlock,
				layerMask = 1 << FadeWallLayer,
			};


			CoreUtils.SetRenderTarget(cmd, FadeWallBuffer, DepthBuffer, ClearFlag.All, clearColor: new Color(0, 0, 0, 0));
			var finalMeshes = RendererList.Create(filteredMeshes);
			//finalMeshes.filteringSettings.renderingLayerMask |= 2;
			HDUtils.DrawRendererList(renderContext, cmd, finalMeshes);

			SetCameraRenderTarget(cmd);

			ShaderProperties.SetFloat("_Opacity", Opacity);
			ShaderProperties.SetFloat("_Opacity", Opacity);
			ShaderProperties.SetTexture("_FadeWallBuffer", FadeWallBuffer);
			ShaderProperties.SetTexture("_DepthBuffer", DepthBuffer);
			CoreUtils.DrawFullScreen(cmd, CopyMatNorm, ShaderProperties, shaderPassId: 0);
		}//#endcolreg

		void RenderShowEdges(ScriptableRenderContext renderContext,
			CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResult, int layer)
		{//#colreg(darkpurple);
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
				layerMask = 1 << layer,
			};

			CoreUtils.SetRenderTarget(cmd, FadeWallBuffer, DepthBuffer, ClearFlag.All, clearColor: new Color(0, 0, 0, 0));
			HDUtils.DrawRendererList(renderContext, cmd, RendererList.Create(filteredMeshes));

			SetCameraRenderTarget(cmd);

			ShaderProperties.SetFloat("_Opacity", Opacity);
			ShaderProperties.SetFloat("_Opacity", Opacity);
			ShaderProperties.SetTexture("_FadeWallBuffer", FadeWallBuffer);
			ShaderProperties.SetTexture("_DepthBuffer", DepthBuffer);
			CoreUtils.DrawFullScreen(cmd, CopyMatHalf, ShaderProperties, shaderPassId: 0);


			// Now render depth only
			var rasterizerState = new RasterState()
			{
				cullingMode = CullMode.Front,
			};
			stateBlock = new RenderStateBlock(RenderStateMask.Depth | RenderStateMask.Raster)
			{
				depthState = new DepthState(writeEnabled: true, compareFunction: CompareFunction.GreaterEqual),
				// We disable the stencil when the depth is overwritten but we don't write to it, to prevent writing to the stencil.
				stencilState = new StencilState(false),
				rasterState = rasterizerState
			};
			filteredMeshes = new RendererListDesc(DepthOnlyShaderTags, cullingResult, hdCamera.camera)
			{
				// We need the lighting render configuration to support rendering lit objects
				rendererConfiguration = PerObjectData.LightProbe | PerObjectData.LightProbeProxyVolume
					| PerObjectData.Lightmaps | PerObjectData.ShadowMask,
				renderQueueRange = RenderQueueRange.all,
				sortingCriteria = SortingCriteria.QuantizedFrontToBack,
				excludeObjectMotionVectors = false,
				stateBlock = stateBlock,
				layerMask = 1 << layer,
			};

			// DO NOT CLEAR DEPTH - we will overwrite with GreaterEqual on top.
			CoreUtils.SetRenderTarget(cmd, DepthBuffer, ClearFlag.None);
			HDUtils.DrawRendererList(renderContext, cmd, RendererList.Create(filteredMeshes));

			// Now render edges
			ShowEdgesMat.SetFloat("_EdgeDetectThreshold", EdgeDetectThreshold);
			ShowEdgesMat.SetColor("_GlowColor", GlowColor);
			ShowEdgesMat.SetFloat("_EdgeRadius", (float)EdgeRadius);
			ShowEdgesMat.SetTexture("_DepthBuffer", DepthBuffer);
			ShowEdgesMat.SetFloat("_InvOpacity", 1 - Opacity);
			ShowEdgesMat.SetFloat("_YThreshold", YThreshold);

			SetCameraRenderTarget(cmd);
			CoreUtils.DrawFullScreen(cmd, ShowEdgesMat, shaderPassId: CompositingPass);

		}//#endcolreg

		protected override void Execute(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResult)
		{//#colreg(darkpurple);
			//RenderFadeWall(renderContext, cmd, hdCamera, cullingResult, FadeWallFrozenLayer);
			RenderFadeWall(renderContext, cmd, hdCamera, cullingResult, FadeWallLayer);

			//RenderShowEdges(renderContext, cmd, hdCamera, cullingResult, ShowEdgesFrozenLayer);
			RenderShowEdges(renderContext, cmd, hdCamera, cullingResult, ShowEdgesLayer);
		}//#endcolreg

		protected override void Cleanup()
		{
			if (FadeWallBuffer != null)
				FadeWallBuffer.Release();
			if (DepthBuffer != null)
				DepthBuffer.Release();
			CoreUtils.Destroy(CopyMatNorm);
			CoreUtils.Destroy(CopyMatHalf);

			CoreUtils.Destroy(ShowEdgesMat);
		}

		Fadable Owner;

		public void GainMonopolisticControl(Fadable owner, float startingOpacity)
		{//#colreg(black);
			if (!isBusy)
			{
				isBusy = true;

				Owner = owner;

				Opacity = startingOpacity;
			}
		}//#endcolreg

		public void ReleaseMonopolisticControl(Fadable owner)
		{//#colreg(black);
			if (isBusy && Owner == owner)
			{
				Owner = null;
				isBusy = false;
			}
		}//#endcolreg


		public virtual bool ChangeOpacity(bool increase)
		{//#colreg(darkblue);
			bool breakRoutine = false;

			if (increase)
			{
				Opacity += Time.deltaTime * 4;

				if (Opacity >= 1)
				{
					Opacity = 1;
					breakRoutine = true;
				}
			}
			else
			{
				Opacity -= Time.deltaTime * 4;

				if (Opacity <= 0)
				{
					Opacity = 0;
					breakRoutine = true;
				}
			}

			return breakRoutine;
		}//#endcolreg



		protected void CleanupStaticVariables(Scene scene, LoadSceneMode mode)
		{
			Instance = null;
			SceneManager.sceneLoaded -= CleanupStaticVariables;
		}

#if UNITY_EDITOR
		protected virtual void PlayModeStateChanged(PlayModeStateChange change)
		{//#colreg(black);
			if (change == PlayModeStateChange.ExitingPlayMode)
			{
				Instance = null;
				HideWarnings = true;
				EditorApplication.playModeStateChanged -= PlayModeStateChanged;
			}
		}//#endcolreg

		protected static bool HideWarnings = false;
#endif
	}
}