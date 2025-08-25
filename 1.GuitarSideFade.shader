Shader "Custom/GuitarSideFade"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _FadeStart ("Fade Start", Float) = 12
        _FadeLength ("Fade Length", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:fade
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _FadeStart;
        float _FadeLength;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            
            // Плавное затухание: 0 на Z > 11.3, 1 на Z <= 10
            float fadeFactor = saturate((_FadeStart - IN.worldPos.z) / _FadeLength);
            
            // Применяем fade к альбедо и альфе для плавности
            o.Albedo = c.rgb * fadeFactor;
            o.Metallic = _Metallic * fadeFactor; // Уменьшаем металличность в невидимой зоне
            o.Smoothness = _Glossiness * fadeFactor; // Уменьшаем гладкость в невидимой зоне
            o.Alpha = fadeFactor; // Плавная прозрачность
        }
        ENDCG
    }
    FallBack "Diffuse"
}