namespace Shapes2D {
    
    using UnityEngine;
    using System.Collections;

    public class TapEffect : MonoBehaviour {

        public float speed = 1;
        public float length = 1.5f;
        float timer;
        Shapes2D.Shape shape;

        // Use this for initialization
        void Start () {
            shape = GetComponent<Shapes2D.Shape>();
        }
        
        // Update is called once per frame
        void Update () {
            transform.localScale += new Vector3(speed * Time.deltaTime, 
                    speed * Time.deltaTime);
            Color color = shape.settings.outlineColor;
            color.a = 1 - timer / length;
            shape.settings.outlineColor = color;
            shape.settings.outlineSize -= Time.deltaTime;
            timer += Time.deltaTime;
            if (timer > length) {
                gameObject.SetActive(false);
                Destroy(gameObject, 1);
            }
        }
    }

}