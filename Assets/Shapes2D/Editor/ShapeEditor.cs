namespace Shapes2D {

    using UnityEngine;
    using UnityEditor;
    using UnityEngine.UI;
    using System.Collections.Generic;

    [CustomEditor(typeof(Shape))]
    [CanEditMultipleObjects]
    public class ShapeEditor : Editor {
        // used during sprite conversion to keep track of objects that we have
        // to temporarily modify
        class GraphicState {
            public Graphic graphic;
            public bool hasMask;
            public bool showMaskGraphic;
        }

        SerializedProperty shapeTypeProp, outlineSizeProp, blurProp,
                outlineColorProp, fillTypeProp, fillColorProp,
                fillColor2Prop, gradientTypeProp, roundnessProp, roundnessTLProp,
                roundnessTRProp, roundnessBLProp, roundnessBRProp, roundnessPerCornerProp,
                fillRotationProp, fillOffsetProp, gradientStartProp, fillTextureProp,
                gridSizeProp, lineSizeProp, triangleOffsetProp, fillScaleProp,
                gradientAxisProp, polygonPresetProp, usePolygonMapProp,
                startAngleProp, endAngleProp, invertArcProp, innerCutoutProp,
                pathThicknessProp, fillPathLoopsProp;

        bool isEditing; // true if we're in polygon/path edit mode in the scene view
        Tool preEditTool = Tool.None; // the tool the user had selected before clicking edit

        void OnEnable () {
            Shape shape = (Shape) serializedObject.targetObject;

            if (!shape.GetComponent<SpriteRenderer>() 
                    && !shape.GetComponent<Image>()) {
                if (shape.GetComponentInParent<Canvas>() == null) {
                    Undo.AddComponent<SpriteRenderer>(shape.gameObject);
                } else {
                    Undo.AddComponent<Image>(shape.gameObject);
                }
                // collapse into the operation that made this happen
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                shape.Configure();
            }
            
            shapeTypeProp = serializedObject.FindProperty("settings._shapeType");
            outlineSizeProp = serializedObject.FindProperty("settings._outlineSize");
            blurProp = serializedObject.FindProperty("settings._blur");
            outlineColorProp = serializedObject.FindProperty("settings._outlineColor");
            roundnessPerCornerProp = serializedObject.FindProperty("settings._roundnessPerCorner");
            roundnessProp = serializedObject.FindProperty("settings._roundness");
            roundnessTLProp = serializedObject.FindProperty("settings._roundnessTopLeft");
            roundnessTRProp = serializedObject.FindProperty("settings._roundnessTopRight");
            roundnessBLProp = serializedObject.FindProperty("settings._roundnessBottomLeft");
            roundnessBRProp = serializedObject.FindProperty("settings._roundnessBottomRight");
            innerCutoutProp = serializedObject.FindProperty("settings._innerCutout");
            startAngleProp = serializedObject.FindProperty("settings._startAngle");
            endAngleProp = serializedObject.FindProperty("settings._endAngle");
            invertArcProp = serializedObject.FindProperty("settings._invertArc");
            fillTypeProp = serializedObject.FindProperty("settings._fillType");
            fillColorProp = serializedObject.FindProperty("settings._fillColor");
            fillColor2Prop = serializedObject.FindProperty("settings._fillColor2");
            fillRotationProp = serializedObject.FindProperty("settings._fillRotation");
            fillOffsetProp = serializedObject.FindProperty("settings._fillOffset");
            fillScaleProp = serializedObject.FindProperty("settings._fillScale");
            gradientTypeProp = serializedObject.FindProperty("settings._gradientType");
            gradientStartProp = serializedObject.FindProperty("settings._gradientStart");
            gradientAxisProp = serializedObject.FindProperty("settings._gradientAxis");
            fillTextureProp = serializedObject.FindProperty("settings._fillTexture");
            gridSizeProp = serializedObject.FindProperty("settings._gridSize");
            lineSizeProp = serializedObject.FindProperty("settings._lineSize");
            triangleOffsetProp = serializedObject.FindProperty("settings._triangleOffset");
            polygonPresetProp = serializedObject.FindProperty("settings._polygonPreset");
            usePolygonMapProp = serializedObject.FindProperty("settings._usePolygonMap");
            pathThicknessProp = serializedObject.FindProperty("settings._pathThickness");
            fillPathLoopsProp = serializedObject.FindProperty("settings._fillPathLoops");
        }
        
        // blend two colors in the same way the shape shader would (premultiplied alpha,
        // but for this process we have turned off the premultiply step so it's just
        // normal alpha blending).  note that for layered semi-transparent regions this
        // will not result in the same color you see in unity.  that's because the color
        // you see includes blending with the background, i.e. the end result when drawn
        // with a shader is: blend(blend(bg_color, shape1_color), shape2_color) whereas
        // when drawn from the converted sprite it's: 
        // blend(bg_color, blend(shape1_color, shape2_color)).
        private static Color BlendColors(Color dst, Color src) {
            Color c = src * src.a + dst * (1 - src.a);
            c.a = src.a + dst.a;
            return c;
        }
        
        private static void BlendTextures(Texture2D dstTex, Texture2D srcTex) {
            for (int x = 0; x < dstTex.width; x++) {
                for (int y = 0; y < dstTex.height; y++) {
                    Color src = srcTex.GetPixel(x, y);
                    if (src.a == 0) {
                        // source pixel is fully transparent, so nothing to do
                        continue;
                    }
                    if (src.a == 1) {
                        // src pixel is fully opaque, so use the child's
                        dstTex.SetPixel(x, y, src);
                        continue;
                    }
                    Color dst = dstTex.GetPixel(x, y);
                    Color result;
                    if (dst.a == 0) {
                        // parent pixel is fully transparent, so use the child's
                        result = src;
                    } else {
                        // both pixels have alpha, so blend them in the same way
                        // the shader would
                        result = BlendColors(dst, src);
                    }
                    dstTex.SetPixel(x, y, result);
                }
            }
        }
        
        private static List<GraphicState> DisableUIGraphics(Canvas canvas) {
            List<GraphicState> graphicStates = new List<GraphicState>();
            List<Graphic> graphics = new List<Graphic>();
            graphics.AddRange(canvas.GetComponentsInChildren<Graphic>());
            graphics.RemoveAll(g => !g.enabled);
            foreach (Graphic g in graphics) {
                GraphicState gs = new GraphicState();
                graphicStates.Add(gs);
                gs.graphic = g;
                Mask mask = g.GetComponent<Mask>();
                gs.hasMask = mask != null && mask.enabled;
                if (gs.hasMask) {
                    gs.showMaskGraphic = mask.showMaskGraphic;
                    mask.showMaskGraphic = false;
                } else {
                    g.enabled = false;
                }
            }
            return graphicStates;
        }

        // fixme - this needs more error handling so we don't leave objects in a weird place if something goes wrong
        private static Vector2 RenderToTexture2D(string path, Shape shape, float pixelsPerUnit = 100) {
            // reset the shape's rotation
            Quaternion oldRotation = shape.transform.rotation;
            shape.transform.rotation = Quaternion.identity;

            // get the desired pixel size of our shape and all its children, which will be the size of our texture
            Vector2 size = shape.GetShapePixelSize(pixelsPerUnit: pixelsPerUnit);
            int w = (int) size.x;
            int h = (int) size.y;
            
            // get all the shapes in draw order
            List<Shape> shapes = shape.GetShapesInDrawOrder();

            // if the shape is a UI component, we need to set up the canvas in a way 
            // that the camera can point to the image only without the UI 
            // components moving around based on the camera
            Canvas canvas = shape.GetComponentInParent<Canvas>();
            int oldCanvasLayer = -1;
            RenderMode oldRenderMode = 0;
            Vector3 oldCanvasScale = Vector3.one;
            List<GraphicState> modifiedGraphics = null;
            if (canvas) {
                oldCanvasLayer = canvas.gameObject.layer;
                canvas.gameObject.layer = 31;
                oldRenderMode = canvas.renderMode;
                // fixme - what happens with nested canvases?
                canvas.renderMode = RenderMode.WorldSpace;
                // if the canvas was in RenderMode.ScreenSpaceCamera then the scale will be weird now that we switched
                // to WorldSpace.  in that case we set the scale to one.
                if (oldRenderMode == RenderMode.ScreenSpaceCamera) {
                    oldCanvasScale = canvas.transform.localScale;
                    canvas.transform.localScale = Vector3.one;
                }
                // without a way to selectively show just the shape we want, this is 
                // the only way I can think of to do it.  even this won't work if the
                // user has a UI component not found by this function.
                modifiedGraphics = DisableUIGraphics(canvas);
            }

            // make a new render texture
            RenderTexture rt = new RenderTexture(w, h, 32, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;
            #if UNITY_5_6_OR_NEWER
                rt.autoGenerateMips = false;
            #else
                rt.generateMips = false;
            #endif
            rt.Create();
            
            // figure out the world space bounds of the shape so we can point the camera at it
            Bounds bounds = shape.GetShapeBounds();

            // set up the camera to point exactly at the object's bounds and set its
            // culling layer to show only layer 31
            // note that if the user has anything on layer 31 then it will also
            // show up in the png, but in that event they can just move it away
            Camera cam = new GameObject().AddComponent<Camera>();
            cam.backgroundColor = new Color(1, 1, 1, 0);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = bounds.center;
            cam.transform.position -= new Vector3(0, 0, 10);
            cam.orthographic = true;
            cam.orthographicSize = bounds.extents.y;
            cam.aspect = bounds.size.x / bounds.size.y;
            cam.targetTexture = rt;
            cam.cullingMask = 1 << 31;

            // make the render texture active so calls to Texture2D.ReadPixels() will
            // read from it
            RenderTexture oldRT = RenderTexture.active;
            RenderTexture.active = rt;

            // draw each shape with blending turned off, and layer them on top of each
            // other with blending between shapes but not between a shape and the 
            // camera's background color, which is what would happen if we just let the
            // camera render all of them - the camera's background color affects the 
            // color of semi-transparent pixels even if the background color has an
            // alpha value of 0.  this is because the shader's blending is trying to 
            // alias against the background color, but in the png we don't want that.
            // there's no shader option I'm aware of that would be able to blend between
            // shapes but not between a shape and the background color, so that's why
            // we have to do it manually.
            Texture2D dstTex = null;
            foreach (Shape s in shapes) {
                if (canvas) {
                    Graphic g = s.GetComponent<Graphic>(); 
                    g.enabled = true;
                    if (s.GetComponent<Mask>() != null) {
                        foreach (GraphicState gs in modifiedGraphics) {
                            if (gs.graphic == g && gs.showMaskGraphic)
                                s.GetComponent<Mask>().showMaskGraphic = true;
                        }
                    }
                }
                if (dstTex == null) {
                    dstTex = s.DrawToTexture2D(cam, w, h);
                } else {
                    Texture2D srcTex = s.DrawToTexture2D(cam, w, h);
                    BlendTextures(dstTex, srcTex);
                    DestroyImmediate(srcTex);
                }
                if (canvas) {
                    if (s.GetComponent<Mask>() == null) {
                        s.GetComponent<Graphic>().enabled = false;
                    } else {
                        s.GetComponent<Mask>().showMaskGraphic = false;
                    }
                }
            }
            
            // put the old render texture back
            RenderTexture.active = oldRT;

            // grab the sprite's pivot point based on the top object's location
            // relative to its children
            Vector2 pivot = shape.GetPivot();

            // restore the shape's rotation
            shape.transform.rotation = oldRotation;

            // restore the canvas
            if (canvas) {
                canvas.gameObject.layer = oldCanvasLayer;
                if (oldRenderMode == RenderMode.ScreenSpaceCamera)
                    canvas.transform.localScale = oldCanvasScale;
                canvas.renderMode = oldRenderMode;
                foreach (GraphicState gs in modifiedGraphics) {
                    gs.graphic.enabled = true;
                    if (gs.hasMask)
                        gs.graphic.GetComponent<Mask>().showMaskGraphic = gs.showMaskGraphic;
                }
            }

            // save the png
            byte[] bytes = dstTex.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);

            // clean up
            DestroyImmediate(dstTex);
            DestroyImmediate(cam.gameObject);
            DestroyImmediate(rt);

            return pivot;
        }
        
        private static Material GetDefaultSpriteMaterial() {
            // this doesn't work on 5.3.3 but does on 5.4
            // AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat")
            // tried various things like creating a new SpriteRenderer and using its
            // material but Unity doesn't seem to like that and will do weird things
            // like destroy the material when you hit play.  so we have a Sprite 
            // Template prefab and we'll grab the material from that.
            SpriteRenderer sr = (SpriteRenderer) Resources.Load(
                        "Shapes2D/Sprite Template", typeof(SpriteRenderer));
            if (!sr) {
                Debug.LogError("Shapes2D: Couldn't get the sprite template from "
                        + "Shapes2D/Resources.  You'll have to manually assign the "
                        + "SpriteRenderer's material."); 
                return null;
            }
            return sr.sharedMaterial;
        }

        private void EditShape() {
            isEditing = true;
            preEditTool = Tools.current;
            Tools.current = Tool.None;
            SceneView.RepaintAll();
        }

        private void StopEditingShape(bool restoreTool) {
            isEditing = false;
            if (restoreTool)
                Tools.current = preEditTool;
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        // verts has the first point duplicated at the end as well
        int GetClosestLineToPoint(Vector3 pos, List<Vector3> verts) {
            Vector2 p = HandleUtility.WorldToGUIPoint(pos);
            int closest = -1;
            float distance = -1;
            for (int i = 0; i < verts.Count - 1; i++) {
                Vector2 v1 = HandleUtility.WorldToGUIPoint(verts[i]);
                Vector2 v2 = HandleUtility.WorldToGUIPoint(verts[i + 1]);
                float testDistance = HandleUtility.DistancePointToLineSegment(p, v1, v2);
                if (closest == -1 || testDistance < distance) {
                    closest = i;
                    distance = testDistance;
                }
            }
            return closest;
        }

        // show an outline when in edit mode
        void DrawShapeBorders(Shape shape) {
            Vector3[] corners = new Vector3[5];
            shape.GetWorldCorners(corners);
            Handles.color = Color.white;
            Handles.DrawAAPolyLine(2f, corners);
        }

        Vector3 GetMouseWorldPos() {
            Vector2 mouseScreenPos = Event.current.mousePosition;
            return HandleUtility.GUIPointToWorldRay(mouseScreenPos).origin;
        }

        void EditPolygon(Shape shape) {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            // get the existing verts
            Vector3[] oldVerts = shape.GetPolygonWorldVertices();
            List<Vector3> verts = new List<Vector3>(oldVerts);
            bool hasMaxVerts = verts.Count == Shape.MaxPolygonVertices;
            // add the first vert at the end as well so Unity will draw it right etc
            verts.Add(verts[0]);
            // are we in delete mode?  what color should handles be?
            Color pink = new Color(1, 0, 0.75f);
            bool deleteMode = false;
            if ((Event.current.control || Event.current.command) && verts.Count > 4) {
                Handles.color = Color.red;
                deleteMode = true;
            } else {
                Handles.color = pink;
            }
            // draw the shape
            Handles.DrawAAPolyLine(3f, verts.ToArray());
            // drag handle result for getting info from our handles
            CustomHandles.DragHandleResult dhResult;
            // draw handles for each existing vert and check if they've been moved or clicked
            bool changed = false;
            for (int i = verts.Count - 2; i >= 0; i--) {
                Vector3 v = verts[i];
                #if UNITY_5_6_OR_NEWER
                    Vector3 newPos = CustomHandles.DragHandle(v, 0.05f * HandleUtility.GetHandleSize(v), 
                            Handles.DotHandleCap, pink, out dhResult);
                #else
                    Vector3 newPos = CustomHandles.DragHandle(v, 0.05f * HandleUtility.GetHandleSize(v), 
                            Handles.DotCap, pink, out dhResult);
                #endif
                if (deleteMode && dhResult == CustomHandles.DragHandleResult.LMBPress) {
                    // the user clicked on the handle while in delete mode, so delete the vert
                    verts.RemoveAt(i);
                    changed = true;
                } else if (!deleteMode && newPos != v) {
                    // the handle has been dragged, so move the vert to the new position
                    verts[i] = new Vector2(newPos.x, newPos.y);
                    changed = true;
                }
            }
            // check if the mouse is hovering over a space where we could add a new vert,
            // and draw it if so
            bool snapped = false;
            Vector3 closestPos = HandleUtility.ClosestPointToPolyLine(verts.ToArray());
            float distance = HandleUtility.DistanceToPolyLine(verts.ToArray());
            bool isCloseToLine = distance < 25;
            if (!changed && isCloseToLine && !hasMaxVerts && !deleteMode) {
                // todo - ClosestPointToPolyLine doesn't work very well in 3D...
                foreach (Vector3 v in verts) {
                    // if close to an existing vert, we don't want to add a new one
                    if (Vector2.Distance(HandleUtility.WorldToGUIPoint(closestPos), 
                            HandleUtility.WorldToGUIPoint(v)) < 15) {
                        snapped = true;
                        break;
                    }
                }
                if (!snapped) {
                    // not too close to an existing vert, so draw a new one.  don't
                    // use an actual handle cause we want to intercept nearby clicks
                    // and not just clicks directly on the handle.
                    Rect rect = new Rect();
                    float dim = 0.05f * HandleUtility.GetHandleSize(closestPos);
                    rect.center = closestPos - new Vector3(dim, dim, 0);
                    rect.size = new Vector2(dim * 2, dim * 2);
                    Handles.color = Color.white; // remove the weird tint it does
                    Handles.DrawSolidRectangleWithOutline(rect, Color.green, Color.clear);
                    if (Event.current.type == EventType.MouseDown) {
                        // the user has clicked the new vert, so add it for real
                        // figure out which line segment it's on
                        int lineStart = GetClosestLineToPoint(closestPos, verts);
                        verts.Insert(lineStart + 1, closestPos);
                        changed = true;
                    }
                }
            }
            // something has been changed, so apply the new verts back to the shape
            if (changed) {
                // make sure to remove the duplicated last vert we added
                Undo.RecordObject(shape, "Edit Shapes2D Polygon Vertices");
                shape.SetPolygonWorldVertices(
                        verts.GetRange(0, verts.Count - 1).ToArray());
                EditorUtility.SetDirty(target);
            } else {
                HandleUtility.Repaint(); // to draw the new vert placeholder handle
                if (Event.current.type == EventType.MouseDown && !isCloseToLine)
                    StopEditingShape(true);
            }
        }

        void DoMovePathPoint(List<PathSegment> segments, int i, Vector2 newPos) {
            PathSegment seg = segments[i / 3];
            Vector2 offset = seg.p1 - seg.midpoint;
            if (i % 3 == 0)
                seg.p0 = newPos;
            if (i % 3 == 2)
                seg.p2 = newPos;
            seg.p1 = seg.midpoint + (Vector3) offset;
            segments[i / 3] = seg;
        }

        void MovePathPointsAtPosition(List<PathSegment> segments, Vector2 oldPos, Vector2 newPos) {
            for (int i = 0; i < segments.Count; i++) {
                PathSegment seg = segments[i];
                if ((Vector2) seg.p0 == oldPos)
                    DoMovePathPoint(segments, i * 3, newPos);
                if ((Vector2) seg.p2 == oldPos)
                    DoMovePathPoint(segments, i * 3 + 2, newPos);
            }
        }

        void MovePathPoint(List<PathSegment> segments, int i, Vector3 newPos, bool moveConnected = true) {
            PathSegment segment = segments[i / 3];
            if (i % 3 == 1) {
                segment.p1 = (Vector2) newPos;
                segments[i / 3] = segment;
                return;
            }
            if (moveConnected)
                MovePathPointsAtPosition(segments, i % 3 == 0 ? segment.p0 : segment.p2, newPos);
            else
                DoMovePathPoint(segments, i, newPos);
        }

        void EditPath(Shape shape) {
            // get the existing verts
            PathSegment[] oldSegments = shape.GetPathWorldSegments();
            List<PathSegment> segments = new List<PathSegment>(oldSegments);
            bool hasMaxSegments = segments.Count == Shape.MaxPathSegments;
            // are we in delete mode?  what color should handles be?
            Color pink = new Color(1, 0, 0.75f);
            bool deleteMode = false;
            if ((Event.current.control || Event.current.command) && segments.Count > 1) {
                Handles.color = Color.red;
                deleteMode = true;
            } else {
                Handles.color = pink;
            }
            bool splitMode = Event.current.shift;
            Vector3 mouseWorldPos = GetMouseWorldPos();
            bool isWithinBounds = shape.PointIsWithinShapeBounds(mouseWorldPos);
            // drag handle result for getting info from our handles
            CustomHandles.DragHandleResult dhResult;
            // draw handles for each existing point and check if they've been moved or clicked
            bool changed = false;
            for (int i = segments.Count * 3 - 1; i >= 0; i--) {
                PathSegment segment = segments[i / 3];
                Vector3 p = segment.p0;
                bool isInfluencePoint = false;
                if (i % 3 == 1) {
                    p = segment.p1;
                    isInfluencePoint = true;
                } else if (i % 3 == 2) {
                    p = segment.p2;
                }
                if (deleteMode && isInfluencePoint)
                    continue;
                float size = 0.04f * HandleUtility.GetHandleSize(p);
                #if UNITY_5_6_OR_NEWER
                    Handles.CapFunction cap = Handles.RectangleHandleCap;
                #else
                    Handles.DrawCapFunction cap = Handles.RectangleCap;
                #endif
                if (isInfluencePoint) { 
                    #if UNITY_5_6_OR_NEWER
                        cap = Handles.CircleHandleCap;
                    #else
                        cap = Handles.CircleCap;
                    #endif
                    size = 0.05f * HandleUtility.GetHandleSize(p);
                }
                if (isInfluencePoint) {
                    Color oldColor = Handles.color;
                    Handles.color = Color.grey;
                    Handles.DrawDottedLine(p, segment.p0, HandleUtility.GetHandleSize(p));
                    Handles.DrawDottedLine(p, segment.p2, HandleUtility.GetHandleSize(p));
                    Handles.color = oldColor;
                }
                Vector3 newPos = CustomHandles.DragHandle(p, size, cap, pink, out dhResult);
                if (deleteMode && !isInfluencePoint && dhResult == CustomHandles.DragHandleResult.LMBPress) {
                    // the user clicked on the handle while in delete mode, so delete the segment
                    segments.RemoveAt(i / 3);
                    changed = true;
                    break;
                } else if (!deleteMode && isInfluencePoint && dhResult == CustomHandles.DragHandleResult.LMBDoubleClick) {
                    segment.MakeLinear();
                    segments[i / 3] = segment;
                    changed = true;
                } else if (!deleteMode && newPos != p) {
                    // the handle has been dragged, so move the point to the new position
                    if (isInfluencePoint || isWithinBounds) {
                        MovePathPoint(segments, i, newPos, moveConnected: !splitMode);
                        changed = true;
                    }
                } else if (!splitMode && !deleteMode && !isInfluencePoint 
                        && dhResult == CustomHandles.DragHandleResult.LMBRelease) {
                    // the handle has been released.  snap it to any nearby points.
                    for (int c = 0; c < segments.Count; c++) {
                        PathSegment seg2 = segments[c];
                        if (seg2.p0 != newPos && Vector2.Distance(seg2.p0, newPos) < HandleUtility.GetHandleSize(newPos) * 0.25f) {
                            newPos = seg2.p0;
                            break;
                        }
                        if (seg2.p2 != newPos && Vector2.Distance(seg2.p2, newPos) < HandleUtility.GetHandleSize(newPos) * 0.25f) {
                            newPos = seg2.p2;
                            break;
                        }
                    }
                    MovePathPoint(segments, i, newPos, moveConnected: true);
                    changed = true;
                }
            }
            // check if the mouse is hovering over a space where we could add a new point,
            // and draw it if so
            bool closeToExistingPoint = false;
            if (!changed && !hasMaxSegments && !deleteMode) {
                foreach (PathSegment s in segments) {
                    // if close to an existing point, we don't want to add a new one
                    if (Vector2.Distance(HandleUtility.WorldToGUIPoint(mouseWorldPos), 
                            HandleUtility.WorldToGUIPoint(s.p0)) < 15) {
                        closeToExistingPoint = true;
                        break;
                    }
                    if (Vector2.Distance(HandleUtility.WorldToGUIPoint(mouseWorldPos), 
                            HandleUtility.WorldToGUIPoint(s.p1)) < 15) {
                        closeToExistingPoint = true;
                        break;
                    }
                    if (Vector2.Distance(HandleUtility.WorldToGUIPoint(mouseWorldPos), 
                            HandleUtility.WorldToGUIPoint(s.p2)) < 15) {
                        closeToExistingPoint = true;
                        break;
                    }
                }
                if (!closeToExistingPoint && isWithinBounds) {
                    // not too close to an existing vert, so draw a new one
                    // find the closest point
                    float closestDistance = 99999;
                    Vector2 closestPoint = Vector2.zero;
                    for (int i = 0; i < segments.Count; i++) {
                        float dist = Vector2.Distance(segments[i].p0, (Vector2) mouseWorldPos);
                        if (dist < closestDistance) {
                            closestPoint = segments[i].p0;
                            closestDistance = dist;
                        }
                        dist = Vector2.Distance(segments[i].p2, (Vector2) mouseWorldPos);
                        if (dist < closestDistance) {
                            closestPoint = segments[i].p2;
                            closestDistance = dist;
                        }
                    }
                    // don't use an actual handle cause we want to intercept nearby clicks
                    // and not just clicks directly on the handle.
                    Rect rect = new Rect();
                    float dim = 0.05f * HandleUtility.GetHandleSize(mouseWorldPos);
                    rect.center = mouseWorldPos - new Vector3(dim, dim, 0);
                    rect.size = new Vector2(dim * 2, dim * 2);
                    Handles.color = Color.white; // remove the weird tint it does
                    Handles.DrawSolidRectangleWithOutline(rect, Color.green, Color.clear);
                    Color oldColor = Handles.color;
                    Handles.color = Color.grey;
                    Handles.DrawDottedLine(rect.center, closestPoint, HandleUtility.GetHandleSize(closestPoint));
                    Handles.color = oldColor;
                    if (Event.current.type == EventType.MouseDown && !Event.current.alt 
                            && !Event.current.shift && !Event.current.command && !Event.current.control) {
                        // the user has clicked to add a new segment, so add it for real
                        segments.Add(new PathSegment(closestPoint, mouseWorldPos));
                        changed = true;
                    }
                }
            }
            // something has been changed, so apply the new points back to the shape
            if (changed) {
                Undo.RecordObject(shape, "Edit Shapes2D Path Points");
                shape.SetPathWorldSegments(segments.GetRange(0, segments.Count).ToArray());
                EditorUtility.SetDirty(target);
            } else {
                HandleUtility.Repaint(); // to draw the new point placeholder handle
                if (Event.current.type == EventType.MouseDown && !isWithinBounds)
                    StopEditingShape(true);
            }
        }

        void OnSceneGUI() {
            Shape shape = (Shape) target;
            if (!isEditing || (shape.settings.shapeType != ShapeType.Polygon && shape.settings.shapeType != ShapeType.Path))
                return;
            if (Tools.current != Tool.None) {
                StopEditingShape(false);
                return;
            }
            // draw some borders so the user knows where the shape should live
            DrawShapeBorders(shape);
            if (shape.settings.shapeType == ShapeType.Polygon)
                EditPolygon(shape);
            else
                EditPath(shape);
            // this prevents the user selecting another object when they are
            // adding poly/path nodes
            if (Event.current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(GetHashCode(), FocusType.Passive));
        }

        private Shapes2DPrefs GetPreferences() { 
            string path = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            int index = path.IndexOf("Shapes2D");
            if (index == -1)
                return null;
            string prefsPath = path.Substring(0, index + 8) + "/Preferences.asset";
            return AssetDatabase.LoadAssetAtPath<Shapes2DPrefs>(prefsPath);
        }

        private void SetPolygonCollider2D(Shape shape) {
            PolygonCollider2D pc2d = shape.GetComponent<PolygonCollider2D>();
            if (shape.settings.shapeType == ShapeType.Polygon) {
                if (!pc2d)
                    pc2d = shape.gameObject.AddComponent<PolygonCollider2D>();
                Vector3[] points = shape.GetPolygonWorldVertices();
                Vector2[] colliderPoints = new Vector2[points.Length];
                for (int i = 0; i < points.Length; i++)
                    colliderPoints[i] = shape.transform.InverseTransformPoint(points[i]);
                Undo.RecordObject(pc2d, "Set PolygonCollider2D Points");
                pc2d.points = colliderPoints;
            }
        }

        private void FromPolygonCollider2D(Shape shape) {
            PolygonCollider2D pc2d = shape.GetComponent<PolygonCollider2D>();
            if (shape.settings.shapeType == ShapeType.Polygon) {
                if (pc2d.points.Length >= 64) {
                    EditorUtility.DisplayDialog("Too many points", 
                            "The PolygonCollider2D has too many points (max 64).", "Okay");
                    return;
                }
                Vector3[] points = new Vector3[pc2d.points.Length];
                for (int i = 0; i < pc2d.points.Length; i++)
                    points[i] = shape.transform.TransformPoint(pc2d.points[i]);
                Undo.RecordObject(shape, "Edit Shapes2D Polygon Vertices");
                shape.SetPolygonWorldVertices(points);
                EditorUtility.SetDirty(target);
            } else if (shape.settings.shapeType == ShapeType.Path) {
                if (pc2d.points.Length >= 32) {
                    EditorUtility.DisplayDialog("Too many points", 
                            "The PolygonCollider2D has too many points (max 32).", "Okay");
                    return;
                }
                PathSegment[] segments = new PathSegment[pc2d.points.Length];
                for (int i = 0; i < pc2d.points.Length; i++) {
                    Vector3 p0 = shape.transform.TransformPoint(pc2d.points[i]);
                    Vector3 p2 = i == pc2d.points.Length - 1 
                            ? shape.transform.TransformPoint(pc2d.points[0])
                            : shape.transform.TransformPoint(pc2d.points[i + 1]);
                    segments[i] = new PathSegment(p0, p2);
                }
                Undo.RecordObject(shape, "Edit Shapes2D Path Segments");
                shape.SetPathWorldSegments(segments);
                EditorUtility.SetDirty(target);
            }
        }

        private void ConvertToSprite(Shape shape) {
            string dname = "Assets/Resources/Shapes2D Sprites";
            string fname = dname + "/" + shape.name + ".png"; 
            string rname = "Shapes2D Sprites/" + shape.name;
            if (!System.IO.Directory.Exists(dname))
                System.IO.Directory.CreateDirectory(dname);
            if (System.IO.File.Exists(fname) 
                    && !EditorUtility.DisplayDialog("Overwrite File?", 
                        "A file with the name " + fname + " already exists.  "
                        + "Are you sure you want to overwrite it?", "Yes", "Cancel"))
                return;
            
            float pixelsPerUnit = 100;
            Shapes2DPrefs prefs = GetPreferences();
            if (prefs) {
                pixelsPerUnit = prefs.pixelsPerUnit;
            } else {
                Debug.LogWarning("Can't find Shapes2D Preferences in Shapes2D/Preferences.  Please re-import Shapes2D.");
            }

            Vector2 pivot = RenderToTexture2D(fname, shape, pixelsPerUnit: pixelsPerUnit);

            // refresh the asset
            AssetDatabase.ImportAsset(fname);
 
            // set the sprite's pivot point so any rotations/position stay the same
            TextureImporter textureImporter = AssetImporter.GetAtPath(fname) 
                    as TextureImporter;
            TextureImporterSettings texSettings = new TextureImporterSettings();
            textureImporter.ReadTextureSettings(texSettings);
            #if UNITY_5_5_OR_NEWER
                texSettings.ApplyTextureType(TextureImporterType.Sprite);
            #else
                texSettings.ApplyTextureType(TextureImporterType.Sprite, true);
            #endif
            texSettings.spritePixelsPerUnit = pixelsPerUnit;
            if (Vector2.Distance(pivot, new Vector2(0.5f, 0.5f)) < 0.01f) {                
                texSettings.spriteAlignment = (int) SpriteAlignment.Center;
                textureImporter.SetTextureSettings(texSettings);
            } else {
                texSettings.spriteAlignment = (int) SpriteAlignment.Custom;
                textureImporter.SetTextureSettings(texSettings);
                textureImporter.spritePivot = pivot;
            }
            AssetDatabase.ImportAsset(fname, ImportAssetOptions.ForceUpdate);
            
            Sprite sprite = Resources.Load<Sprite>(rname);

            Undo.RecordObjects(shape.GetUndoObjects().ToArray(), 
                    "Convert to Sprite");
                    
            shape.SetAsSprite(sprite, GetDefaultSpriteMaterial());
            
            // exit the gui routine because otherwise we get annoying errors because
            // we deleted a material and unity still wants to draw it
            EditorGUIUtility.ExitGUI();
        }

        public override void OnInspectorGUI() {
            serializedObject.Update(); // dunno what this does but it's in the examples?
            
            Shape shape = (Shape) serializedObject.targetObject;

            EditorGUI.BeginDisabledGroup(!shape.enabled);

            // shape type
            EditorGUILayout.PropertyField(shapeTypeProp);

            ShapeType shapeType = (ShapeType) shapeTypeProp.enumValueIndex;
            if (shapeType == ShapeType.Rectangle) {
                // rectangle props
                EditorGUILayout.PropertyField(roundnessPerCornerProp);
                if (shape.settings.roundnessPerCorner) {
                    EditorGUILayout.PropertyField(roundnessTLProp);           
                    EditorGUILayout.PropertyField(roundnessTRProp);           
                    EditorGUILayout.PropertyField(roundnessBLProp);           
                    EditorGUILayout.PropertyField(roundnessBRProp);
                } else {
                    EditorGUILayout.PropertyField(roundnessProp);
                    roundnessTLProp.floatValue = roundnessProp.floatValue;          
                    roundnessTRProp.floatValue = roundnessProp.floatValue;          
                    roundnessBLProp.floatValue = roundnessProp.floatValue;          
                    roundnessBRProp.floatValue = roundnessProp.floatValue;          
                }
            } else if (shapeType == ShapeType.Ellipse) {
                //ellipse props
                EditorGUILayout.PropertyField(startAngleProp);
                EditorGUILayout.PropertyField(endAngleProp);
                EditorGUILayout.PropertyField(invertArcProp);
                EditorGUILayout.PropertyField(innerCutoutProp);
            } else if (shapeType == ShapeType.Polygon) {
                // polygon props
                EditorGUILayout.PropertyField(polygonPresetProp);
                EditorGUI.BeginDisabledGroup(Selection.objects.Length != 1);
                if (GUILayout.Toggle(isEditing, "Edit Shape", "Button")) {
                    if (!isEditing)
                        EditShape();
                    GUIStyle helpStyle = new GUIStyle(GUI.skin.label);
                    helpStyle.wordWrap = true;
                    helpStyle.normal.textColor = Color.green;
                    EditorGUILayout.LabelField("Click on a segment to add a new node (up to 64).\nCtrl-click nodes to delete.\nSee docs about performance!", helpStyle);
                } else {
                    if (isEditing)
                        StopEditingShape(true);
                }
                if (shape.GetComponent<PolygonCollider2D>() && GUILayout.Button("From Polygon Collider 2D"))
                    FromPolygonCollider2D(shape);
                if (GUILayout.Button("Set Polygon Collider 2D"))
                    SetPolygonCollider2D(shape);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.PropertyField(usePolygonMapProp, 
                        new GUIContent("Optimize rendering (see docs!)"));
            } else if (shapeType == ShapeType.Triangle) {
                // triangle props
                EditorGUILayout.PropertyField(triangleOffsetProp);
            } else if (shapeType == ShapeType.Path) {
                EditorGUILayout.PropertyField(pathThicknessProp);
                EditorGUILayout.PropertyField(fillPathLoopsProp);
                EditorGUI.BeginDisabledGroup(Selection.objects.Length != 1);
                if (GUILayout.Toggle(isEditing, "Edit Path", "Button")) {
                    if (!isEditing)
                        EditShape();                        
                    GUIStyle helpStyle = new GUIStyle(GUI.skin.label);
                    helpStyle.wordWrap = true;
                    helpStyle.normal.textColor = Color.green;
                    EditorGUILayout.LabelField("Click to add a new segment (up to 32).\nShift-drag to separate connected nodes or prevent snapping.\nCtrl-click nodes to delete.\nDouble-click a circular node to make it linear.\nSee docs about performance!", helpStyle);
                } else {
                    if (isEditing)
                        StopEditingShape(true);
                }
                if (shape.GetComponent<PolygonCollider2D>() && GUILayout.Button("From Polygon Collider 2D"))
                    FromPolygonCollider2D(shape);
                EditorGUI.EndDisabledGroup();
            }            

            // common props
            EditorGUILayout.PropertyField(blurProp);
            EditorGUILayout.PropertyField(outlineSizeProp);
            EditorGUILayout.PropertyField(outlineColorProp);

            // fill props
            EditorGUILayout.PropertyField(fillTypeProp);
            FillType fillType = (FillType) fillTypeProp.enumValueIndex;
            if (fillType == FillType.Gradient) {
                EditorGUILayout.PropertyField(gradientTypeProp);
                if ((GradientType) gradientTypeProp.enumValueIndex < GradientType.Radial)
                    EditorGUILayout.PropertyField(gradientAxisProp);
                EditorGUILayout.PropertyField(gradientStartProp);
            }
            if (fillType >= FillType.SolidColor && fillType < FillType.Texture) {
                EditorGUILayout.PropertyField(fillColorProp);
            }
            if (fillType == FillType.Texture) {
                EditorGUILayout.PropertyField(fillTextureProp);
                EditorGUILayout.PropertyField(fillScaleProp);
            }
            if (fillType >= FillType.Gradient && fillType < FillType.Texture) {
                EditorGUILayout.PropertyField(fillColor2Prop);
            }
            if (fillType >= FillType.Gradient) {
                EditorGUILayout.PropertyField(fillOffsetProp);
                EditorGUILayout.PropertyField(fillRotationProp);
            }
            if (fillType == FillType.Grid || fillType == FillType.Stripes) {
                EditorGUILayout.PropertyField(lineSizeProp);
            }
            if (fillType == FillType.Grid || fillType == FillType.Stripes
                    || fillType == FillType.CheckerBoard) {
                EditorGUILayout.PropertyField(gridSizeProp);
            }
            
            EditorGUI.BeginDisabledGroup(Selection.objects.Length != 1);
            if (GUILayout.Button("Convert to Sprite"))
                ConvertToSprite(shape);
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();

            // if the material has been destroyed, configure everything again.
            // or if the shape has been re-enabled after being converted to a sprite,
            // attempt to restore the scale it had previously
            if (shape.enabled && (!shape.IsConfigured() || shape.wasConverted)) {
                Undo.RecordObjects(shape.GetUndoObjects().ToArray(), 
                        "Re-enable Shapes2D Component");
                shape.Configure();
                if (shape.wasConverted)
                    shape.RestoreFromConversion();
                // combine with the re-enable action
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            }
        }
    }

}