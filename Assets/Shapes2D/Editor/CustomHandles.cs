namespace Shapes2D {

    using UnityEngine;
    using UnityEditor;

    // http://answers.unity3d.com/questions/463207/how-do-you-make-a-custom-handle-respond-to-the-mou.html
    public class CustomHandles
    {
        // internal state for DragHandle()
        static int s_DragHandleHash = "DragHandleHash".GetHashCode();
        static Vector2 s_DragHandleMouseStart;
        static Vector2 s_DragHandleMouseCurrent;
        static Vector3 s_DragHandleWorldStart;
        static float s_DragHandleClickTime = 0;
        static int s_DragHandleClickID;
        static float s_DragHandleDoubleClickInterval = 0.5f;
        static bool s_DragHandleHasMoved;

        // externally accessible to get the ID of the most resently processed DragHandle
        public static int lastDragHandleID;

        public enum DragHandleResult
        {
            none = 0,

            LMBPress,
            LMBClick,
            LMBDoubleClick,
            LMBDrag,
            LMBRelease,

            RMBPress,
            RMBClick,
            RMBDoubleClick,
            RMBDrag,
            RMBRelease,
        };

#if UNITY_5_6_OR_NEWER
        public static Vector3 DragHandle(Transform transform, Vector3 position, float handleSize, Handles.CapFunction capFunc, Color colorSelected, out DragHandleResult result)
#else
        public static Vector3 DragHandle(Transform transform, Vector3 position, float handleSize, Handles.DrawCapFunction capFunc, Color colorSelected, out DragHandleResult result)
#endif
        {
            int id = GUIUtility.GetControlID(s_DragHandleHash, FocusType.Passive);
            lastDragHandleID = id;

            Vector3 screenPosition = Handles.matrix.MultiplyPoint(position);
            Matrix4x4 cachedMatrix = Handles.matrix;

            result = DragHandleResult.none;

            switch (Event.current.GetTypeForControl(id))
            {
            case EventType.MouseDown:
                if (HandleUtility.nearestControl == id && (Event.current.button == 0 || Event.current.button == 1))
                {
                    GUIUtility.hotControl = id;
                    s_DragHandleMouseCurrent = s_DragHandleMouseStart = Event.current.mousePosition;
                    s_DragHandleWorldStart = position;
                    s_DragHandleHasMoved = false;

                    Event.current.Use();
                    EditorGUIUtility.SetWantsMouseJumping(1);

                    if (Event.current.button == 0)
                        result = DragHandleResult.LMBPress;
                    else if (Event.current.button == 1)
                        result = DragHandleResult.RMBPress;
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == id && (Event.current.button == 0 || Event.current.button == 1))
                {
                    GUIUtility.hotControl = 0;
                    Event.current.Use();
                    EditorGUIUtility.SetWantsMouseJumping(0);

                    if (Event.current.button == 0)
                        result = DragHandleResult.LMBRelease;
                    else if (Event.current.button == 1)
                        result = DragHandleResult.RMBRelease;

                    if (Event.current.mousePosition == s_DragHandleMouseStart)
                    {
                        bool doubleClick = (s_DragHandleClickID == id) &&
                            (Time.realtimeSinceStartup - s_DragHandleClickTime < s_DragHandleDoubleClickInterval);

                        s_DragHandleClickID = id;
                        s_DragHandleClickTime = Time.realtimeSinceStartup;

                        if (Event.current.button == 0)
                            result = doubleClick ? DragHandleResult.LMBDoubleClick : DragHandleResult.LMBClick;
                        else if (Event.current.button == 1)
                            result = doubleClick ? DragHandleResult.RMBDoubleClick : DragHandleResult.RMBClick;
                    }
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    s_DragHandleMouseCurrent = Event.current.mousePosition;
                    var ray = HandleUtility.GUIPointToWorldRay(s_DragHandleMouseCurrent);
                    var plane = new Plane(transform.TransformDirection(Vector3.back), transform.position);
                    if (plane.Raycast(ray, out float rayDistance))
                    {
                        position = ray.GetPoint(rayDistance);

                        if (Event.current.button == 0)
                            result = DragHandleResult.LMBDrag;
                        else if (Event.current.button == 1)
                            result = DragHandleResult.RMBDrag;

                        s_DragHandleHasMoved = true;
                    }

                    GUI.changed = true;
                    Event.current.Use();
                }
                break;

            case EventType.Repaint:
                Color currentColour = Handles.color;
                if (id == GUIUtility.hotControl && s_DragHandleHasMoved)
                    Handles.color = colorSelected;

                Handles.matrix = Matrix4x4.identity;
                #if UNITY_5_6_OR_NEWER
                    capFunc(id, screenPosition, transform.rotation, handleSize, EventType.Repaint);
                #else
                    capFunc(id, screenPosition, transform.rotation, handleSize);
                #endif
                Handles.matrix = cachedMatrix;

                Handles.color = currentColour;
                break;

            case EventType.Layout:
                Handles.matrix = Matrix4x4.identity;
                HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(screenPosition, handleSize));
                Handles.matrix = cachedMatrix;
                break;
            }

            return position;
        }
    }

}