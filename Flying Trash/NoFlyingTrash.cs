using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;

namespace FlyingTrash
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public sealed class NoFlyingTrash : MonoBehaviour
	{
		public Texture Texture;

		[NonSerialized][HideInInspector]
		public bool IsVisible = false;

		Mesh Mesh;
		Material Material;
		int MainPass;
		// To make sure the shader ends up in the build, we keep it's reference in the custom pass
		[SerializeField, HideInInspector]
		Shader Shader;

		private void OnEnable()
		{//#colreg(darkorange);
			IsVisible = true;

			var meshRenderer = GetComponent<MeshRenderer>();
			meshRenderer.materials = new Material[0];
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
			meshRenderer.lightProbeUsage = LightProbeUsage.Off;
			meshRenderer.allowOcclusionWhenDynamic = false;

			Shader = Shader.Find("Hidden/NoFlyingTrash");

			var filter = GetComponent<MeshFilter>();
			Mesh = filter.sharedMesh;

			if (!Shader)
				Debug.LogError("Couldn't find the 'Hidden/NoFlyingTrash' Shader!", gameObject);
			else
			{
				Material = new Material(Shader);
				Material.hideFlags = HideFlags.HideAndDontSave;
				Material.SetColor("OutputColor", Color.white);
				Material.SetTexture("_MainTex", Texture);
				MainPass = Material.FindPass("ForwardOnly");
			}

			if (IsVisible)
				OnBecameVisible();
		}//#endcolreg

		private void OnDisable()
		{//#colreg(darkorange);
			OnBecameInvisible();
		}//#endcolreg

		private void OnDestroy()
		{//#colreg(darkorange);
			OnBecameInvisible();
		}//#endcolreg

		private void OnBecameVisible()
		{
			IsVisible = true;
			FlyingTrashSystem.AddMesh(this);
		}

		private void OnBecameInvisible()
		{
			IsVisible = false;
			FlyingTrashSystem.RemoveMesh(this);
		}

		public void RenderMesh(CommandBuffer cmd)
		{//#colreg(darkpurple);
			cmd.DrawMesh(Mesh, transform.localToWorldMatrix, Material, 0, MainPass);
		}//#endcolreg
	}

	public static class FlyingTrashSystem
	{
		public static HashSet<NoFlyingTrash> AllMeshes = new HashSet<NoFlyingTrash>();

		public static void AddMesh(NoFlyingTrash mesh)
		{//#colreg(darkred);
			AllMeshes.Add(mesh);
		}//#endcolreg

		public static void RemoveMesh(NoFlyingTrash mesh)
		{//#colreg(darkred);
			AllMeshes.Remove(mesh);
		}//#endcolreg
	}
}
