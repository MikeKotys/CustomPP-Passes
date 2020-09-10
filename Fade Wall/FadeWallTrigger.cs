using UnityEngine;

namespace FadeableWall
{
	public class FadeWallTrigger : MonoBehaviour
	{
		public Fadable Fadable;

		private void OnTriggerEnter(Collider other)
		{
			Fadable.IncreaseCollision();
		}

		private void OnTriggerExit(Collider other)
		{
			Fadable.DecreaseCollision();
		}
	}
}
