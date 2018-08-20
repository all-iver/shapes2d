Shader "Shapes2D/Shape" {
	Properties {
        // this is to make SpriteRenderer happy
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Shapes2D_SrcBlend ("Src Blend", Float) = 0
        [HideInInspector] _Shapes2D_DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _Shapes2D_SrcAlpha ("Src Alpha", Float) = 0
        [HideInInspector] _Shapes2D_DstAlpha ("Dst Alpha", Float) = 0

        // UI-related masking options, Unity complains if they aren't here
		[HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
		[HideInInspector] _Stencil ("Stencil ID", Float) = 0
		[HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
		[HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
		[HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
		[HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }
    SubShader {
        Tags { 
			"Queue"="Transparent" 
			"IgnoreProjector"="True" 
			"RenderType"="Transparent" 
			"PreviewType"="Plane"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend [_Shapes2D_SrcBlend] [_Shapes2D_DstBlend], [_Shapes2D_SrcAlpha] [_Shapes2D_DstAlpha]

        // UI-related masking options
		Stencil {
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}
        ColorMask [_ColorMask]
        
        Pass {
            CGPROGRAM
            
            #pragma multi_compile RECTANGLE ELLIPSE SIMPLE_ELLIPSE POLYGON_MAP POLYGON_8 POLYGON_16 POLYGON_24 POLYGON_32 POLYGON_40 POLYGON_48 POLYGON_56 POLYGON_64 TRIANGLE PATH_1 PATH_2 PATH_4 PATH_8 PATH_16 PATH_24 PATH_32 FILLED_PATH_1 FILLED_PATH_2 FILLED_PATH_4 FILLED_PATH_8 FILLED_PATH_16 FILLED_PATH_24 FILLED_PATH_32
            #pragma multi_compile FILL_NONE FILL_OUTLINE_COLOR FILL_SOLID_COLOR FILL_GRADIENT FILL_GRID FILL_CHECKERBOARD FILL_STRIPES FILL_TEXTURE
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
			#include "UnityUI.cginc"

            // UI-related masking options
            int _UseClipRect = 0;
			float4 _ClipRect;

            // values passed in from user space
            float _PixelSize; // in world units (if 0, we'll calc it ourselves)
            float _XScale;
            float _YScale;
            fixed4 _OutlineColor;
            fixed4 _FillColor;
            fixed4 _FillColor2;
            float _FillRotation;
            float _FillOffsetX;
            float _FillOffsetY;
            float _FillScaleX;
            float _FillScaleY;
            float _GridSize;
            float _LineSize;
            int _GradientType;
            int _GradientAxis;
            float _GradientStart;
            float _OutlineSize;
            float _Blur;
            sampler2D _FillTexture;

            // this is disabled when we're rendering to texture during sprite conversion
            int _PreMultiplyAlpha = 1;

            // these are internal globals, not passed in from user space
            float _OuterBlur;
            float _InnerBlur;

            #include "Common.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};
            
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
				float4 modelPos : TEXCOORD1;
                fixed4 color : COLOR;
            };

            v2f vert(appdata_t v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // translate input uv coords (which are from 0 - 1 on either dimension) to something more 
                // useful (-0.5 to 0.5).
                o.uv = v.texcoord.xy - 0.5;
				o.modelPos = v.vertex;
                // hijack modelPos.z to be distance from camera plane since it 
                // isn't being used for anything else.  used for pixel size.
                o.modelPos.z = -UnityObjectToViewPos(v.vertex).z;
                // for Unity versions < 5.4.0f3
                // o.modelPos.z = -mul(UNITY_MATRIX_MV, v.vertex).z;
                o.color = v.color;
                return o;
            }

            #if RECTANGLE
                #include "Rectangle.cginc"
            #endif
            #if ELLIPSE || SIMPLE_ELLIPSE
                #include "Ellipse.cginc"
            #endif
            #if TRIANGLE
                #include "Triangle.cginc"
            #endif
            #if POLYGON_MAP || POLYGON_8 || POLYGON_16 || POLYGON_24 || POLYGON_32 || POLYGON_40 || POLYGON_48 || POLYGON_56 || POLYGON_64
                #include "Polygon.cginc"
            #endif
            #if PATH_1 || PATH_2 || PATH_4 || PATH_8 || PATH_16 || PATH_24 || PATH_32 || FILLED_PATH_1 || FILLED_PATH_2 || FILLED_PATH_4 || FILLED_PATH_8 || FILLED_PATH_16 || FILLED_PATH_24 || FILLED_PATH_32 
                #include "Path.cginc"
            #endif
            
            ENDCG
        }
    }
}