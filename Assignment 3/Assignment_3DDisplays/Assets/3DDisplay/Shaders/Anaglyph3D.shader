// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Anaglyph3D"
{
    Properties
    {
        _LeftEyeTex ("Left Eye Texture", 2D) = "white" {}
        _RightEyeTex ("Right Eye Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag

            #pragma target 3.0

            sampler2D _LeftEyeTex;
            sampler2D _RightEyeTex;

            struct appdata
            {
                float4 vertex : POSITION; 
                float2 uv : TEXCOORD0; 
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                /* Info
                 * This shader has two textures (_LeftEyeTex and _RightEyeTex).
                 * The AnaglyphManager.cs assigns the cameras' images to these textures.
                 * In this fragment shader, for each fragment, we can sample these textures to create a new outputColor
                 *  that combines the colors from both textures.
                 * 
                 * ToDo
                 *
                 * - For each eye:
                 *   - Get the color from the eye's texture at the position of i. The 
                 *                   
                 * - Fill the outputColor's color channels with the textures' color values
                 */
                
                float4 outputColor;
                outputColor.a = 1;

                return outputColor;

            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
