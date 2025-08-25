Shader "Custom/GuitarFade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FadeStart ("Fade Start", Float) = 11.3
        _FadeLength ("Fade Length", Float) = 1
        _ScrollOffset ("Scroll Offset", Float) = 0.0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 200
        ZWrite On // Включаем запись в буфер глубины
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _FadeStart;
            float _FadeLength;
            float _ScrollOffset;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float worldZ : TEXCOORD1;
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.y += _ScrollOffset;
                o.worldZ = mul(unity_ObjectToWorld, v.vertex).z;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float fadeFactor = saturate((i.worldZ - _FadeStart) / _FadeLength);
                col.a *= 1.0 - fadeFactor;
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}