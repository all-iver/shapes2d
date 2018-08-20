float4 _Roundness; // tl, tr, bl, br

fixed4 frag(v2f i) : SV_Target {
    float2 pos = prepare(i.uv, i.modelPos.z);

    // get the roundness for this corner in world units, avoiding conditionals because 
    // they tend to break things on various platforms (gles3) and they are slow in 
    // shaders anyway
    float tl = and(when_le(pos.x, 0), when_ge(pos.y, 0)) * _Roundness.x;
    float tr = and(when_ge(pos.x, 0), when_ge(pos.y, 0)) * _Roundness.y;
    float bl = and(when_le(pos.x, 0), when_le(pos.y, 0)) * _Roundness.z;
    float br = and(when_ge(pos.x, 0), when_le(pos.y, 0)) * _Roundness.w;
    float roundness = tl + tr + bl + br;

    // radius of the circle at this corner, making sure not to go past halfway
    float radius = min(min(_XScale / 2, roundness), _YScale / 2);
    
    // handy distance to round rectangle formula
    float2 extents = float2(_XScale, _YScale) / 2 - radius;
    float2 delta = abs(pos) - extents;
    // first component is distance to closest side when not in a corner circle, second
    // is distance to the rounded part when in a corner circle
    float dist = radius - (min(max(delta.x, delta.y), 0) + length(max(delta, 0)));

    fixed4 color = color_from_distance(dist, fill(i.uv), _OutlineColor) * i.color;
    if (_PreMultiplyAlpha == 1)
        color.rgb *= color.a;

    if (_UseClipRect == 1)
        color.a *= UnityGet2DClipping(i.modelPos.xy, _ClipRect);
    clip(color.a - 0.001);
 
    return color;
}