namespace Shapes2D {

    using UnityEngine;
    using UnityEngine.UI;
    using System.Collections.Generic;
    using UnityEngine.Serialization;

    /// <summary>
    /// Shapes2D base shape types.
    /// </summary>
    public enum ShapeType {
        Ellipse,
        Polygon,
        Rectangle,
        Triangle,
        Path
    }
    
    /// <summary>
    /// Shapes2D fill types.
    /// </summary>
    public enum FillType {
        None,
        MatchOutlineColor,
        SolidColor,
        Gradient,
        Grid,
        CheckerBoard,
        Stripes,
        Texture
    }
    
    /// <summary>
    /// Shapes2D gradient fill types.
    /// </summary>
    public enum GradientType {
        Linear,
        Cylindrical,
        Radial
    }
    
    /// <summary>
    /// Shapes2D gradient fill direction.
    /// </summary>
    public enum GradientAxis {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Shapes2D polygon presets.
    /// </summary>
    public enum PolygonPreset {
        Custom, Triangle, Diamond, Pentagon, Hexagon, Heptagon, Octagon, Nonagon, Decagon, Hendecagon,
        Dodecagon, Star, Arrow
    }

    /// <summary>
    /// Shapes2D path segment, for Path shape types.  It describes a quadratic Bézier curve.
    /// </summary>
    [System.SerializableAttribute]
    public struct PathSegment {
        public Vector3 p0, p1, p2;

        public PathSegment(Vector2 p0, Vector2 p1, Vector3 p2) {
            this.p0 = p0;
            this.p1 = p1;
            this.p2 = p2;
        }

        public PathSegment(Vector2 p0, Vector2 p2) {
            this.p0 = p0;
            this.p2 = p2;
            this.p1 = p0 + (p2 - p0) / 2;
        }

        public void Clamp() {
            p0.x = Mathf.Clamp01(p0.x + 0.5f) - 0.5f;
            p0.y = Mathf.Clamp01(p0.y + 0.5f) - 0.5f;
            p2.x = Mathf.Clamp01(p2.x + 0.5f) - 0.5f;
            p2.y = Mathf.Clamp01(p2.y + 0.5f) - 0.5f;
        }

        public Vector3 midpoint { get { return p0 + (p2 - p0) / 2; } } 

        public void MakeLinear() {
            p1 = p0 + (p2 - p0) / 2;
        }
    }
    
    /// <summary>
    /// The MonoBehaviour for a Shapes2D Shape.  
    /// Can be used with either a SpriteRenderer or an Image.
    /// If the Object does not have either component, one will be added depending on if there is a Canvas in the parent hierarchy or not.
    /// This component will also replace the Material and Sprite of an existing SpriteRenderer or Image attached to the GameObject.
    /// </summary>
    [ExecuteInEditMode]
    public class Shape : MonoBehaviour, IMaterialModifier {

        static Vector2[] GetVerticesForPreset(PolygonPreset preset) {
            if (preset == PolygonPreset.Triangle) {
                return new Vector2[] { new Vector2(0, 0.5f), new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f) }; 
            } else if (preset == PolygonPreset.Diamond) {
                return new Vector2[] { new Vector2(0, 0.5f), new Vector2(-0.5f, 0), new Vector2(0, -0.5f), new Vector2(0.5f, 0) }; 
            } else if (preset >= PolygonPreset.Pentagon && preset <= PolygonPreset.Dodecagon) {
                int sides = preset - PolygonPreset.Pentagon + 5;
                float slice = (Mathf.PI * 2) / sides;
                Vector2[] verts = new Vector2[sides];
                for (int i = 0; i < sides; i++) {
                    float angle = slice * i + Mathf.PI / 2; // rotate so the poly is oriented nicely
                    verts[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) / 2;
                }
                return verts;
            } else if (preset == PolygonPreset.Star) {
                return new Vector2[] { new Vector2(0, 0.5f), new Vector2(-0.15f, 0.2f), 
                        new Vector2(-0.5f, 0.16f), new Vector2(-0.23f, -0.05f),
                        new Vector2(-0.3f, -0.4f), new Vector2(0, -0.15f),
                        new Vector2(0.3f, -0.4f), new Vector2(0.23f, -0.05f),
                        new Vector2(0.5f, 0.16f), new Vector2(0.15f, 0.2f)
                }; 
            } else if (preset == PolygonPreset.Arrow) {
                return new Vector2[] { new Vector2(0.5f, 0), new Vector2(0, 0.4f),
                        new Vector2(0, 0.15f), new Vector2(-0.5f, 0.15f),
                        new Vector2(-0.5f, -0.15f), new Vector2(0, -0.15f),
                        new Vector2(0, -0.4f)
                }; 
            }
            throw new System.InvalidOperationException("Unrecognized PolygonPreset");
        }

        /// <summary>
        /// The max number of vertices a polygon can have (currently 64).
        /// </summary>
        public static readonly int MaxPolygonVertices = 64;

        /// <summary>
        /// The resolution of the polygon map, if enabled.
        /// </summary>
        public static readonly int PolyMapResolution = 32;

        /// <summary>
        /// The max number of segments a path can have (currently 32).
        /// </summary>
        public static readonly int MaxPathSegments = 32;

        /// <summary>
        /// Used internally by ShapeEditor during sprite conversion.  
        /// Compares Shapes in draw order.
        /// </summary>
        public class ShapeDrawOrderComparer : IComparer<Shape> {
            public int Compare(Shape s1, Shape s2) {
                int layer1, layer2, order1, order2;
                if (s1.spriteRenderer) {
                    layer1 = s1.spriteRenderer.sortingLayerID;
                    order1 = s1.spriteRenderer.sortingOrder;
                } else if (s1.image) {
                    layer1 = s1.image.canvas.sortingLayerID;
                    order1 = s1.image.canvas.sortingOrder;
                    foreach (Shape s in s1.image.canvas.GetComponentsInChildren<Shape>()) {
                        if (s == s1)
                            break;
                        order1 ++;
                    }
                } else {
                    throw new System.NotImplementedException();
                }
                if (s2.spriteRenderer) {
                    layer2 = s2.spriteRenderer.sortingLayerID;
                    order2 = s2.spriteRenderer.sortingOrder;
                } else if (s2.image) {
                    layer2 = s2.image.canvas.sortingLayerID;
                    order2 = s2.image.canvas.sortingOrder;
                    foreach (Shape s in s2.image.canvas.GetComponentsInChildren<Shape>()) {
                        if (s == s2)
                            break;
                        order2 ++;
                    }
                } else {
                    throw new System.NotImplementedException();
                }
                if (layer1 != layer2)
                    return layer1.CompareTo(layer2);
                if (order1 != order2)
                    return order1.CompareTo(order2);
                return s1.transform.position.z.CompareTo(s2.transform.position.z);
            }
        }    

        // the temporary material we'll use.  unfortunately this breaks batching, but we have
        // to do it in order for each shape to have its own custom parameters (including scale).
        // we could use a MaterialPropertyBlock for SpriteRenderers (which still doesn't batch but
        // at least doesn't duplicate the material), but CanvasRenderer doesn't have that
        // functionality at all and to make things worse MPB can't set blend mode properties which
        // we use during sprite conversion.
        Material material;
        SpriteRenderer spriteRenderer;
        Image image;
        
        // rect transform for figuring out the scale on UI components
        RectTransform rectTransform;
        
        // used for detecting when the object scale has changed so we can re-compute shader values
        Vector3 computedScale;
        Rect computedRect;

        /// <summary>
        /// Runtime-editable attributes of a Shapes2D Shape.
        /// </summary> 
        [System.SerializableAttribute]
        public class UserProps {
            /// <summary> 
            /// Stores whether we need to send changed properties to the shader.  
            /// You do not need to set this directly.
            /// </summary>
            [System.NonSerializedAttribute]
            public bool dirty = true;

            /// <summary> 
            /// Stores whether we need to recreate the poly map for optimized Polygon shape types.  
            /// You do not need to set this directly.
            /// </summary>
            [System.NonSerializedAttribute]
            public bool polyMapNeedsRegen = true;

            [SerializeField]
            [FormerlySerializedAs("blur")]
            private float _blur = 0f;
            /// <summary>
            /// The amount of blur in world units applied to the Shape.
            /// If the Shape has an outline, blur is applied to both the inner and outer parts of the outline. 
            /// Set to 0 to have Shapes2D dynamically apply a small amount of blur that varies with the screen resolution.
            /// </summary> 
            public float blur { get { return _blur; } set { _blur = value < 0 ? 0 : value; dirty = true; } }

            private bool _correctScaling = false;
            /// <summary>
            /// If shapeType is Ellipse and outlineSize > 0, enabling this will use a much more complicated (but glitchy) algorithm that tries to apply an equal thickness at all places along the outline.
            /// This only applies in cases when the algorithm decides it's likely to be useful (very non-circular ellipses).
            /// With this disabled, a simpler algorithm is used that does a decent job, but outlines may have irregular thickness with thick outlines and very non-circular ellipses.
            /// I strongly recommend leaving this disabled unless you really need it, and are able to test that it works reliably in all situations and platforms you care about.
            /// The innerCutout and arcAngle properties do not work when correctScaling is turned on and the algorithm is applied.
            /// </summary>
            public bool correctScaling { get { return _correctScaling; } set { _correctScaling = value; dirty = true; } } 

