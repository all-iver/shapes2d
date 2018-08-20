namespace Shapes2D {

	using UnityEngine;
	using System.Collections;

	public class PacMan : MonoBehaviour {

		public float degrees = 20;
		public float speed = 45;
		Shape shape;
		bool dir;

		void Awake() {
			shape = GetComponent<Shape>();
		}

		// Update is called once per frame
		void Update () {
			float delta = Time.deltaTime * speed * (dir ? 1 : -1);
			shape.settings.startAngle = Mathf.Clamp(shape.settings.startAngle + delta, 0, degrees);;
			shape.settings.endAngle = 360 - shape.settings.startAngle;
			if (shape.settings.startAngle >= degrees || shape.settings.startAngle == 0)
				dir = !dir;
		}
	}

}