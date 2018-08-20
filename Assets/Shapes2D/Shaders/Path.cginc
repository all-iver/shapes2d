#if PATH_1 || FILLED_PATH_1
    #define MAX_SEGMENTS 1
#elif PATH_2 || FILLED_PATH_2
    #define MAX_SEGMENTS 2
#elif PATH_4 || FILLED_PATH_4
    #define MAX_SEGMENTS 4
#elif PATH_8 || FILLED_PATH_8
    #define MAX_SEGMENTS 8
#elif PATH_16 || FILLED_PATH_16
    #define MAX_SEGMENTS 16
#elif PATH_24 || FILLED_PATH_24
    #define MAX_SEGMENTS 24
#elif PATH_32 || FILLED_PATH_32
    #define MAX_SEGMENTS 32
#endif
#define MAX_POINTS 3 * MAX_SEGMENTS

// each point is (x, y, is-in-loop, unused)
float4 _Points[MAX_POINTS];
int _NumSegments;
float _Thickness;

float3 get_cubic_roots(float4 coefficients) {
    // eliminate a3 by dividing it out
    coefficients /= coefficients.x;
    float a2 = coefficients.y;
    float a1 = coefficients.z;
    float a0 = coefficients.w;
    // follow along at http://mathworld.wolfram.com/CubicFormula.html
    // thanks also to https://www.shadertoy.com/view/XdB3Ww
    float Q = (3. * a1 - (a2 * a2)) / 9.;
    float R = (9. * a2 * a1 - 27. * a0 - 2. * (a2 * a2 * a2)) / 54.;
    float D = Q * Q * Q + R * R;
    if (D < 0.) {
        float theta = acos(R / sqrt(-(Q * Q * Q)));
        float3 i1 = float3(0., 2. * PI, 4. * PI) + theta;
        float3 i2 = cos(i1 / 3.);
        return 2. * sqrt(-Q) * i2 - 1/3. * a2;
    } else {
        // if sqrt(D) and R are very close to each other, R - sd wigs
        // out due to numerical cancellation.  so multiply by the 
        // conjugate to get a slightly different equation that avoids 
        // that operation (i.e., we end up doing R + sd instead).
        float sd = sqrt(D);
        // float2 st = float2(sd, -sd) + R;
        // if (sign(R) == sign(sd))
        //     st = float2(R + sd, -pow(Q, 3) / (R + sd));
        // else
        //     st = float2(R - sd, -pow(Q, 3) / (R - sd));
        float rsd = (when_neq(sign(R), sign(sd)) * -2 + 1) * sd + R;
        float2 st = float2(rsd, -(Q * Q * Q) / rsd);
        // preserve the sign of R +- sqrt(D) after taking the cube root
        st = sign(st) * pow(abs(st), 1/3.);
        float r = -1/3. * a2 + st.x + st.y;
        return float3(r, 0, 0);
    }
}

