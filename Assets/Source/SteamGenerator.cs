using FMODUnity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.VFX;

namespace Quinn
{
	public class SteamGenerator : MonoBehaviour
	{
		[SerializeField, Required]
		private VisualEffect VFX;
		[SerializeField]
		private EventReference SFX;

		public void Generate()
		{
			VFX.Play();
			Audio.Play(SFX, transform.position);
		}
	}
}
