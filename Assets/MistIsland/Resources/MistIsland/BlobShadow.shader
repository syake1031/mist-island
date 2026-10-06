// キャラクターの足元に出す丸い影（半透明）。
Shader "MistIsland/BlobShadow"
{
    Properties
    {
        _Color ("Color", Color) = (0.1, 0.12, 0.1, 0.3)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2 - 1;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv);
                float a = saturate(1 - d);
                a = a * a * (3 - 2 * a);
                return float4(_Color.rgb, _Color.a * a);
            }
            ENDCG
        }
    }

    Fallback Off
}
