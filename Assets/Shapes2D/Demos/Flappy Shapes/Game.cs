namespace Shapes2D {
    
    using UnityEngine;
    using System.Collections;
    using UnityEngine.UI;

    public class Game : MonoBehaviour {

        public Bird bird;
        public GameObject pipeSetPrefab;
        public float pipeDistance = 8; // distance between pipes in world units
        public float scrollSpeed = 2; // world units per second
        public Transform pipeSpawnLocation, pipeKillLocation;
        public float pipeVariation = 5; // world units up a pipe can go from its starting position
        float pipeTimer;
        public Button startButton, resetButton;
        public Text score;
        Transform pipes;
        public Shapes2D.Shape ground;

        // Use this for initialization
        void Start () {
            AdjustWidths();
            
            pipes = new GameObject().transform;
            pipes.name = "Pipes";
            pipes.SetParent(transform);
            pipeTimer = pipeDistance / scrollSpeed;
        }
        
        void AdjustWidths() {
            // adjust the width of the sky & ground to the camera size
            Vector3 scale = GameObject.Find("Sky").transform.localScale;
            scale.x = (Camera.main.orthographicSize * 2) / ((float) Screen.height / Screen.width) + 1;
            GameObject.Find("Sky").transform.localScale = scale;
            scale = GameObject.Find("Grass").transform.localScale;
            scale.x = (Camera.main.orthographicSize * 2) / ((float) Screen.height / Screen.width) + 1;
            GameObject.Find("Grass").transform.localScale = scale;
            scale = GameObject.Find("Dirt").transform.localScale;
            scale.x = (Camera.main.orthographicSize * 2) / ((float) Screen.height / Screen.width) + 1;
            GameObject.Find("Dirt").transform.localScale = scale;
        }
        
        public void OnStartButton() {
            startButton.gameObject.SetActive(false);
            resetButton.gameObject.SetActive(false);
            if (bird.IsDead()) {
                bird.Reset();
                for (int i = 0; i < pipes.childCount; i++) {
                    Transform p = pipes.GetChild(i);
                    p.gameObject.SetActive(false);
                }
            } else if (!bird.IsPlaying()) {
                bird.Play();
            }
        }
        
        // Update is called once per frame
        void Update () {
            score.text = bird.GetScore().ToString();
            
            if (bird.IsDead()) {
                startButton.gameObject.SetActive(false);
                resetButton.gameObject.SetActive(true);
                return;
            } 
            if (!bird.IsPlaying()) {
                startButton.gameObject.SetActive(true);
                resetButton.gameObject.SetActive(false);
                return;
            }
            
            // scroll all the pipes
            for (int i = 0; i < pipes.childCount; i++) {
                Transform p = pipes.GetChild(i);
                if (!p.gameObject.activeSelf)
                    continue;
                p.position -= new Vector3(scrollSpeed * Time.deltaTime, 0, 0);
                if (p.position.x < pipeKillLocation.position.x)
                    p.gameObject.SetActive(false);
            }
            
            // spawn/reset a pipe if it's time
            pipeTimer += Time.deltaTime;
            if (pipeTimer >= pipeDistance / scrollSpeed) {
                pipeTimer -= pipeDistance / scrollSpeed;
                Transform newPipe = null;
                for (int i = 0; i < pipes.childCount; i++) {
                    Transform p = pipes.GetChild(i);
                    if (!p.gameObject.activeSelf) {
                        newPipe = p;
                        newPipe.gameObject.SetActive(true);
                        break;
                    }
                }
                if (newPipe == null) {
                    newPipe = Instantiate<GameObject>(pipeSetPrefab).transform;
                    newPipe.SetParent(pipes);
                }
                newPipe.transform.position = pipeSpawnLocation.position 
                        + new Vector3(0, Random.Range(0, pipeVariation), 0);
            }
            
            // scroll the ground pattern so it looks like it's moving
            // not currently sure why this needs to have a scale factor
            ground.settings.fillOffset += new Vector2(scrollSpeed * Time.deltaTime / 1.27f, 0);
        }
    }

}