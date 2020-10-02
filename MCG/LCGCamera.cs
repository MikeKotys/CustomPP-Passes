using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MCGPostEffect
{
	// Make sure this effect is applied AFTER color balance (HDR to LDR conversion).
	// Remember, scripts that sit on cameras might be attached to multiple cameras (say in multiplayer scenario).

	/// <summary>Creates render targets for the lcg lights clears them each frame. Also keeps track of all
	/// the LCG light scripts in the scene and calls their StoreLightAttenuation() scripts each frame if they are
	/// visible by this camera.</summary>
	[ExecuteInEditMode]
	[RequireComponent(typeof(Camera))]
	[ImageEffectAllowedInSceneView]
	public sealed class LCGCamera : MonoBehaviour
	{
		[Tooltip("The resolution of the auxhiliary attenuation maps (2 maps total).")]
		public int MapResolution = 1024;

		[Tooltip("Allows vision cones from a point light shadow map to be rendered.")]
		public bool EnableVisionCones = false;

		// Don't touch '[NonSerialized]' or shit can get wierd.
		[HideInInspector][NonSerialized]
		public Camera Camera;

		/// <summary>Camera's Viewprojection matrix</summary>
		[HideInInspector][NonSerialized]
		public Matrix4x4 ViewProj;

		/// <summary>Allows to rank-order LUTs and prioritize one over the other. 
		///	Also cuts-off low-priority lights' luts if there are more than 8 lights on the screen.</summary>
		[HideInInspector][NonSerialized]
		public int Current_LUT_Num = 0;

		/// <summary>Holds attenuations of each of the (up-to) 8 lights for further use in the post-processing effect</summary>
		RenderTexture Attenuations1RT, Attenuations2RT;

		/// <summary>Stores light vision cones (a shadowmap of a light source from one of the point lights inside the Hunter's head).</summary>
		RenderTexture VisionConesRT;

		/// <summary>Actually performs the color-grading using stored attenuations from RTs
		/// and fades low-priority lights in favor of high-priority ones if there is an overlap.</summary>
		Material LCG_PostProcessMat;

		/// <summary>Subtracts 1 from an alpha channel of the destination texture.</summary>
		Material ClearEmissionAlphaMat;

		/// <summary>Copies an alpha channel of a source texture into every channel of the destination texture.</summary>
		Material CopyAlphaMat;

		/// <summary>Clears RTs between frames. IMPORTANT!</summary>
		CommandBuffer ClearRTsCommandBuffer;

		/// <summary>Accumulates attenuation.</summary>
		CommandBuffer LCGAttenuationCommandBuffer;

		/// <summary>Cookie texture for the spotlight. MAKE SURE IT IS IMPORTED AS 'COOKIE'!!!</summary>
		[HideInInspector][NonSerialized]
		public Texture2D ConeSpotTexture;

		/// <summary>Mesh used to render the light during the lighting stage .</summary>
		[HideInInspector][NonSerialized]
		public Mesh ConeMesh;
		/// <summary>Mesh used to render the light during the lighting stage .</summary>
		[HideInInspector][NonSerialized]
		public Mesh SphereMesh;

		[Tooltip("A point light inside the Hunter's head which shadowmap will be used to render vision cones via Color Grading.")]
		public Light VisionLight;

		[Tooltip("Used to store (serialize) the Texture3D LUT used by this class for vision cones.")]
		public Texture3D VisionConesLUT3D;

		[Tooltip("The power of the VisionCones LUT3D (an argument for the lerp operation).")]
		public float Vision_LUT3D_Power;
		
		[Tooltip("Used to store (serialize) the Texture3D LUT used in areas of the screen, where there is no LCG.")]
		public Texture3D DefaultLUT3D;

		[Tooltip("The power of the default LUT.")]
		public float DefaultLUTStrength = 1;

		[Tooltip("A cube that is used to clear Emission texture's alpha channel to zero. Must cover the entire camera.")]
		public GameObject ClearEmissionAlphaGO;

		Mesh ClearEmissionAlphaMesh;

		static bool OnSceneLoadedMethodAdded = false;

		void Start() { }

		void OnEnable()
		{   //#colreg(darkorange*0.5);
			if (Camera == null)
				Camera = GetComponent<Camera>();

			if (ConeSpotTexture == null)
				ConeSpotTexture = Resources.Load<Texture2D>("Textures/Cone Spot Texture");

			if (ClearRTsCommandBuffer == null)
			{
				ClearRTsCommandBuffer = new CommandBuffer();
				ClearRTsCommandBuffer.name = "Clear LCG Attenuation RTs";
				var buffers = Camera.GetCommandBuffers(CameraEvent.BeforeLighting);
				int n = buffers.Length;
				while (--n > -1)
				{
					if (buffers[n].name == ClearRTsCommandBuffer.name)
						Camera.RemoveCommandBuffer(CameraEvent.BeforeLighting, buffers[n]);
				}
				Camera.AddCommandBuffer(CameraEvent.BeforeLighting, ClearRTsCommandBuffer);
			}

			if (LCGAttenuationCommandBuffer == null)
			{
				LCGAttenuationCommandBuffer = new CommandBuffer();
				LCGAttenuationCommandBuffer.name = "LCG Attenuation";
				var buffers = Camera.GetCommandBuffers(CameraEvent.AfterLighting);
				int n = buffers.Length;
				while (--n > -1)
				{
					if (buffers[n].name == LCGAttenuationCommandBuffer.name)
						Camera.RemoveCommandBuffer(CameraEvent.AfterLighting, buffers[n]);
				}
				Camera.AddCommandBuffer(CameraEvent.AfterLighting, LCGAttenuationCommandBuffer);
			}

			if (Attenuations1RT == null || Attenuations2RT == null)
				SetRenderTexture();

			if (LCG_PostProcessMat == null)
			{
				Shader shader = Shader.Find("Hidden/LCG_PostProcess");

				if (!shader)
					Debug.LogError("Couldn't find the 'Hidden/LCG_PostProcess' Shader!", gameObject);
				else
				{
					LCG_PostProcessMat = new Material(shader);
					LCG_PostProcessMat.hideFlags = HideFlags.HideAndDontSave;
				}
			}
			
			if (ClearEmissionAlphaMat == null)
			{
				Shader shader = Shader.Find("Hidden/ClearAlphaChannel");

				if (!shader)
					Debug.LogError("Couldn't find the 'Hidden/ClearAlphaChannel' Shader!", gameObject);
				else
				{
					ClearEmissionAlphaMat = new Material(shader);
					ClearEmissionAlphaMat.hideFlags = HideFlags.HideAndDontSave;
				}
			}

			if (CopyAlphaMat == null)
			{
				Shader shader = Shader.Find("Hidden/CopyAlpha");

				if (!shader)
					Debug.LogError("Couldn't find the 'Hidden/CopyAlpha' Shader!", gameObject);
				else
				{
					CopyAlphaMat = new Material(shader);
					CopyAlphaMat.hideFlags = HideFlags.HideAndDontSave;
				}
			}



			if (ClearEmissionAlphaGO != null && ClearEmissionAlphaMesh == null)
			{
				var filter = ClearEmissionAlphaGO.GetComponent<MeshFilter>();
				if (filter != null)
					ClearEmissionAlphaMesh = filter.sharedMesh;
			}

			if (SphereMesh == null)
			{
				GameObject go = Instantiate(Resources.Load("Light Sphere")) as GameObject;
				SphereMesh = go.GetComponent<MeshFilter>().sharedMesh;
#if UNITY_EDITOR
				DestroyImmediate(go);
#else
				Destroy(go);
#endif
			}

			if (ConeMesh == null)
			{
				GameObject go = Instantiate(Resources.Load("Light Cone")) as GameObject;
				ConeMesh = go.GetComponent<MeshFilter>().sharedMesh;
#if UNITY_EDITOR
				DestroyImmediate(go);
#else
				Destroy(go);
#endif
			}

			if (Application.isPlaying && EnableVisionCones)
				LCG_PostProcessMat.EnableKeyword("VISION_CONES_ENABLED");
			else
				LCG_PostProcessMat.DisableKeyword("VISION_CONES_ENABLED");

			if (!OnSceneLoadedMethodAdded)
			{

				OnSceneLoadedMethodAdded = true;
				SceneManager.sceneLoaded += OnSceneLoadedDestroyLCGSystem;
			}
		}//#endcolreg


		void OnSceneLoadedDestroyLCGSystem(Scene scene, LoadSceneMode mode)
		{
			LCGSystem.Instance.DestroyLCGSystem();
			SceneManager.sceneLoaded -= OnSceneLoadedDestroyLCGSystem;
			OnSceneLoadedMethodAdded = false;
		}

		private void OnDestroy()
		{
			if (Attenuations1RT != null)
			{
				Attenuations1RT.Release();
				Attenuations1RT = null;
			}
			if (Attenuations2RT != null)
			{
				Attenuations2RT.Release();
				Attenuations2RT = null;
			}
			if (VisionConesRT != null)
			{
				VisionConesRT.Release();
				VisionConesRT = null;
			}
		}

		void SetRenderTexture()
		{   //#colreg(black*2);
			if (Attenuations1RT != null)
#if UNITY_EDITOR
				DestroyImmediate(Attenuations1RT);
#else
				Destroy(Attenuations1RT);
#endif
			// FP16 (ARGBHalf) is necessary to prevent banding.
			Attenuations1RT = new RenderTexture(MapResolution, MapResolution, depth: 0, format: RenderTextureFormat.ARGBHalf);
			Attenuations1RT.name = "LCG Attenuations 1st RT";

			if (Attenuations2RT != null)
#if UNITY_EDITOR
				DestroyImmediate(Attenuations2RT);
#else
				Destroy(Attenuations2RT);
#endif
			// FP16 (ARGBHalf) is necessary to prevent banding.
			Attenuations2RT = new RenderTexture(MapResolution, MapResolution, depth: 0, format: RenderTextureFormat.ARGBHalf);
			Attenuations2RT.name = "LCG Attenuations 2nd RT";

			if (VisionConesRT != null && EnableVisionCones)
#if UNITY_EDITOR
				DestroyImmediate(VisionConesRT);
#else
				Destroy(VisionConesRT);
#endif
			VisionConesRT = new RenderTexture(1024, 1024, depth: 0, format: RenderTextureFormat.R16);
			VisionConesRT.name = "VisionCones RT";
		}   //#endcolreg


		Texture EmissionTexture;

		// We need a connection between LCG lights and the camera, because we set the shader parameters from camera's values.
		private void OnPreRender()
		{//#colreg(darkcyan*0.6);
			if (VisionLight != null && EnableVisionCones)
				Shader.SetGlobalVector("TargetLightColor", VisionLight.color * VisionLight.intensity);

			ClearRTsCommandBuffer.Clear();
			// First - clear the alpha channel of the emission texture .
			if (ClearEmissionAlphaMesh != null)
			{
				ClearRTsCommandBuffer.DrawMesh(ClearEmissionAlphaMesh,
					ClearEmissionAlphaGO.transform.localToWorldMatrix, ClearEmissionAlphaMat, 0, 0);
			}
			RenderTargetIdentifier[] mrt = { Attenuations1RT, Attenuations2RT };
			ClearRTsCommandBuffer.SetRenderTarget(mrt, Attenuations1RT);
			ClearRTsCommandBuffer.ClearRenderTarget(true, true, new Color(0, 0, 0, 0));

			Matrix4x4 proj = Matrix4x4.Perspective(Camera.fieldOfView, Camera.aspect, Camera.nearClipPlane, Camera.farClipPlane);

			proj = GL.GetGPUProjectionMatrix(proj, true);
			ViewProj = proj * Camera.worldToCameraMatrix;

			int lastPriority = int.MinValue;
			Current_LUT_Num = -1;

			LCGAttenuationCommandBuffer.Clear();
			if (EnableVisionCones)
			{
				// First - copy the alpha channel from the Emission texture to store it for later.
				LCGAttenuationCommandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, VisionConesRT, CopyAlphaMat);
			}

			if (LCG_PostProcessMat)
			{
				LCG_PostProcessMat.DisableKeyword("LUT_ZERO_LUMA");
				LCG_PostProcessMat.DisableKeyword("LUT_ONE_LUMA");
				LCG_PostProcessMat.DisableKeyword("LUT_TWO_LUMA");
				LCG_PostProcessMat.DisableKeyword("LUT_THREE_LUMA");
				LCG_PostProcessMat.DisableKeyword("LUT_FOUR_LUMA");
				LCG_PostProcessMat.DisableKeyword("LUT_FIVE_LUMA");
				LCG_PostProcessMat.DisableKeyword("LUT_SIX_LUMA");
				LCG_PostProcessMat.DisableKeyword("LUT_SEVEN_LUMA");
			}

			// Accumulate light attenuation.
			LCGAttenuationCommandBuffer.SetRenderTarget(mrt, Attenuations1RT);
			for (int i = 0; i < LCGSystem.Instance.SortedLCGs.Count; i++)
			{
				int priority = -1;
				LCG lcg = LCGSystem.Instance.SortedLCGs[i].LCG;
				MCG_Mesh lcg_Mesh = null;
				bool ignoreThisLCG = false;
				bool isSubtractingLuma = false;
				if (lcg != null)
				{
					if (!lcg.Light.isActiveAndEnabled || !lcg.isActiveAndEnabled)
						ignoreThisLCG = true;
					else
					{
						if (lcg.SubtractLuminosity)
							isSubtractingLuma = true;
					}

					priority = lcg.Priority;
				}
				else
				{
					lcg_Mesh = LCGSystem.Instance.SortedLCGs[i].LCG_Mesh;

					if (!lcg_Mesh.isActiveAndEnabled)
						ignoreThisLCG = true;
					else
					{
						if (lcg_Mesh.SubtractLuminosity)
							isSubtractingLuma = true;
					}

					priority = lcg_Mesh.Priority;
				}

				if (!ignoreThisLCG && Current_LUT_Num < 7)
				{
					Current_LUT_Num++;

					// Lights with the same priority are rendered additively into the same channel in the AttenuationRT
					if (lastPriority == priority && Current_LUT_Num > 0)
						Current_LUT_Num--;

					if (lcg != null)
						lcg.RenderLightAttenuation(this, LCGAttenuationCommandBuffer);
					//else
					//	lcg_Mesh.RenderMesh(this, LCGAttenuationCommandBuffer);

					if (isSubtractingLuma)
					{
						if (Current_LUT_Num == 0)
							LCG_PostProcessMat.EnableKeyword("LUT_ZERO_LUMA");
						if (Current_LUT_Num == 1)
							LCG_PostProcessMat.EnableKeyword("LUT_ONE_LUMA");
						else if (Current_LUT_Num == 2)
							LCG_PostProcessMat.EnableKeyword("LUT_TWO_LUMA");
						else if (Current_LUT_Num == 3)
							LCG_PostProcessMat.EnableKeyword("LUT_THREE_LUMA");
						else if (Current_LUT_Num == 4)
							LCG_PostProcessMat.EnableKeyword("LUT_FOUR_LUMA");
						else if (Current_LUT_Num == 5)
							LCG_PostProcessMat.EnableKeyword("LUT_FIVE_LUMA");
						else if (Current_LUT_Num == 6)
							LCG_PostProcessMat.EnableKeyword("LUT_SIX_LUMA");
						else if (Current_LUT_Num == 7)
							LCG_PostProcessMat.EnableKeyword("LUT_SEVEN_LUMA");
					}

					lastPriority = priority;
				}
			}
		}//#endcolreg

		public float E_RequestOverrideSecondaryLCGs;

		private void OnRenderImage(RenderTexture source, RenderTexture destination)
		{//#colreg(darkpurple*0.6);
			if (LCG_PostProcessMat)
			{
				LCG_PostProcessMat.DisableKeyword("LUT_ONE");
				LCG_PostProcessMat.DisableKeyword("LUT_TWO");
				LCG_PostProcessMat.DisableKeyword("LUT_THREE");
				LCG_PostProcessMat.DisableKeyword("LUT_FOUR");
				LCG_PostProcessMat.DisableKeyword("LUT_FIVE");
				LCG_PostProcessMat.DisableKeyword("LUT_SIX");
				LCG_PostProcessMat.DisableKeyword("LUT_SEVEN");
				if (Current_LUT_Num > 0)
					LCG_PostProcessMat.EnableKeyword("LUT_ONE");
				if (Current_LUT_Num > 1)
					LCG_PostProcessMat.EnableKeyword("LUT_TWO");
				if (Current_LUT_Num > 2)
					LCG_PostProcessMat.EnableKeyword("LUT_THREE");
				if (Current_LUT_Num > 3)
					LCG_PostProcessMat.EnableKeyword("LUT_FOUR");
				if (Current_LUT_Num > 4)
					LCG_PostProcessMat.EnableKeyword("LUT_FIVE");
				if (Current_LUT_Num > 5)
					LCG_PostProcessMat.EnableKeyword("LUT_SIX");
				if (Current_LUT_Num > 6)
					LCG_PostProcessMat.EnableKeyword("LUT_SEVEN");

				if (VisionConesLUT3D != null && EnableVisionCones)
				{
					LCG_PostProcessMat.SetTexture("_LUT3DVISION", VisionConesLUT3D);
					int lutSize = VisionConesLUT3D.width;
					LCG_PostProcessMat.SetFloat("_LUT_ScaleVISION", (lutSize - 1) / (1.0f * lutSize));
					LCG_PostProcessMat.SetFloat("_LUT_OffsetVISION", 1.0f / (2.0f * lutSize));
					LCG_PostProcessMat.SetFloat("_LUT3DVISION_POWER", Vision_LUT3D_Power);
					LCG_PostProcessMat.SetTexture("_VisionConeTex", VisionConesRT);
				}
				
				if (DefaultLUT3D != null)
				{
					LCG_PostProcessMat.EnableKeyword("LUT_DEFAULT");
					LCG_PostProcessMat.SetTexture("_LUT3Default", DefaultLUT3D);
					int lutSize = DefaultLUT3D.width;
					LCG_PostProcessMat.SetFloat("_LUT_ScaleDefault", (lutSize - 1) / (1.0f * lutSize));
					LCG_PostProcessMat.SetFloat("_LUT_OffsetDefault", 1.0f / (2.0f * lutSize));
					LCG_PostProcessMat.SetFloat("_LUT_StrengthDefault", DefaultLUTStrength);
				}
				else
					LCG_PostProcessMat.DisableKeyword("LUT_DEFAULT");

				LCG_PostProcessMat.SetTexture("_SourceTex", source);
				LCG_PostProcessMat.SetTexture("_Attenuations1RT", Attenuations1RT);
				LCG_PostProcessMat.SetTexture("_Attenuations2RT", Attenuations2RT);

				LCG_PostProcessMat.SetFloat("OverrideSecondaryLCGs", E_RequestOverrideSecondaryLCGs);

				Graphics.Blit(source, destination, LCG_PostProcessMat);
			}
			Current_LUT_Num = 0;

			E_RequestOverrideSecondaryLCGs = 1;
		}//#endcolreg
	}

	public sealed class LCGSystem
	{
		static LCGSystem PrivateInstance;
		public static LCGSystem Instance
		{
			get
			{
				if (PrivateInstance == null)
					PrivateInstance = new LCGSystem();
				return PrivateInstance;
			}
		}

		public void DestroyLCGSystem()
		{
			PrivateInstance = null;
		}


		/// <summary>LCG elements are dynamically collected every frame and stored in this sorted list.</summary>
		public List<LCG_Combined> SortedLCGs = new List<LCG_Combined>();

		void SortTheList()
		{
			SortedLCGs.Clear();

			int n = AllLights.Count;
			while (--n > -1)
			{
				LCG lcg = Instance.AllLights[n];
				SortedLCGs.Add(new LCG_Combined() { LCG = lcg, Priority = lcg.Priority });
			}

			n = AllMeshes.Count;
			while (--n > -1)
			{
				MCG_Mesh lcgMesh = Instance.AllMeshes[n];
				SortedLCGs.Add(new LCG_Combined() { LCG_Mesh = lcgMesh, Priority = lcgMesh.Priority });
			}

			SortedLCGs = SortedLCGs.OrderBy(x => x.Priority).ToList();
		}

		internal List<LCG> AllLights = new List<LCG>();

		public void AddLight(LCG lcg)
		{//#colreg(darkred);
			if (lcg.isActiveAndEnabled && lcg.Light.enabled && lcg.IsVisible && lcg.LUT3D != null)
			{
				bool alreadyHere = AllLights.Remove(lcg);
				AllLights.Add(lcg);
				if (!alreadyHere)
					SortTheList();
			}
		}//#endcolreg

		public void RemoveLight(LCG lcg)
		{//#colreg(darkred);
			if (AllLights.Remove(lcg))
				SortTheList();
		}//#endcolreg


		internal List<MCG_Mesh> AllMeshes = new List<MCG_Mesh>();

		public void AddMesh(MCG_Mesh lcg)
		{//#colreg(darkred);
			if (lcg.isActiveAndEnabled && lcg.IsVisible && lcg.LUT3D != null)
			{
				bool alreadyHere = AllMeshes.Remove(lcg);
				AllMeshes.Add(lcg);
				if (!alreadyHere)
					SortTheList();
			}
		}//#endcolreg

		public void RemoveMesh(MCG_Mesh lcg)
		{//#colreg(darkred);
			if (AllMeshes.Remove(lcg))
				SortTheList();
		}//#endcolreg

#if UNITY_EDITOR
		public static void UpdateLUT(ref int oldPriority, ref int priority, LCG currentLCG, MCG_Mesh currentLCGMesh,
			Texture3D oldLUT3D, Texture3D lut3D, ref float oldLUTStrength, ref float lutStrength)
		{//#colreg(green);
			if (oldPriority != priority)
			{
				oldPriority = priority;
				Instance.SortTheList();	//!Important!

				if (currentLCG != null)
				{
					for (int i = 0; i < Instance.AllLights.Count; i++)
					{
						LCG lcg = Instance.AllLights[i] as LCG;

						if (lcg != null && lcg != currentLCG && lcg.Priority == priority)
						{
							var otherSerializedObj = new SerializedObject(currentLCG);
							if (otherSerializedObj != null)
							{
								oldLUTStrength = lcg.LUTStrength;
								Undo.RecordObject(currentLCG, "Change Priority");
								lut3D = lcg.LUT3D;
								lutStrength = lcg.LUTStrength;
							}
						}
					}
				}
				else
				{
					for (int i = 0; i < Instance.AllMeshes.Count; i++)
					{
						MCG_Mesh lcgMesh = Instance.AllMeshes[i] as MCG_Mesh;

						if (lcgMesh != null && lcgMesh != currentLCGMesh && lcgMesh.Priority == priority)
						{
							var otherSerializedObj = new SerializedObject(currentLCGMesh);
							if (otherSerializedObj != null)
							{
								oldLUTStrength = lcgMesh.LUTStrength;
								Undo.RecordObject(currentLCGMesh, "Change Priority");
								lut3D = lcgMesh.LUT3D;
								lutStrength = lcgMesh.LUTStrength;
							}
						}
					}
				}
			}

			if (oldLUT3D != lut3D)
			{
				oldLUT3D = lut3D;

				if (currentLCG != null)
				{
					for (int i = 0; i < Instance.AllLights.Count; i++)
					{
						LCG lcg = Instance.AllLights[i] as LCG;

						if (lcg != null && lcg != currentLCG && lcg.Priority == priority)
						{
							var otherSerializedObj = new SerializedObject(lcg);
							if (otherSerializedObj != null)
							{
								lcg.OldLUT3D = lut3D;
								Undo.RecordObject(lcg, "Change LUT3D");
								lcg.LUT3D = lut3D;
							}
						}
					}
				}
				else
				{
					for (int i = 0; i < Instance.AllMeshes.Count; i++)
					{
						MCG_Mesh lcgMesh = Instance.AllMeshes[i] as MCG_Mesh;

						if (lcgMesh != null && lcgMesh != currentLCGMesh && lcgMesh.Priority == priority)
						{
							var otherSerializedObj = new SerializedObject(lcgMesh);
							if (otherSerializedObj != null)
							{
								lcgMesh.OldLUT3D = lut3D;
								Undo.RecordObject(lcgMesh, "Change LUT3D");
								lcgMesh.LUT3D = lut3D;
							}
						}
					}
				}
			}

			if (oldLUTStrength != lutStrength)
			{
				oldLUTStrength = lutStrength;

				if (currentLCG != null)
				{
					for (int i = 0; i < Instance.AllLights.Count; i++)
					{
						LCG lcg = Instance.AllLights[i] as LCG;

						if (lcg != null && lcg != currentLCG && lcg.Priority == priority)
						{
							var otherSerializedObj = new SerializedObject(lcg);
							if (otherSerializedObj != null)
							{
								//lcg.oldLUTStrength = lutStrength;
								Undo.RecordObject(lcg, "Change LUTStrength");
								lcg.LUTStrength = lutStrength;
							}
						}
					}
				}
				else
				{
					for (int i = 0; i < Instance.AllMeshes.Count; i++)
					{
						MCG_Mesh lcgMesh = Instance.AllMeshes[i] as MCG_Mesh;

						if (lcgMesh != null && lcgMesh != currentLCGMesh && lcgMesh.Priority == priority)
						{
							var otherSerializedObj = new SerializedObject(lcgMesh);
							if (otherSerializedObj != null)
							{
								//lcg.oldLUTStrength = lutStrength;
								Undo.RecordObject(lcgMesh, "Change LUTStrength");
								lcgMesh.LUTStrength = lutStrength;
							}
						}
					}
				}
			}
		}//#endcolreg
#endif
	}

	public sealed class LCG_Combined
	{
		public int Priority;

		public LCG LCG;
		public MCG_Mesh LCG_Mesh;
	}
}