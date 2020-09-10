using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FadeableWall
{
	public class Fadable : MonoBehaviour
	{
#if UNITY_EDITOR
		[Layer]
#endif
		public int NonHiddenLayer = 0;

		[Tooltip("Should the walls of the object be shown after it was hidden?")]
		public bool ShowWallEdges = false;

		public Transform[] TriggerCollisions;

		int CollisionCount = 0;

		Renderer[] AllFadableRenderers;

		static int FadingLayer;
		static int FadingLayerEdges;

		void Awake()
		{//#colreg(darkorange);
			FadingLayer = LayerMask.NameToLayer("FadeWall");
			FadingLayerEdges = LayerMask.NameToLayer("FWShowEdges");

			List<Renderer> renderers = new List<Renderer>();

			// First - save all renderers we have.
			RecursiveSetupRenderers(renderers, transform);
			AllFadableRenderers = renderers.ToArray();

			// Now clone all the GameObjects with renderers and set their shadowCastingMode to ShadowsOnly.
			//	Parent each clone to the GameObject that was cloned.
			GameObject go;

			for (int i = 0; i < AllFadableRenderers.Length; i++)
			{
				var renderer = AllFadableRenderers[i];
				go = new GameObject(renderer.name);
				go.transform.parent = renderer.transform;
				go.transform.localPosition = Vector3.zero;
				go.transform.localRotation = Quaternion.identity;
				go.transform.localScale = Vector3.one;
				go.layer = 0;

				var filter = renderer.GetComponent<MeshFilter>();
				var newFilter = go.AddComponent<MeshFilter>();
				newFilter.sharedMesh = filter.sharedMesh;

				var meshRenderer = renderer as MeshRenderer;
				if (meshRenderer != null)
				{
					MeshRenderer newMeshRenderer = go.AddComponent<MeshRenderer>();

					newMeshRenderer.sharedMaterials = meshRenderer.sharedMaterials;
					newMeshRenderer.allowOcclusionWhenDynamic = meshRenderer.allowOcclusionWhenDynamic;
					newMeshRenderer.enabled = meshRenderer.enabled;
					newMeshRenderer.probeAnchor = meshRenderer.probeAnchor;
					newMeshRenderer.rayTracingMode = meshRenderer.rayTracingMode;
					newMeshRenderer.receiveShadows = meshRenderer.receiveShadows;
					newMeshRenderer.reflectionProbeUsage = meshRenderer.reflectionProbeUsage;
					newMeshRenderer.rendererPriority = meshRenderer.rendererPriority;
					newMeshRenderer.renderingLayerMask = meshRenderer.renderingLayerMask;
					newMeshRenderer.lightProbeUsage = meshRenderer.lightProbeUsage;
					newMeshRenderer.motionVectorGenerationMode = meshRenderer.motionVectorGenerationMode;
					newMeshRenderer.motionVectorGenerationMode = meshRenderer.motionVectorGenerationMode;

					newMeshRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
				}
				else
				{
					SkinnedMeshRenderer newSkinnedMeshRenderer = go.AddComponent<SkinnedMeshRenderer>();

					newSkinnedMeshRenderer.sharedMaterials = meshRenderer.sharedMaterials;
					newSkinnedMeshRenderer.allowOcclusionWhenDynamic = meshRenderer.allowOcclusionWhenDynamic;
					newSkinnedMeshRenderer.enabled = meshRenderer.enabled;
					newSkinnedMeshRenderer.probeAnchor = meshRenderer.probeAnchor;
					newSkinnedMeshRenderer.rayTracingMode = meshRenderer.rayTracingMode;
					newSkinnedMeshRenderer.receiveShadows = meshRenderer.receiveShadows;
					newSkinnedMeshRenderer.reflectionProbeUsage = meshRenderer.reflectionProbeUsage;
					newSkinnedMeshRenderer.rendererPriority = meshRenderer.rendererPriority;
					newSkinnedMeshRenderer.renderingLayerMask = meshRenderer.renderingLayerMask;
					newSkinnedMeshRenderer.lightProbeUsage = meshRenderer.lightProbeUsage;
					newSkinnedMeshRenderer.motionVectorGenerationMode = meshRenderer.motionVectorGenerationMode;
					newSkinnedMeshRenderer.motionVectorGenerationMode = meshRenderer.motionVectorGenerationMode;

					newSkinnedMeshRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
				}
			}

			for (int i = 0; i < TriggerCollisions.Length; i++)
				RecursiveSetUpTriggers(TriggerCollisions[i]);
		}//#endcolreg

		void RecursiveSetUpTriggers(Transform current)
		{//#colreg(black);
			var collider = current.GetComponent<Collider>();
			if (collider != null && collider.isTrigger)
			{
				var trigger = current.gameObject.AddComponent<FadeWallTrigger>();
				trigger.Fadable = this;
			}

			for (int i = 0; i < current.childCount; i++)
				RecursiveSetUpTriggers(current.GetChild(i));
		}//#endcolreg

		void RecursiveSetupRenderers(List<Renderer> renderers, Transform current)
		{//#colreg(black);
			var renderer = current.GetComponent<Renderer>();
			if (renderer != null)
			{
#if UNITY_EDITOR
				// Find all components on this GameObject - make sure no extra stuff wa added as we will clone this GameObject.
				var components = renderer.GetComponents<Component>();

				for (int i = 0; i < components.Length; i++)
				{
					var trans = components[i] as Transform;
					var filter = components[i] as MeshFilter;
					var rendr = components[i] as Renderer;

					if (trans == null && filter == null && rendr == null)
						Debug.LogError("A Renderer inside a Fadable parent has unrecognized component '" + components[i].GetType()
							+ "' added to it! Please move this component to a separate GameObject inside this parent that has no"
							+ " Renderer component on it!", current);
				}
#endif
				// Set the initial state of the renderer - put it on a layer, drawn by the camera.
				renderer.gameObject.layer = NonHiddenLayer;

				renderer.shadowCastingMode = ShadowCastingMode.Off;

				renderers.Add(renderer);
			}

			for (int i = 0; i < current.childCount; i++)
				RecursiveSetupRenderers(renderers, current.GetChild(i));
		}//#endcolreg

		bool InitiallyRequestedFadeOut = false;

		public void IncreaseCollision()
		{//#colreg(darkred);
			CollisionCount++;
			if (CollisionCount == 1)
			{
				if (!IsInQueue && !IsChangingOpacity)
				{
					InitiallyRequestedFadeOut = true;
					StartCoroutine(QueueForFadeWall());
				}
			}
		}//#endcolreg

		public void DecreaseCollision()
		{//#colreg(darkred);
			CollisionCount--;

			if (CollisionCount <= 0)
			{
				CollisionCount = 0;

				if (!IsInQueue && !IsChangingOpacity)
				{
					InitiallyRequestedFadeOut = false;
					StartCoroutine(QueueForFadeWall());
				}
			}
		}//#endcolreg

		bool IsInQueue = false;

		IEnumerator QueueForFadeWall()
		{//#colreg(darkred);
			IsInQueue = true;
			while (true)
			{
				if (FadeWall.Instance == null)
					break;
				else
				{
					if (FadeWall.Instance.IsBusy)
						yield return null;
					else
					{
						bool isFadingOut = false;
						if (CollisionCount > 0)
							isFadingOut = true;

						if (isFadingOut != InitiallyRequestedFadeOut)
							break;
						else
						{
							FadeWall.Instance.GainMonopolisticControl(this, isFadingOut ? 1 : 0);

							if (isFadingOut)
							{
								for (int i = 0; i < AllFadableRenderers.Length; i++)
								{
									if (ShowWallEdges)
										AllFadableRenderers[i].gameObject.layer = FadeWall.Instance.ShowEdgesLayer;
									else
										AllFadableRenderers[i].gameObject.layer = FadeWall.Instance.FadeWallLayer;
								}
							}
							else
							{
								if (!ShowWallEdges)
								{
									for (int i = 0; i < AllFadableRenderers.Length; i++)
										AllFadableRenderers[i].enabled = true;
								}
							}

							if (!IsChangingOpacity)
								StartCoroutine(GradualyChangeOpacity());
						}
						break;
					}
				}
			}

			IsInQueue = false;
		}//#endcolreg

		bool IsChangingOpacity = false;

		IEnumerator GradualyChangeOpacity()
		{//#colreg(darkblue);
			IsChangingOpacity = true;
			while (true)
			{
				if (FadeWall.Instance == null)
					break;
				else
				{
					if (FadeWall.Instance.ChangeOpacity(CollisionCount == 0))
					{
						FadeWall.Instance.ReleaseMonopolisticControl(this);

						if (CollisionCount > 0)
						{
							if (!ShowWallEdges)
							{
								for (int i = 0; i < AllFadableRenderers.Length; i++)
									AllFadableRenderers[i].enabled = false;
							}
						}
						else
						{
							for (int i = 0; i < AllFadableRenderers.Length; i++)
								AllFadableRenderers[i].gameObject.layer = NonHiddenLayer;
						}

						break;
					}
				}

				yield return null;
			}
			IsChangingOpacity = false;
		}//#endcolreg
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(LayerAttribute))]
	public class LayerAttributeEditor : PropertyDrawer
	{
		public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
		{
			EditorGUI.BeginProperty(pos, label, prop);
			int index = prop.intValue;
			if (index > 31)
			{
				Debug.LogError("CustomPropertyDrawer, layer index is to high '" + index + "', is set to 31.");
				index = 31;
			}
			else if (index < 0)
			{
				Debug.LogError("CustomPropertyDrawer, layer index is to low '" + index + "', is set to 0");
				index = 0;
			}
			prop.intValue = EditorGUI.LayerField(pos, label, index);
			EditorGUI.EndProperty();
		}
	}

	public class LayerAttribute : PropertyAttribute
	{
	}
#endif
}
