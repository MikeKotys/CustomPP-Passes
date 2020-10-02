// LGC lights sharing the same priroty/3d texture should reference the same Texture3D asset (different assets with same data as of right now).
// Lights with no shadows are not supported.
// MAKE SURE ConeSpot texture IS IMPROTED AS 'Cookie'!!!

using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MCGPostEffect
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(MeshFilter))]
	[RequireComponent(typeof(MeshRenderer))]
	/// <summary>Attach this to a lightsource (point or spot) to be able to colorgrade the pixels of the world that this light touches.</summary>
	public sealed class LCG : MonoBehaviour
	{
		[Tooltip("Higher priority LCG lights will trump lower priority lights with their LUTs" +
			" (HighPriorityLut*HighPriorityAttenuation\n+ LowPriorityLut*LowPriorityAttenuation\n*(1-HighPriorityAttenuation))." +
			"\nLights with the same Priority numbers would share the same LUT texture used even if you try to set different ones.")]
		public int Priority = 100;
#if UNITY_EDITOR
		[SerializeField][HideInInspector]
		int OldPriority;
#endif

		[Tooltip("Recorded attenuation/LUT mask would be multiplied by this value.")]
		public float AttenuationMultiplier = 1;

		[Tooltip("Allows you to soften or overcharge the strength of the current LUT.")]
		public float LUTStrength = 1;
#if UNITY_EDITOR
		[SerializeField][HideInInspector]
		float OldLUTStrength = 1;
#endif

		[Tooltip("Allows you to ingore the cookie texture for the light and render the full cone/sphere without the cookie texture.")]
		public bool IgnoreCookie = false;

		[Tooltip("The Look Up Texture that contains the required color grading effect.")]
		public Texture3D LUT3D;

		[Tooltip("If checked, the overall luminocity of the final picture will be substracted" +
			" or added to the base value of this LCG.")]
		public bool SubtractLuminosity = false;

		[Tooltip("How much luminocity of the final picture should be substracted or added to" +
			" the base value of this LCG (to add set this to a negative value.")]
		public float LuminositySencitivity = 1;

#if UNITY_EDITOR
		[HideInInspector][SerializeField]
		public Texture3D OldLUT3D;
		// Catch duplication events
		//[SerializeField]
		//[HideInInspector]
		//int instanceID = 0;

#endif

		/// <summary>Invisible status is assigned through the OnBecameInvisible() event. Invisible lights are not rendered.</summary>
		[HideInInspector]
		public bool IsVisible = false;

		/// <summary>Mesh used as a renderable object inside the MeshRenderer/MeshFilter to cull this light
		/// properly and utilize OnBecameVisible/OnBecameInvisible methods.</summary>
		Mesh SphereMeshLarge;

		/// <summary>Mesh used as a renderable object inside the MeshRenderer/MeshFilter to cull this light
		/// properly and utilize OnBecameVisible/OnBecameInvisible methods.</summary>
		Mesh ConeMeshLarge;

		Material LCG_Attenuation;

		/// <summary>Light attached to this GameObject.</summary>
		[HideInInspector]
		public Light Light;

		/// <summary>MeshFilter is a required component for visibility checks.</summary>
		MeshFilter MeshFilter;
		void Start() { }

		/// <summary>We check Light.transform.hasChanged before recomputing the light position.
		/// This prevents cases when no recomutation is done at all</summary>
		bool firstLaunch = true;

		private void Awake()
		{
			Initialize(ignoreChecks: true);
		}

#if UNITY_EDITOR
		private void OnEnable()
		{
			if (!EditorApplication.isPlaying)
				Initialize();
		}
#endif

#if UNITY_EDITOR
		[MenuItem("CONTEXT/LCG/Reinitialize")]
		static void Reinitialize(MenuCommand menuCommand)
		{
			var lcg = menuCommand.context as LCG;

			if (lcg != null)
				lcg.Initialize(true);
		}
#endif

		MeshRenderer MeshRenderer;

		void Initialize(bool ignoreChecks = false)
		{   //#colreg(darkorange*0.5);
			name = gameObject.name;

			IsVisible = true;

			firstLaunch = true;

			//#if UNITY_EDITOR
			//			// Catch duplication events
			//			if (instanceID != GetInstanceID())
			//			{
			//				if (instanceID == 0)
			//				{
			//					instanceID = GetInstanceID();
			//				}
			//				else
			//				{
			//					instanceID = GetInstanceID();
			//					if (instanceID < 0)
			//					{
			//						if (LUT.Texture3D != null)
			//						{
			//							var oldTexture3D = LUT.Texture3D;
			//							LUT.Texture3D = new Texture3D(oldTexture3D.width, oldTexture3D.height,
			//								oldTexture3D.depth, oldTexture3D.format, false);
			//							LUT.Texture3D.SetPixels(oldTexture3D.GetPixels());
			//							LUT.Texture3D.Apply();
			//							//Texture2DPath does not need to be cloned.
			//						}
			//					}
			//				}
			//			}
			//#endif
			if (Light == null || ignoreChecks)
				Light = GetComponent<Light>();
			if (Light == null)
				Light = gameObject.AddComponent<Light>();

			if (LCG_Attenuation == null || ignoreChecks)
			{
				Shader shader = Shader.Find("Hidden/LCG_Attenuation");

				if (!shader)
					Debug.LogError("Couldn't find the 'LCG_Attenuation' Shader!");
				else
				{
					LCG_Attenuation = new Material(shader);
					LCG_Attenuation.hideFlags = HideFlags.HideAndDontSave;
				}
			}

			if (SphereMeshLarge == null || ignoreChecks)
			{
				GameObject go = Instantiate(Resources.Load("Light Sphere Large")) as GameObject;
				SphereMeshLarge = go.GetComponent<MeshFilter>().sharedMesh;


#if UNITY_EDITOR
			if (EditorApplication.isPlaying)
				Destroy(go);
			else
				DestroyImmediate(go);
#else
				Destroy(go);
#endif
			}

			if (ConeMeshLarge == null || ignoreChecks)
			{
				GameObject go = Instantiate(Resources.Load("Light Cone Large")) as GameObject;
				ConeMeshLarge = go.GetComponent<MeshFilter>().sharedMesh;
#if UNITY_EDITOR
				if (EditorApplication.isPlaying)
					Destroy(go);
				else
					DestroyImmediate(go);
#else
				Destroy(go);
#endif
			}


			// Make sure we have a mesh filter and a mesh renderer so that the light is culled properly and
			//	OnBecameVisible/OnBecameInvisible methods are called correctly.
			if (MeshFilter == null || ignoreChecks)
				MeshFilter = GetComponent<MeshFilter>();
			if (MeshFilter == null)
				MeshFilter = gameObject.AddComponent<MeshFilter>();
			MeshRenderer = GetComponent<MeshRenderer>();
			if (MeshRenderer == null)
				MeshRenderer = gameObject.AddComponent<MeshRenderer>();

			// Make sure the meshes we added are not actually rendered even when MeshRenderer is enabled.
			MeshRenderer.materials = new Material[0];
			MeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			MeshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
			MeshRenderer.lightProbeUsage = LightProbeUsage.Off;
			MeshRenderer.allowOcclusionWhenDynamic = false;
			SetMesh();
			LCGSystem.Instance.AddLight(this);
		}   //#endcolreg

		void OnDisable()
		{
			IsVisible = false;
			LCGSystem.Instance.RemoveLight(this);
		}

		private void OnDestroy()
		{
			LCGSystem.Instance.RemoveLight(this);
		}

		/// <summary>Ensures that rendered mesh is the same as the light type. At the same time ensures that transform
		///		of the current GameObject is representative of the light parameters.</summary>
		void SetMesh()
		{
			if (Light.type == LightType.Spot)
			{
				MeshFilter.sharedMesh = ConeMeshLarge;
				transform.localScale = new Vector3(Light.range, Light.range, Light.range);
			}
			else
			{
				MeshFilter.sharedMesh = SphereMeshLarge;
				transform.localScale = new Vector3(Light.range, Light.range, Light.range);
			}
		}

		private void OnBecameVisible()
		{
			IsVisible = true;
			LCGSystem.Instance.AddLight(this);
		}

		private void OnBecameInvisible()
		{
			IsVisible = false;
			LCGSystem.Instance.RemoveLight(this);
		}


		Matrix4x4 LightWorld;
		Matrix4x4 WorldToLight;
		Vector4 LightPos;



		public void RenderLightAttenuation(LCGCamera lcgCamera, CommandBuffer CommandBuffer)
		{//#colreg(darkpurple);
			if (MeshRenderer.materials.Length > 0)
				MeshRenderer.materials = new Material[0];

			LCG_Attenuation.SetMatrix("MyView", lcgCamera.Camera.worldToCameraMatrix);
			LCG_Attenuation.SetMatrix("MyViewProjection", lcgCamera.ViewProj);

			LCG_Attenuation.SetVector("ColorMask0", new Vector4(0, 0, 0, 0));
			LCG_Attenuation.SetVector("ColorMask1", new Vector4(0, 0, 0, 0));
			LCG_Attenuation.SetFloat("AttenuationMultiplier", AttenuationMultiplier);

			switch (lcgCamera.Current_LUT_Num)
			{
				case 0: LCG_Attenuation.SetVector("ColorMask0", new Vector4(1, 0, 0, 0)); break;
				case 1: LCG_Attenuation.SetVector("ColorMask0", new Vector4(0, 1, 0, 0)); break;
				case 2: LCG_Attenuation.SetVector("ColorMask0", new Vector4(0, 0, 1, 0)); break;
				case 3: LCG_Attenuation.SetVector("ColorMask0", new Vector4(0, 0, 0, 1)); break;
				case 4: LCG_Attenuation.SetVector("ColorMask1", new Vector4(1, 0, 0, 0)); break;
				case 5: LCG_Attenuation.SetVector("ColorMask1", new Vector4(0, 1, 0, 0)); break;
				case 6: LCG_Attenuation.SetVector("ColorMask1", new Vector4(0, 0, 1, 0)); break;
				case 7: LCG_Attenuation.SetVector("ColorMask1", new Vector4(0, 0, 0, 1)); break;
			}

			Shader.SetGlobalTexture("_LUT3D" + lcgCamera.Current_LUT_Num, LUT3D);
			int lutSize = LUT3D.width;
			Shader.SetGlobalFloat("_LUT_Scale" + lcgCamera.Current_LUT_Num, (lutSize - 1) / (1.0f * lutSize));
			Shader.SetGlobalFloat("_LUT_Offset" + lcgCamera.Current_LUT_Num, 1.0f / (2.0f * lutSize));
			Shader.SetGlobalFloat("_LUT_Strength" + lcgCamera.Current_LUT_Num, LUTStrength);
			Shader.SetGlobalFloat("LUMA_Sensitivity" + lcgCamera.Current_LUT_Num, LuminositySencitivity);
			
			if (Light.transform.hasChanged || firstLaunch)
			{
				LightPos = new Vector4(Light.transform.position.x,
					Light.transform.position.y, Light.transform.position.z, 1.0f / (Light.range * Light.range));
			}
			LCG_Attenuation.SetVector("_LightPos", LightPos);

			if (Light.type == LightType.Spot)
			{
				if (Light.transform.hasChanged || firstLaunch)
				{
					float scale = Light.range;
					float angleScale = Mathf.Tan((Light.spotAngle + 1) * 0.5f * Mathf.Deg2Rad) * Light.range;
					LightWorld = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(angleScale, angleScale, scale));

					Matrix4x4 view = Matrix4x4.TRS(Light.transform.position, Light.transform.rotation, Vector3.one).inverse;

					Matrix4x4 clip = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0.0f), Quaternion.identity, new Vector3(-0.5f, -0.5f, 1.0f));
					Matrix4x4 proj = Matrix4x4.Perspective(Light.spotAngle, 1, 0, 1);
					WorldToLight = clip * proj * view;
				}

				LCG_Attenuation.SetMatrix("_WorldToLight", WorldToLight);

				LCG_Attenuation.DisableKeyword("LCG_POINT");
				LCG_Attenuation.EnableKeyword("LCG_SPOT");

				if (Light.cookie == null || IgnoreCookie)
				//{
					//LCG_Attenuation.EnableKeyword("LCG_NO_COOKIE");
					LCG_Attenuation.SetTexture("_LightTexture0", lcgCamera.ConeSpotTexture);
				//}
				else
				//{
					//LCG_Attenuation.DisableKeyword("LCG_NO_COOKIE");
					LCG_Attenuation.SetTexture("_LightTexture0", Light.cookie);
				//}

				CommandBuffer.DrawMesh(lcgCamera.ConeMesh, LightWorld, LCG_Attenuation, 0, 0);
			}
			else
			{
				if (Light.transform.hasChanged || firstLaunch)
				{
					float scale = Light.range;
					LightWorld = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(scale, scale, scale));
				}

				LCG_Attenuation.DisableKeyword("LCG_SPOT");
				LCG_Attenuation.EnableKeyword("LCG_POINT");

				CommandBuffer.DrawMesh(lcgCamera.SphereMesh, LightWorld, LCG_Attenuation, 0, 0);
			}

			// THIS LINE MUST BE AT THE BOTTOM!
			Light.transform.hasChanged = false;
			firstLaunch = false;
		}   //#endcolreg


