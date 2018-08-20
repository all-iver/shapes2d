float4 _ArcAngles; // min, max, use_arc, invert_arc in radians from 0 - PI2
float4 _InnerRadii; // just x & y


// gets distances to the edge of the shape, and then distances to outer_blur, outline and inner_blur 
// from the edge of the shape
float4 ellipse_distances(float theta, float2 radii) {
    float2 rad_outer_blur = radii;
    float2 rad_outer = rad_outer_blur - _OuterBlur;
    float2 rad_inner = rad_outer - _OutlineSize;
    float2 rad_inner_blur = rad_inner - _InnerBlur;

    // get distances from the edge of the shape
    float sqrSinTheta = pow(sin(theta), 2);
    float sqrCosTheta = pow(cos(theta), 2);
    float edge = (rad_outer_blur[0] * rad_outer_blur[1]) / sqrt(pow(rad_outer_blur[0], 2) 
            * sqrSinTheta + pow(rad_outer_blur[1], 2) * sqrCosTheta);
    float outer_blur = edge - (rad_outer[0] * rad_outer[1]) / sqrt(pow(rad_outer[0], 2) 
            * sqrSinTheta + pow(rad_outer[1], 2) * sqrCosTheta);
    float outline = edge - (rad_inner[0] * rad_inner[1]) / sqrt(pow(rad_inner[0], 2) 
            * sqrSinTheta + pow(rad_inner[1], 2) * sqrCosTheta);
    float inner_blur = edge - (rad_inner_blur[0] * rad_inner_blur[1]) / sqrt(pow(rad_inner_blur[0], 2) 
            * sqrSinTheta + pow(rad_inner_blur[1], 2) * sqrCosTheta);

    return float4(edge, outer_blur, outline, inner_blur);
}

// gets distances to the edge of the shape, and then distances to outer_blur, outline and inner_blur 
// from the edge of the shape - but going from the inside out, for an inner "donut hole" ellipse
float4 ellipse_distances_inverse(float theta, float2 radii) {
    float2 rad_outer_blur = radii;
    float2 rad_outer = rad_outer_blur + _OuterBlur;
    float2 rad_inner = rad_outer + _OutlineSize;
    float2 rad_inner_blur = rad_inner + _InnerBlur;

    // get distances from the edge of the shape
    float sqrSinTheta = pow(sin(theta), 2);
    float sqrCosTheta = pow(cos(theta), 2);
    float edge = (rad_outer_blur[0] * rad_outer_blur[1]) / sqrt(pow(rad_outer_blur[0], 2) 
            * sqrSinTheta + pow(rad_outer_blur[1], 2) * sqrCosTheta);
    float outer_blur = (rad_outer[0] * rad_outer[1]) / sqrt(pow(rad_outer[0], 2) 
            * sqrSinTheta + pow(rad_outer[1], 2) * sqrCosTheta) - edge;
    float outline = (rad_inner[0] * rad_inner[1]) / sqrt(pow(rad_inner[0], 2) 
            * sqrSinTheta + pow(rad_inner[1], 2) * sqrCosTheta) - edge;
    float inner_blur = (rad_inner_blur[0] * rad_inner_blur[1]) / sqrt(pow(rad_inner_blur[0], 2) 
            * sqrSinTheta + pow(rad_inner_blur[1], 2) * sqrCosTheta) - edge;

    return float4(edge, outer_blur, outline, inner_blur);
}

