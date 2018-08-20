float _TriangleOffset; // the position of the top point of the triangle

float distance_to_triangle(float2 pos, float top_offset, float2 size) {
    float2 p1 = float2(size.x / 2, -size.y / 2);
    float2 p2 = float2(top_offset * size.x - size.x / 2, size.y / 2);
    float2 p3 = float2(-size.x / 2, -size.y / 2);
    float c1 = cross2(p2 - p1, pos - p1);
    float c2 = cross2(p3 - p2, pos - p2);
    float is_inside = and(when_ge(c1, 0), when_ge(c2, 0));
    float dist1 = distance_to_line(pos, p1, p2);
    float dist2 = distance_to_line(pos, p3, p2);
    float dist3 = distance_to_line(pos, p1, p3);
    return (1 - is_inside) * -1 + is_inside * min(min(dist1, dist2), dist3);
}

fixed4 frag(v2f i) : SV_Target {
    float2 pos = prepare(i.uv, i.modelPos.z);

    float dist = distance_to_triangle(pos, _TriangleOffset, float2(_XScale, _YScale));
    float is_outside = when_le(dist, 0);
        
    fixed4 color = color_from_distance(dist, fill(i.uv), _OutlineColor) * i.color;                
    if (_PreMultiplyAlpha == 1)
        color.rgb *= color.a;

    if (_UseClipRect == 1)
        color.a *= UnityGet2DClipping(i.modelPos.xy, _ClipRect);
    clip(color.a - 0.001);

    return (1 - is_outside) * color + is_outside * fixed4(0, 0, 0, 0);
}