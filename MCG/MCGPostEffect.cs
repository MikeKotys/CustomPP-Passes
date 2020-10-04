using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;
using UnityEngine.Experimental.Rendering;

namespace MCGPostEffect
{
	[Serializable, VolumeComponentMenu("Post-processing/Custom/Mesh Color Grading")]
	public sealed class MCGPostEffect : CustomPostProcessVolumeComponent, IPostProcessComponent
	{
		public static MCGPostEffect Instance;

		//[Tooltip("Used to store (serialize) the Texture3D LUT used in areas of the screen, where there is no LCG.")]
		//public TextureParameter DefaultLUT3D = new TextureParameter(null);


		[Tooltip("The power of the default LUT.")]
		public ClampedFloatParameter DefaultLUTStrength = new ClampedFloatParameter(1, 0, 1);

		public bool IsActive() => true;

		public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.BeforePostProcess;

		public int CurrentLUTNum;	//#color(red);

		RTHandle Attenuation1RT;
		RTHandle Attenuation2RT;

		MaterialPropertyBlock Properties;

		Material MCGPPMat;

		public override void Setup()
		{//#colreg(darkorange);
			Properties = new MaterialPropertyBlock();
			var shader = Shader.Find("Hidden/MCG_PostProcess");
			if (shader != null)
				MCGPPMat = new Material(shader);
#if UNITY_EDITOR
			else
				Debug.LogError("Could not find shader 'Hidden/MCG_PostProcess'");
#endif
			Instance = this;
		}//#endcolreg

