Shader "Custom/GuitarFade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScrollOffset ("Scroll Offset", Range(0, 1)) = 0
        _MaskHeight ("Mask Height", Range(0, 1)) = 1
        _CoverHeight ("Cover Height", Range(0, 1)) = 1
        
        // Star Power Properties
        [Header(Star Power)]
        _StarPower ("Star Power", Range(0, 1)) = 0
        _StarTint ("Star Tint", Color) = (0.55, 0.75, 1.0, 0.35)
        _StarHueShift ("Star Hue Shift", Range(-180, 180)) = 120
        _StarIntensity ("Star Intensity", Range(0, 2)) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _ScrollOffset;
            float _MaskHeight;
            float _CoverHeight;
            
            // Star Power variables
            float _StarPower;
            float4 _StarTint;
            float _StarHueShift;
            float _StarIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            // Hue rotation function for Star Power effect
            float3 HueRotate(float3 rgb, float degrees)
            {
                float3x3 k = float3x3(
                    0.299, 0.587, 0.114,
                    0.299, 0.587, 0.114,
                    0.299, 0.587, 0.114
                );
                float3 lum = mul(k, rgb);
                float3 cr = rgb - lum;

                float rad = radians(degrees);
                float s = sin(rad), c = cos(rad);

                // Rotation around the "gray" axis
                float3x3 R = float3x3(
                    0.299 + 0.701*c + 0.168*s, 0.587 - 0.587*c + 0.330*s, 0.114 - 0.114*c - 0.497*s,
                    0.299 - 0.299*c - 0.328*s, 0.587 + 0.413*c + 0.035*s, 0.114 - 0.114*c + 0.292*s,
                    0.299 - 0.300*c + 1.250*s, 0.587 - 0.588*c - 1.050*s, 0.114 + 0.886*c - 0.203*s
                );

                return saturate(mul(R, rgb));
            }
            
            // Apply Star Power effect
            float3 ApplyStarPower(float3 baseRgb, float starPower, float4 tintRgb, float hueShift, float intensity)
            {
                if (starPower <= 0) return baseRgb;
                
                // First apply hue shift (red -> blue)
                float3 hueShifted = HueRotate(baseRgb, hueShift);
                
                // Then apply blue tint with intensity
                float3 tinted = lerp(baseRgb, hueShifted, 0.8);
                tinted = lerp(tinted, tintRgb.rgb * tinted, tintRgb.a * intensity);
                
                // Blend based on star power strength
                return lerp(baseRgb, tinted, starPower);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Apply scrolling offset
                float2 uv = i.uv;
                uv.y += _ScrollOffset;
                
                // Sample texture
                fixed4 col = tex2D(_MainTex, uv);
                
                // Apply Star Power effect
                col.rgb = ApplyStarPower(col.rgb, _StarPower, _StarTint, _StarHueShift, _StarIntensity);
                
                return col;
            }
            ENDCG
        }
    }
}