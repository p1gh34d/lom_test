Shader "Custom/NoteFade"
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
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" }
        LOD 200
        ZWrite Off
        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:blend vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float facing : VFACE;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _FadeStart;
        float _FadeLength;

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.facing = 0; // Инициализация
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Отбрасываем задние грани
            if (IN.facing < 0) discard;

            // Если текстура отсутствует или белая, используем только _Color
            fixed4 texColor = tex2D(_MainTex, IN.uv_MainTex);
            fixed4 c = (texColor.r == 1.0 && texColor.g == 1.0 && texColor.b == 1.0 && texColor.a == 1.0) ? _Color : texColor * _Color;
            
            // Плавное затухание: 1 на Z <= 10, 0 на Z >= 12
            float fadeFactor = saturate((_FadeStart - IN.worldPos.z) / _FadeLength);
            // Гарантируем полную непрозрачность на Z <= 0
            fadeFactor = IN.worldPos.z <= 0 ? 1.0 : fadeFactor;
            
            // Применяем свойства без затемнения
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a * fadeFactor; // Прозрачность зависит от текстуры и затухания
        }
        ENDCG
    }
    FallBack "Diffuse"
}