		public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
		{//#colreg(darkpurple);
			if (Attenuation1RT == null)
			{
				Attenuation1RT = RTHandles.Alloc(source.rt.width, source.rt.height, colorFormat: GraphicsFormat.R8G8B8A8_SRGB);
				Attenuation1RT.rt.name = "MCG Attenuation buffer 1";
			}
			if (Attenuation2RT == null)
			{
				Attenuation2RT = RTHandles.Alloc(source.rt.width, source.rt.height, colorFormat: GraphicsFormat.R8G8B8A8_SRGB);
				Attenuation2RT.rt.name = "MCG Attenuation buffer 2";
			}

			RenderTargetIdentifier[] mrt = new RenderTargetIdentifier[2];
			mrt[0] = Attenuation1RT;
			mrt[1] = Attenuation2RT;

			CoreUtils.SetRenderTarget(cmd, mrt, Attenuation2RT, ClearFlag.All);

			int lastPriority = int.MinValue;
			CurrentLUTNum = -1;

			MCGPPMat.DisableKeyword("LUT_ZERO_LUMA");
			MCGPPMat.DisableKeyword("LUT_ONE_LUMA");
			MCGPPMat.DisableKeyword("LUT_TWO_LUMA");
			MCGPPMat.DisableKeyword("LUT_THREE_LUMA");
			MCGPPMat.DisableKeyword("LUT_FOUR_LUMA");
			MCGPPMat.DisableKeyword("LUT_FIVE_LUMA");
			MCGPPMat.DisableKeyword("LUT_SIX_LUMA");
			MCGPPMat.DisableKeyword("LUT_SEVEN_LUMA");

			MCGPPMat.DisableKeyword("LUT_ZERO_COLOR");
			MCGPPMat.DisableKeyword("LUT_ONE_COLOR");
			MCGPPMat.DisableKeyword("LUT_TWO_COLOR");
			MCGPPMat.DisableKeyword("LUT_THREE_COLOR");
			MCGPPMat.DisableKeyword("LUT_FOUR_COLOR");
			MCGPPMat.DisableKeyword("LUT_FIVE_COLOR");
			MCGPPMat.DisableKeyword("LUT_SIX_COLOR");
			MCGPPMat.DisableKeyword("LUT_SEVEN_COLOR");

			// Render a series of meshes of each LUT slot we have occupied.
			for (int i = 0; i < MCGSystem.SortedLCGs.Length; i++)
			{
				int priority = -1;
				var lcgMesh = MCGSystem.SortedLCGs[i];
				bool ignoreThisLCG = false;
				bool isSubtractingLuma = false;
				bool usesTargetColor = false;
				if (lcgMesh != null)
				{
					if (!lcgMesh.isActiveAndEnabled)
						ignoreThisLCG = true;
					else
					{
						if (lcgMesh.SubtractLuminosity)
							isSubtractingLuma = true;
						if (lcgMesh.UseTargetColor)
							usesTargetColor = true;
					}

					priority = lcgMesh.Priority;
				}

				if (!ignoreThisLCG && CurrentLUTNum < 7)
				{
					CurrentLUTNum++;

					// Lights with the same priority are rendered additively into the same channel in the AttenuationRT
					if (lastPriority == priority && CurrentLUTNum > 0)
						CurrentLUTNum--;

					lcgMesh.RenderMesh(this, cmd, MCGPPMat);    //#color(purple);

					if (isSubtractingLuma)
					{
						if (CurrentLUTNum == 0)
							MCGPPMat.EnableKeyword("LUT_ZERO_LUMA");
						if (CurrentLUTNum == 1)
							MCGPPMat.EnableKeyword("LUT_ONE_LUMA");
						else if (CurrentLUTNum == 2)
							MCGPPMat.EnableKeyword("LUT_TWO_LUMA");
						else if (CurrentLUTNum == 3)
							MCGPPMat.EnableKeyword("LUT_THREE_LUMA");
						else if (CurrentLUTNum == 4)
							MCGPPMat.EnableKeyword("LUT_FOUR_LUMA");
						else if (CurrentLUTNum == 5)
							MCGPPMat.EnableKeyword("LUT_FIVE_LUMA");
						else if (CurrentLUTNum == 6)
							MCGPPMat.EnableKeyword("LUT_SIX_LUMA");
						else if (CurrentLUTNum == 7)
							MCGPPMat.EnableKeyword("LUT_SEVEN_LUMA");
					}

					if (usesTargetColor)
					{
						if (CurrentLUTNum == 0)
							MCGPPMat.EnableKeyword("LUT_ZERO_COLOR");
						if (CurrentLUTNum == 1)
							MCGPPMat.EnableKeyword("LUT_ONE_COLOR");
						else if (CurrentLUTNum == 2)
							MCGPPMat.EnableKeyword("LUT_TWO_COLOR");
						else if (CurrentLUTNum == 3)
							MCGPPMat.EnableKeyword("LUT_THREE_COLOR");
						else if (CurrentLUTNum == 4)
							MCGPPMat.EnableKeyword("LUT_FOUR_COLOR");
						else if (CurrentLUTNum == 5)
							MCGPPMat.EnableKeyword("LUT_FIVE_COLOR");
						else if (CurrentLUTNum == 6)
							MCGPPMat.EnableKeyword("LUT_SIX_COLOR");
						else if (CurrentLUTNum == 7)
							MCGPPMat.EnableKeyword("LUT_SEVEN_COLOR");
					}

					lastPriority = priority;
				}
			}


			MCGPPMat.DisableKeyword("LUT_ONE");
			MCGPPMat.DisableKeyword("LUT_TWO");
			MCGPPMat.DisableKeyword("LUT_THREE");
			MCGPPMat.DisableKeyword("LUT_FOUR");
			MCGPPMat.DisableKeyword("LUT_FIVE");
			MCGPPMat.DisableKeyword("LUT_SIX");
			MCGPPMat.DisableKeyword("LUT_SEVEN");
			if (CurrentLUTNum > 0)
				MCGPPMat.EnableKeyword("LUT_ONE");
			if (CurrentLUTNum > 1)
				MCGPPMat.EnableKeyword("LUT_TWO");
			if (CurrentLUTNum > 2)
				MCGPPMat.EnableKeyword("LUT_THREE");
			if (CurrentLUTNum > 3)
				MCGPPMat.EnableKeyword("LUT_FOUR");
			if (CurrentLUTNum > 4)
				MCGPPMat.EnableKeyword("LUT_FIVE");
			if (CurrentLUTNum > 5)
				MCGPPMat.EnableKeyword("LUT_SIX");
			if (CurrentLUTNum > 6)
				MCGPPMat.EnableKeyword("LUT_SEVEN");

			MCGPPMat.SetTexture("_Attenuation1RT", Attenuation1RT);
			MCGPPMat.SetTexture("_Attenuation2RT", Attenuation2RT);
			MCGPPMat.SetTexture("_InputTexture", source);

			//if (DefaultLUT3D != null)
			//{
			//	MCGPPMat.EnableKeyword("LUT_DEFAULT");
			//	MCGPPMat.SetTexture("_LUT3Default", DefaultLUT3D.value);
			//	int lutSize = DefaultLUT3D.value.width;
			//	MCGPPMat.SetFloat("_LUT_ScaleDefault", (lutSize - 1) / (1.0f * lutSize));
			//	MCGPPMat.SetFloat("_LUT_OffsetDefault", 1.0f / (2.0f * lutSize));
			//	MCGPPMat.SetFloat("_LUT_StrengthDefault", DefaultLUTStrength.value);
			//}
			//else
			//	MCGPPMat.DisableKeyword("LUT_DEFAULT");

			MCGPPMat.SetFloat("OverrideSecondaryLCGs", 1);

			HDUtils.DrawFullScreen(cmd, MCGPPMat, destination);
		}//#endcolreg

		public override void Cleanup()
		{
			RTHandles.Release(Attenuation1RT);
			Instance = null;
		}

	}
}