float2 distance_to_segment(float2 M, float2 b0, float2 b1, float2 b2) {
    // get the coefficients, thanks to
    // http://blog.gludion.com/2009/08/distance-to-quadratic-bezier-curve.html
    // note that when b1 is too close to the midpoint between b0 and b2, B ~= 0 and we get problems.
    // (handling that case in user space before we even get here)
    float2 A = b1 - b0;
    float2 B = b2 - b1 - A;
    float2 Mp = b0 - M;
    float a = dot(B, B);
    float b = 3. * dot(A, B);
    float c = 2. * dot(A, A) + dot(Mp, B);
    float d = dot(Mp, A);
    float3 roots = clamp(get_cubic_roots(float4(a, b, c, d)), 0, 1);
    float2 D = 2. * A;

    // thanks yet again to http://alienryderflex.com/polyspline/
    float flip = 1;
    #if FILLED_PATH_1 || FILLED_PATH_2 || FILLED_PATH_4 || FILLED_PATH_8 || FILLED_PATH_16 || FILLED_PATH_24 || FILLED_PATH_32
        // make sure M.y doesn't equal b0.y or b2.y, which could cause F1 or F2 to be exactly 0
        // (becomes a problem especially when rotating)
        float testY = M.y + when_lt(abs(b0.y - M.y), .0001) * .0002;
        testY += when_lt(abs(b2.y - testY), .0001) * .0002;
        float bottomPart=2.*(b0.y+b2.y-b1.y-b1.y);
        // prevent division-by-zero (also handled in user space)
        // if (abs(bottomPart) <= 0.0001) {
        //     b1.y += 0.0001;
        //     bottomPart = -0.0004;
        // }
        float sRoot=D.y; 
        sRoot*=sRoot;
        sRoot-=2.*bottomPart*(b0.y - testY);
        if (sRoot >= 0) {
            sRoot=sqrt(sRoot);
            float topPart=2.*(b0.y-b1.y);
            float F1 = (topPart+sRoot)/bottomPart;
            float F2 = (topPart-sRoot)/bottomPart;
            if (F1>=0. && F1<=1.) {
                float xPart=b0.x+F1*(b1.x-b0.x); 
                if (xPart+F1*(b1.x+F1*(b2.x-b1.x)-xPart)<M.x) 
                    flip *= -1;
            }
            if (F2>=0. && F2<=1.) {
                float xPart=b0.x+F2*(b1.x-b0.x); 
                if (xPart+F2*(b1.x+F2*(b2.x-b1.x)-xPart)<M.x) 
                    flip *= -1;
            }
        }
    #endif

    // find the positions on the curve of each root
    // thanks to https://www.shadertoy.com/view/XdB3Ww for this simplification
    float2 p1 = roots.x * (D + B * roots.x) + b0;
    float2 p2 = roots.y * (D + B * roots.y) + b0;
    float2 p3 = roots.z * (D + B * roots.z) + b0;
    // figure out which point is closest to M
	float dist1 = length(p1 - M);
	float dist2 = length(p2 - M);
	float dist3 = length(p3 - M);
    float dist = min(min(dist1, dist2), dist3);
    return float2(dist, flip);
}

float2 distance_to_path(float2 pos) {
    float closest_distance = 9999999;
    int odd_nodes = -1; // used for testing if the point is inside a filled path
    // you can't do variable-length loops in webgl (or es2 technically I think), 
    // hence the constant...so we have to iterate through MAX_SEGMENTS rather than 
    // _NumSegments which is the number of vertices actually in our poly
    for (int i = 0; i < MAX_SEGMENTS; i++) {
        // loop_over is 1 when we're past the number of sides in the poly
        float loop_over = when_ge(i, _NumSegments);
        float2 b0 = _Points[i * 3 + 0];
        float2 b1 = _Points[i * 3 + 1];
        float2 b2 = _Points[i * 3 + 2];
        float2 result = distance_to_segment(pos, b0, b1, b2) + loop_over * 9999999;
        float dist = result.x;
        closest_distance = min(dist, closest_distance);
        if (_Points[i * 3].z == 1)
            odd_nodes *= result.y / (loop_over * (result.y - 1) + 1);
    }
    return odd_nodes * closest_distance + _Thickness;
}

fixed4 frag(v2f i) : SV_Target {
    float2 pos = prepare(i.uv, i.modelPos.z);

    float dist = distance_to_path(pos);
    float is_outside = when_lt(dist, 0);
        
    fixed4 color = color_from_distance(dist, fill(i.uv), _OutlineColor) * i.color;                
    if (_PreMultiplyAlpha == 1)
        color.rgb *= color.a;

    if (_UseClipRect == 1)
        color.a *= UnityGet2DClipping(i.modelPos.xy, _ClipRect);
    clip(color.a - 0.001);

    return (1 - is_outside) * color + is_outside * fixed4(0, 0, 0, 0);
}