namespace Shapes2D {

	using UnityEngine;
	using System.Collections;
	using System.Collections.Generic;

	public class Spawner : MonoBehaviour {

		public GameObject prefab;
		float interval = 0.000001f;
		int count = 50;
		bool dynamic = true;
		bool optimizePolygons = true;
		int shapeType = 0;
		bool randomStyle = true;
		float timer;
		int spawned;
		bool done;
		float scale = 1;
		List<Shape> shapes = new List<Shape>();
		float deltaTime = 0.0f;

		// Use this for initialization
		void Start () {
			// Profiler.maxNumberOfSamplesPerFrame = 8000000;
		}

		void OnGUI() {
			// fps counter from http://wiki.unity3d.com/index.php?title=FramesPerSecond
			int w = Screen.width, h = Screen.height;
			GUIStyle style = new GUIStyle();	
			Rect rect = new Rect(10, h - h * 2 / 100 - 10, w, h * 2 / 100);
			style.alignment = TextAnchor.UpperLeft;
			style.fontSize = h * 2 / 100;
			style.normal.textColor = Color.white;
			float msec = deltaTime * 1000.0f;
			float fps = 1.0f / deltaTime;
			string text = string.Format("{0:0.0} ms ({1:0.} fps)", msec, fps);
			GUI.Label(rect, text, style);

			GUI.Box(new Rect(0, 0, 300, 400), "Shapes2D Stress Test");
			Vector2 offset = new Vector2(10, 30);
			GUI.Label(new Rect(offset.x, offset.y, 200, 20), "# of shapes to spawn (" + count + "):");
			offset.y += 30;
			count = (int) GUI.HorizontalSlider(new Rect(offset.x, offset.y, 100, 20), count, 1, 5000); 
			offset.y += 30;
			dynamic = GUI.Toggle(new Rect(offset.x, offset.y, 100, 20), dynamic, "Dynamic");
			offset.y += 30;
			optimizePolygons = GUI.Toggle(new Rect(offset.x, offset.y, 200, 20), optimizePolygons, "Optimize Polygons");
			offset.y += 30;
			GUI.Label(new Rect(offset.x, offset.y, 275, 20), "Shape Type (0 = Test Shape, 1 = random) (" + shapeType + ")");
			offset.y += 30;
			shapeType = (int) GUI.HorizontalSlider(new Rect(offset.x, offset.y, 100, 20), shapeType, 0, 16); 
			offset.y += 30;
			randomStyle = GUI.Toggle(new Rect(offset.x, offset.y, 100, 20), randomStyle, "Random Style");
			offset.y += 30;
			GUI.Label(new Rect(offset.x, offset.y, 200, 20), "Scale (" + scale + ")");
			offset.y += 30;
			scale = GUI.HorizontalSlider(new Rect(offset.x, offset.y, 100, 20), scale, 0.1f, 5f); 
			offset.y += 30;
			if (GUI.Button(new Rect(offset.x, offset.y, 100, 40), "Restart")) {
				for (int i = 0; i < transform.childCount; i++) {
					transform.GetChild(i).gameObject.SetActive(false);
					Destroy(transform.GetChild(i).gameObject);
				}
				timer = 0;
				spawned = 0;
				done = false;
				shapes.Clear();
			}
			offset.y += 50;
		}

		// Update is called once per frame
		void Update () {
			deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
			
			if (dynamic) {
				float outline = (Mathf.Cos(Time.timeSinceLevelLoad * 5) + 1) / 2 * (scale / 4);
				for (int i = 0; i < shapes.Count; i++)
					shapes[i].settings.outlineSize = outline;
			}
			if (done)
				return;
			timer += Time.deltaTime;
			while (timer >= interval) {
				timer -= interval;
				if (spawned < count) {
					float spread = Camera.main.orthographicSize;
					GameObject go = Instantiate(prefab);
					go.transform.SetParent(transform);
					go.transform.position = transform.position 
							+ new Vector3(Random.Range(-spread, spread), Random.Range(-spread, spread), 0);
					go.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
					go.SetActive(true);
					go.transform.localScale = new Vector3(scale, scale, 1);
					spawned ++;
					Shape shape = go.GetComponent<Shape>();
					if (!shape)
						continue;
					shapes.Add(shape);
					if (shapeType == 1) {
						shape.settings.shapeType = (ShapeType) Random.Range(0, 4);
						if (shape.settings.shapeType == ShapeType.Polygon)
							shape.settings.polygonPreset = (PolygonPreset) Random.Range(1, 13);
					} else {
						if (shapeType == 2)
							shape.settings.shapeType = ShapeType.Rectangle;
						if (shapeType == 3)
							shape.settings.shapeType = ShapeType.Ellipse;
						if (shapeType == 4)
							shape.settings.shapeType = ShapeType.Triangle;
						if (shapeType >= 5) {
							shape.settings.shapeType = ShapeType.Polygon;
							shape.settings.polygonPreset = (PolygonPreset) (shapeType - 4); 
						}
					}
					if (randomStyle) {
						shape.settings.outlineColor = Random.ColorHSV(0, 1, 0, 1, 0, 1, 0.25f, 1);
						shape.settings.fillColor = Random.ColorHSV(0, 1, 0, 1, 0, 1, 0.25f, 1);
						shape.settings.fillColor2 = Random.ColorHSV(0, 1, 0, 1, 0, 1, 0.25f, 1);
						shape.settings.fillType = (FillType) Random.Range(0, 7);
						shape.settings.lineSize = Random.Range(scale * 0.01f, scale * 0.5f);
						shape.settings.gridSize = Random.Range(scale * 0.01f, scale * 0.5f);
						shape.settings.gradientType = (GradientType) Random.Range(0, 3);
						shape.settings.gradientAxis = (GradientAxis) Random.Range(0, 2);
						shape.settings.gradientStart = Random.Range(0f, 1f);
						shape.settings.triangleOffset = Random.Range(0f, 1f);
						shape.settings.fillRotation = Random.Range(0f, 360f);
						shape.settings.fillOffset = new Vector2(Random.Range(0, scale), Random.Range(0, scale));
						shape.settings.roundness = Random.Range(0, scale / 2);
						if (Random.Range(0, 2) == 1)
							shape.settings.blur = Random.Range(0, scale / 8);
						shape.settings.outlineSize = Random.Range(0, scale / 4);
					}
					if (shape.settings.shapeType == ShapeType.Polygon)
						shape.settings.usePolygonMap = optimizePolygons;
				} else {
					done = true;
				}
			}
		}
	}

}