// this method is pretty fast, and has accurate distances on the minor and 
// major dimensions but only at 0 and 90 degrees - outlines get stretched when 
// the ratio of major to minor gets too far away from 1.
fixed4 nested_ellipse_method(float2 pos, fixed4 fill_color, fixed4 outline_color) {
    float dist_to_pos = length(pos);

    // angle of pos from the center
    float theta = atan2(pos.y, pos.x + when_eq(pos.x, 0) * 0.00001);

    // useful info about the outer ellipse
    float2 radii = float2(_XScale, _YScale) / 2;
    float4 distances = ellipse_distances(theta, radii);
    
    // distance from the current point to the edge of the outer ellipse
    float dist = distances.x - dist_to_pos;

    if (_ArcAngles.z > 0) {
        // normalize theta to something more useful.  theta as returned from atan() is 
        // 0 to PI or 0 to -PI depending on which side of the x axis you are, so this turns
        // it into 0 to 2PI so we can do simple comparisons between angles.
        float negative_theta = when_lt(theta, 0);
        float norm_theta = negative_theta * (PI2 + theta) + (1 - negative_theta) * theta;

        // see if we're closer to one of the arc lines than to the ellipse curve.  doesn't matter
        // if our line segment extends past the radius (in fact it helps if it does).
        float max_dim = max(_XScale, _YScale);
        float2 p1 = float2(cos(_ArcAngles.x) * max_dim, sin(_ArcAngles.x) * max_dim);
        float2 p2 = float2(cos(_ArcAngles.y) * max_dim, sin(_ArcAngles.y) * max_dim);
        float d1 = distance_to_line_segment(pos, float2(0, 0), p1); 
        float d2 = distance_to_line_segment(pos, float2(0, 0), p2);
        float line_dist = min(d1, d2);
        float4 line_distances = float4(0, _OuterBlur, _OuterBlur + _OutlineSize, _OuterBlur + _OutlineSize + _InnerBlur);
        // ellipse distances are weird, so don't just use min(line_dist, dist) - check if we're 
        // closer relative to the total outline distance
        float use_line = when_lt(line_dist / line_distances.w, dist / distances.w) * _ArcAngles.z;
        dist = use_line * line_dist + (1 - use_line) * dist;
        distances = use_line * line_distances + (1 - use_line) * distances; 
        
        // see if we're outside the visible part given the arc constraints 
        float less_than_min = when_lt(norm_theta, _ArcAngles.x);
        float greater_than_max = when_gt(norm_theta, _ArcAngles.y);
        float outside_arc = or(less_than_min, greater_than_max);
        outside_arc = _ArcAngles.w * outside_arc + (1 - _ArcAngles.w) * (1 - outside_arc);
        dist *= outside_arc;
    }
    
    if (_InnerRadii.x > 0 || _InnerRadii.y > 0) {
        // are we closer to the inner ellipse?
        // useful info about the "donut hole" inner ellipse
        float2 inner_radii = _InnerRadii.xy;
        float4 inner_distances = ellipse_distances_inverse(theta, inner_radii);
        // distance from the current point to the edge of the inner ellipse, on the inside
        float inner_dist = dist_to_pos - inner_distances.x;
        // ellipse distances are weird, so don't just use min(inner_dist, dist) - check if we're 
        // closer relative to the total outline distance
        float use_inner = when_lt(inner_dist / inner_distances.w, dist / distances.w);
        dist = use_inner * inner_dist + (1 - use_inner) * dist;
        distances = use_inner * inner_distances + (1 - use_inner) * distances;
    }

    if (_OutlineSize == 0) {
        return fill_blend(dist, fill_color, distances.y);        
    } else {
        return outline_fill_blend(dist, fill_color, outline_color,
                distances.y, distances.z, distances.w);
    }
}

// in standard form so b3 == 1.  this just returns a real root.
float get_cubic_root(float b2, float b0) {
    // in standard form - seems to have fewer glitches
    // http://mathworld.wolfram.com/CubicFormula.html
    // note that b1 has been removed cause it's always 0
    float p = (-b2 * b2) / 3.;
    float q = (-27 * b0 - 2 * b2 * b2 * b2) / 27.;
    float Q = p / 3.;
    float R = q / 2.;
    float D = Q * Q * Q + R * R;
    if (D < 0) {
        float phi = acos(clamp(R / sqrt(max(0, -Q * -Q * -Q)), -1, 1));
        return 2 * sqrt(max(0, -Q)) * cos(phi / 3.) - 1/3. * b2;
    } else {
        float S = pow(max(0, R + sqrt(max(0, D))), 1/3.);
        float n = R - sqrt(max(0, D));
        // if R is very close to sqrt(D) then we get noise because n may 
        // end up negative (I assume due to floating point inaccuracies?)
        // and the pow(n, 1/3.) is undefined.
        float T = pow(max(0, n), 1/3.);
        return -1/3. * b2 + (S + T);
    }
    
    // // coefficients of the resolvent cubic in general form
    // float b2 = -a2;
    // float b1 = a1 * a3 - 4. * a0;
    // float b0 = 4. * a0 * a2 - a1 * a1 - a0 * a3 * a3;
    // float Q = (3 * b1 - b2 * b2) / 9.;
    // float R = (9 * b2 * b1 - 27 * b0 - 2 * b2 * b2 * b2) / 54.;
    // float D = Q * Q * Q + R * R;
    // if (D < 0) {
    //     float phi = acos(R / sqrt(-Q * -Q * -Q));
    //     return 2 * sqrt(-Q) * cos(phi / 3.) - 1/3. * b2;
    // } else {
    //     float S = pow(R + sqrt(D), 1/3.);
    //     float T = pow(R - sqrt(D), 1/3.);
    //     // a root of the resolvent cubic
    //     return -1/3. * b2 + (S + T);
    // }
}

