// Basic shader for ShadowClones to use, while also writing to the depth buffer for UI culling.
// Uses portions of Poiyomi's Toon Shader for UDIM discarding support:

// MIT License
// Copyright (c) 2023 Poiyomi Inc.

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

Shader "Noachi/ShadowClone"
{
    Properties
    {
        // Common properties
        _ZWrite ("ZWrite", Int) = 1

        // UDIM Discard properties
        _UDIMDiscardMode ("UDIM Discard Mode", Float) = 0
        _UDIMDiscardUV ("UDIM Discard UV", Float) = 0
        _UDIMDiscardRow0_0 ("UDIM Discard Row 0 - Column 0", Float) = 0
        _UDIMDiscardRow0_1 ("UDIM Discard Row 0 - Column 1", Float) = 0
        _UDIMDiscardRow0_2 ("UDIM Discard Row 0 - Column 2", Float) = 0
        _UDIMDiscardRow0_3 ("UDIM Discard Row 0 - Column 3", Float) = 0
        _UDIMDiscardRow1_0 ("UDIM Discard Row 1 - Column 0", Float) = 0
        _UDIMDiscardRow1_1 ("UDIM Discard Row 1 - Column 1", Float) = 0
        _UDIMDiscardRow1_2 ("UDIM Discard Row 1 - Column 2", Float) = 0
        _UDIMDiscardRow1_3 ("UDIM Discard Row 1 - Column 3", Float) = 0
        _UDIMDiscardRow2_0 ("UDIM Discard Row 2 - Column 0", Float) = 0
        _UDIMDiscardRow2_1 ("UDIM Discard Row 2 - Column 1", Float) = 0
        _UDIMDiscardRow2_2 ("UDIM Discard Row 2 - Column 2", Float) = 0
        _UDIMDiscardRow2_3 ("UDIM Discard Row 2 - Column 3", Float) = 0
        _UDIMDiscardRow3_0 ("UDIM Discard Row 3 - Column 0", Float) = 0
        _UDIMDiscardRow3_1 ("UDIM Discard Row 3 - Column 1", Float) = 0
        _UDIMDiscardRow3_2 ("UDIM Discard Row 3 - Column 2", Float) = 0
        _UDIMDiscardRow3_3 ("UDIM Discard Row 3 - Column 3", Float) = 0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Pass
        {
            // Write to depth buffer
            ZWrite [_ZWrite]
            ColorMask 0
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

			// Common properties
			int _ZWrite;

            // UDIM Discard properties
            float _UDIMDiscardMode;
            float _UDIMDiscardUV;
            float _UDIMDiscardRow0_0;
            float _UDIMDiscardRow0_1;
            float _UDIMDiscardRow0_2;
            float _UDIMDiscardRow0_3;
            float _UDIMDiscardRow1_0;
            float _UDIMDiscardRow1_1;
            float _UDIMDiscardRow1_2;
            float _UDIMDiscardRow1_3;
            float _UDIMDiscardRow2_0;
            float _UDIMDiscardRow2_1;
            float _UDIMDiscardRow2_2;
            float _UDIMDiscardRow2_3;
            float _UDIMDiscardRow3_0;
            float _UDIMDiscardRow3_1;
            float _UDIMDiscardRow3_2;
            float _UDIMDiscardRow3_3;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv[4] : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv[4] : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);

				if (_ZWrite == 0) return (v2f)-1; // Early return if ZWrite is off, Noachi request
                
                UNITY_BRANCH
                if(_UDIMDiscardMode == 0) // Discard Vertices instead of just pixels
                {
                    // Branchless (inspired by s-ilent)
                    float2 udim = 0; 
                    // Select UV
                    udim += (v.uv[0].xy * (_UDIMDiscardUV == 0));
                    udim += (v.uv[1].xy * (_UDIMDiscardUV == 1));
                    udim += (v.uv[2].xy * (_UDIMDiscardUV == 2));
                    udim += (v.uv[3].xy * (_UDIMDiscardUV == 3));

                    float isDiscarded = 0;
                    float4 xMask = float4(  (udim.x >= 0 && udim.x < 1), 
                    (udim.x >= 1 && udim.x < 2),
                    (udim.x >= 2 && udim.x < 3),
                    (udim.x >= 3 && udim.x < 4));

                    isDiscarded += (udim.y >= 0 && udim.y < 1) * dot(float4(_UDIMDiscardRow0_0, _UDIMDiscardRow0_1, _UDIMDiscardRow0_2, _UDIMDiscardRow0_3), xMask);
                    isDiscarded += (udim.y >= 1 && udim.y < 2) * dot(float4(_UDIMDiscardRow1_0, _UDIMDiscardRow1_1, _UDIMDiscardRow1_2, _UDIMDiscardRow1_3), xMask);
                    isDiscarded += (udim.y >= 2 && udim.y < 3) * dot(float4(_UDIMDiscardRow2_0, _UDIMDiscardRow2_1, _UDIMDiscardRow2_2, _UDIMDiscardRow2_3), xMask);
                    isDiscarded += (udim.y >= 3 && udim.y < 4) * dot(float4(_UDIMDiscardRow3_0, _UDIMDiscardRow3_1, _UDIMDiscardRow3_2, _UDIMDiscardRow3_3), xMask);

                    isDiscarded *= any(float4(udim.y >= 0, udim.y < 4, udim.x >= 0, udim.x < 4)); // never discard outside 4x4 grid in pos coords 

                    // Use a threshold so that there's some room for animations to be close to 0, but not exactly 0
                    const float threshold = 0.001;
                    if(isDiscarded > threshold) // Early Return skips rest of vertex shader
                    {
                        return (v2f)-1;
                    }
                }
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
				if (_ZWrite == 0)
				{
					clip(-1); // Clip if ZWrite is off, Noachi request
				 	return 0;
				}

                if(_UDIMDiscardMode == 1) // Don't run if in vertex mode
                {
                    float2 udim = floor(i.uv[_UDIMDiscardUV]);

                    float isDiscarded = 0;
                    float4 xMask = float4(  (udim.x >= 0 && udim.x < 1), 
                    (udim.x >= 1 && udim.x < 2),
                    (udim.x >= 2 && udim.x < 3),
                    (udim.x >= 3 && udim.x < 4));

                    isDiscarded += (udim.y >= 0 && udim.y < 1) * dot(float4(_UDIMDiscardRow0_0, _UDIMDiscardRow0_1, _UDIMDiscardRow0_2, _UDIMDiscardRow0_3), xMask);
                    isDiscarded += (udim.y >= 1 && udim.y < 2) * dot(float4(_UDIMDiscardRow1_0, _UDIMDiscardRow1_1, _UDIMDiscardRow1_2, _UDIMDiscardRow1_3), xMask);
                    isDiscarded += (udim.y >= 2 && udim.y < 3) * dot(float4(_UDIMDiscardRow2_0, _UDIMDiscardRow2_1, _UDIMDiscardRow2_2, _UDIMDiscardRow2_3), xMask);
                    isDiscarded += (udim.y >= 3 && udim.y < 4) * dot(float4(_UDIMDiscardRow3_0, _UDIMDiscardRow3_1, _UDIMDiscardRow3_2, _UDIMDiscardRow3_3), xMask);

                    isDiscarded *= any(float4(udim.y >= 0, udim.y < 4, udim.x >= 0, udim.x < 4)); // never discard outside 4x4 grid in pos coords 

                    const float threshold = 0.001;
                    clip(threshold - isDiscarded); // Clip if discarded
                }
                return 0;
            }
            ENDCG
        }
        
        Pass
        {
            Tags{ "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

			// Common properties
			int _ZWrite;

            // UDIM Discard properties
            float _UDIMDiscardMode;
            float _UDIMDiscardUV;
            float _UDIMDiscardRow0_0;
            float _UDIMDiscardRow0_1;
            float _UDIMDiscardRow0_2;
            float _UDIMDiscardRow0_3;
            float _UDIMDiscardRow1_0;
            float _UDIMDiscardRow1_1;
            float _UDIMDiscardRow1_2;
            float _UDIMDiscardRow1_3;
            float _UDIMDiscardRow2_0;
            float _UDIMDiscardRow2_1;
            float _UDIMDiscardRow2_2;
            float _UDIMDiscardRow2_3;
            float _UDIMDiscardRow3_0;
            float _UDIMDiscardRow3_1;
            float _UDIMDiscardRow3_2;
            float _UDIMDiscardRow3_3;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv[4] : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv[4] : TEXCOORD0;
            };
            
            v2f vert (appdata v)
            {
				if (_ZWrite == 0) return (v2f)-1; // Early return if ZWrite is off, Noachi request

                UNITY_BRANCH
                if(_UDIMDiscardMode == 0) // Discard Vertices instead of just pixels
                {
                    // Branchless (inspired by s-ilent)
                    float2 udim = 0; 
                    // Select UV
                    udim += (v.uv[0].xy * (_UDIMDiscardUV == 0));
                    udim += (v.uv[1].xy * (_UDIMDiscardUV == 1));
                    udim += (v.uv[2].xy * (_UDIMDiscardUV == 2));
                    udim += (v.uv[3].xy * (_UDIMDiscardUV == 3));

                    float isDiscarded = 0;
                    float4 xMask = float4(  (udim.x >= 0 && udim.x < 1), 
                    (udim.x >= 1 && udim.x < 2),
                    (udim.x >= 2 && udim.x < 3),
                    (udim.x >= 3 && udim.x < 4));

                    isDiscarded += (udim.y >= 0 && udim.y < 1) * dot(float4(_UDIMDiscardRow0_0, _UDIMDiscardRow0_1, _UDIMDiscardRow0_2, _UDIMDiscardRow0_3), xMask);
                    isDiscarded += (udim.y >= 1 && udim.y < 2) * dot(float4(_UDIMDiscardRow1_0, _UDIMDiscardRow1_1, _UDIMDiscardRow1_2, _UDIMDiscardRow1_3), xMask);
                    isDiscarded += (udim.y >= 2 && udim.y < 3) * dot(float4(_UDIMDiscardRow2_0, _UDIMDiscardRow2_1, _UDIMDiscardRow2_2, _UDIMDiscardRow2_3), xMask);
                    isDiscarded += (udim.y >= 3 && udim.y < 4) * dot(float4(_UDIMDiscardRow3_0, _UDIMDiscardRow3_1, _UDIMDiscardRow3_2, _UDIMDiscardRow3_3), xMask);

                    isDiscarded *= any(float4(udim.y >= 0, udim.y < 4, udim.x >= 0, udim.x < 4)); // never discard outside 4x4 grid in pos coords 

                    // Use a threshold so that there's some room for animations to be close to 0, but not exactly 0
                    const float threshold = 0.001;
                    if(isDiscarded > threshold) // Early Return skips rest of vertex shader
                    {
                        return (v2f)-1;
                    }
                }
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
				if (_ZWrite == 0)
				{
					clip(-1); // Clip if ZWrite is off, Noachi request
					return 0;
				}

                if(_UDIMDiscardMode == 1) // Don't run if in vertex mode
                {
                    float2 udim = floor(i.uv[_UDIMDiscardUV]);

                    float isDiscarded = 0;
                    float4 xMask = float4(  (udim.x >= 0 && udim.x < 1), 
                    (udim.x >= 1 && udim.x < 2),
                    (udim.x >= 2 && udim.x < 3),
                    (udim.x >= 3 && udim.x < 4));

                    isDiscarded += (udim.y >= 0 && udim.y < 1) * dot(float4(_UDIMDiscardRow0_0, _UDIMDiscardRow0_1, _UDIMDiscardRow0_2, _UDIMDiscardRow0_3), xMask);
                    isDiscarded += (udim.y >= 1 && udim.y < 2) * dot(float4(_UDIMDiscardRow1_0, _UDIMDiscardRow1_1, _UDIMDiscardRow1_2, _UDIMDiscardRow1_3), xMask);
                    isDiscarded += (udim.y >= 2 && udim.y < 3) * dot(float4(_UDIMDiscardRow2_0, _UDIMDiscardRow2_1, _UDIMDiscardRow2_2, _UDIMDiscardRow2_3), xMask);
                    isDiscarded += (udim.y >= 3 && udim.y < 4) * dot(float4(_UDIMDiscardRow3_0, _UDIMDiscardRow3_1, _UDIMDiscardRow3_2, _UDIMDiscardRow3_3), xMask);

                    isDiscarded *= any(float4(udim.y >= 0, udim.y < 4, udim.x >= 0, udim.x < 4)); // never discard outside 4x4 grid in pos coords 

                    const float threshold = 0.001;
                    clip(threshold - isDiscarded); // Clip if discarded
                }
                return 0;
            }
            
            ENDCG
        }
    }
}