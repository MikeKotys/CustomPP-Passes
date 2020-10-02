using System;
using UnityEngine;

namespace FadeableWall
{
	public class FadeWallTrigger : MonoBehaviour
	{
		[Tooltip("Should the target angle be used to filter out triggers?")]
		public bool UseTargetAngle = false;

		[Tooltip("Should this trigger fade in and disable the fadable component when the hunter enters it?")]
		public bool DeactivateFadableOnEnter = false;

		[Tooltip("What angle should the hunter face for the script to activate.")]
		public float TargetAngle;

		[NonSerialized, HideInInspector]
		public Fadable Fadable;

		/// <summary>Has the <see cref="Fadable"/> been deactivated?</summary>
		bool DeactivatedFadable = false;

		/// <summary>Has the standard functionality of <see cref="Fadable"/> been enabled?</summary>
		bool IncreasedFadableCollision = false;

		void ActivateTrigger()
		{//#colreg(black);
			if (!IncreasedFadableCollision)
			{
				IncreasedFadableCollision = true;
				if (Fadable != null)
					Fadable.IncreaseCollision();
			}
		}//#endcolreg

		void DeactivateTrigger()
		{//#colreg(black);
			if (IncreasedFadableCollision)
			{
				IncreasedFadableCollision = false;
				if (Fadable != null)
					Fadable.DecreaseCollision();
			}
		}//#endcolreg

		private void OnTriggerStay(Collider other)
		{//#colreg(darkblue);
			if (DeactivateFadableOnEnter)
			{
				if (!DeactivatedFadable)
				{
					Fadable.Deactivate();
					DeactivatedFadable = true;
				}
			}
			else
			{
				if (!UseTargetAngle)
					ActivateTrigger();
				//else if (CameraController.TheMainCamera != null)
				//{
				//	float angle = Mathf.DeltaAngle(CameraController.TheMainCamera.transform.eulerAngles.y, TargetAngle);

				//	if (angle > -70 && angle < 70)
				//		ActivateTrigger();
				//	else
				//		DeactivateTrigger();
				//}
			}
		}//#endcolreg

		private void OnTriggerExit(Collider other)
		{//#colreg(darkred);
			if (DeactivateFadableOnEnter)
			{
				if (DeactivatedFadable)
				{
					Fadable.Activate();
					DeactivatedFadable = false;
				}
			}
			else
				DeactivateTrigger();
		}//#endcolreg
	}
}
