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
	/// <summary>Creates a fading wall effect. Works together with <see cref="Fadable"/>. Preforms up to 3 operations simultaneously:
	/// 1. Drawing 'Fade Wall' models in the process of fading (FadeWalls layer).
	/// 2. Drawing 'Show Edges' models in the process of fading (ShowEdges layer).
	/// 3. Drawing 'Show Edges' models that have faded out completely and are currenty semi-transparent (SEFrozen Layer).
	/// A <see cref="Fadable"/> class can controll one of the first 2 processes in a monopolistic way (untill models fade
	/// in/out completely).</summary>
	public class FadeWall : CustomPass
	{
		/// <summary>Singleton.</summary>
		public static FadeWall Instance;

#if UNITY_EDITOR
		[Layer, Tooltip("The layer of the models that are currently fading away without showing their edges.")]
#endif
		public int FadeWallLayer = 0;
		[NonSerialized, HideInInspector]
		public float FadeWallOpacity = 1;

#if UNITY_EDITOR
		[Layer, Tooltip("The layer of the models that are currently fading away WITH showing their edges.")]
#endif
		public int ShowEdgesLayer = 0;
		[NonSerialized, HideInInspector]
		public float ShowEdgesOpacity = 1;
#if UNITY_EDITOR
		[Layer, Tooltip("Buildings that should be shown with the 'Show Edges' effect will be set to this layer" +
			" when fully faded.")]
#endif
		public int ShowEdgesFrozenLayer = 0;

		/// <summary>The models with the 'Show edges' effect never fade completely - they fade untill their alpha is
		/// equal to this value.</summary>
		[Range(.01f, 1)]
		public float MinSEColorAlpha = .23f;

		[Range(.1f, 5)]
		public float EdgeDetectThreshold = 1;
		[Range(1, 6)]
		public int EdgeRadius = 2;

		/// <summary>The collor of the edges for the 'Show Edges' effect.</summary>
		[ColorUsage(true, true)]
		public Color _EdgeColor = Color.white;
		/// <summary>The collor that the model will be filled with after it fades ('Show Edges' effect).</summary>
		[ColorUsage(true, true)]
		public Color FillColor = Color.white;

		// To make sure the shader ends up in the build, we keep it's reference in the custom pass
		[SerializeField, HideInInspector]
		Shader CopyShader;
		/// <summary>Copies the contents of the temporary textures to the camera buffer with custom Opacity.</summary>
		Material CopyMatNorm;
		/// <summary>Copies the contents of the temporary textures to the camera buffer with custom Opacity.
		/// Shows everything below <see cref="YThreshold"/> with alpha == 1.</summary>
		Material CopyMatHalf;

		[SerializeField, HideInInspector]
		Shader ShowEdgesShader;
		/// <summary>Read the depth buffer and creates the edges map from it.</summary>
		Material ShowEdgesMat;
		int CompositingPass;

		MaterialPropertyBlock ShaderProperties;
		ShaderTagId[] NormalShaderTags;
		ShaderTagId[] DepthOnlyShaderTags;
		/// <summary>Keep colors here to blend them into the scene with the custom Opacity.</summary>
		RTHandle FadeWallBuffer;
		/// <summary>Keep depth here to blend the models into the scene properly
		/// and to be able to analyze depth for the 'Show Edges' effect.</summary>
		RTHandle DepthBuffer;

		/// <summary>Indicates that a <see cref="Fadable"/> class has taken the monopolistic
		/// control of the 'Fade Wall' functionality of this custom pass.</summary>
		[HideInInspector]
		public bool IsBusyFW => IsBusyFWPrivate;
		protected bool IsBusyFWPrivate = false;
		Fadable OwnerFW;

		/// <summary>Indicates that a <see cref="Fadable"/> class has taken the monopolistic
		/// control of the 'Show Edges' functionality of this custom pass.</summary>
		[HideInInspector]
		public bool IsBusySE => IsBusySEPrivate;
		protected bool IsBusySEPrivate = false;
		Fadable OwnerSE;


		/// <summary>A horizontal line in screen space that separates the fadable part of the colors and the
		/// constant alpha == 1 part.</summary>
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
			CopyMatNorm.DisableKeyword("SHOW_HALF");
			CopyMatHalf = CoreUtils.CreateEngineMaterial(CopyShader);
			CopyMatHalf.EnableKeyword("SHOW_HALF");
			CopyMatHalf.SetFloat("_YThreshold", YThreshold);
			CopyMatHalf.SetFloat("_MinSEColorAlpha", MinSEColorAlpha);
			CopyMatHalf.SetColor("_FillColor", FillColor);
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
				Vector2.one, TextureXR.slices, depthBufferBits: DepthBits.Depth24, dimension: TextureXR.dimension,
				colorFormat: GraphicsFormat.R16_UInt, useDynamicScale: true, name: "Fade Wall Depth Buffer");

			name = "Fade Walls";
		}//#endcolreg



		protected override void AggregateCullingParameters(ref ScriptableCullingParameters cullingParameters, HDCamera hdCamera)
		{
			//base.AggregateCullingParameters(ref cullingParameters, hdCamera);
			cullingParameters.cullingMask = (uint)LayerMask.GetMask(LayerMask.LayerToName(FadeWallLayer),
				LayerMask.LayerToName(ShowEdgesLayer), LayerMask.LayerToName(ShowEdgesFrozenLayer));
		}




		void RenderShowEdges(ScriptableRenderContext renderContext,
			CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResult, int layer, float opacity)
		{//#colreg(darkpurple);

			// 1. First, accumulate the models colors as usual.
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


			// 2. Copy the models colors into the camera buffer with custom Opacity, 
			//	while showing everything below the YThreshold clearly with alpha == 1 and
			//	never allowing Opacity to go below  MinSEColorAlpha.

			ShaderProperties.Clear();
			ShaderProperties.SetFloat("_Opacity", opacity);
			ShaderProperties.SetTexture("_FadeWallBuffer", FadeWallBuffer);
			ShaderProperties.SetTexture("_DepthBuffer", DepthBuffer);
			CoreUtils.DrawFullScreen(cmd, CopyMatHalf, ShaderProperties, shaderPassId: 0);


			// 3. Now render depth only with GreaterEqual compare function.

			var rasterizerState = new RasterState()
			{
				// We r using GreaterEqual function for the depth pass.
				cullingMode = CullMode.Front,
			};
			stateBlock = new RenderStateBlock(RenderStateMask.Depth | RenderStateMask.Raster)
			{
				// GreaterEqual compare function ensures that we see the wall edges even through geometry
				//	like the 'CUT' parts of the building.
				depthState = new DepthState(writeEnabled: true, compareFunction: CompareFunction.GreaterEqual),
				stencilState = new StencilState(false),
				rasterState = rasterizerState
			};
			filteredMeshes = new RendererListDesc(DepthOnlyShaderTags, cullingResult, hdCamera.camera)
			{
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

			// 4. Finally render edges from the depth map.
			ShaderProperties.Clear();
			ShaderProperties.SetFloat("_EdgeDetectThreshold", EdgeDetectThreshold);
			ShaderProperties.SetColor("_EdgeColor", _EdgeColor);
			ShaderProperties.SetFloat("_EdgeRadius", (float)EdgeRadius);
			ShaderProperties.SetTexture("_DepthBuffer", DepthBuffer);
			ShaderProperties.SetFloat("_InvOpacity", 1 - opacity);
			ShaderProperties.SetFloat("_YThreshold", YThreshold);

			SetCameraRenderTarget(cmd);
			CoreUtils.DrawFullScreen(cmd, ShowEdgesMat, ShaderProperties, shaderPassId: CompositingPass);
		}//#endcolreg





		protected override void Execute(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera hdCamera,
			CullingResults cullingResult)
		{//#colreg(darkpurple);

			// 1. Render models in the process of fading away with the 'Fade Wall' effect.
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

			ShaderProperties.Clear();
			ShaderProperties.SetFloat("_Opacity", FadeWallOpacity);
			ShaderProperties.SetTexture("_FadeWallBuffer", FadeWallBuffer);
			ShaderProperties.SetTexture("_DepthBuffer", DepthBuffer);
			CoreUtils.DrawFullScreen(cmd, CopyMatNorm, ShaderProperties, shaderPassId: 0);


			// 2. Render models with the 'Show Edges' effect that are have faded away.
			RenderShowEdges(renderContext, cmd, hdCamera, cullingResult, ShowEdgesFrozenLayer, 0);
			// 3. Render models in the process of fading away with the 'Show Edges' effect.
			RenderShowEdges(renderContext, cmd, hdCamera, cullingResult, ShowEdgesLayer, ShowEdgesOpacity);
		}//#endcolreg

		/// <summary>Secure the control of the 'Show Edges' functionality of this custom pass.</summary>
		public void GainMonopolisticControlSE(Fadable owner, float startingOpacity)
		{//#colreg(black);
			if (!IsBusySEPrivate)
			{
				IsBusySEPrivate = true;

				OwnerSE = owner;

				ShowEdgesOpacity = startingOpacity;
			}
		}//#endcolreg

		/// <summary>Secure the control of the 'Fade Wall' functionality of this custom pass.</summary>
		public void GainMonopolisticControlFW(Fadable owner, float startingOpacity)
		{//#colreg(black);
			if (!IsBusyFWPrivate)
			{
				IsBusyFWPrivate = true;

				OwnerFW = owner;

				FadeWallOpacity = startingOpacity;
			}
		}//#endcolreg

		public void ReleaseMonopolisticControlSE(Fadable owner)
		{//#colreg(black);
			if (IsBusySEPrivate && OwnerSE == owner)
			{
				OwnerSE = null;
				IsBusySEPrivate = false;
			}
		}//#endcolreg

		public void ReleaseMonopolisticControlFW(Fadable owner)
		{//#colreg(black);
			if (IsBusyFWPrivate && OwnerFW == owner)
			{
				OwnerFW = null;
				IsBusyFWPrivate = false;
			}
		}//#endcolreg


		/// <summary>Slowly change the opacity of the 'Show Edges' functionality of this custom class.</summary>
		/// <returns>TRUE if the model is fully faded.</returns>
		public virtual bool ChangeOpacitySE(bool increase)
		{//#colreg(darkblue);
			bool breakRoutine = false;

			if (increase)
			{
				ShowEdgesOpacity += Time.deltaTime * 6;

				if (ShowEdgesOpacity >= 1)
				{
					ShowEdgesOpacity = 1;
					breakRoutine = true;
				}
			}
			else
			{
				ShowEdgesOpacity -= Time.deltaTime * 6;

				if (ShowEdgesOpacity <= 0)
				{
					ShowEdgesOpacity = 0;
					breakRoutine = true;
				}
			}

			return breakRoutine;
		}//#endcolreg



		/// <summary>Slowly change the opacity of the 'Fade Wall' functionality of this custom class.</summary>
		/// <returns>TRUE if the model is fully faded.</returns>
		public virtual bool ChangeOpacityFW(bool increase)
		{//#colreg(darkblue);
			bool breakRoutine = false;

			if (increase)
			{
				FadeWallOpacity += Time.deltaTime * 6;

				if (FadeWallOpacity >= 1)
				{
					FadeWallOpacity = 1;
					breakRoutine = true;
				}
			}
			else
			{
				FadeWallOpacity -= Time.deltaTime * 6;

				if (FadeWallOpacity <= 0)
				{
					FadeWallOpacity = 0;
					breakRoutine = true;
				}
			}

			return breakRoutine;
		}//#endcolreg





		protected override void Cleanup()
		{//#colreg(black);
			if (FadeWallBuffer != null)
				FadeWallBuffer.Release();
			if (DepthBuffer != null)
				DepthBuffer.Release();

			CoreUtils.Destroy(CopyMatNorm);
			CoreUtils.Destroy(CopyMatHalf);

			CoreUtils.Destroy(ShowEdgesMat);
		}//#endcolreg



		/// <summary>Ensures that the static variables are cleared</summary>
		protected void CleanupStaticVariables(Scene scene, LoadSceneMode mode)
		{
			Instance = null;
			SceneManager.sceneLoaded -= CleanupStaticVariables;
		}


#if UNITY_EDITOR
		protected static bool HideWarnings = false;

		/// <summary>Ensure that there are no warnings on play mode stopping</summary>
		protected virtual void PlayModeStateChanged(PlayModeStateChange change)
		{//#colreg(black);
			if (change == PlayModeStateChange.ExitingPlayMode)
			{
				Instance = null;
				HideWarnings = true;
				EditorApplication.playModeStateChanged -= PlayModeStateChanged;
			}
		}//#endcolreg
#endif
	}
}

//%%% During a fading phase, make a list that accumulates ALL the new meshes that request to
//	be faded and fade them all once the current fading phase ends.