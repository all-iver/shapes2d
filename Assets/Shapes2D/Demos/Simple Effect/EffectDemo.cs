namespace Shapes2D {

	using UnityEngine;
	using System.Collections;

	public class EffectDemo : MonoBehaviour {

		public TapEffect tapEffect;

		// Update is called once per frame
		void Update () {
			if (InputUtils.MouseDownOrTap()) {
				Vector2 pos = InputUtils.MouseOrTapPosition();
				Vector3 worldPos = InputUtils.InputToWorldPosition(pos);
				Instantiate(tapEffect, worldPos, Quaternion.identity);
			}
		}
	}

}