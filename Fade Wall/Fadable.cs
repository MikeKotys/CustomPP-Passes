using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FadeableWall
{
	/// <summary>Put this on a model to make it disapear when the camera's collider hits the model's collider.</summary>
	public class Fadable : MonoBehaviour
	{
		[Header("This will create cloned MeshRenderers", order = 0)]
		[Header("with 'ShadowsOnly' to render shadows", order = 1)]
		[Header("all the time regardless of the fading.", order = 2)]

		[Tooltip("Should the walls of the object be shown after it was hidden?" +
			" Also ensures that some parts of the model are always shown (below the Y line).")]
		public bool ShowWallEdges = false;

		[Tooltip("Which trigger coliders should trigger the hiding effect?")]
		public Transform[] TriggerCollisions;

		[Tooltip("All renderers eligable to be faded.")]
		public List<Renderer> AllFadableRenderers;

		/// <summary>How many trigger colliders collided with the camera collider.</summary>
		int CollisionCount = 0;

		/// <summary>Which layer should the model be on</summary>
		int OriginalLayer = 0;

		/// <summary>In deactivated mode the <see cref="Fadable"/> will simply fade in the model
		/// and stop performing its functions.</summary>
		bool IsDeactivated = false;

		void Awake()
		{//#colreg(darkorange);
			OriginalLayer = gameObject.layer;

			// First - save all renderers we have.
			if (AllFadableRenderers.Any())
			{
				int n = AllFadableRenderers.Count;
				while (--n > -1)
				{
					if (!AllFadableRenderers[n].gameObject.activeSelf)
						AllFadableRenderers.Remove(AllFadableRenderers[n]);
				}

			}
			else
			{
				List<Renderer> renderers = new List<Renderer>();
				RecursiveSetupRenderers(renderers, transform);
				AllFadableRenderers = renderers;
			}

			// Now clone all the GameObjects with renderers and set their shadowCastingMode to ShadowsOnly.
			//	Parent each clone to the GameObject that was cloned.
			GameObject go;

			for (int i = 0; i < AllFadableRenderers.Count; i++)
			{
				var renderer = AllFadableRenderers[i];
				if (renderer.shadowCastingMode != ShadowCastingMode.Off)
				{
					go = new GameObject(renderer.name);
					go.transform.parent = renderer.transform;
					go.transform.localPosition = Vector3.zero;
					go.transform.localRotation = Quaternion.identity;
					go.transform.localScale = Vector3.one;
					go.layer = OriginalLayer;

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
				renderer.shadowCastingMode = ShadowCastingMode.Off;
			}

			for (int i = 0; i < TriggerCollisions.Length; i++)
				RecursiveSetUpTriggers(TriggerCollisions[i]);
		}//#endcolreg

		void RecursiveSetUpTriggers(Transform current)
		{//#colreg(black);
			var collider = current.GetComponent<Collider>();
			if (collider != null && collider.isTrigger)
			{
				var trigger = current.gameObject.GetComponent<FadeWallTrigger>();

				if (trigger == null)
					trigger = current.gameObject.AddComponent<FadeWallTrigger>();
				trigger.Fadable = this;
			}

			for (int i = 0; i < current.childCount; i++)
				RecursiveSetUpTriggers(current.GetChild(i));
		}//#endcolreg

		void RecursiveSetupRenderers(List<Renderer> renderers, Transform current)
		{//#colreg(black);
			var renderer = current.GetComponent<Renderer>();
			if (renderer != null && current.gameObject.activeSelf)
				renderers.Add(renderer);

			for (int i = 0; i < current.childCount; i++)
				RecursiveSetupRenderers(renderers, current.GetChild(i));
		}//#endcolreg

		bool InitiallyRequestedFadeOut = false;

		//HashSet<FadeWallTrigger> ActivatedTriggers = new HashSet<FadeWallTrigger>();

		public void IncreaseCollision(/*FadeWallTrigger trigger*/)
		{//#colreg(darkred);
			CollisionCount++;
			//ActivatedTriggers.Add(trigger);
			if (CollisionCount == 1)
			{
				if (!IsInQueue && !IsChangingOpacity)
				{
					InitiallyRequestedFadeOut = true;
					if (!IsDeactivated)
						StartCoroutine(QueueForFadeWall());
				}
			}
		}//#endcolreg

		public void DecreaseCollision(/*FadeWallTrigger trigger*/)
		{//#colreg(darkred);
			CollisionCount--;
			//ActivatedTriggers.Remove(trigger);

			if (CollisionCount <= 0)
			{
				CollisionCount = 0;

				if (!IsInQueue && !IsChangingOpacity)
				{
					InitiallyRequestedFadeOut = false;
					if (!IsDeactivated)
						StartCoroutine(QueueForFadeWall());
				}
			}
		}//#endcolreg


		/// <summary>How many collision triggers have called <see cref="Deactivate()"/>?</summary>
		int DeactivationCalls = 0;

		public void Deactivate()
		{//#colreg(darkred);
			DeactivationCalls++;
			if (DeactivationCalls > 0)
			{
				IsDeactivated = true;
				CollisionCount = 0;
				StartCoroutine(QueueForFadeWall());
			}
		}//#endcolreg


		public void Activate()
		{//#colreg(darkred);
			DeactivationCalls--;
			if (DeactivationCalls <= 0)
				IsDeactivated = false;
		}//#endcolreg


		/// <summary>Prevent multiple <see cref="QueueForFadeWall"/> coroutines running at the same time.</summary>
		bool IsInQueue = false;

		/// <summary>Waits till the FadeWall custom pass frees up and uses it monopilisticly to
		///		hide the models this class is responsible for.</summary>
		IEnumerator QueueForFadeWall()
		{//#colreg(blue*1.5);
			IsInQueue = true;
			while (true)
			{
				if (FadeWall.Instance == null)
					break;
				else
				{
					if ((ShowWallEdges && FadeWall.Instance.IsBusySE)
						|| (!ShowWallEdges && FadeWall.Instance.IsBusyFW))
						yield return null;
					else
					{
						bool isFadingOut = !IsDeactivated || CollisionCount > 0;

						if (isFadingOut != InitiallyRequestedFadeOut)
							break;
						else
						{
							if (ShowWallEdges)
								FadeWall.Instance.GainMonopolisticControlSE(this, isFadingOut ? 1 : 0);
							else
								FadeWall.Instance.GainMonopolisticControlFW(this, isFadingOut ? 1 : 0);

							for (int i = 0; i < AllFadableRenderers.Count; i++)
							{
								if (ShowWallEdges)
									AllFadableRenderers[i].gameObject.layer = FadeWall.Instance.ShowEdgesLayer;
								else
								{
									AllFadableRenderers[i].enabled = true;
									AllFadableRenderers[i].gameObject.layer = FadeWall.Instance.FadeWallLayer;
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

		/// <summary>Prevent multiple <see cref="GradualyChangeOpacity"/> coroutines running at the same time.</summary>
		bool IsChangingOpacity = false;

		/// <summary>Slowly change the opacity to fade in/out the models.</summary>
		IEnumerator GradualyChangeOpacity()
		{//#colreg(blue*1.5);
			IsChangingOpacity = true;
			while (true)
			{
				if (FadeWall.Instance == null)
					break;
				else
				{
					if ((ShowWallEdges && FadeWall.Instance.ChangeOpacitySE(IsDeactivated || CollisionCount == 0))
						|| (!ShowWallEdges && FadeWall.Instance.ChangeOpacityFW(IsDeactivated || CollisionCount == 0)))
					{
						if (ShowWallEdges)
							FadeWall.Instance.ReleaseMonopolisticControlSE(this);
						else
							FadeWall.Instance.ReleaseMonopolisticControlFW(this);

						if (!IsDeactivated || CollisionCount > 0)
						{
							// Fully faded OUT.
							for (int i = 0; i < AllFadableRenderers.Count; i++)
							{
								if (ShowWallEdges)
									AllFadableRenderers[i].gameObject.layer = FadeWall.Instance.ShowEdgesFrozenLayer;
								else
									AllFadableRenderers[i].enabled = false;
							}
						}
						else
						{
							// Fully faded IN.
							for (int i = 0; i < AllFadableRenderers.Count; i++)
								AllFadableRenderers[i].gameObject.layer = OriginalLayer;
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
	/// <summary>Allows to pick the layer mask for a custom variable the same way as the layer picker dropdown in Unity.</summary>
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

	/// <summary>Allows to pick the layer mask for a custom variable the same way as the layer picker dropdown in Unity.</summary>
	public class LayerAttribute : PropertyAttribute
	{
	}
#endif
}
