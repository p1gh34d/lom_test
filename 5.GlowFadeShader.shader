Shader "Custom/GlowFadeShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (1,1,1,1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 0.1
        _GlowWidth ("Glow Width", Range(0, 0.3)) = 0.035
        _GlowSteps ("Glow Steps", Range(1, 5)) = 1
        _FadeStart ("Fade Start", Float) = 12
        _FadeLength ("Fade Length", Float) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        // Слой 1: Самое широкое свечение
        Pass
        {
            Cull Off
            Blend SrcAlpha One
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 worldPos : TEXCOORD1; // Добавляем для fade
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0; // Передаём позицию для fade
            };

            float _GlowWidth;
            fixed4 _GlowColor;
            float _GlowIntensity;
            float _GlowSteps;
            float _FadeStart;
            float _FadeLength;

            v2f vert (appdata v)
            {
                v2f o;
                float width = _GlowWidth * (1.0 + 2.0 / _GlowSteps);
                v.vertex += float4(v.normal * width, 0);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz; // Мировая позиция
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float fadeFactor = saturate((_FadeStart - i.worldPos.z) / _FadeLength); // Затухание из AllFade
                float alpha = (1.0 - 2.0 / _GlowSteps) * fadeFactor; // Учитываем затухание
                fixed4 glow = fixed4(_GlowColor.rgb * _GlowIntensity * alpha, alpha * _GlowColor.a);
                clip(fadeFactor - 0.01); // Обрезаем как в AllFade
                return glow;
            }
            ENDCG
        }

        // Слой 2: Среднее свечение
        Pass
        {
            Cull Off
            Blend SrcAlpha One
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 worldPos : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            float _GlowWidth;
            fixed4 _GlowColor;
            float _GlowIntensity;
            float _GlowSteps;
            float _FadeStart;
            float _FadeLength;

            v2f vert (appdata v)
            {
                v2f o;
                float width = _GlowWidth * (1.0 + 1.0 / _GlowSteps);
                v.vertex += float4(v.normal * width, 0);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float fadeFactor = saturate((_FadeStart - i.worldPos.z) / _FadeLength);
                float alpha = (1.0 - 1.0 / _GlowSteps) * fadeFactor;
                fixed4 glow = fixed4(_GlowColor.rgb * _GlowIntensity * alpha, alpha * _GlowColor.a);
                clip(fadeFactor - 0.01);
                return glow;
            }
            ENDCG
        }

        // Слой 3: Яркое ядро свечения
        Pass
        {
            Cull Off
            Blend SrcAlpha One
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 worldPos : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            float _GlowWidth;
            fixed4 _GlowColor;
            float _GlowIntensity;
            float _GlowSteps;
            float _FadeStart;
            float _FadeLength;

            v2f vert (appdata v)
            {
                v2f o;
                float width = _GlowWidth * (1.0 + 0.0 / _GlowSteps);
                v.vertex += float4(v.normal * width, 0);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float fadeFactor = saturate((_FadeStart - i.worldPos.z) / _FadeLength);
                float alpha = (1.0 - 0.0 / _GlowSteps) * fadeFactor;
                fixed4 glow = fixed4(_GlowColor.rgb * _GlowIntensity * alpha, alpha * _GlowColor.a);
                clip(fadeFactor - 0.01);
                return glow;
            }
            ENDCG
        }

        // Основной меш
        Pass
        {
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _BaseColor;
            float _FadeStart;
            float _FadeLength;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _BaseColor;
                float fadeFactor = saturate((_FadeStart - i.worldPos.z) / _FadeLength);
                col.a = fadeFactor; // Применяем затухание к альфе
                clip(fadeFactor - 0.01); // Обрезаем как в AllFade
                return col;
            }
            ENDCG
        }
    }
}