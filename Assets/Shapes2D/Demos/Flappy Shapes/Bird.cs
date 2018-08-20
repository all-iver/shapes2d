namespace Shapes2D {

	using UnityEngine;
	using System.Collections;
	using UnityEngine.EventSystems;

	public class Bird : MonoBehaviour {

		Rigidbody2D rb;
		public float force = 8;
		bool dead, playing;
		Vector3 startPosition;
		int score = 0;

		// Use this for initialization
		void Start () {
			rb = GetComponent<Rigidbody2D>();
			startPosition = transform.position;
			Reset();
		}
		
		void OnTriggerEnter2D(Collider2D other) {
			if (other.name == "Pass Trigger") {
				score ++;
				return;
			}
			Die();
		}

		public int GetScore() {
			return score;
		}

		void OnCollisionEnter2D(Collision2D coll) {
			Die();
		}
		
		public bool IsDead() {
			return dead;
		}

		public void Reset() {
			transform.position = startPosition;
			GetComponent<Animator>().enabled = false;
			rb.isKinematic = true;
			dead = false;
			playing = false;
			transform.rotation = Quaternion.Euler(0, 0, 0);
			score = 0;
		}
		
		public bool IsPlaying() {
			return playing;
		}
		
		public void Play() {
			if (dead)
				Reset();
			GetComponent<Animator>().enabled = true;
			rb.isKinematic = false;
			playing = true;
			Flap();
		}
		
		void Die() {
			GetComponent<Animator>().enabled = false;
			rb.velocity = new Vector2(0, 0);
			rb.AddForce(new Vector2(0, -force * 2), ForceMode2D.Impulse);
			dead = true;
			playing = false;
			transform.rotation = Quaternion.Euler(0, 0, -50);
		}
		
		void Flap() {
			if (rb.velocity.y < 0)
				rb.velocity = new Vector2(rb.velocity.x, 0);
			rb.AddForce(new Vector2(0, force), ForceMode2D.Impulse);
			transform.rotation = Quaternion.Euler(0, 0, 30);
		}
		
		void Update() {
			if (!playing)
				return;
			if (InputUtils.MouseDownOrTap() 
					&& !EventSystem.current.IsPointerOverGameObject())
				Flap();
			float theta = Mathf.LerpAngle(-30, 50, 
					Mathf.Clamp(rb.velocity.y, -1, 1));
			transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, 0, theta), Time.deltaTime * 5);
		}
	}

}