            [SerializeField]
            [Range(0, 360)]
            private float _endAngle = 360;
            /// <summary>
            /// If shapeType is Ellipse, specifies the end angle in degrees of the visible arc area.  Only areas of the ellipse between startAngle and endAngle will be visible.
            /// </summary>
            public float endAngle { get { return _endAngle; } set { _endAngle = Mathf.Clamp(value, 0, 360); dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("fillColor")]
            private Color _fillColor = Color.white;
            /// <summary>
            /// The Shape's fill color, depending on fillType.
            /// </summary>
            public Color fillColor { get { return _fillColor; } set { _fillColor = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("fillColor2")]
            private Color _fillColor2 = Color.black;
            /// <summary>
            /// For certain fill types, this is the second fill color used.
            /// </summary>
            public Color fillColor2 { get { return _fillColor2; } set { _fillColor2 = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("fillOffset")]
            private Vector2 _fillOffset = new Vector2(0, 0);
            /// <summary>
            /// An offset applied to the fill in world units.
            /// </summary>
            public Vector2 fillOffset { get { return _fillOffset; } set { _fillOffset = value; dirty = true; } } 

            [SerializeField]
            private bool _fillPathLoops = false;
            /// <summary>
            /// If shapeType is Path, this will cause any loops in the path to be filled.
            /// </summary>
            public bool fillPathLoops { get { return _fillPathLoops; } set { _fillPathLoops = value; dirty = true; } } 

            [Range(0f, 360f)] 
            [SerializeField]
            [FormerlySerializedAs("fillRotation")]
            private float _fillRotation = 0f;
            /// <summary>
            /// The rotation of the fill, in degrees.
            /// </summary>
            public float fillRotation { get { return _fillRotation; } set { _fillRotation = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("fillScale")]
            private Vector2 _fillScale = new Vector2(1, 1);
            /// <summary>
            /// A scale applied to the fill.
            /// </summary>
            public Vector2 fillScale { get { return _fillScale; } set { _fillScale = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("fillTexture")]
            private Texture2D _fillTexture;
            /// <summary>
            /// A Texture2D to use for the fill, if fillType is Texture.
            /// </summary>
            public Texture2D fillTexture { get { return _fillTexture; } set { _fillTexture = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("fillType")]
            private FillType _fillType = FillType.SolidColor;
            /// <summary>
            /// The Shape's fill type, a Shapes2D.FillType.
            /// </summary>
            public FillType fillType { get { return _fillType; } set { _fillType = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("gradientAxis")]
            private GradientAxis _gradientAxis = GradientAxis.Horizontal;
            /// <summary>
            /// A Shapes2D.GradientAxis specifying the gradient direction, if the fillType is Gradient.
            /// The difference between this and simply using fillRotation to rotate a gradient fill is that Shapes2D uses the axis to determine the gradient's start and end positions.
            /// When using a rotated gradient rather than specifying the axis directly, the gradient may not stretch the way you expect it to when you scale the Shape.
            /// </summary>
            public GradientAxis gradientAxis { get { return _gradientAxis; } set { _gradientAxis = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("gradientStart")]
            [Range(0, 1)]
            private float _gradientStart = 0f;
            /// <summary>
            /// The gradient's start position as a percentage (from 0-1) along its gradientAxis, if the fillType is Gradient.
            /// </summary>
            public float gradientStart { get { return _gradientStart; } set { _gradientStart = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("gradientType")]
            private GradientType _gradientType = GradientType.Linear;
            /// <summary>
            /// A Shapes2D.GradientType specifying the gradient type, if the fillType is Gradient.
            /// </summary>
            public GradientType gradientType { get { return _gradientType; } set { _gradientType = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("gridSize")]
            private float _gridSize = 0.2f;
            /// <summary>
            /// If fillType is Grid, Checkerboard or Stripes, gridSize is the size in world units of the grid (or the distance between lines).
            /// </summary>
            public float gridSize { get { return _gridSize; } set { _gridSize = value < 0.01f ? 0.01f : value; dirty = true; } } 

            [SerializeField]
            private Vector2 _innerCutout;
            /// <summary>
            /// If shapeType is Ellipse, setting this will cut out an inner "donut hole" of the ellipse.  Each dimension is specified from 0-1 and is a percentage of the X and Y radii of the ellipse.
            /// Use by itself or in combination with startAngle and endAngle to turn a pie shape into an arc. 
            /// </summary>
            public Vector2 innerCutout { get { return _innerCutout; } 
                set { 
                    _innerCutout = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y)); 
                    dirty = true; 
                } 
            } 

            [SerializeField]
            private bool _invertArc = false;
            /// <summary>
            /// If shapeType is Ellipse, this inverts startAngle and endAngle so that they describe the invisible area rather than the visible area of the ellipse.
            /// </summary>
            public bool invertArc { get { return _invertArc; } set { _invertArc = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("lineSize")]
            private float _lineSize = 0.05f;
            /// <summary>
            /// If fillType is Grid or Stripes, lineSize is the size in world units of the lines in the fill.
            /// </summary> 
            public float lineSize { get { return _lineSize; } set { _lineSize = value < 0.01f ? 0.01f : value; dirty = true; } } 

            [HideInInspector]
            [FormerlySerializedAs("numSides")]
            /// <summary>
            /// Obsolete since version 1.3; use polygonPreset instead.
            /// </summary>
            public int _obsoleteNumSides = -1;

            [SerializeField]
            [FormerlySerializedAs("outlineColor")]
            private Color _outlineColor = Color.grey;
            /// <summary>
            /// The color of the outline, if any.
            /// </summary>
            public Color outlineColor { get { return _outlineColor; } set { _outlineColor = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("outlineSize")]
            private float _outlineSize = 0f;
            /// <summary>
            /// Outline size in world units.
            /// Specifically, this is the thickness in world units of the shape's outline on one side of the Shape.
            /// </summary> 
            public float outlineSize { get { return _outlineSize; } set { _outlineSize = value < 0 ? 0 : value; dirty = true; } }

            [SerializeField]
            private PathSegment[] _pathSegments = new PathSegment[] {
                new PathSegment(new Vector2(-0.3f, 0f), new Vector2(0.3f, 0f))
            };
            /// <summary>
            /// If shapeType is Path, this holds a list of PathSegments.  
            /// A segment is a quadratic Bézier curve, with each curve made of 3 points.  The first and last point in each segment will be clamped to the range -0.5, 0.5.
            /// To make a connected path, set points of connected segments to be equal to each other.
            /// You can have from 1 to MaxPathSegments (currently 32).
            /// If you modify the returned array directly rather than setting a new array, call settings.pathSegments = settings.pathSegments anyway so the shape knows to update the shader and values are sanitized.
            /// </summary>
            public PathSegment[] pathSegments {
                get { return _pathSegments; } 
                set { 
                    if (_pathSegments.Length < 1 || _pathSegments.Length > MaxPathSegments)
                        throw new System.InvalidOperationException("Shapes2D: A path must have between one and MaxPathSegments segments (currently " + MaxPathSegments + ").");
                    _pathSegments = value;
                    for (int i = 0; i < _pathSegments.Length; i++)
                        _pathSegments[i].Clamp();
                    dirty = true;
                } 
            }            
            
            [SerializeField]
            private float _pathThickness = 0.1f;
            /// <summary>
            /// If shapeType is Path, this is the thickness of the path in world units.
            /// </summary>
            public float pathThickness { get { return _pathThickness; } set { _pathThickness = value < 0.01f ? 0.01f : value; dirty = true; } }

            [SerializeField]
            private PolygonPreset _polygonPreset = PolygonPreset.Pentagon;
            /// <summary>
            /// For use with shapeType Polygon, sets the vertices to a copy from the given Shapes2D.PolygonPreset.
            /// </summary>
            public PolygonPreset polygonPreset {
                get {
                    return _polygonPreset;
                }
                set {
                    if (value != PolygonPreset.Custom)
                        _polyVertices = Shape.GetVerticesForPreset(value);
                    _polygonPreset = value;
                    dirty = true;
                    polyMapNeedsRegen = true;
                }
            }

            [SerializeField]
            [FormerlySerializedAs("_vertices")]
            private Vector2[] _polyVertices = GetVerticesForPreset(PolygonPreset.Pentagon);
            /// <summary>
            /// If shapeType is Polygon, this holds its vertices normalized from -0.5 to 0.5 (verts will be clamped to this range).
            /// You can have from 3 to MaxPolygonVertices (currently 64).
            /// If you modify the returned vertex array directly rather than setting a new array, call settings.polyVertices = settings.polyVertices anyway so the shape knows to update the shader and values are sanitized.
            /// </summary>
            public Vector2[] polyVertices { 
                get { return _polyVertices; } 
                set { 
                    if (_polyVertices.Length < 3 || _polyVertices.Length > MaxPolygonVertices)
                        throw new System.InvalidOperationException("Shapes2D: Vertex count must be between 3 and MaxPolygonVertices - currently " + MaxPolygonVertices + ".");
                    _polyVertices = value;
                    for (int i = 0; i < _polyVertices.Length; i++) {
                        _polyVertices[i].x = Mathf.Clamp01(_polyVertices[i].x + 0.5f) - 0.5f;
                        _polyVertices[i].y = Mathf.Clamp01(_polyVertices[i].y + 0.5f) - 0.5f;
                    } 
                    _polygonPreset = PolygonPreset.Custom; 
                    dirty = true;
                    polyMapNeedsRegen = true;
                } 
            }            

            [SerializeField]
            [FormerlySerializedAs("roundness")]
            private float _roundness = 0f;
            /// <summary>
            /// If shapeType is Rectangle and roundnessPerCorner is false, this specifies the amount of the rectangle, along a single side, that will be rounded.
            /// For UI shapes this is in world units so borders stay the same as you scale, like a 9-slice.  For sprite shapes it's a percentage of the available area so shapes won't change as they scale. 
            /// </summary>
            public float roundness { get { return _roundness; } set { _roundness = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("roundnessBottomLeft")]
            private float _roundnessBottomLeft = 0f;
            /// <summary>
            /// If shapeType is Rectangle and roundnessPerCorner is true, this specifies the amount of the rectangle in world units, along a single side, that will be rounded.
            /// </summary>
            public float roundnessBottomLeft { get { return _roundnessBottomLeft; } set { _roundnessBottomLeft = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("roundnessBottomRight")]
            private float _roundnessBottomRight = 0f;
            /// <summary>
            /// If shapeType is Rectangle and roundnessPerCorner is true, this specifies the amount of the rectangle in world units, along a single side, that will be rounded.
            /// </summary>
            public float roundnessBottomRight { get { return _roundnessBottomRight; } set { _roundnessBottomRight = value; dirty = true; } } 
            
            [SerializeField]
            [FormerlySerializedAs("roundnessPerCorner")]
            private bool _roundnessPerCorner = false;
            /// <summary>
            /// If shapeType is Rectangle, set this to true if you want to use per-corner roundness attributes instead of a single value for all corners.
            /// </summary>
            public bool roundnessPerCorner { get { return _roundnessPerCorner; } set { _roundnessPerCorner = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("roundnessTopLeft")]
            private float _roundnessTopLeft = 0f;
            /// <summary>
            /// If shapeType is Rectangle and roundnessPerCorner is true, this specifies the amount of the rectangle in world units, along a single side, that will be rounded.
            /// </summary>
            public float roundnessTopLeft { get { return _roundnessTopLeft; } set { _roundnessTopLeft = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("roundnessTopRight")]
            private float _roundnessTopRight = 0f;
            /// <summary>
            /// If shapeType is Rectangle and roundnessPerCorner is true, this specifies the amount of the rectangle in world units, along a single side, that will be rounded.
            /// </summary>
            public float roundnessTopRight { get { return _roundnessTopRight; } set { _roundnessTopRight = value; dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("shapeType")]
            private ShapeType _shapeType = ShapeType.Rectangle;
            /// <summary>
            /// The base shape type, a Shapes2D.ShapeType.
            /// </summary>
            public ShapeType shapeType { get { return _shapeType; } set { _shapeType = value; dirty = true; } }
            
            [SerializeField]
            [Range(0, 360)]
            private float _startAngle = 0;
            /// <summary>
            /// If shapeType is Ellipse, specifies the start angle in degrees of the visible arc area.  Only areas of the ellipse between startAngle and endAngle will be visible.
            /// </summary>
            public float startAngle { get { return _startAngle; } set { _startAngle = Mathf.Clamp(value, 0, 360); dirty = true; } } 

            [SerializeField]
            [FormerlySerializedAs("triangleOffset")]
            [Range(0, 1)]
            private float _triangleOffset = 0.5f;
            /// <summary>
            /// If shapeType is Triangle, this is position on the x axis of the topmost point as a percentage of its width, from 0-1.
            /// It may be a little clumsy but you should be able to get any triangle you want by using triangleOffset and rotating the Shape.
            /// </summary>
            public float triangleOffset { get { return _triangleOffset; } set { _triangleOffset = value; dirty = true; } } 

            [SerializeField]
            private bool _usePolygonMap = false;
            /// <summary>
            /// If shapeType is Polygon, enabling this will pre-compute some data allowing us to render much faster.
            /// Most polygons will render correctly using this optimization, but not all.  In general, vertices cannot be too close to each other, and vertices cannot be too close to non-adjacent polygon edges, or you'll see rendering issues.
            /// Also, polygon edges can't intersect with each other.
            /// If you get good performance with leaving this off (or plan to Convert to Sprite anyway), then leave it off.  On mobile, polygon rendering is very slow and you'll probably need this optimization. 
            /// </summary>
            public bool usePolygonMap { get { return _usePolygonMap; } 
                set { 
                    if (value != _usePolygonMap)
                        polyMapNeedsRegen = true;
                    _usePolygonMap = value; 
                    dirty = true;
                } 
            }
        }
        
        protected class ShaderProps {
            // we'll get scale from the object's scale in unity
            public float xScale;
            public float yScale;
            
            // settings common to all shapes
            public ShapeType shapeType = ShapeType.Rectangle;
            public float outlineSize = 0.03f;
            public float blur = 0.027f;
            public Color outlineColor = new Color(1, 1, 1, 1);
            public FillType fillType = FillType.MatchOutlineColor;
            public Color fillColor = new Color(1, 1, 1, 1);
            public Color fillColor2 = new Color(1, 1, 1, 1);
            public float fillRotation = 0f;
            public Vector2 fillOffset = new Vector2(0, 0);
            public Vector2 fillScale = new Vector2(1, 1);
            public GradientType gradientType = GradientType.Linear;
            public GradientAxis gradientAxis = GradientAxis.Horizontal;
            public float gradientStart = 0f;
            public Texture2D fillTexture;
            public float gridSize = 0.5f;
            public float lineSize = 0.1f;

            // settings for rectangles
            public Vector4 roundnessVec = Vector4.zero;
            
            // settings for ellipses
            public Vector4 innerRadii;
            public float arcMinAngle;
            public float arcMaxAngle;
            public bool invertArc;
            public bool useArc;
            public bool correctScaling;
            
            // settings for triangles
            public float triangleOffset = 0.5f;

            // settings for polygons
            public int numPolyVerts;
            public Vector4[] polyVertices;
            public bool usePolygonMap;
            public Texture2D polyMap;

            // settings for paths
            public float pathThickness;
            public int numPathSegments;
            public Vector4[] pathPoints;
            public bool fillPathLoops;
        }

        /// <summary>
        /// Shape attributes that can be modified at runtime (see UserProps).
        /// </summary>
        public UserProps settings = new UserProps();

        // these settings are a transformed copy of the above that we'll actually
        // pass to the shader
        ShaderProps shaderSettings = new ShaderProps();

        /// <summary>
        /// Used internally by ShapeEditor during sprite conversion.  
        /// This is true if the Shape Component is disabled because it was converted to a sprite.
        /// </summary>
        [HideInInspector]
        public bool wasConverted;

        /// <summary>
        /// Used internally by ShapeEditor during sprite conversion.  
        /// If the Shape has been converted to a sprite (and is not a Canvas Shape), this is the local scale it had before the conversion process.
        /// </summary>
        [HideInInspector]
        public Vector3 preConvertedScale;

        // string concat is slow & wasteful so we'll cache the list of shader var names
        // for use when sending polygon vertices to the shader
        private static string[] _ShaderVertexVarNames;
        private static string[] ShaderVertexVarNames {
            get {
                if (_ShaderVertexVarNames == null) {
                    _ShaderVertexVarNames = new string[MaxPolygonVertices];
                    for (int i = 0; i < MaxPolygonVertices; i++)
                        _ShaderVertexVarNames[i] = "_Verts" + i;
                }
                return _ShaderVertexVarNames;
            }
        }
        
        // string concat is slow & wasteful so we'll cache the list of shader var names
        // for use when sending path points to the shader
        private static string[] _ShaderPointsVarNames;
        private static string[] ShaderPointsVarNames {
            get {
                if (_ShaderPointsVarNames == null) {
                    _ShaderPointsVarNames = new string[MaxPathSegments * 3];
                    for (int i = 0; i < MaxPathSegments * 3; i++)
                        _ShaderPointsVarNames[i] = "_Points" + i;
                }
                return _ShaderPointsVarNames;
            }
        }

        void Start() {
            if (!GetComponent<SpriteRenderer>() && !GetComponent<Image>()) {
                Debug.LogWarning("Shapes2D: Shape component needs a SpriteRenderer or an Image.");
                return;
            }
            Configure();
        }

        void OnDestroy() {
            if (material != null) {
                DestroyImmediate(material);
                material = null;
            }
        }

        Material GetBaseMaterial(ShapeType st) {
            Material baseMaterial = (Material) Resources.Load(
                        "Shapes2D/Materials/Shape", typeof(Material));
            if (!baseMaterial) {
                Debug.LogError("Shapes2D: Couldn't find a base material in Shapes2D/Materials/Resources."); 
                return null;
            }
            return baseMaterial;
        }

        /// <summary>
        /// Used internally by Shape and ShapeEditor.
        /// Makes sure the GameObject has a SpriteRenderer or Image component, and sets them up with a new Material and an appropriate sprite.
        /// </summary>
        // note - don't put anything consequential in here, i.e. nothing that
        // modifies serialized attributes - this can't be undone by the unity
        // editor        
        public void Configure() {
            // make sure we have a renderer we can work with 
            if (!GetComponent<SpriteRenderer>() && !GetComponent<Image>())
                return;

            // load the material from resources
            if (!material) {
                Material baseMaterial = GetBaseMaterial(settings.shapeType);
                material = Instantiate(baseMaterial);
            }
            material.name = "Shapes2D Material";

            spriteRenderer = GetComponent<SpriteRenderer>();
            image = GetComponent<Image>();
            rectTransform = null;
            if (spriteRenderer) {
                image = null;
                spriteRenderer.material = material;
                spriteRenderer.sprite = Resources.Load<Sprite>("Shapes2D/1x1");
            } else if (image) {
                spriteRenderer = null;
                image.material = material;
                image.sprite = null;
                image.type = Image.Type.Simple;
                rectTransform = GetComponent<RectTransform>();
            }
            
            settings.dirty = true;
            settings.polyMapNeedsRegen = true;
        }
        
        void SetKeyword(Material material, string keyword, bool value) {
            if (value)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }
        
        // give our computed shaderSettings data to the Shader via a Material
        void ApplyShaderPropertiesToMaterial(Material material, bool disableBlending = false) {
            if (IsUIComponent() && !image.canvas)
                return; // this happens in the editor when working with prefabs for some reason

            // make sure we are using the right shader variant for this shape type
            SetKeyword(material, "RECTANGLE", shaderSettings.shapeType == ShapeType.Rectangle);
            SetKeyword(material, "ELLIPSE", shaderSettings.shapeType == ShapeType.Ellipse && shaderSettings.correctScaling);
            SetKeyword(material, "SIMPLE_ELLIPSE", shaderSettings.shapeType == ShapeType.Ellipse && !shaderSettings.correctScaling);
            SetKeyword(material, "POLYGON_MAP", shaderSettings.shapeType == ShapeType.Polygon 
                    && shaderSettings.usePolygonMap);
            SetKeyword(material, "POLYGON_8", shaderSettings.shapeType == ShapeType.Polygon 
                    && shaderSettings.numPolyVerts <= 8);
            SetKeyword(material, "POLYGON_16", shaderSettings.shapeType == ShapeType.Polygon 
                    && shaderSettings.numPolyVerts > 8 && shaderSettings.numPolyVerts <= 16);
            SetKeyword(material, "POLYGON_24", shaderSettings.shapeType == ShapeType.Polygon 
                    && shaderSettings.numPolyVerts > 16 && shaderSettings.numPolyVerts <= 24);
            SetKeyword(material, "POLYGON_32", shaderSettings.shapeType == ShapeType.Polygon 
                    && shaderSettings.numPolyVerts > 24 && shaderSettings.numPolyVerts <= 32);
            SetKeyword(material, "POLYGON_40", shaderSettings.shapeType == ShapeType.Polygon 
                    && shaderSettings.numPolyVerts > 32 && shaderSettings.numPolyVerts <= 40);
            SetKeyword(material, "POLYGON_48", shaderSettings.shapeType == ShapeType.Polygon 
                    && shaderSettings.numPolyVerts > 40 && shaderSettings.numPolyVerts <= 48);
            SetKeyword(material, "POLYGON_56", shaderSettings.shapeType == ShapeType.Polygon 
                    && shaderSettings.numPolyVerts > 48 && shaderSettings.numPolyVerts <= 56);
            SetKeyword(material, "POLYGON_64", shaderSettings.shapeType == ShapeType.Polygon 
                    && shaderSettings.numPolyVerts > 56 && shaderSettings.numPolyVerts <= 64);
            SetKeyword(material, "TRIANGLE", shaderSettings.shapeType == ShapeType.Triangle);
            SetKeyword(material, "PATH_1", shaderSettings.shapeType == ShapeType.Path && !shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments == 1);
            SetKeyword(material, "PATH_2", shaderSettings.shapeType == ShapeType.Path && !shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments == 2);
            SetKeyword(material, "PATH_4", shaderSettings.shapeType == ShapeType.Path && !shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments >= 3 && shaderSettings.numPathSegments <= 4);
            SetKeyword(material, "PATH_8", shaderSettings.shapeType == ShapeType.Path && !shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments >= 5 && shaderSettings.numPathSegments <= 8);
            SetKeyword(material, "PATH_16", shaderSettings.shapeType == ShapeType.Path && !shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments >= 9 && shaderSettings.numPathSegments <= 16);
            SetKeyword(material, "PATH_24", shaderSettings.shapeType == ShapeType.Path && !shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments >= 17 && shaderSettings.numPathSegments <= 25);
            SetKeyword(material, "PATH_32", shaderSettings.shapeType == ShapeType.Path && !shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments >= 25 && shaderSettings.numPathSegments <= 32);
            SetKeyword(material, "FILLED_PATH_1", shaderSettings.shapeType == ShapeType.Path && shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments == 1);
            SetKeyword(material, "FILLED_PATH_2", shaderSettings.shapeType == ShapeType.Path && shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments == 2);
            SetKeyword(material, "FILLED_PATH_4", shaderSettings.shapeType == ShapeType.Path && shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments >= 3 && shaderSettings.numPathSegments <= 4);
            SetKeyword(material, "FILLED_PATH_8", shaderSettings.shapeType == ShapeType.Path && shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments >= 5 && shaderSettings.numPathSegments <= 8);
            SetKeyword(material, "FILLED_PATH_16", shaderSettings.shapeType == ShapeType.Path && shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments >= 9 && shaderSettings.numPathSegments <= 16);
            SetKeyword(material, "FILLED_PATH_24", shaderSettings.shapeType == ShapeType.Path && shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments >= 17 && shaderSettings.numPathSegments <= 24);
            SetKeyword(material, "FILLED_PATH_32", shaderSettings.shapeType == ShapeType.Path && shaderSettings.fillPathLoops 
                    && shaderSettings.numPathSegments >= 25 && shaderSettings.numPathSegments <= 32);
            SetKeyword(material, "FILL_NONE", shaderSettings.fillType == FillType.None);
            SetKeyword(material, "FILL_CHECKERBOARD", shaderSettings.fillType == FillType.CheckerBoard);
            SetKeyword(material, "FILL_GRADIENT", shaderSettings.fillType == FillType.Gradient);
            SetKeyword(material, "FILL_GRID", shaderSettings.fillType == FillType.Grid);
            SetKeyword(material, "FILL_OUTLINE_COLOR", shaderSettings.fillType == FillType.MatchOutlineColor);
            SetKeyword(material, "FILL_SOLID_COLOR", shaderSettings.fillType == FillType.SolidColor);
            SetKeyword(material, "FILL_STRIPES", shaderSettings.fillType == FillType.Stripes);
            SetKeyword(material, "FILL_TEXTURE", shaderSettings.fillType == FillType.Texture);

            if (disableBlending) {
                // disable blending
                material.SetInt("_Shapes2D_SrcBlend", (int) UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_Shapes2D_DstBlend", (int) UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_Shapes2D_SrcAlpha", (int) UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_Shapes2D_DstAlpha", (int) UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_PreMultiplyAlpha", 0);
            } else {
                // blending is One OneMinusSrcAlpha (premultiplied alpha blending)
                material.SetInt("_Shapes2D_SrcBlend", (int) UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_Shapes2D_DstBlend", (int) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_Shapes2D_SrcAlpha", (int) UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_Shapes2D_DstAlpha", (int) UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_PreMultiplyAlpha", 1);
            }

            if (IsUIComponent() && image.canvas.renderMode != RenderMode.WorldSpace)
                material.SetFloat("_PixelSize", 1f / image.canvas.scaleFactor);
            else
                material.SetFloat("_PixelSize", 0); // let the shader figure it out
            material.SetFloat("_XScale", shaderSettings.xScale);
            material.SetFloat("_YScale", shaderSettings.yScale);
            material.SetFloat("_OutlineSize", shaderSettings.outlineSize);
            material.SetFloat("_Blur", shaderSettings.blur);
            material.SetColor("_OutlineColor", shaderSettings.outlineColor);
            if (shaderSettings.fillType >= FillType.SolidColor 
                    && shaderSettings.fillType < FillType.Texture)
                material.SetColor("_FillColor", shaderSettings.fillColor);
            if (shaderSettings.fillType >= FillType.Gradient
                    && shaderSettings.fillType < FillType.Texture)
                material.SetColor("_FillColor2", shaderSettings.fillColor2);
            if (shaderSettings.fillType > FillType.SolidColor) {
                material.SetFloat("_FillRotation", shaderSettings.fillRotation);
                material.SetFloat("_FillOffsetX", shaderSettings.fillOffset.x);
                material.SetFloat("_FillOffsetY", shaderSettings.fillOffset.y);
            }
            if (shaderSettings.fillType == FillType.Texture) {
                material.SetFloat("_FillScaleX", shaderSettings.fillScale.x);
                material.SetFloat("_FillScaleY", shaderSettings.fillScale.y);
            }
            if (shaderSettings.fillType >= FillType.Grid
                    && shaderSettings.fillType < FillType.Texture) {
                material.SetFloat("_GridSize", shaderSettings.gridSize);
                material.SetFloat("_LineSize", shaderSettings.lineSize);
            }
            if (shaderSettings.fillType == FillType.Gradient) {
                material.SetInt("_GradientType", (int) shaderSettings.gradientType);
                if (shaderSettings.gradientType < GradientType.Radial)
                    material.SetInt("_GradientAxis", (int) shaderSettings.gradientAxis);
                material.SetFloat("_GradientStart", shaderSettings.gradientStart);
            }
            if (shaderSettings.fillTexture)
                material.SetTexture("_FillTexture", shaderSettings.fillTexture);
            if (shaderSettings.shapeType == ShapeType.Rectangle) {
                material.SetVector("_Roundness", shaderSettings.roundnessVec);
            } else if (shaderSettings.shapeType == ShapeType.Ellipse) {
                material.SetVector("_InnerRadii", shaderSettings.innerRadii);
                material.SetVector("_ArcAngles", new Vector4(shaderSettings.arcMinAngle, 
                        shaderSettings.arcMaxAngle, shaderSettings.useArc ? 1 : 0, 
                        shaderSettings.invertArc ? 1 : 0));
            } else if (shaderSettings.shapeType == ShapeType.Polygon) {
                material.SetFloat("_NumVerts", shaderSettings.numPolyVerts);                
                #if UNITY_5_4_OR_NEWER
                    // in Unity 5.4 you can't resize an array property up, so we have to set the largest
                    // possible size once.  furthermore, Unity likes to reset our properties constantly
                    // so we need to check and make sure that hasn't happened.
                    if (!material.HasProperty("_Verts"))
                        material.SetVectorArray("_Verts", new Vector4[MaxPolygonVertices]);
                    material.SetVectorArray("_Verts", shaderSettings.polyVertices);
                #else
                    for (int i = 0; i < shaderSettings.numPolyVerts; i++)
                        material.SetVector(ShaderVertexVarNames[i], shaderSettings.polyVertices[i]);
                #endif
                if (shaderSettings.usePolygonMap)
                    material.SetTexture("_PolyMap", shaderSettings.polyMap);
            } else if (settings.shapeType == ShapeType.Triangle) {
                material.SetFloat("_TriangleOffset", shaderSettings.triangleOffset);
            } else if (settings.shapeType == ShapeType.Path) {
                material.SetFloat("_Thickness", shaderSettings.pathThickness);
                material.SetFloat("_NumSegments", shaderSettings.numPathSegments);                
                #if UNITY_5_4_OR_NEWER
                    // in Unity 5.4 you can't resize an array property up, so we have to set the largest
                    // possible size once.  furthermore, Unity likes to reset our properties constantly
                    // so we need to check and make sure that hasn't happened.
                    if (!material.HasProperty("_Points"))
                        material.SetVectorArray("_Points", new Vector4[MaxPathSegments * 3]);
                    material.SetVectorArray("_Points", shaderSettings.pathPoints);
                #else
                    for (int i = 0; i < shaderSettings.numPathSegments * 3; i++)
                        material.SetVector(ShaderPointsVarNames[i], shaderSettings.pathPoints[i]);
                #endif
            }
        }
        
        void GetRectTransformWorldCorners(out Vector3 topLeft, out Vector3 topRight, 
                out Vector3 bottomLeft, out Vector3 bottomRight) {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            bottomLeft = corners[0];
            topLeft = corners[1];
            topRight = corners[2];
            bottomRight = corners[3];
        }

        /// <summary>
        /// Gets the size of the shape in "pre-transformed" units.  
        /// That just means that for UI components it doesn't include the transform's scale, but it does scale up to the canvas size.
        /// You can use this for translating shape normalized coords (i.e. from -0.5 to 0.5) to world coords by multiplying by this and then using TransformPoint() (only necessary for UI shapes).
        /// In other words, this is an additional scale you can apply for UI shapes to scale up to the canvas world size.   
        /// </summary>
        public Vector2 GetScale() {
            Vector2 size = new Vector2(1, 1);
            if (IsUIComponent()) {
                // figure out the size for UI RectTransform objects
                Vector3 bottomLeft, bottomRight, topLeft, topRight;
                GetRectTransformWorldCorners(out topLeft, out topRight, 
                        out bottomLeft, out bottomRight);
                topLeft = transform.InverseTransformPoint(topLeft);
                bottomLeft = transform.InverseTransformPoint(bottomLeft);
                topRight = transform.InverseTransformPoint(topRight);
                bottomRight = transform.InverseTransformPoint(bottomRight);
                size.x = Vector2.Distance(topLeft, topRight);
                size.y = Vector2.Distance(topLeft, bottomLeft);
                size /= image.canvas.scaleFactor;
            } else {
                return new Vector2(1, 1);
            }
            size.x = size.x < 0 ? -size.x : size.x;
            size.y = size.y < 0 ? -size.y : size.y;
            return size;
        }

        /// <summary>
        /// Gets the size of the shape in world units.
        /// </summary>
        public Vector2 GetSize() {
            Vector2 size = new Vector2(1, 1);
            if (IsUIComponent()) {
                // in some circumstances in the editor, this can happen
                if (!image.canvas)
                    return size;
                // figure out the size for UI RectTransform objects
                Vector3 bottomLeft, bottomRight, topLeft, topRight;
                GetRectTransformWorldCorners(out topLeft, out topRight, 
                        out bottomLeft, out bottomRight);
                // topLeft = transform.InverseTransformPoint(topLeft);
                // bottomLeft = transform.InverseTransformPoint(bottomLeft);
                // topRight = transform.InverseTransformPoint(topRight);
                // bottomRight = transform.InverseTransformPoint(bottomRight);
                // if the canvas is in ScreenSpaceCamera mode, Unity does some calculations based on the plane distance
                // and the camera's aspect and fov.  I'm not sure exactly what Unity does but using the canvas scale 
                // seems to be a shortcut.
                var scaleFactor = image.canvas.scaleFactor;
                if (image.canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    scaleFactor = image.canvas.transform.lossyScale.x;
                size.x = Vector2.Distance(topLeft, topRight) / scaleFactor;
                size.y = Vector2.Distance(topLeft, bottomLeft) / scaleFactor;
            } else {
                // get the size for normal Transform objects
                size.x = transform.lossyScale.x;
                size.y = transform.lossyScale.y;
            }
            size.x = size.x < 0 ? -size.x : size.x;
            size.y = size.y < 0 ? -size.y : size.y;
            return size;
        }
        
        // copy the user settings into shaderSettings, transforming them as
        // necessary for the Shader to understand.  this doesn't actually pass
        // anything to the Shader.
        void ComputeShaderProperties() {
            shaderSettings.shapeType = settings.shapeType;

            // get the scale, which is just the size because everything is relative 
            // to a 1x1 world unit square
            Vector2 scale = GetSize();
            shaderSettings.xScale = scale.x;
            shaderSettings.yScale = scale.y;

            // common shader properties
            shaderSettings.outlineSize = settings.outlineSize;
            shaderSettings.blur = settings.blur;
            shaderSettings.outlineColor = settings.outlineColor;

            // sanitize some properties for the shader
            float minDim = Mathf.Min(shaderSettings.xScale, shaderSettings.yScale);
            shaderSettings.outlineSize = Mathf.Min(shaderSettings.outlineSize, minDim / 2);
            
            // fill type properties
            shaderSettings.fillType = settings.fillType;
            shaderSettings.fillColor = settings.fillColor;
            shaderSettings.fillColor2 = settings.fillColor2;
            shaderSettings.fillRotation = settings.fillRotation * Mathf.Deg2Rad;
            shaderSettings.fillOffset = settings.fillOffset;
            shaderSettings.fillScale = settings.fillScale;
            shaderSettings.fillTexture = settings.fillTexture;
            shaderSettings.gradientType = settings.gradientType;
            shaderSettings.gradientAxis = settings.gradientAxis;
            shaderSettings.gradientStart = settings.gradientStart;
            shaderSettings.gridSize = settings.gridSize; //Mathf.Max(0.01f, settings.gridSize);
            shaderSettings.lineSize = settings.lineSize; // Mathf.Max(0.01f, settings.lineSize);
            
            if (settings.shapeType == ShapeType.Rectangle) {
                // rectangle specific properties
                if (spriteRenderer) {
                    // for sprite-based shapes it makes more sense to have roundness
                    // be a percentage of the dimensions so you can scale up and down
                    // uniformly
                    float xScale = shaderSettings.xScale;
                    float yScale = shaderSettings.yScale;
                    shaderSettings.roundnessVec = new Vector4(
                            Mathf.Min(settings.roundnessTopLeft * xScale / 2, 
                                settings.roundnessTopLeft * yScale / 2),
                            Mathf.Min(settings.roundnessTopRight * xScale / 2, 
                                settings.roundnessTopRight * yScale / 2),
                            Mathf.Min(settings.roundnessBottomLeft * xScale / 2, 
                                settings.roundnessBottomLeft * yScale / 2),
                            Mathf.Min(settings.roundnessBottomRight * xScale / 2, 
                                settings.roundnessBottomRight * yScale / 2));
                } else {
                    // for UI components, roundness should be in world units so when 
                    // you scale the component the borders stay the same size
                    shaderSettings.roundnessVec = new Vector4(
                            settings.roundnessTopLeft,
                            settings.roundnessTopRight, 
                            settings.roundnessBottomLeft,
                            settings.roundnessBottomRight);
                }
            } else if (settings.shapeType == ShapeType.Ellipse) {
                // ellipse specific properties
                shaderSettings.correctScaling = settings.correctScaling;
                shaderSettings.innerRadii = new Vector4(shaderSettings.xScale * settings.innerCutout.x, 
                        shaderSettings.yScale * settings.innerCutout.y, 0, 0) / 2;
                // avoid glitchiness if one component is 0 and the other isn't
                if (shaderSettings.innerRadii.x > 0)
                    shaderSettings.innerRadii.y = Mathf.Max(shaderSettings.innerRadii.y, 0.001f);
                if (shaderSettings.innerRadii.y > 0)
                    shaderSettings.innerRadii.x = Mathf.Max(shaderSettings.innerRadii.x, 0.001f);
                float arcMinAngle = Mathf.Min(settings.startAngle, settings.endAngle);
                float arcMaxAngle = Mathf.Max(settings.startAngle, settings.endAngle);
                shaderSettings.arcMinAngle = arcMinAngle * Mathf.Deg2Rad;
                shaderSettings.arcMaxAngle = arcMaxAngle * Mathf.Deg2Rad;
                shaderSettings.invertArc = settings.invertArc;
                shaderSettings.useArc = arcMinAngle != arcMaxAngle && !(arcMinAngle == 0 && arcMaxAngle == 360);
            } else if (settings.shapeType == ShapeType.Polygon) {
                // polygon specific properties
                shaderSettings.numPolyVerts = settings.polyVertices.Length;
                if (shaderSettings.polyVertices == null || shaderSettings.polyVertices.Length != shaderSettings.numPolyVerts)
                    shaderSettings.polyVertices = new Vector4[shaderSettings.numPolyVerts];
                // for the shader, each vertex is a Vector4 where zw is the endpoint of its line segment
                int j = shaderSettings.numPolyVerts - 1;
                for (int i = 0; i < settings.polyVertices.Length; i++) {
                    Vector2 vi = new Vector2(settings.polyVertices[i].x * shaderSettings.xScale,
                            settings.polyVertices[i].y * shaderSettings.yScale);
                    Vector2 vj = new Vector2(settings.polyVertices[j].x * shaderSettings.xScale,
                            settings.polyVertices[j].y * shaderSettings.yScale);
                    shaderSettings.polyVertices[i] = new Vector4(vi.x, vi.y, vj.x, vj.y);
                    j = i;
                }
                shaderSettings.usePolygonMap = settings.usePolygonMap;
                if (shaderSettings.usePolygonMap) {
                    if (shaderSettings.polyMap == null) {
                        shaderSettings.polyMap = new Texture2D(PolyMapResolution, 
                                PolyMapResolution, TextureFormat.ARGB32, false);
                        shaderSettings.polyMap.filterMode = FilterMode.Point;
                        shaderSettings.polyMap.wrapMode = TextureWrapMode.Clamp;
                    }
                    if (settings.polyMapNeedsRegen) {
                        PolyMap.GeneratePolyMap(settings.polyVertices, shaderSettings.polyMap);
                        settings.polyMapNeedsRegen = false;
                    }
                }
            } else if (settings.shapeType == ShapeType.Triangle) {
                // triangle specific properties
                shaderSettings.triangleOffset = settings.triangleOffset;
            } else if (settings.shapeType == ShapeType.Path) {
                // path specific properties
                shaderSettings.pathThickness = settings.pathThickness / 2;
                shaderSettings.numPathSegments = settings.pathSegments.Length;
                if (shaderSettings.pathPoints == null || shaderSettings.pathPoints.Length != shaderSettings.numPathSegments * 3)
                    shaderSettings.pathPoints = new Vector4[shaderSettings.numPathSegments * 3];
                for (int i = 0; i < shaderSettings.numPathSegments; i++) {
                    PathSegment segment = settings.pathSegments[i];
                    Vector2 p0 = new Vector2(segment.p0.x * shaderSettings.xScale, segment.p0.y * shaderSettings.yScale);
                    Vector2 p1 = new Vector2(segment.p1.x * shaderSettings.xScale, segment.p1.y * shaderSettings.yScale);
                    Vector2 p2 = new Vector2(segment.p2.x * shaderSettings.xScale, segment.p2.y * shaderSettings.yScale);
                    // the shader bugs out with divide-by-zero when p1 is the exact vertical midpoint (or near it)
                    if (Mathf.Abs((p2.y - p1.y) - (p1.y - p0.y)) < 0.001)
                        p1 = Vector2.Lerp(p0, p2, 0.45f) + new Vector2(0, 0.001f); 
                    float segmentIsInLoop = SegmentIsInLoop(i) ? 1 : 0;
                    shaderSettings.pathPoints[i * 3] = new Vector4(p0.x, p0.y, segmentIsInLoop, 0);
                    shaderSettings.pathPoints[i * 3 + 1] = new Vector4(p1.x, p1.y, segmentIsInLoop, 0);
                    shaderSettings.pathPoints[i * 3 + 2] = new Vector4(p2.x, p2.y, segmentIsInLoop, 0);
                }
                shaderSettings.fillPathLoops = settings.fillPathLoops;
                shaderSettings.pathThickness = Mathf.Max(shaderSettings.pathThickness, shaderSettings.outlineSize / 2);
            }
        }

        int FindPathLoop(Vector2 current, Vector2 seek, int skipSegment, int count, List<Vector2> visited, int iterations) {
            if (iterations > settings.pathSegments.Length * settings.pathSegments.Length) {
                // not 100% sure this won't recurse forever in some situations, so this is a failsafe
                throw new System.InvalidOperationException("Too many iterations in Shapes2D::FindPathLoop! There must be a bug in the algorithm.");
            }
            visited.Add(current);
            for (int i = 0; i < settings.pathSegments.Length; i++) {
                if (i == skipSegment)
                    continue;
                PathSegment seg = settings.pathSegments[i];
                if ((Vector2) seg.p0 == current && !visited.Contains(seg.p2)) {
                    if ((Vector2) seg.p2 == seek)
                        count ++;
                    else
                        count = FindPathLoop(seg.p2, seek, skipSegment, count, visited, iterations + 1);
                }
                if ((Vector2) seg.p2 == current && !visited.Contains(seg.p0)) {
                    if ((Vector2) seg.p0 == seek)
                        count ++;
                    else
                        count = FindPathLoop(seg.p0, seek, skipSegment, count, visited, iterations + 1);
                }
            }
            visited.RemoveAt(visited.Count - 1);
            return count;
        }

        bool SegmentIsInLoop(int segmentIndex) {
            PathSegment segment = settings.pathSegments[segmentIndex];
            List<Vector2> visited = new List<Vector2>();
            if (FindPathLoop(segment.p0, segment.p2, segmentIndex, 0, visited, 0) != 1)
                return false;
            visited.Clear();
            if (FindPathLoop(segment.p2, segment.p0, segmentIndex, 0, visited, 0) != 1)
                return false;
            return true;
        }

        bool SegmentIsOpen(int i) {
            PathSegment seg = settings.pathSegments[i];
            bool foundP0 = false, foundP2 = false;
            for (int c = 0; c < settings.pathSegments.Length; c++) {
                if (c == i)
                    continue;
                if (settings.pathSegments[c].p0 == seg.p0 || settings.pathSegments[c].p2 == seg.p0)
                    foundP0 = true; 
                if (settings.pathSegments[c].p0 == seg.p2 || settings.pathSegments[c].p2 == seg.p2)
                    foundP2 = true; 
            }
            return !(foundP0 && foundP2);
        }

        // called by the CanvasRenderer if we are attached to a UI object.  return
        // our temporary Material that should have been created in Configure() so
        // it renders with that instead of its original Material.
        Material IMaterialModifier.GetModifiedMaterial(Material oldMaterial) {
            if (!enabled || material == null)
                return oldMaterial;

            Mask mask = GetComponent<Mask>();
            if (mask != null) {
                int i = 0;
                Component[] components = GetComponents<Component>();
                foreach (Component c in components) {
                    if (c == mask)
                        break;
                    i ++;
                }
                int i2 = 0;
                foreach (Component c in components) {
                    if (c == this)
                        break;
                    i2 ++;
                }
                if (i2 < i)
                    Debug.LogWarning("Warning (Object " + name + ") - If a Mask and a Shapes2D Shape are on the same GameObject, the Shape needs to be further down in the component order!  You can move components up and down using the gear icon in the Inspector."); 
            }

            if (oldMaterial != null && UIShapeIsMasked(this, true)) {
                material.SetFloat("_Stencil", oldMaterial.GetFloat("_Stencil"));
                material.SetFloat("_StencilOp", oldMaterial.GetFloat("_StencilOp"));
                material.SetFloat("_StencilComp", oldMaterial.GetFloat("_StencilComp"));
                material.SetFloat("_StencilReadMask", oldMaterial.GetFloat("_StencilReadMask"));
                material.SetFloat("_StencilWriteMask", oldMaterial.GetFloat("_StencilWriteMask"));
                material.SetFloat("_ColorMask", oldMaterial.GetFloat("_ColorMask"));
            } else {
                material.SetFloat("_Stencil", 0);
                material.SetFloat("_StencilOp", 0);
                material.SetFloat("_StencilComp", 8);
                material.SetFloat("_StencilReadMask", 255);
                material.SetFloat("_StencilWriteMask", 255);
                material.SetFloat("_ColorMask", 15);
            }
            RectMask2D r2DMask = GetComponentInParent<RectMask2D>();
            if (r2DMask != null && r2DMask.enabled) {
                material.SetInt("_UseClipRect", 1);
            } else {
                material.SetInt("_UseClipRect", 0);
            }
            return material;
        }
        
        /// <summary>
        /// Used internally by Shape and ShapeEditor.
        /// Computes and sends the Shape's attributes to the shader.
        /// Normally you don't need to call this (even when changing settings on the fly), but you may need to call this manually to force an update if you change settings from Start() to avoid a flicker on the first frame.
        /// </summary>
        public void ComputeAndApply(bool disableBlending = false) {
            // put the user's settings into shaderSettings, tweaking them as 
            // necessary
            ComputeShaderProperties();

            // apply the shaderSettings depending on how our object is configured
            if (material)
                ApplyShaderPropertiesToMaterial(material, disableBlending);

            // save the current scale so we'll know to re-compute if it changes
            computedScale = transform.lossyScale;
            if (IsUIComponent())
                computedRect = rectTransform.rect;
            settings.dirty = false;
        }

        // this happens when properties change in the scene view, including from undo/redo 
        // (which normally we wouldn't detect as a change because Unity bypasses our property 
        // setters).  we can also check values from the inspector are legit here.
        void OnValidate() {
            if (!IsConfigured())
                return;
            // convert old version polygons to new ones
            if (settings._obsoleteNumSides != -1) {
                settings.polygonPreset = ((PolygonPreset) ((int) settings._obsoleteNumSides - 2));
                settings._obsoleteNumSides = -1;
            }
            // if the preset has been set in the inspector, it won't have used the property setter
            // so we can just set it here using the property to actually apply the preset
            settings.polygonPreset = settings.polygonPreset;
            settings.usePolygonMap = settings.usePolygonMap;
            // sanitize roundness
            if (settings.roundness < 0)
                settings.roundness = 0;
            if (settings.roundnessTopLeft < 0)
                settings.roundnessTopLeft = 0;
            if (settings.roundnessTopRight < 0)
                settings.roundnessTopRight = 0;
            if (settings.roundnessBottomLeft < 0)
                settings.roundnessBottomLeft = 0;
            if (settings.roundnessBottomRight < 0)
                settings.roundnessBottomRight = 0;
            // for sprite rectangles, roundness is a percentage rather than 
            // a value in world units
            if (!IsUIComponent()) {
                if (settings.roundness > 1)
                    settings.roundness = 1;
                if (settings.roundnessTopLeft > 1)
                    settings.roundnessTopLeft = 1;
                if (settings.roundnessTopRight > 1)
                    settings.roundnessTopRight = 1;
                if (settings.roundnessBottomLeft > 1)
                    settings.roundnessBottomLeft = 1;
                if (settings.roundnessBottomRight > 1)
                    settings.roundnessBottomRight = 1;
            }
            // sanitize some things
            settings.blur = settings.blur;
            settings.outlineSize = settings.outlineSize;
            settings.lineSize = settings.lineSize;
            settings.gridSize = settings.gridSize;
            settings.innerCutout = settings.innerCutout;
            settings.pathThickness = settings.pathThickness;

            #if UNITY_EDITOR
                CheckForShaderReset();
            #endif
        }
        
        void Update() {
            // check if the scale has changed - if so we need to send the new scale values to the shader
            if (transform.lossyScale != computedScale 
                    || (IsUIComponent() && rectTransform.rect != computedRect))
                settings.dirty = true;
            if (!enabled || !gameObject.activeInHierarchy)
                return;
            if (!settings.dirty)
                return;
            ComputeAndApply();
        }
        
        /// <summary>
        /// Used internally by Shape and ShapeEditor.
        /// Returns true if the Shape is a UI component; i.e. it uses an Image and has a Canvas somewhere in the parent hierarchy.
        /// </summary>
        public bool IsUIComponent() {
            return rectTransform != null;
        }

        Vector2 BiLerp(Vector3[] corners, Vector2 normalized) {
            Vector2 p1 = Vector2.LerpUnclamped(corners[0], corners[3], normalized.x + 0.5f);
            Vector2 p2 = Vector2.LerpUnclamped(corners[1], corners[2], normalized.x + 0.5f);
            return Vector2.LerpUnclamped(p1, p2, normalized.y + 0.5f);
        }

        // given the world corners of a rect and a world point, gives you the
        // point normalized in the rect from -0.5 to 0.5 on x and y.  does not
        // clamp it to those ranges though.
        Vector2 NormalizePointInRect(Vector3[] corners, Vector3 point) {
            point -= corners[0];
            Vector2 x = corners[3] - corners[0];
            Vector2 y = corners[1] - corners[0];
            Vector2 v1 = Vector3.Project(point, x);
            Vector2 v2 = Vector3.Project(point, y);
            Vector2 v3 = new Vector2(v1.magnitude / x.magnitude,// * Mathf.Sign(v1.x), 
                    v2.magnitude / y.magnitude);// * Mathf.Sign(v2.y));
            if (Mathf.Sign(v1.x) != Mathf.Sign(x.x))
                v3.x *= -1;
            if (Mathf.Sign(v2.y) != Mathf.Sign(y.y))
                v3.y *= -1;
            return v3 - new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// Used internally by Shape and ShapeEditor.
        /// Returns whether a world point is inside the shape's bounds.
        /// </summary>
        public bool PointIsWithinShapeBounds(Vector3 point) {
            Vector3[] corners = new Vector3[5];
            GetWorldCorners(corners);
            Vector2 p = NormalizePointInRect(corners, point);
            return p.x >= -0.5 && p.x <= 0.5 && p.y >= -0.5 && p.y <= 0.5;
        }
        
        /// <summary>
        /// If shapeType is Polygon, gets the polygon's vertices in world space.
        /// You may want to look at settings.polyVertices instead.
        /// </summary>
        public Vector3[] GetPolygonWorldVertices() {
            // get the world corners and interpolate to find the world coords.
            // we can't use the transform because RectTransform transforms 
            // based on the pivot and anchors rather than the center of the
            // actual image.
            Vector3[] corners = new Vector3[5];
            GetWorldCorners(corners);
            Vector3[] verts3D = new Vector3[settings.polyVertices.Length];
            for (int i = 0; i < settings.polyVertices.Length; i++) {
                Vector2 v2 = settings.polyVertices[i];
                verts3D[i] = BiLerp(corners, v2);
            }
            return verts3D;
        }

        /// <summary>
        /// If shapeType is Polygon, this sets its vertices from a list of world vertices (the verts are flattened to 2D after transformation).  
        /// Polygons must have between 3 and MaxPolygonVertices (currently 64) vertices for performance reasons.
        /// Vertices will be connected in the order given to make lines.
        /// This is used by ShapeEditor when editing the polygon in the scene view.
        /// </summary>
        public void SetPolygonWorldVertices(Vector3[] verts3D) {
            Vector3[] corners = new Vector3[5];
            GetWorldCorners(corners);
            Vector2[] verts = new Vector2[verts3D.Length];
            for (int i = 0; i < verts3D.Length; i++)
                verts[i] = NormalizePointInRect(corners, verts3D[i]);
            settings.polyVertices = verts;
        }

        /// <summary>
        /// If shapeType is Path, gets the path's segments in world space.
        /// You may want to look at settings.pathSegments instead.
        /// </summary>
        public PathSegment[] GetPathWorldSegments() {
            // get the world corners and interpolate to find the world coords.
            // we can't use the transform because RectTransform transforms 
            // based on the pivot and anchors rather than the center of the
            // actual image.
            Vector3[] corners = new Vector3[5];
            GetWorldCorners(corners);
            PathSegment[] worldSegments = new PathSegment[settings.pathSegments.Length];
            for (int i = 0; i < settings.pathSegments.Length; i++) {
                PathSegment segment = settings.pathSegments[i];
                PathSegment worldSegment = new PathSegment(BiLerp(corners, segment.p0), 
                        BiLerp(corners, segment.p1), BiLerp(corners, segment.p2));
                worldSegments[i] = worldSegment;
            }
            return worldSegments;
        }

        /// <summary>
        /// If shapeType is Path, this sets its segments from a list of world segments (the points are flattened to 2D after transformation).  
        /// This is used by ShapeEditor when editing the path in the scene view.
        /// </summary>
        public void SetPathWorldSegments(PathSegment[] worldSegments) {
            Vector3[] corners = new Vector3[5];
            GetWorldCorners(corners);
            PathSegment[] segments = new PathSegment[worldSegments.Length];
            for (int i = 0; i < worldSegments.Length; i++) {
                PathSegment worldSegment = worldSegments[i];
                Vector2 p0 = NormalizePointInRect(corners, worldSegment.p0);
                Vector2 p1 = NormalizePointInRect(corners, worldSegment.p1);
                Vector2 p2 = NormalizePointInRect(corners, worldSegment.p2);
                segments[i] = new PathSegment(p0, p1, p2);
            }
            settings.pathSegments = segments;
        }

        void CheckForShaderReset() {
            // in the editor, all our shader properties get reset after the user saves the scene.  
            // that's because they aren't declared to Unity in the shader so it doesn't know to
            // re-apply them after the save.  we could declare them, except I don't think there's
            // a way to declare a float array like with our polygon vertices (at least before 
            // Unity 5.4).  so instead we can just check if a property exists, and if it doesn't
            // re-apply them all.  this doesn't apply during runtime.
            if (!IsConfigured())
                return;
            if (!material.HasProperty("_XScale"))
                ApplyShaderPropertiesToMaterial(material);
        }

        void OnWillRenderObject() {
            #if UNITY_EDITOR
                CheckForShaderReset();
            #endif
        }


        
        /*****************************************************************************
        /* The remaining methods are used internally by ShapeEditor.
        /*****************************************************************************/
        
        bool UIShapeIsMasked(Shape shape, bool includeSelf) {
            Mask[] masks = GetComponentsInParent<Mask>();
            foreach (Mask mask in masks) {
                if (mask.enabled && (includeSelf || mask.gameObject != shape.gameObject))
                    return true;
            }
            return false;
        }

        bool UIShapeIsRectMasked(Shape shape, bool includeSelf) {
            RectMask2D[] masks = GetComponentsInParent<RectMask2D>();
            foreach (RectMask2D mask in masks) {
                if (mask.enabled && (includeSelf || mask.gameObject != shape.gameObject))
                    return true;
            }
            return false;
        }

        Vector2 GetWorldCenter() {
            if (rectTransform) {
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                Vector3 bottomLeft = corners[0], topRight = corners[2];
                return (topRight - bottomLeft) / 2 + bottomLeft;
            } else {
                return transform.position;
            }
        }

        /// <summary>
        /// Used internally by ShapeEditor during sprite conversion.
        /// Gets a Bounds encompassing this Shape and all its child Shapes in world space.
        /// Note that for UI components this will be the world space of the object itself in Unity's editor, and not the world space it will take up when rendered on the camera.
        /// </summary>
        public Bounds GetShapeBounds() {
            Bounds bounds = new Bounds();
            bounds.center = GetWorldCenter();
            foreach (Shape shape in GetComponentsInChildren<Shape>()) {
                if (!shape.enabled)
                    continue;
                if (shape.rectTransform) {
                    if (shape != this && UIShapeIsMasked(shape, false))
                        continue;
                    if (shape != this && UIShapeIsRectMasked(shape, false))
                        continue;
                    Vector3[] corners = new Vector3[4];
                    shape.rectTransform.GetWorldCorners(corners);
                    for (int i = 0; i < 4; i++)
                        bounds.Encapsulate(corners[i]);
                } else {
                    Bounds childBounds = shape.spriteRenderer.bounds;
                    bounds.Encapsulate(childBounds.min);
                    bounds.Encapsulate(childBounds.max);
                    // bounds.Encapsulate(shape.transform.position + shape.transform.lossyScale / 2);
                    // bounds.Encapsulate(shape.transform.position - shape.transform.lossyScale / 2);
                }
            }
            return bounds;
        }
        
        /// <summary>
        /// Used internally by ShapeEditor during sprite conversion.
        /// Returns the size the Shape should be in pixels if rendered out to a sprite.
        /// Uses 100 pixels per world unit (configurable in Shapes2D/Preferences) for Shapes in world space, and world units / scaleFactor for UI components not in world space.
        /// </summary>
        public Vector2 GetShapePixelSize(float pixelsPerUnit = 100) {
            Bounds bounds = GetShapeBounds();
            if (IsUIComponent()) {
                int w, h;
                if (image.canvas.renderMode == RenderMode.WorldSpace) {
                    w = Mathf.Max(1, Mathf.RoundToInt(bounds.size.x * pixelsPerUnit));
                    h = Mathf.Max(1, Mathf.RoundToInt(bounds.size.y * pixelsPerUnit));
                } else {
                    // canvas is not in world mode, so when we call GetWorldCorners() we'll get
                    // screen space instead, which we can assume is just pixels.
                    // if the screen space has been stretched by a CanvasScaler, the values
                    // will be dependent on the current game window size which we don't want,
                    // so check the scaleFactor.
                    var scaleFactor = image.canvas.scaleFactor;
                    if (image.canvas.renderMode == RenderMode.ScreenSpaceCamera)
                        scaleFactor = image.canvas.transform.lossyScale.x;
                    w = Mathf.Max(1, Mathf.RoundToInt(bounds.size.x / scaleFactor));
                    h = Mathf.Max(1, Mathf.RoundToInt(bounds.size.y / scaleFactor));
                }
                return new Vector2(w, h);
            } else {
                int w = Mathf.Max(1, Mathf.RoundToInt(bounds.size.x * pixelsPerUnit));
                int h = Mathf.Max(1, Mathf.RoundToInt(bounds.size.y * pixelsPerUnit));
                return new Vector2(w, h);
            } 
        }

        /// <summary>
        /// Used internally by ShapeEditor during sprite conversion.
        /// Returns a List of this Shape and all child Shapes in draw order.
        /// </summary>
        public List<Shape> GetShapesInDrawOrder() {
            List<Shape> shapes = new List<Shape>();
            shapes.AddRange(GetComponentsInChildren<Shape>());
            shapes.RemoveAll(s => !s.enabled);
            shapes.Sort(new ShapeDrawOrderComparer());
            return shapes;
        }
        
        /// <summary>
        /// Used internally by ShapeEditor during sprite conversion.
        /// Uses Camera cam to render the Shape out to a new Texture2D of dimensions w,h.
        /// Requires some setup - not for general use.
        /// </summary>
        public Texture2D DrawToTexture2D(Camera cam, int w, int h) {
            int oldLayer = gameObject.layer;
            // the camera's culling mask should have been set to layer 31 only 
            gameObject.layer = 31;
            // disable blending so semi-transparent pixels don't blend with the RenderTexture's
            // background color (this happens even if the background color has alpha 0)
            ComputeAndApply(true);
            // render to the camera's target RenderTexture
            cam.Render();
            // restore the layer
            gameObject.layer = oldLayer;
            // restore blending
            ComputeAndApply(false);
            Texture2D tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Point;
            // the camera's RenderTexture should already have been set as active, so this
            // will read from its buffer into the Texture2D 
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            return tex;
        }
        
        /// <summary>
        /// Used internally by ShapeEditor during sprite conversion.
        /// Gets a list of Objects that need to be in the Undo stack during sprite conversion.
        /// </summary>
        public List<Object> GetUndoObjects() {
            List<Object> undoObjects = new List<Object>();
            undoObjects.Add(gameObject);
            undoObjects.Add(this);
            // do GetComponent() calls here instead of relying on our existing
            // relationships in case they aren't set up yet.
            if (GetComponent<Image>())
                undoObjects.Add(GetComponent<Image>());
            if (GetComponent<SpriteRenderer>())
                undoObjects.Add(GetComponent<SpriteRenderer>());
            if (GetComponent<Mask>())
                undoObjects.Add(GetComponent<Mask>());
            if (material)
                undoObjects.Add(material);
            undoObjects.Add(transform);
            if (GetComponent<RectTransform>())
                undoObjects.Add(GetComponent<RectTransform>());
            foreach (Shape s in GetComponentsInChildren<Shape>())
                undoObjects.Add(s.gameObject);
            return undoObjects;
        }
        
        /// <summary>
        /// Used internally by ShapeEditor during sprite conversion.
        /// Changes this Shape to use its SpriteRenderer or Image component and disables the Shape component.
        /// Also makes any child Shapes inactive and sets localScale to 1 (for non-UI Shapes).
        /// </summary>
        public void SetAsSprite(Sprite sprite, Material spriteDefaultMaterial) {
            if (spriteRenderer) {
                spriteRenderer.sprite = sprite;
                spriteRenderer.material = null;
                spriteRenderer.sharedMaterial = spriteDefaultMaterial;
            }
            if (image) {
                image.sprite = sprite;
                image.material = null;
            }
            if (GetComponent<Mask>() && GetComponent<Mask>().enabled) {
                GetComponent<Mask>().showMaskGraphic = true;
            }

            // disable this shape and make any child shapes inactive
            enabled = false;
            foreach (Shape s in GetComponentsInChildren<Shape>()) {
                if (s == this || !s.enabled)
                    continue;
                s.gameObject.SetActive(false);
            }

            // save the scale so we can convert back later even if it's too late
            // to use the undo system
            wasConverted = true;
            preConvertedScale = transform.localScale;
            
            // change the scale to 1 globally (if it's a UI component the scale is harder
            // to get right so the user may have to re-scale manually)
            if (!rectTransform) {
                Transform parent = transform.parent;
                transform.SetParent(null);
                transform.localScale = new Vector3(1, 1, 1);
                transform.SetParent(parent);
            }
            
            if (material) {
                DestroyImmediate(material);
                material = null;
            }
        }
        
        /// <summary>
        /// Used internally by ShapeEditor during sprite conversion.
        /// Returns the pivot point that the converted sprite should have when this Shape and its child Shapes are compressed into a single image.
        /// </summary>
        public Vector2 GetPivot() {
            Vector3 center = GetWorldCenter();
            Bounds bounds = GetShapeBounds();
            center = center - bounds.min;
            return new Vector2(center.x / bounds.size.x, center.y / bounds.size.y);
        }

        /// <summary>
        /// Used internally.
        /// Returns the shape's world corners, not including any children, as 5 Vector3s (bottom left,
        /// top left, top right, bottom right, and bottom left again).
        /// </summary>
        public Vector3[] GetWorldCorners(Vector3[] corners) {
            if (IsUIComponent()) {
                rectTransform.GetWorldCorners(corners);
            } else {
                Vector2 scale = GetScale();
                corners[0] = transform.TransformPoint(new Vector3(-0.5f * scale.x, -0.5f * scale.y, 0));
                corners[1] = transform.TransformPoint(new Vector3(-0.5f * scale.x, 0.5f * scale.y, 0));
                corners[2] = transform.TransformPoint(new Vector3(0.5f * scale.x, 0.5f * scale.y, 0));
                corners[3] = transform.TransformPoint(new Vector3(0.5f * scale.x, -0.5f * scale.y, 0));
            }
            corners[4] = corners[0];
            return corners;
        }
 
        /// <summary>
        /// Used internally by ShapeEditor during sprite conversion.
        /// If this Shape has been converted into a sprite, this applies the scale it had previously.
        /// </summary>
        public void RestoreFromConversion() {
            if (!rectTransform) {
                transform.localScale = preConvertedScale;
            }
            wasConverted = false;
        }
        
        /// <summary>
        /// Used internally by ShapeEditor during sprite conversion.
        /// Returns true if the Shape has been configured correctly and is using a Shapes2D Material.
        /// </summary>
        public bool IsConfigured() {
            return material != null;
        }
    }
}