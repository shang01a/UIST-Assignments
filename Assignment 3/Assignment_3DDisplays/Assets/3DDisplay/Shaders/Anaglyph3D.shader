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
            // Sample the image rendered by each eye.
            fixed4 leftColor = tex2D(_LeftEyeTex, i.uv);
            fixed4 rightColor = tex2D(_RightEyeTex, i.uv);

            // Left eye is viewed through the red lens.
            // Right eye is viewed through the cyan lens.
            fixed4 outputColor;

            outputColor.r = leftColor.r;
            outputColor.g = rightColor.g;
            outputColor.b = rightColor.b;
            outputColor.a = 1.0;

            return outputColor;
        }

            ENDCG
        }
    }
    Fallback "Diffuse"
}
