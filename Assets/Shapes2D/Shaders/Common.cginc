#define PI 3.141592653
#define PI2 6.283185307

// ***** 
// begin http://theorangeduck.com/page/avoiding-shader-conditionals
// *****

float4 when_eq(float4 x, float4 y) {
  return 1.0 - abs(sign(x - y));
}

float4 when_neq(float4 x, float4 y) {
  return abs(sign(x - y));
}

float4 when_gt(float4 x, float4 y) {
  return max(sign(x - y), 0.0);
}

float4 when_lt(float4 x, float4 y) {
  return max(sign(y - x), 0.0);
}

float4 when_ge(float4 x, float4 y) {
  return 1.0 - when_lt(x, y);
}

float4 when_le(float4 x, float4 y) {
  return 1.0 - when_gt(x, y);
}

float4 and(float4 a, float4 b) {
  return a * b;
}

float4 or(float4 a, float4 b) {
  return min(a + b, 1.0);
}

float4 xor(float4 a, float4 b) {
  return (a + b) % 2.0;
}

float4 not(float4 a) {
  return 1.0 - a;
}

// *****
// end http://theorangeduck.com/page/avoiding-shader-conditionals
// *****

float2 prepare(float2 uv, float distanceFromCameraPlane) {
    float2 pos = float2(uv.x * _XScale, uv.y * _YScale);

    // figure out the size of a pixel in world units at the z depth of the 
    // shape, which is sort of the same as fwidth(pos) but works on more 
    // platforms.  won't look right in 3D unless the shape is billboarded,
    // but this is Shapes2D and not Shapes3D so we'll have to live with it.
    if (_PixelSize == 0) {
        if (unity_OrthoParams.w == 0) {
            // get the vertical fov, thanks http://answers.unity3d.com/questions/770838/how-can-i-extract-the-fov-information-from-the-pro.html
            float t = unity_CameraProjection._m11;
            float fov = atan(1.0 / t);
            // get the distance from the current point world position to the 
            // camera's plane, and then figure out the pixel size based on the 
            // fov at that distance.  probably not actually "correct" but it 
            // seems to work well regardless of fov or camera angle/position.
            float h = tan(fov) * distanceFromCameraPlane * 2;
            _PixelSize = h / _ScreenParams.y;
        } else {
            _PixelSize = (_ScreenParams.z - 1) * unity_OrthoParams.x * 2;
        }
    }

    // put a little resolution-independent AA on things if desired
    if (_Blur == 0) {
        // fwidth is not supported on all GLES platforms, so we are now using
        // the _PixelSize method which should be available everywhere (though
        // only when using an orthographic camera)
        // float2 aa = fwidth(pos);
        // _Blur = length(aa);
        _Blur = sqrt(_PixelSize * _PixelSize * 2);
        if (_OutlineSize > 0) {
            // subtract the AA blur from the outline so sizes stay mostly 
            // correct and outlines don't look too thick when scaled down.
            // don't clamp _OutlineSize to 0 or it will break comparisons in
            // other places (should fix that at some point).
            _OutlineSize -= _Blur;
        }
    }

    float min_dim = min(_XScale, _YScale) / 2;
    
    // blur on the outside of the shape in world coords
    _OuterBlur = max(min(_Blur, min_dim - _OutlineSize), 0);
    // blur on the inside of the outline in world coords
    _InnerBlur = max(min(_OuterBlur, min_dim - _OuterBlur - _OutlineSize), 0);
    
    // return the local position of the pixel in the quad centered at 0, 0 
    return pos;
}

// get the 2-dimensional cross product
float cross2(float2 p1, float2 p2) {
    return p1.x * p2.y - p1.y * p2.x;
}

// returns the distance from a point to a line described by two points
float distance_to_line(float2 pos, float2 p1, float2 p2) {
    return abs((p2.x - p1.x) * (p1.y - pos.y) - (p1.x - pos.x) * (p2.y - p1.y)) 
            / distance(p1, p2); 
}

