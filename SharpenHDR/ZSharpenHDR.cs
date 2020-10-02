using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

namespace ZSharpenHDR
{
	[Serializable, VolumeComponentMenu("Post-processing/Custom/ZSharpenHDR")]
	public sealed class ZSharpenHDR : CustomPostProcessVolumeComponent, IPostProcessComponent
	{
		[Tooltip("Controls the intensity of the effect.")]
		public ClampedFloatParameter SharpenAmmount = new ClampedFloatParameter(1.0f, 0f, 15f);

		Material CopyMaterial;
		Material SharpenMaterial;

		public bool IsActive() => SharpenMaterial != null && SharpenAmmount.value > 0f && CopyMaterial != null;

		public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

		RTHandle BlurredRT;
		MaterialPropertyBlock Properties;
		public override void Setup()
		{
			var shader = Shader.Find("Hidden/Shader/ZCopy");
			if (shader != null)
				CopyMaterial = new Material(shader);

			shader = Shader.Find("Hidden/Shader/ZSharpenHDR");
			if (shader != null)
				SharpenMaterial = new Material(shader);

			Properties = new MaterialPropertyBlock();
		}

		public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
		{
			if (SharpenMaterial != null && CopyMaterial != null)
			{
				if (BlurredRT == null)
					BlurredRT = RTHandles.Alloc((int)((float)source.rt.width * .5f), (int)((float)source.rt.height * .5f),
						colorFormat: source.rt.graphicsFormat);

				Properties.SetTexture("_InputTexture", source);
				HDUtils.DrawFullScreen(cmd, CopyMaterial, BlurredRT, Properties);
				Properties.SetTexture("_BlurredTex", BlurredRT);
				Properties.SetFloat("_SharpenAmount", SharpenAmmount.value);
				HDUtils.DrawFullScreen(cmd, SharpenMaterial, destination, Properties);
				//SharpenMaterial.SetFloat("_SharpenAmount", SharpenAmmount.value);
				//SharpenMaterial.SetTexture("_SharpenAmount", source);
				//HDUtils.DrawFullScreen(cmd, SharpenMaterial, destination);
			}
		}

		public override void Cleanup()
		{
			CoreUtils.Destroy(CopyMaterial);
			CoreUtils.Destroy(SharpenMaterial);
			RTHandles.Release(BlurredRT);
		}

	}
}