namespace Shapes2D {

    using UnityEngine;

    class PolyMap {
        struct DistanceResult {
            public int index, index2;
        }

        // http://stackoverflow.com/questions/849211/shortest-distance-between-a-point-and-a-line-segment
        static float DistanceToLineSegment(Vector2 pos, Vector2 p1, Vector2 p2) {
            // vector operations are pretty slow!  do it all manually...
            float dx = p2.x - p1.x;
            float dy = p2.y - p1.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float l2 = dist * dist;
            float c1x = pos.x - p1.x;
            float c1y = pos.y - p1.y;
            float c2x = p2.x - p1.x;
            float c2y = p2.y - p1.y;
            float dot = c1x * c2x + c1y * c2y;
            dot /= l2;
            float t = dot < 0 ? 0 : dot;
            t = t > 1 ? 1 : t;
            float projx = p1.x + t * dx;
            float projy = p1.y + t * dy;
            dx = projx - pos.x;
            dy = projy - pos.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static DistanceResult GetClosestLineSegments(Vector2 pos, Vector2[] vertices) {
            DistanceResult dr;
            dr.index = -1;
            dr.index2 = -1;
            float closestDistance = -1;
            float closestDistance2 = -1;
            int j = vertices.Length - 1;
            for (int i = 0; i < vertices.Length; i++) {
                float dist = DistanceToLineSegment(pos, vertices[i], vertices[j]);
                if (dr.index == -1 || dist < closestDistance) {
                    dr.index2 = dr.index;
                    closestDistance2 = closestDistance;
                    dr.index = i;
                    closestDistance = dist;
                } else if (dr.index2 == -1 || dist < closestDistance2) {
                    dr.index2 = i;
                    closestDistance2 = dist;
                }
                j = i;
            }
            return dr;
        }

        // thanks to http://alienryderflex.com/polygon/.  if the number of nodes (points
        // where a polygon line crosses the horizontal axis of the test position) to the
        // left of the test position is odd, then the point is inside the poly!
        static bool PointIsInsidePoly(Vector2 pos, Vector2[] vertices) {
            bool oddNodes = false;
            int j = vertices.Length - 1;
            for (int i = 0; i < vertices.Length; i++) {
                Vector2 vi = vertices[i];
                Vector2 vj = vertices[j];
                if ((vi.y < pos.y && vj.y >= pos.y || vj.y < pos.y && vi.y >= pos.y)
                        && (vi.x <= pos.x || vj.x <= pos.x)) {
                    if (vi.x + (pos.y - vi.y) / (vj.y - vi.y) * (vj.x - vi.x) < pos.x)
                        oddNodes = !oddNodes; 
                }
                j = i;
            }
            return oddNodes;
        }

        static bool IntersectsVerticalLineSegment(Vector2 v1, Vector2 v2, Vector2 p1, Vector2 p2) {
            float pminx = p1.x < p2.x ? p1.x : p2.x;
            float pminy = p1.y < p2.y ? p1.y : p2.y;
            // float vminx = v1.x < v2.x ? v1.x : v2.x;
            float vminy = v1.y < v2.y ? v1.y : v2.y;
            float pmaxx = p1.x > p2.x ? p1.x : p2.x;
            float pmaxy = p1.y > p2.y ? p1.y : p2.y;
            // float vmaxx = v1.x > v2.x ? v1.x : v2.x;
            float vmaxy = v1.y > v2.y ? v1.y : v2.y;
            if (pminx > v1.x || pmaxx < v1.x || pmaxy < vminy || pminy > vmaxy)
                return false;
            if (p2.x == p1.x)
                return p1.x == v1.x && ((pminy >= vminy && pminy <= vmaxy) || (pmaxy >= vminy && pmaxy <= vmaxy));
            float m = (p2.y - p1.y) / (p2.x - p1.x);
            float b = p2.y - m * p2.x;
            float y = m * v1.x + b;
            return y >= vminy && y <= vmaxy;
        }

        static bool IntersectsHorizontalLineSegment(Vector2 v1, Vector2 v2, Vector2 p1, Vector2 p2) {
            float pminx = p1.x < p2.x ? p1.x : p2.x;
            float pminy = p1.y < p2.y ? p1.y : p2.y;
            float vminx = v1.x < v2.x ? v1.x : v2.x;
            // float vminy = v1.y < v2.y ? v1.y : v2.y;
            float pmaxx = p1.x > p2.x ? p1.x : p2.x;
            float pmaxy = p1.y > p2.y ? p1.y : p2.y;
            float vmaxx = v1.x > v2.x ? v1.x : v2.x;
            // float vmaxy = v1.y > v2.y ? v1.y : v2.y;
            if (pminx > vmaxx || pmaxx < vminx || pmaxy < v1.y || pminy > v1.y)
                return false;
            if (p2.x == p1.x)
                return p1.x >= vminx && p2.x <= vmaxx;   
            float m = (p2.y - p1.y) / (p2.x - p1.x);
            float b = p2.y - m * p2.x;
            float x = (v1.y - b) / m;
            return x >= vminx && x <= vmaxx;
        }

        static int RectIntersectsPolyEdge(Vector2 tl, Vector2 tr, Vector2 bl, Vector2 br,
                Vector2[] vertices, bool[] results) {
            int edgeCount = 0;
            int j = vertices.Length - 1;
            for (int i = 0; i < vertices.Length; i++) {
                Vector2 p1 = vertices[i];
                Vector2 p2 = vertices[j];
                bool top = IntersectsHorizontalLineSegment(tl, tr, p1, p2);
                bool bottom = IntersectsHorizontalLineSegment(bl, br, p1, p2);
                bool left = IntersectsVerticalLineSegment(tl, bl, p1, p2);
                bool right = IntersectsVerticalLineSegment(tr, br, p1, p2);
                results[i] = top || bottom || left || right;
                if (top || bottom || left || right)
                    edgeCount ++;
                j = i;
            }
            return edgeCount;
        }

        static int RectContainsVertex(Vector2 tl, Vector2 tr, Vector2 bl, Vector2 br,
                Vector2[] vertices, bool[] results) {
            int vertCount = 0;
            int i = 0;
            foreach (Vector2 v in vertices) {
                results[i] = v.x >= tl.x && v.x <= tr.x && v.y >= bl.y && v.y <= tl.y;
                if (results[i])
                    vertCount ++;
                i ++;
            }
            return vertCount;
        }

        // which side of the line is the point on?  consistent winding means this tells us 
        // if a point is on the inside of a convex polygon or not (or a convex section of a
        // concave poly which is what we care about), assuming it isn't excluded by any of 
        // the other lines.
        static bool PointLineTest(Vector2 pos, Vector2 p1, Vector2 p2) {
            return (pos.x - p1.x) * (p2.y - p1.y) - (pos.y - p1.y) * (p2.x - p1.x) > 0;
        }

        // maybe could use Vector2.Angle() instead?
        static float GetAngleBetween(Vector2 v1, Vector2 v2) {
            float a1 = Mathf.Atan2(v1.y, v1.x) * Mathf.Rad2Deg;
            float a2 = Mathf.Atan2(v2.y, v2.x) * Mathf.Rad2Deg;
            if (a1 < 0)
                a1 += 360;
            if (a2 < 0)
                a2 += 360;
            float result = a1 - a2;
            if (result < 0)
                result += 360;
            return result;
        }

        public static void GeneratePolyMap(Vector2[] vertices, Texture2D polyMap) {
            if (polyMap.width != polyMap.height)
                throw new System.InvalidOperationException("Poly map texture must be square!");
            int resolution = polyMap.width;
            Vector2 tex = new Vector2(1f / resolution, 1f / resolution);
            bool[] edgeIntersections = new bool[vertices.Length];
            bool[] vertIntersections = new bool[vertices.Length];
            // for each texel
            for (int x = 0; x < resolution; x++) {
                for (int y = 0; y < resolution; y++) {
                    Vector2 center = new Vector2(x, y) * tex.x + tex / 2 - new Vector2(0.5f, 0.5f);
                    Vector2 tl = center + new Vector2(-tex.x / 2, tex.y / 2);
                    Vector2 tr = center + tex / 2;
                    Vector2 bl = center - tex / 2;
                    Vector2 br = center + new Vector2(tex.x / 2, -tex.y / 2);
                    int edgeCount = RectIntersectsPolyEdge(tl, tr, bl, br, vertices, edgeIntersections);
                    bool inside = PointIsInsidePoly(center, vertices);
                    // get the closest line segments to the texel center.  it's much faster
                    // for the shader to just look at a few line segments than all of them, but 
                    // when the closest lines to the center of the texel aren't the closest for all 
                    // pixels in the texel there will be rendering flaws (though hopefully one of them 
                    // will be the closest one to each pixel in reasonably spaced polygons).
                    // note that in most cases we could do a simpler algorithm that just checks the
                    // closest of the two nearest lines in the fragment shader and colors pixels
                    // inside of the winner, but it gets fuzzy in many cases where fragments are
                    // exactly as far (or almost exactly as far) from each line.
                    DistanceResult dr = GetClosestLineSegments(center, vertices);
                    // we'll send a 'mode' to the shader indicating what it should do.
                    // 0 = all pixels in the texel are outside the poly
                    // 1 = all pixels in the texel are inside the poly
                    // 2 = must be inside the closest line
                    // 3 = must be inside both lines
                    // 4 = must be inside either line
                    int mode = 0;
                    if (edgeCount == 0) {
                        // no intersection, so all points are on the same side of the poly
                        mode = inside ? 1 : 0;
                    } else if (edgeCount == 1) {
                        // intersects with 1 edge, just be inside that edge.
                        // make sure dr.index is the intersecting edge - it's possible it isn't.
                        mode = 2;
                        if (!edgeIntersections[dr.index]) {
                            int temp = dr.index;
                            dr.index = dr.index2;
                            dr.index2 = temp;
                        }
                        // mode = 5;
                    } else if (RectContainsVertex(tl, tr, bl, br, vertices, vertIntersections) > 0) {
                        // the texel contains >= 1 vertex, so we need to get the angle between
                        // the vert's lines to figure out what to do.  multiple verts in a single texel 
                        // are not supported.
                        // find the relevant vertex
                        Vector2 v = Vector2.zero;
                        int i;
                        for (i = 0; i < vertices.Length; i++) {
                            if (vertIntersections[i]) {
                                v = vertices[i];
                                break;
                            }
                        }
                        // find the neighboring verts.  make sure their lines are the closest segments too.
                        // v1 -- v -- v2
                        dr.index = i;
                        int j = i == 0 ? vertices.Length - 1 : i - 1;
                        Vector2 v1 = vertices[j];
                        j = i == vertices.Length - 1 ? 0 : i + 1;
                        dr.index2 = j;
                        Vector2 v2 = vertices[j];
                        // find the angle between the two lines
                        float angle = GetAngleBetween(v1 - v, v2 - v);
                        // each pixel needs to be inside the joint
                        if (angle <= 180) {
                            mode = 3;
                        } else {
                            mode = 4;
                        }
                        // mode = 5;
                    } else {
                        // edge count is > 1 and no vertex intersection.
                        // we can tell if the lines are facing in the same direction by using the
                        // center as a reference and checking if it is on the same or different
                        // side of each line, and whether or not it's inside the poly.
                        int found = 0;
                        bool test1 = false, test2 = false;
                        for (int i = 0; i < vertices.Length; i++) { 
                            if (!edgeIntersections[i])
                                continue;
                            int j = i == 0 ? vertices.Length - 1 : i - 1;
                            if (found == 0)
                                test1 = PointLineTest(center, vertices[i], vertices[j]);
                            else
                                test2 = PointLineTest(center, vertices[i], vertices[j]);
                            found ++;
                            if (found == 2)
                                break;
                        }
                        if ((test1 == test2) && inside)
                            mode = 3;
                        else if ((test1 == test2) && !inside)
                            mode = 4;
                        else if ((test1 != test2) && inside)
                            mode = 4;
                        else if ((test1 != test2) && !inside)
                            mode = 3;
                        // mode = 5;
                    }
                    polyMap.SetPixel(x, y, new Color(dr.index / 256f, dr.index2 / 256f, 0, mode / 256f));
                }
            }
            polyMap.Apply();
        }
    }

}