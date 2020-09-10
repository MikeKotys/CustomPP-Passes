using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

namespace ZSuppressionBlur
{
	[Serializable, VolumeComponentMenu("Post-processing/Custom/ZSuppressionBlur")]
	public sealed class ZSuppressionBlur : CustomPostProcessVolumeComponent, IPostProcessComponent
	{
		[Tooltip(".")]
		public ClampedFloatParameter BlurAmmount = new ClampedFloatParameter(0.5f, 0f, 15f);
		[Tooltip(".")]
		public ClampedIntParameter Downsample = new ClampedIntParameter(1, 0, 2);
		[Tooltip(".")]
		public ClampedFloatParameter BlurSize = new ClampedFloatParameter(3.0f, 0f, 10.0f);
		[Tooltip(".")]
		public ClampedIntParameter BlurIterations = new ClampedIntParameter(2, 1, 4);
		[Tooltip(".")]
		public ClampedIntParameter BlurType = new ClampedIntParameter(1, 0, 1);


		Material SuppressionBlurMat;

		public bool IsActive() => SuppressionBlurMat != null && BlurAmmount.value > 0f;

		public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

		RTHandle rt;
		RTHandle rt2;
		MaterialPropertyBlock Properties;
		public override void Setup()
		{
			var shader = Shader.Find("Hidden/Shader/ZSuppressionBlur");
			if (shader != null)
				SuppressionBlurMat = new Material(shader);

			Properties = new MaterialPropertyBlock();
		}


		public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
		{
			if (SuppressionBlurMat != null)
			{
				float widthMod = 1.0f / (1.0f * (1 << Downsample.value));

				int rtW = source.rt.width >> Downsample.value;
				int rtH = source.rt.height >> Downsample.value;

				// downsample
				if (rt == null || rt.rt.width != rtW || rt.rt.height != rtH)
					rt = RTHandles.Alloc(rtW, rtH, colorFormat: source.rt.graphicsFormat);
				Properties.Clear();
				Properties.SetTexture("_SourceTex", source);
				Properties.SetVector("_Parameter", new Vector4(BlurSize.value * widthMod, -BlurSize.value * widthMod, 0.0f, 0.0f));
				HDUtils.DrawFullScreen(cmd, SuppressionBlurMat, rt, Properties, 0);

				var passOffs = BlurType.value == 0 ? 0 : 2;		//0 - Standard Gauss

				for (int i = 0; i < BlurIterations.value; i++)
				{
					float iterationOffs = i * 1.0f;
					Properties.Clear();
					Properties.SetVector("_Parameter",
						new Vector4(BlurSize.value * widthMod + iterationOffs, -BlurSize.value * widthMod - iterationOffs, 0.0f, 0.0f));

					// vertical blur
					if (rt2 == null || rt2.rt.width != rtW || rt2.rt.height != rtH)
						rt2 = RTHandles.Alloc(rtW, rtH, colorFormat: source.rt.graphicsFormat);
					Properties.SetTexture("_MainTex", rt);
					HDUtils.DrawFullScreen(cmd, SuppressionBlurMat, rt2, Properties, 1 + passOffs);

					Properties.Clear();
					Properties.SetVector("_Parameter",
						new Vector4(BlurSize.value * widthMod + iterationOffs, -BlurSize.value * widthMod - iterationOffs, 0.0f, 0.0f));
					Properties.SetTexture("_MainTex", rt2);
					// horizontal blur
					HDUtils.DrawFullScreen(cmd, SuppressionBlurMat, rt, Properties, 1 + passOffs);
				}

				Properties.SetTexture("_SourceTex", source);
				Properties.SetTexture("_BlurredTex", rt);
				//Combine
				HDUtils.DrawFullScreen(cmd, SuppressionBlurMat, destination, Properties, 5);
			}
		}

		public override void Cleanup()
		{
			CoreUtils.Destroy(SuppressionBlurMat);
			//CoreUtils.Destroy(SharpenMaterial);
			RTHandles.Release(rt);
			RTHandles.Release(rt2);
		}

	}
}