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

        /// <summary>
        /// Returns the point on the segment at t, where t is from 0-1.
        /// </summary>
        public Vector2 GetPoint(float t) {
            t = Mathf.Clamp01(t);
            // https://stackoverflow.com/questions/5634460/quadratic-bézier-curve-calculate-points :D
            return new Vector2(
                (1 - t) * (1 - t) * p0.x + 2 * (1 - t) * t * p1.x + t * t * p2.x,
                (1 - t) * (1 - t) * p0.y + 2 * (1 - t) * t * p1.y + t * t * p2.y
            );
        }

        /// <summary>
        /// Returns the approximate length of this segment by dividing it up and summing the lengths of each part.
        /// </summary>
        public float GetLength(int subdivisions = 10) { 
            if (subdivisions <= 0)
                throw new System.Exception("Must have at least 1 subdivision in GetLength().");
            float length = 0;
            Vector3 last = p0;
            float step = 1f / subdivisions;
            for (int i = 0; i < subdivisions; i++) {
                Vector3 p = GetPoint(i * step + step);
                length += Vector3.Distance(last, p);
                last = p;
            }
            return length;
        }
    }

}