// in standard form so a4 == 1.  we return a different root depending on if the
// ellipse has its major axis as y (i.e. is vertically stretched rather than 
// horizontally).
float get_quartic_root(float a3, float a2, float a1, float a0, bool vertical) {
    // coefficients of the resolvent cubic equation
    // http://mathworld.wolfram.com/ResolventCubic.html
    float b2 = -a2;
    // b1 always ends up being 0
    // float b1 = a1 * a3 - 4. * a0;
    float b0 = 4. * a0 * a2 - a1 * a1 - a0 * a3 * a3;

    float y1 = get_cubic_root(b2, b0);

    // take the cubic root back to the quartic and find its root
    // http://mathworld.wolfram.com/QuarticEquation.html
    float R = sqrt(max(1/4. * a3 * a3 - a2 + y1, 0));

    float e1 = 3/4. * a3 * a3 - R * R - 2. * a2;
    float e2 = 1/4. * (4 * a3 * a2 - 8 * a1 - a3 * a3 * a3);
    float e3 = 0;
    if (R != 0)
        e3 = pow(R, -1);

    // thanks to shadertoy user kusma for the hint about -1 <= acos <= 1
    // https://www.shadertoy.com/view/4sS3zz
    if (vertical) {
        float v = e1 + e2 * e3;
        // with very small values of v, sqrt(v) causes glitchiness.  this isn't a real
        // fix but it at least draws something close to the right thing.
        float D = sqrt(max(0, v));
        float z1 = acos(clamp(-1/4. * a3 + 1/2. * R + 1/2. * D, -1, 1));
        // float z2 = acos(min(- 1/4. * a3 + 1/2. * R - 1/2. * D, 1));
        return z1;
    } else {
        float v = e1 - e2 * e3;
        // with very small values of v, sqrt(v) causes glitchiness.  this isn't a real
        // fix but it at least draws something close to the right thing.
        float E = sqrt(max(0, v));
        float z3 = acos(clamp(-1/4. * a3 - 1/2. * R + 1/2. * E, -1, 1));
        // float z4 = acos(min(- 1/4. * a3 - 1/2. * R - 1/2. * E, 1));
        return z3;
    }
}

// note - this method is "correct" but it has glitches, meaning noise appears sometimes.
// I haven't been able to figure out why - it doesn't seem like there are any possible
// math domain errors, but maybe there is some floating point inaccuracy somewhere.
// we try to mitigate it by drawing an "incorrect" but less glitchy method for some
// pixels/ellipses, but it's not perfect.
fixed4 quartic_method(float2 pos, fixed4 fill_color, fixed4 outline_color) {
    // if outside the ellipse, return clear
    if (ellipse(pos, float2(_XScale, _YScale) / 2) > 1)
        return fixed4(0, 0, 0, 0);

    float a = _XScale / 2;
    float b = _YScale / 2;
    float x = abs(pos.x);
    float y = abs(pos.y);
    
    // this method glitches out a lot for near-circles so render those using
    // a simpler method.  it's pretty hard to tell the difference a lot of the time.
    // also, with very small values of x and y, we get glitches.  this isn't a real
    // fix but it at least draws something close to the right thing.
    if (1 - min(a, b) / max(a, b) < 0.25f
            || x / a < 0.005f || y / b < 0.005f)
        return nested_ellipse_method(pos, fill_color, outline_color);
        
    // coefficients of the quartic equation
    // many thanks to http://iquilezles.org/www/articles/ellipsedist/ellipsedist.htm
    // for help with the math
    float m = x * (a / (b * b - a * a));
    float n = y * (b / (b * b - a * a));
    float a4 = 1.;
    float a3 = 2. * m;
    float a2 = m * m + n * n - 1.;
    float a1 = -2. * m;
    float a0 = -m * m;
    
    // theta, a root of the quartic equation, is the angle on the ellipse corresponding 
    // to the nearest point on the ellipse to our current point.  given the equation:
    //   f(theta) = get the distance from the current point (inside the ellipse) to the 
    //              point at theta (on the ellipse),
    // then the derivative of f represents how the distance changes as you change theta.
    // the values of theta for which the derivative returns 0 (i.e. the roots) represent
    // the points where the distance is nearest or farthest from the current point.
    // this derivative turns out to be a quartic eqation (see the above iquilezles 
    // article), so finding the correct root gives us the theta we need. 
    float theta = get_quartic_root(a3, a2, a1, a0, _XScale < _YScale);

    float2 radii = float2(_XScale, _YScale) / 2;
    // the nearest point on the ellipse to our current point.  the line going between our
    // current point and the nearest point is the normal, and measuring the distance from
    // the current point to the nearest point tells us whether we're on the outline or not.
    float2 nearest = float2(cos(theta) * radii[0], sin(theta) * radii[1]);                
    nearest.x *= sign(pos.x);
    nearest.y *= sign(pos.y);
    float dist = distance(nearest, pos);

    return color_from_distance(dist, fill_color, outline_color);                
}

fixed4 frag(v2f i) : SV_Target {
    float2 pos = prepare(i.uv, i.modelPos.z);

    fixed4 outline_color = _OutlineColor;
    fixed4 fill_color = fill(i.uv);

    #if ELLIPSE
        fixed4 color = quartic_method(pos, fill_color, outline_color);
    #else // SIMPLE_ELLIPSE
        fixed4 color = nested_ellipse_method(pos, fill_color, outline_color);
    #endif

    color *= i.color;

    if (_PreMultiplyAlpha == 1)
        color.rgb *= color.a;

    if (_UseClipRect == 1)
        color.a *= UnityGet2DClipping(i.modelPos.xy, _ClipRect);
    clip(color.a - 0.001);
    
    return color;
}