// http://stackoverflow.com/questions/849211/shortest-distance-between-a-point-and-a-line-segment
float distance_to_line_segment(float2 pos, float2 p1, float2 p2) {
    float l2 = pow(distance(p1, p2), 2);
    float t = clamp(dot(pos - p1, p2 - p1) / l2, 0, 1);
    float2 projection = p1 + t * (p2 - p1);
    return distance(pos, projection);
}

// returns 1 on the ellipse and <1 inside the ellipse
float ellipse(float2 pos, float2 radii) {
    return pow(pos[0], 2) / pow(radii[0], 2) + pow(pos[1], 2) / pow(radii[1], 2);
}

// returns the distance to the ellipse from the center at angle theta
float ellipse_distance(float theta, float2 radii) {
    return (radii[0] * radii[1]) / sqrt(pow(radii[0], 2) 
            * pow(sin(theta), 2) + pow(radii[1], 2) * pow(cos(theta), 2));
}

float2 point_on_ellipse(float theta, float2 radii) {
    float x = (radii.x * radii.y) / sqrt(pow(radii.y, 2) + pow(radii.x, 2) * pow(tan(theta), 2));
    return float2(x, sqrt(1 - pow(x / radii.x, 2)) * radii.y);
}

float2 rotate_fill(float2 fpos) {
    float2 old_fpos = fpos;
    fpos.x = old_fpos.x * cos(_FillRotation) 
            - old_fpos.y * sin(_FillRotation);
    fpos.y = old_fpos.x * sin(_FillRotation) 
            + old_fpos.y * cos(_FillRotation);
    return fpos;
}

// returns the fill color for the given uv point in the quad.
// @param uv the uv coords from -0.5 to 0.5
fixed4 fill(float2 uv) {
    float2 fpos = float2(uv.x * _XScale, uv.y * _YScale);
    
    #if FILL_NONE
        return fixed4(0, 0, 0, 0);
    #elif FILL_OUTLINE_COLOR
        return _OutlineColor;
    #elif FILL_SOLID_COLOR
        // fill with the fill color
        return _FillColor;
    #elif FILL_GRADIENT
        // gradient
        // todo - simplify and try to remove conditionals
        fpos = rotate_fill(fpos);
        fpos += float2(_FillOffsetX, _FillOffsetY);
        float gmin = 0, gmax = 0, current = 0;
        if (_GradientType == 0) {
            // linear gradient
            if (_GradientAxis == 0) {
                gmin = -_XScale / 2 + _GradientStart * _XScale;
                gmax = _XScale / 2;
                current = fpos.x;
            } else {
                gmin = -_YScale / 2 + _GradientStart * _YScale;
                gmax = _YScale / 2;
                current = fpos.y;
            }
        } else if (_GradientType == 1) {
            // cylindrical gradient
            if (_GradientAxis == 0) {
                gmin = _GradientStart / 2 * _XScale;
                gmax = _XScale / 2;
                current = abs(fpos.x);
            } else {
                gmin = _GradientStart / 2 * _YScale;
                gmax = _YScale / 2;
                current = abs(fpos.y);
            }
        } else {
            // radial gradient
            gmax = length(float2(_XScale, _YScale)) / 2;
            gmin = gmax * _GradientStart;
            current = length(fpos);
        }
        if (current < gmin)
            return _FillColor;
        if (gmax == gmin)
            return _FillColor2;
        return lerp(_FillColor, _FillColor2, (current - gmin) / (gmax - gmin));
    #elif FILL_GRID
        // grid - background is _FillColor, lines are _FillColor2
        fpos = rotate_fill(fpos);
        fpos += float2(_FillOffsetX, _FillOffsetY);
        // float edge = min(fwidth(fpos) * 2, _GridSize);
        float edge = min(_PixelSize * 2, _GridSize);
        // webgl breaks with component-wise ops for some reason and only shows vertical
        // grid lines...sigh
        // float2 p = abs(frac(fpos / _GridSize) * _GridSize * 2 - _GridSize);
        // float2 mix = smoothstep(_GridSize - _LineSize - edge, _GridSize - _LineSize, p);
        // return lerp(_FillColor, _FillColor2, max(mix.x, mix.y));
        float px = abs(frac(fpos.x / _GridSize) * _GridSize * 2 - _GridSize);
        float py = abs(frac(fpos.y / _GridSize) * _GridSize * 2 - _GridSize);
        float mixx = smoothstep(_GridSize - _LineSize - edge, _GridSize - _LineSize, px);
        float mixy = smoothstep(_GridSize - _LineSize - edge, _GridSize - _LineSize, py);
        return lerp(_FillColor, _FillColor2, max(mixx, mixy));
    #elif FILL_CHECKERBOARD
        // checkerboard
        fpos = rotate_fill(fpos);
        fpos += float2(_FillOffsetX, _FillOffsetY);
        // float edge = min(fwidth(fpos), _GridSize);
        float edge = min(_PixelSize, _GridSize);
        float2 p = frac(fpos / _GridSize);
        float2 mix = smoothstep(0, edge / _GridSize, p);
        float tile = abs(floor(fpos.y / _GridSize) + floor(fpos.x / _GridSize)) % 2;
        fixed4 color1 = tile * _FillColor + (1 - tile) * _FillColor2;
        fixed4 color2 = tile * _FillColor2 + (1 - tile) * _FillColor;
        return lerp(color1, color2, min(mix.x, mix.y));
    #elif FILL_STRIPES
        // stripes
        fpos = rotate_fill(fpos);
        fpos += float2(_FillOffsetX, _FillOffsetY);
        // float edge = min(fwidth(fpos) * 2, _GridSize);
        float edge = min(_PixelSize * 2, _GridSize);
        float p = abs(frac(fpos.x / _GridSize) * _GridSize * 2 - _GridSize);
        float mix = smoothstep(_GridSize - _LineSize - edge, _GridSize - _LineSize, p);
        return lerp(_FillColor, _FillColor2, mix);
    #elif FILL_TEXTURE
        // texture
        fpos = rotate_fill(fpos);
        fpos /= float2(_XScale, _YScale);
        fpos += float2(0.5, 0.5);
        fpos += float2(_FillOffsetX, _FillOffsetY);
        fpos /= float2(_FillScaleX, _FillScaleY);
        return tex2D(_FillTexture, fpos);
    #endif
}

