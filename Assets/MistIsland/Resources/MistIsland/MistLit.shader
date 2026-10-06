// 霧の島の見た目用シェーダー。
// ライトと霧はレンダーパイプラインに頼らず、MistVisuals が設定するグローバル値で計算する。
// そのため URP でも Built-in でも同じ見た目になる（影はなし）。
Shader "MistIsland/MistLit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _UseVertexColor ("Use Vertex Color", Float) = 0
        _Emission ("Emission", Float) = 0
        _WaveAmp ("Wave Amplitude", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float _UseVertexColor;
            float _Emission;
            float _WaveAmp;

            float4 _MI_SunDir;
            float4 _MI_SunColor;
            float4 _MI_Ambient;
            float4 _MI_FogColor;
            float _MI_FogStart;
            float _MI_FogEnd;
            float _MI_FogHeight;
            float _MI_Time;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);

                if (_WaveAmp > 0)
                {
                    float a = wp.x * 0.35 + _MI_Time * 1.1;
                    float b = wp.z * 0.3 + _MI_Time * 0.8;
                    float c = (wp.x + wp.z) * 0.6 + _MI_Time * 1.7;
                    wp.y += (sin(a) + cos(b) + 0.4 * sin(c)) * _WaveAmp;
                    float dx = (0.35 * cos(a) + 0.24 * cos(c)) * _WaveAmp;
                    float dz = (-0.3 * sin(b) + 0.24 * cos(c)) * _WaveAmp;
                    n = normalize(float3(-dx, 1, -dz));
                }

                o.pos = mul(UNITY_MATRIX_VP, wp);
                o.worldPos = wp.xyz;
                o.worldNormal = n;
                o.color = lerp(float4(1, 1, 1, 1), v.color, saturate(_UseVertexColor));
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 baseColor = _Color.rgb * i.color.rgb;
                float3 n = normalize(i.worldNormal);
                float3 l = normalize(_MI_SunDir.xyz + float3(0, 0.0001, 0));

                // 柔らかい陰影（ハーフランバート）
                float ndl = dot(n, l) * 0.5 + 0.5;
                ndl = ndl * ndl;
                float3 lit = baseColor * (_MI_Ambient.rgb + _MI_SunColor.rgb * ndl);
                lit += baseColor * _Emission;

                // 距離の霧 + 低いところに溜まる霧
                float dist = distance(_WorldSpaceCameraPos.xyz, i.worldPos);
                float fogRange = max(0.001, _MI_FogEnd - _MI_FogStart);
                float distFog = saturate((dist - _MI_FogStart) / fogRange);
                float heightFog = saturate(1 - (i.worldPos.y + 0.5) / max(0.001, _MI_FogHeight));
                float fog = saturate(distFog + heightFog * heightFog * 0.35);

                return float4(lerp(lit, _MI_FogColor.rgb, fog), 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
