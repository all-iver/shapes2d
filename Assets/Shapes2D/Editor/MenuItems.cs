namespace Shapes2D {

    using UnityEngine;
    using UnityEditor; 
    using UnityEngine.UI;
    using UnityEngine.EventSystems;
        
    public class MenuItems {
        // thanks to flatuicolors.com
        static Color[] colors = { 
            new Color(26 / 255f, 188 / 255f, 156 / 255f),
            new Color(46 / 255f, 204 / 255f, 113 / 255f),
            new Color(52 / 255f, 152 / 255f, 219 / 255f),
            new Color(155 / 255f, 89 / 255f, 182 / 255f),
            new Color(22 / 255f, 160 / 255f, 133 / 255f),
            new Color(39 / 255f, 174 / 255f, 96 / 255f),
            new Color(41 / 255f, 128 / 255f, 185 / 255f),
            new Color(142 / 255f, 68 / 255f, 173 / 255f),
            new Color(241 / 255f, 196 / 255f, 15 / 255f),
            new Color(230 / 255f, 126 / 255f, 34 / 255f),
            new Color(231 / 255f, 76 / 255f, 60 / 255f),
            new Color(149 / 255f, 165 / 255f, 166 / 255f),
            new Color(243 / 255f, 156 / 255f, 18 / 255f),
            new Color(211 / 255f, 84 / 255f, 0 / 255f),
            new Color(192 / 255f, 57 / 255f, 43 / 255f),
            new Color(127 / 255f, 140 / 255f, 141 / 255f)
        };

        public static bool AddToSelected(GameObject go) {
            if (Selection.activeTransform == null) {
                Selection.activeTransform = go.transform;
                return false;
            }
            go.transform.SetParent(Selection.activeTransform);
            go.transform.Translate(Selection.activeTransform.position);
            Selection.activeTransform = go.transform;
            return true;
        }
        
        public static void AddToSelectedNonCanvas(GameObject go) {
            if (Selection.activeGameObject == null) {
                Selection.activeTransform = go.transform;
                return;
            } 
            if (Selection.activeGameObject.GetComponentInParent<Canvas>() == null) {
                AddToSelected(go);
                return;
            }
            Selection.activeTransform = go.transform;
        }

        public static bool AddToSelectedCanvas(GameObject go) {
            if (Selection.activeGameObject == null)
                return false;
            if (Selection.activeGameObject.GetComponentInParent<Canvas>() == null)
                return false;
            return AddToSelected(go);
        }
        
        public static GameObject CreatePrefab(string name, bool withUndo = true) {
            GameObject go = GameObject.Instantiate(
                    Resources.Load<GameObject>("Shapes2D/Prefabs/" + name));
            go.name = name;
            go.GetComponent<Shape>().settings.fillColor = colors[Random.Range(0, colors.Length)];
            if (withUndo)
                Undo.RegisterCreatedObjectUndo(go, "Create Shapes2D " + name);
            return go;
        }
        
        private static Canvas CreateCanvas() {
            Canvas canvas = new GameObject().AddComponent<Canvas>();
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            canvas.name = "Canvas";
            canvas.gameObject.layer = LayerMask.NameToLayer("UI");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (GameObject.FindObjectOfType<EventSystem>() == null) {
                GameObject es = new GameObject();
                es.name = "Event System";
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                es.transform.SetParent(canvas.transform);
            }
            return canvas;
        }
        
        [MenuItem("GameObject/Shapes2D/Sprites/Arc", false, 10)]
        private static void CreateArc() {
            MenuItems.AddToSelectedNonCanvas(MenuItems.CreatePrefab("Arc"));
        }

        [MenuItem("GameObject/Shapes2D/Sprites/Curve", false, 10)]
        private static void CreateCurve() {
            MenuItems.AddToSelectedNonCanvas(MenuItems.CreatePrefab("Curve"));
        }

        [MenuItem("GameObject/Shapes2D/Sprites/Donut", false, 10)]
        private static void CreateDonut() {
            MenuItems.AddToSelectedNonCanvas(MenuItems.CreatePrefab("Donut"));
        }

        [MenuItem("GameObject/Shapes2D/Sprites/Ellipse", false, 10)]
        private static void CreateEllipse() {
            MenuItems.AddToSelectedNonCanvas(MenuItems.CreatePrefab("Ellipse"));
        }

        [MenuItem("GameObject/Shapes2D/Sprites/Filled Path", false, 10)]
        private static void CreateFilledPath() {
            MenuItems.AddToSelectedNonCanvas(MenuItems.CreatePrefab("Filled Path"));
        }

        [MenuItem("GameObject/Shapes2D/Sprites/Line Path", false, 10)]
        private static void CreateLinePath() {
            MenuItems.AddToSelectedNonCanvas(MenuItems.CreatePrefab("Line Path"));
        }

        [MenuItem("GameObject/Shapes2D/Sprites/Pie", false, 10)]
        private static void CreatePie() {
            MenuItems.AddToSelectedNonCanvas(MenuItems.CreatePrefab("Pie"));
        }

        [MenuItem("GameObject/Shapes2D/Sprites/Polygon", false, 10)]
        private static void CreatePolygon() {
            MenuItems.AddToSelectedNonCanvas(MenuItems.CreatePrefab("Polygon"));
        }

        [MenuItem("GameObject/Shapes2D/Sprites/Rectangle", false, 10)]
        private static void CreateRectangle() {
            MenuItems.AddToSelectedNonCanvas(MenuItems.CreatePrefab("Rectangle"));
        }

        [MenuItem("GameObject/Shapes2D/Sprites/Rounded Rectangle", false, 10)]
        private static void CreateRoundedRectangle() {
            MenuItems.AddToSelectedNonCanvas(MenuItems.CreatePrefab("Rounded Rectangle"));
        }

        [MenuItem("GameObject/Shapes2D/Sprites/Triangle", false, 10)]
        private static void CreateTriangle() {
            MenuItems.AddToSelectedNonCanvas(MenuItems.CreatePrefab("Triangle"));
        }

        [MenuItem("GameObject/Shapes2D/UI/Button", false, 10)]
        private static void CreateButton() {
            GameObject go = MenuItems.CreatePrefab("Button", false);
            go.name = "Shapes2D Button";
            if (MenuItems.AddToSelectedCanvas(go)) {
                Undo.RegisterCreatedObjectUndo(go, "Create Shapes2D Button");
            } else {
                bool createdCanvas = false;
                Canvas canvas = GameObject.FindObjectOfType<Canvas>();
                if (canvas == null || !canvas.enabled || canvas.transform.parent != null) {
                    canvas = CreateCanvas();
                    createdCanvas = true;
                }
                go.transform.SetParent(canvas.transform);
                Selection.activeTransform = go.transform;
                if (createdCanvas)
                    Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Create Shapes2D Button");
                else
                    Undo.RegisterCreatedObjectUndo(go, "Create Shapes2D Button");
            }
            go.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            go.transform.localScale = new Vector3(1, 1, 1);
        }

        [MenuItem("GameObject/Shapes2D/UI/Panel", false, 10)]
        private static void CreatePanel() {
            GameObject go = MenuItems.CreatePrefab("Panel", false);
            go.name = "Shapes2D Panel";
            if (MenuItems.AddToSelectedCanvas(go)) {
                Undo.RegisterCreatedObjectUndo(go, "Create Shapes2D Panel");
            } else {
                bool createdCanvas = false;
                Canvas canvas = GameObject.FindObjectOfType<Canvas>();
                if (canvas == null || !canvas.enabled || canvas.transform.parent != null) {
                    canvas = CreateCanvas();
                    createdCanvas = true;
                }
                go.transform.SetParent(canvas.transform);
                Selection.activeTransform = go.transform;
                if (createdCanvas)
                    Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Create Shapes2D Panel");
                else
                    Undo.RegisterCreatedObjectUndo(go, "Create Shapes2D Panel");
            }
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(0, 0);
            rt.offsetMax = new Vector2(0, 0);
            rt.transform.localScale = new Vector3(1, 1, 1);
        }        
    }

}