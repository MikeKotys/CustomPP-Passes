using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

namespace MCGPostEffect
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public sealed class MCG_Mesh : MonoBehaviour
	{
		[Header("Only the RED channel of the", order = 0)]
		[Header("texture will apply", order = 1)]

		[Tooltip("Higher priority LCG lights will trump lower priority lights with their LUTs" +
			" (HighPriorityLut*HighPriorityAttenuation\n+ LowPriorityLut*LowPriorityAttenuation\n*(1-HighPriorityAttenuation))." +
			"\nLights with the same Priority numbers would share the same LUT texture used even if you try to set different ones.")]
		public int Priority = 100;
#if UNITY_EDITOR
		[SerializeField][HideInInspector]
		int OldPriority;
#endif

		public Texture MeshTexture;

		[Tooltip("Recorded attenuation/LUT mask would be multiplied by this value.")]
		public float AttenuationMultiplier = 1;

		[Tooltip("Allows you to soften or overcharge the strength of the current LUT.")]
		public float LUTStrength = 1;
#if UNITY_EDITOR
		[SerializeField][HideInInspector]
		float OldLUTStrength = 1;
#endif

		[Tooltip("The Look Up Texture that contains the required color grading effect.")]
		public Texture3D LUT3D;
#if UNITY_EDITOR
		[HideInInspector][SerializeField]
		public Texture3D OldLUT3D;
#endif

		[Tooltip("If checked, the overall luminosity of the final picture will be subtracted" +
			" or added to the base value of this LCG.")]
		public bool SubtractLuminosity = false;

		[Tooltip("If checked, the selected target color will be used to attenuate the strength of this LUT.")]
		public bool UseTargetColor = false;

		[Tooltip("The target color that will be used to attenuate the strength of this LUT.")]
		public Color TargetColor;

		[Tooltip("How much luminosity of the final picture should be subtracted or added to" +
		         " the base value of this LCG (to add set this to a negative value.")]
		public float LuminositySensitivity = 1;

		[NonSerialized][HideInInspector]
		public bool IsVisible = false;

		[NonSerialized][HideInInspector]
		public Mesh MainMesh;

		public Material MainMat;

		int MainPass;

		private void OnEnable()
		{//#colreg(darkorange*.5);
			IsVisible = true;

			var meshRenderer = GetComponent<MeshRenderer>();
			meshRenderer.materials = new Material[0];
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
			meshRenderer.lightProbeUsage = LightProbeUsage.Off;
			meshRenderer.allowOcclusionWhenDynamic = false;

			var filter = GetComponent<MeshFilter>();
			MainMesh = filter.sharedMesh;

			var shader = Shader.Find("Hidden/MCGUnlit");

			if (!shader)
				Debug.LogError("Couldn't find the 'Hidden/MCGUnlit' Shader!", gameObject);
			else
			{
				MainMat = new Material(shader);
				MainMat.hideFlags = HideFlags.HideAndDontSave;
				MainMat.SetColor("OutputColor", Color.white);
				MainMat.SetTexture("_MainTex", MeshTexture);
				MainPass = MainMat.FindPass("ForwardOnly");
			}

			LCGSystem.Instance.AddMesh(this);

			if (IsVisible)
				OnBecameVisible();

			SceneManager.sceneLoaded += SceneLoaded;
		}//#endcolreg

		void OnDisable()
		{//#colreg(darkorange);
			OnBecameInvisible();
			LCGSystem.Instance.RemoveMesh(this);
		}//#endcolreg

		private void OnDestroy()
		{//#colreg(black);
			OnBecameInvisible();
			LCGSystem.Instance.RemoveMesh(this);
#if UNITY_EDITOR
			DestroyImmediate(MainMat);
#else
			Destroy(MainMat);
#endif
		}//#endcolreg

		void SceneLoaded(Scene scene, LoadSceneMode mode)
		{
			MCGSystem.ClearLists();
			SceneManager.sceneLoaded -= SceneLoaded;
		}

		private void OnLevelWasLoaded(int level)
		{
			MCGSystem.ClearLists();
		}

		public void OnBecameVisible()
		{
			IsVisible = true;
			MCGSystem.AddMesh(this);
		}

		public void OnBecameInvisible()
		{
			IsVisible = false;
			MCGSystem.RemoveMesh(this);
		}

		public void RenderMesh(MCGPostEffect postEffect, CommandBuffer cmd, Material mcgPPMat)
		{//#colreg(darkpurple);
#if UNITY_EDITOR
			if (MainMesh == null)
			{
				var filter = GetComponent<MeshFilter>();
				MainMesh = filter.sharedMesh;
			}

			if (MainMesh == null)
				Debug.Log("MESH NOT SETUP FOR " + nameof(MCG_Mesh), this);
#endif
			MainMat.SetFloat("AttenuationMultiplier", AttenuationMultiplier);
			MainMat.SetVector("ColorMask0", new Vector4(0, 0, 0, 0));
			MainMat.SetVector("ColorMask1", new Vector4(0, 0, 0, 0));
			MainMat.SetTexture("_UnlitColorMap", MeshTexture);
			MainMat.SetColor("_UnlitColor", Color.white);

			switch (postEffect.CurrentLUTNum)
			{
				case 0: MainMat.SetVector("ColorMask0", new Vector4(1, 0, 0, 0)); break;
				case 1: MainMat.SetVector("ColorMask0", new Vector4(0, 1, 0, 0)); break;
				case 2: MainMat.SetVector("ColorMask0", new Vector4(0, 0, 1, 0)); break;
				case 3: MainMat.SetVector("ColorMask0", new Vector4(0, 0, 0, 1)); break;
				case 4: MainMat.SetVector("ColorMask1", new Vector4(1, 0, 0, 0)); break;
				case 5: MainMat.SetVector("ColorMask1", new Vector4(0, 1, 0, 0)); break;
				case 6: MainMat.SetVector("ColorMask1", new Vector4(0, 0, 1, 0)); break;
				case 7: MainMat.SetVector("ColorMask1", new Vector4(0, 0, 0, 1)); break;
			}

			cmd.DrawMesh(MainMesh, transform.localToWorldMatrix, MainMat, 0, MainPass);

			mcgPPMat.SetTexture("_LUT3D" + postEffect.CurrentLUTNum, LUT3D);
			int lutSize = LUT3D.width;
			mcgPPMat.SetFloat("_LUT_Scale" + postEffect.CurrentLUTNum, (lutSize - 1) / (1.0f * lutSize));
			mcgPPMat.SetFloat("_LUT_Offset" + postEffect.CurrentLUTNum, 1.0f / (2.0f * lutSize));
			mcgPPMat.SetFloat("_LUT_Strength" + postEffect.CurrentLUTNum, LUTStrength);
			mcgPPMat.SetFloat("LUMA_Sensitivity" + postEffect.CurrentLUTNum, LuminositySensitivity);
			mcgPPMat.SetColor("TargetColor" + postEffect.CurrentLUTNum, TargetColor);
		}//#endcolreg

#if UNITY_EDITOR
		private void Update()
		{//#colreg(darkblue);
			UpdateLUT();
			LCGSystem.Instance.AddMesh(this);
			MainMat.SetTexture("_MainTex", MeshTexture);
		}//#endcolreg

		private void OnGUI()
		{
			LCGSystem.UpdateLUT(ref OldPriority, ref Priority, null, this, OldLUT3D, LUT3D, ref OldLUTStrength, ref LUTStrength);
		}

		public void UpdateLUT()
		{
			LCGSystem.UpdateLUT(ref OldPriority, ref Priority, null, this, OldLUT3D, LUT3D, ref OldLUTStrength, ref LUTStrength);
		}
#endif
	}

	public static class MCGSystem
	{
		static List<MCG_Mesh> AllMeshes = new List<MCG_Mesh>();

		/// <summary>This list sorts MCG-s for the Priority variable.</summary>
		public static MCG_Mesh[] SortedLCGs = new MCG_Mesh[0];

		static void SortTheList()
		{
			SortedLCGs = new MCG_Mesh[AllMeshes.Count];

			int n = AllMeshes.Count;
			while (--n > -1)
				SortedLCGs[n] = AllMeshes[n];

			SortedLCGs = SortedLCGs.OrderBy(x => x.Priority).ToArray();
		}

		public static void AddMesh(MCG_Mesh lcg)
		{//#colreg(darkred);
			if (lcg.isActiveAndEnabled && lcg.IsVisible && lcg.LUT3D != null)
			{
				bool alreadyHere = AllMeshes.Remove(lcg);
				AllMeshes.Add(lcg);
				if (!alreadyHere)
					SortTheList();
			}
		}//#endcolreg

		public static void RemoveMesh(MCG_Mesh lcg)
		{//#colreg(darkred);
			if (AllMeshes.Remove(lcg))
				SortTheList();
		}//#endcolreg

		public static void ClearLists()
		{
			AllMeshes.Clear();
			SortedLCGs = null;
		}
	}
}


// Changing TargetColor or LuminocitySensitivity on one MCG_Mesh component must change it on every other component
//	with the same Priority.
// Remove LCG.cs completely.