#if UNITY_EDITOR
		// We need Update, because StoreLightAttenuation() is not guaranteed to be called
		private void Update()
		{   //#colreg(darkblue);
			SetMesh();
			UpdateLUT();
			LCGSystem.Instance.AddLight(this);
		}   //#endcolreg
#endif

#if UNITY_EDITOR
		private void OnGUI()
		{
			UpdateLUT();
		}

		public void UpdateLUT()
		{
			LCGSystem.UpdateLUT(ref OldPriority, ref Priority, this, null, OldLUT3D, LUT3D, ref OldLUTStrength, ref LUTStrength);
		}

		//public void UpdateLUT()
		//{//#colreg(green);
		//	if (OldPriority != Priority)
		//	{
		//		OldPriority = Priority;

		//		var lights = Resources.FindObjectsOfTypeAll(typeof(LCG));
		//		for (int i = 0; i < lights.Length; i++)
		//		{
		//			LCG lcg = lights[i] as LCG;

		//			if (lcg != null && lcg != this && lcg.Priority == Priority)
		//			{
		//				var otherSerializedObj = new SerializedObject(this);
		//				if (otherSerializedObj != null)
		//				{
		//					OldLUTStrength = lcg.LUTStrength;
		//					Undo.RecordObject(this, "Change Priority");
		//					LUT3D = lcg.LUT3D;
		//					LUTStrength = lcg.LUTStrength;
		//				}
		//			}
		//		}
		//	}

		//	if (OldLUT3D != LUT3D)
		//	{
		//		OldLUT3D = LUT3D;

		//		var lights = Resources.FindObjectsOfTypeAll(typeof(LCG));
		//		for (int i = 0; i < lights.Length; i++)
		//		{
		//			LCG lcg = lights[i] as LCG;

		//			if (lcg != null && lcg != this && lcg.Priority == Priority)
		//			{
		//				var otherSerializedObj = new SerializedObject(lcg);
		//				if (otherSerializedObj != null)
		//				{
		//					lcg.OldLUT3D = LUT3D;
		//					Undo.RecordObject(lcg, "Change LUT3D");
		//					lcg.LUT3D = LUT3D;
		//				}
		//			}
		//		}
		//	}

		//	if (OldLUTStrength != LUTStrength)
		//	{
		//		OldLUTStrength = LUTStrength;

		//		var lights = Resources.FindObjectsOfTypeAll(typeof(LCG));
		//		for (int i = 0; i < lights.Length; i++)
		//		{
		//			LCG lcg = lights[i] as LCG;

		//			if (lcg != null && lcg != this && lcg.Priority == Priority)
		//			{
		//				var otherSerializedObj = new SerializedObject(lcg);
		//				if (otherSerializedObj != null)
		//				{
		//					lcg.OldLUTStrength = LUTStrength;
		//					Undo.RecordObject(lcg, "Change LUTStrength");
		//					lcg.LUTStrength = LUTStrength;
		//				}
		//			}
		//		}
		//	}
		//}//#endcolreg

#endif
	}
}
