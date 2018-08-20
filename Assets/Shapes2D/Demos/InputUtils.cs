namespace Shapes2D {
    
    using UnityEngine;
    using System.Collections;

    public class InputUtils {

        public static Vector3 InputToWorldPosition(Vector2 inputPos) {
            Vector3 pos = new Vector3(inputPos.x, inputPos.y, 
                    -Camera.main.transform.position.z);
            return Camera.main.ScreenToWorldPoint(pos);
        }
        
        static bool WasMouseDown(int button) {
            return Input.GetMouseButtonDown(button);
        }
        
        static bool WasFingerDown() {
            foreach (Touch t in Input.touches) {
                if (t.phase == TouchPhase.Began)
                    return true;
            }
            return false;
        }

        static Vector2 FirstTouchPosition() {
            foreach (Touch t in Input.touches) {
                if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved 
                        || t.phase == TouchPhase.Stationary)
                    return t.position;
            }
            throw new System.InvalidOperationException("No touch exists.");
        }

        public static bool MouseDownOrTap() {
            #pragma warning disable 0162
            #if UNITY_EDITOR
                return WasMouseDown(0);
            #endif
            #if UNITY_IPHONE || UNITY_ANDROID
                return WasFingerDown();
            #endif
            return WasMouseDown(0);
            #pragma warning restore 0162
        }
        
        public static Vector2 MouseOrTapPosition() {
            #pragma warning disable 0162
            #if UNITY_EDITOR
                return Input.mousePosition;
            #endif
            #if UNITY_IPHONE || UNITY_ANDROID
                return FirstTouchPosition();
            #endif
            return Input.mousePosition;
            #pragma warning restore 0162
        }

    }

}