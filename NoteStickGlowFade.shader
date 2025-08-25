Shader "Custom/NoteStickGlowShader"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _MainTex_ST ("Texture Scale and Offset", Vector) = (1,1,0,0)
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _GlowColor ("Glow Color", Color) = (1,1,1,1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 7
        _GlowScale ("Glow Scale X", Range(1, 3)) = 3
        _GlowScaleZ ("Glow Offset Z", Range(0, 2)) = 1.1
        _GlowFade ("Glow Fade", Range(0.1, 2)) = 0.4
        _GlowOffsetX ("Glow Offset X", Float) = 0
        _FadeStart ("Fade Start", Float) = 12
        _FadeLength ("Fade Length", Float) = 0.8
        _GlowEnabled ("Glow Enabled", Range(0, 1)) = 0
        _AutoFlipOffset ("Auto Flip Glow Offset By World X", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" }
        LOD 200

        // Pass для свечения за пределами меша
        Pass
        {
            Cull Back
            Blend SrcAlpha One
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 objectCenter : TEXCOORD2;
                float stickLength : TEXCOORD3;
                float stickWidth : TEXCOORD4;
                float maxZ : TEXCOORD5;
                float baseLength : TEXCOORD6;
            };

            float _GlowScale;
            float _GlowScaleZ;
            fixed4 _GlowColor;
            float _GlowIntensity;
            float _GlowFade;
            float _GlowOffsetX;
            float _FadeStart;
            float _FadeLength;
            float _GlowEnabled;
            float _AutoFlipOffset;

            v2f vert (appdata v)
            {
                v2f o;
                if (_GlowEnabled > 0.5)
                {
                    v.vertex.x *= _GlowScale;
                    if (v.vertex.z > 0) // Добавляем фиксированное смещение по Z для дальнего конца
                    {
                        v.vertex.z += _GlowScaleZ;
                    }
                }
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv;
                o.objectCenter = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 minBounds = mul(unity_ObjectToWorld, float4(-0.5, 0, -0.5, 1)).xyz;
                float3 maxBounds = mul(unity_ObjectToWorld, float4(0.5, 0, 0.5, 1)).xyz;
                o.maxZ = maxBounds.z;
                o.baseLength = abs(maxBounds.z - minBounds.z);
                o.stickLength = o.baseLength;
                o.stickWidth = abs(maxBounds.x - minBounds.x) * _GlowScale;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_GlowEnabled < 0.5) discard;
                float fadeFactor = saturate((_FadeStart - i.worldPos.z) / _FadeLength);
                float sideSign = sign(i.objectCenter.x);
                float magnitude = abs(_GlowOffsetX);
                float appliedOffset = (_AutoFlipOffset > 0.5) ? (magnitude * (-sideSign)) : _GlowOffsetX;
                appliedOffset = (abs(sideSign) < 0.5) ? 0.0 : appliedOffset;
                float xDist = abs(i.worldPos.x - (i.objectCenter.x + appliedOffset));
                float xAlpha = saturate(1.0 - xDist / (i.stickWidth * _GlowFade));
                // Z-затухание: внутри палочки — полная яркость, в ауре — затухание
                float zAlpha;
                if (i.worldPos.z <= i.maxZ) // Внутри палочки
                {
                    zAlpha = 1.0; // Полная яркость
                }
                else // В ауре за дальним концом
                {
                    float auraDist = i.worldPos.z - i.maxZ;
                    zAlpha = saturate(1.0 - auraDist / (_GlowScaleZ * 1.5));
                }
                float alpha = fadeFactor * xAlpha * zAlpha;
                fixed4 glow = fixed4(_GlowColor.rgb * _GlowIntensity * alpha, alpha * _GlowColor.a);
                clip(fadeFactor - 0.01);
                return glow;
            }
            ENDCG
        }

        // Основной Pass с PBR и тенями
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:blend vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _BaseColor;
        half _Glossiness;
        half _Metallic;
        float _FadeStart;
        float _FadeLength;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float facing : VFACE;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.facing = 0;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            if (IN.facing < 0) discard;

            fixed4 texColor = tex2D(_MainTex, IN.uv_MainTex);
            fixed4 c = (texColor.r == 1.0 && texColor.g == 1.0 && texColor.b == 1.0 && texColor.a == 1.0) ? _BaseColor : texColor * _BaseColor;

            float fadeFactor = saturate((_FadeStart - IN.worldPos.z) / _FadeLength);
            if (IN.worldPos.z <= 0) fadeFactor = 1.0;

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a * fadeFactor;
        }
        ENDCG
    }
    FallBack "Diffuse"
}