// this seems a little nicer than smoothstep for antialiasing, though
// not necessarily for large amounts of blurring...there are tradeoffs,
// might want to separate blur from antialias
float blur(float edge1, float edge2, float amount) {
    return clamp(lerp(0, 1, (amount - edge1) / (edge2 - edge1)), 0, 1);
    // return clamp(pow(max(0, amount - edge1), 2) / pow(edge2 - edge1, 2), 0, 1);
    // return clamp((amount - edge1) / (edge2 - edge1), 0, 1);
    // amount = clamp(amount, edge1, edge2);
    // return pow((amount - edge1) / max(edge2 - edge1, 0.0001), 0.9);
    // amount = clamp((amount - edge1) / (edge2 - edge1), 0.0, 1.0);
    // return amount*amount*amount*(amount*(amount*6 - 15) + 10);
}

fixed4 outline_fill_blend(float dist, fixed4 fill_color, fixed4 outline_color,
        float outer_blur, float outline, float inner_blur) {
    float mix = blur(outline, inner_blur, dist);
    #if FILL_NONE
        fixed4 color = outline_color;
        color.a *= (1 - mix);
    #else
        fixed4 color = lerp(outline_color, fill_color, mix);
    #endif
    float alpha = blur(0, 1, dist / outer_blur);
    color.a *= alpha;
    return color;
}

fixed4 fill_blend(float dist, fixed4 fill_color, float outer_blur) {
    float alpha = blur(0, outer_blur, dist);
    fixed4 color = fill_color;
    color.a *= alpha;
    return color;
}

// given a distance from the very edge of the shape going inwards, returns the
// color that should be at the current point
fixed4 color_from_distance(float dist, fixed4 fill_color, fixed4 outline_color) {
    if (_OutlineSize == 0) {
        return fill_blend(dist, fill_color, _OuterBlur);
    } else {
        float outline = _OuterBlur + _OutlineSize;
        float inner_blur = outline + _InnerBlur;
        return outline_fill_blend(dist, fill_color, outline_color,
                _OuterBlur, outline, inner_blur);
    }
}