#if POLYGON_8
    #define MAX_VERTS 8
#elif POLYGON_16
    #define MAX_VERTS 16
#elif POLYGON_24
    #define MAX_VERTS 24
#elif POLYGON_32
    #define MAX_VERTS 32
#elif POLYGON_40
    #define MAX_VERTS 40
#elif POLYGON_48
    #define MAX_VERTS 48
#elif POLYGON_56
    #define MAX_VERTS 56
#elif POLYGON_64 || POLYGON_MAP
    #define MAX_VERTS 64
#endif

float4 _Verts[MAX_VERTS];
int _NumVerts; // the number of vertices in the polygon
sampler2D _PolyMap;

// this algorithm is slow and kind of silly to have in a shader, but it works!
// returns <0 if the point is outside the poly.
float get_closest_distance(float2 pos, float2 radii) {
    // simplifies the algo - hopefully this value is higher than any real value
    float closest_distance = 99999999;
    int nodes = 0; // used for testing if the point is inside the poly, see below
    // you can't do variable-length loops in webgl (or es2 technically I think), 
    // hence the constant...so we have to iterate through MAX_VERTS rather than 
    // _NumVerts which is the number of vertices actually in our poly
    for (int i = 0; i < MAX_VERTS; i++) {
        // loop_over is 1 when we're past the number of sides in the poly
        float loop_over = when_ge(i, _NumVerts);

        // each "vert" contains its edge partner in zw
        float4 side = _Verts[i];

        // todo - try and do angular line joins instead of curved?  for concave
        // line joins we get curves, but for convex we get angles. :(
        float dist = distance_to_line_segment(pos, side.xy, side.zw) + 99999999 * loop_over;
        closest_distance = min(dist, closest_distance);

        // thanks to http://alienryderflex.com/polygon/.  if the number of nodes (points
        // where a polygon line crosses the horizontal axis of the test position) to the
        // left of the test position is odd, then the point is inside the poly!
        // avoid conditionals - they tend to break on some platforms :/
        float intersects_y = or(and(when_lt(side.y, pos.y), when_ge(side.w, pos.y)),
                and(when_lt(side.w, pos.y), when_ge(side.y, pos.y)));
        float node_x = side.x + (pos.y - side.y) / (side.w - side.y) * (side.z - side.x);
        nodes += when_lt(node_x, pos.x) * intersects_y * (1 - loop_over);

        // I think conditionals based on uniforms are ok?  but trying to return
        // or break out of this loop early makes galaxy 5 phones hang at the
        // unity splash screen.  works fine on other platforms I've tested, but
        // I don't actually know if it's a speed boost anyway.
        // if (i == _NumVerts - 1)
        //     break;
    }
    return closest_distance * when_eq(nodes % 2, 1) + -1 * when_neq(nodes % 2, 1);
}

// tells you which side of the line p1->p2 pos is
int point_line_test(float2 pos, float2 p1, float2 p2) {
    return when_gt((pos.x - p1.x) * (p2.y - p1.y) - (pos.y - p1.y) * (p2.x - p1.x), 0);
}

fixed4 frag(v2f i) : SV_Target {
    float2 pos = prepare(i.uv, i.modelPos.z);

    #if !POLYGON_MAP
        // dist is < 0 when pos is outside the poly
        float dist = get_closest_distance(pos, float2(_XScale, _YScale) / 2);
        float is_inside = when_ge(dist, 0);

    #else
        // use the polygon map texture optimization.  the texture gives us the pre-computed
        // two closest sides to the center of the texel, and what operation to perform.
        fixed4 data = tex2D(_PolyMap, i.uv + 0.5);

        // the indices of the two closest lines are in the r & g components,
        // with 100 added to the index if the line test result is positive
        int index1 = (int) (data.r * 256.0);
        int index2 = (int) (data.g * 256.0);

        float4 l1 = _Verts[index1];
        float4 l2 = _Verts[index2];

        // check if the points are inside the lines
        int ptest1 = point_line_test(pos, l1.xy, l1.zw);
        int ptest2 = point_line_test(pos, l2.xy, l2.zw);

        // shortest distance to one of the lines
        float dist1 = distance_to_line_segment(pos, l1.xy, l1.zw);
        float dist2 = distance_to_line_segment(pos, l2.xy, l2.zw);
        float dist = min(dist1, dist2);

        // mode == 0, all pixels in the texel are outside the poly
        // mode == 1, all pixels in the texel are inside the poly
        // mode == 2, must be inside first line in texel
        // mode == 3, must be inside both lines
        // mode == 4, must be inside at least one of the lines
        // mode == 5, must be inside closest line in the texel 
        int mode = (int) (data.a * 256.0);
        float is_inside = when_eq(mode, 1) 
                + when_eq(mode, 2) * ptest1 
                + when_eq(mode, 3) * and(ptest1, ptest2)
                + when_eq(mode, 4) * or(ptest1, ptest2);
                // + when_eq(mode, 5) * 
                //     (when_lt(dist1, dist2) * ptest1 
                //     + when_gt(dist1, dist2) * ptest2 
                //     + when_eq(dist1, dist2) * and(ptest1, ptest2));
    #endif

    fixed4 color = color_from_distance(dist, fill(i.uv), _OutlineColor) * i.color;
    if (_PreMultiplyAlpha == 1)
        color.rgb *= color.a;

    if (_UseClipRect == 1)
        color.a *= UnityGet2DClipping(i.modelPos.xy, _ClipRect);
    clip(color.a - 0.001);
    
    return is_inside * color;
}