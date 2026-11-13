// Yarış görüntüsüne uygulanan ekran efekti: parlak yerlerden taşan ışık (bloom), renk canlılığı/kontrast ve kenar karartma.
// Resources içinde durur ki derlemeye dahil edilsin (PostFx.cs yükler).
Shader "Hidden/TextRun/PostFx"
{
    Properties
    {
        _MainTex ("Kaynak", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        sampler2D _BloomTex;
        float2 _Direction;
        half _Threshold;
        half _Intensity;
        half _Saturation;
        half _Contrast;
        half _Vignette;

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            float2 uvBloom : TEXCOORD1;
        };

        v2f vert(appdata_img v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.texcoord;
            o.uvBloom = v.texcoord;
            #if UNITY_UV_STARTS_AT_TOP
            if (_MainTex_TexelSize.y < 0.0) o.uvBloom.y = 1.0 - o.uvBloom.y;
            #endif
            return o;
        }

        // Yalnızca eşiği aşan parlaklık kalır
        half4 fragBright(v2f i) : SV_Target
        {
            half3 c = tex2D(_MainTex, i.uv).rgb;
            half peak = max(c.r, max(c.g, c.b));
            half keep = max(0.0, peak - _Threshold) / max(peak, 0.0001);
            return half4(c * keep, 1.0);
        }

        // Tek yönde yumuşatma (yatay ve dikey art arda uygulanır)
        half4 fragBlur(v2f i) : SV_Target
        {
            float2 step = _Direction * _MainTex_TexelSize.xy;
            half3 c = tex2D(_MainTex, i.uv).rgb * 0.227;
            c += (tex2D(_MainTex, i.uv + step * 1.385).rgb + tex2D(_MainTex, i.uv - step * 1.385).rgb) * 0.316;
            c += (tex2D(_MainTex, i.uv + step * 3.231).rgb + tex2D(_MainTex, i.uv - step * 3.231).rgb) * 0.070;
            return half4(c, 1.0);
        }

        // Asıl görüntü + taşan ışık, ardından renk ayarı ve kenar karartma
        half4 fragCompose(v2f i) : SV_Target
        {
            half3 c = tex2D(_MainTex, i.uv).rgb;
            c += tex2D(_BloomTex, i.uvBloom).rgb * _Intensity;

            half grey = dot(c, half3(0.299, 0.587, 0.114));
            c = lerp(grey.xxx, c, _Saturation);
            c = (c - 0.5) * _Contrast + 0.5;

            float2 fromCenter = i.uv - 0.5;
            c *= 1.0 - dot(fromCenter, fromCenter) * _Vignette;
            return half4(saturate(c), 1.0);
        }
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBright
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragCompose
            ENDCG
        }
    }

    Fallback Off
}
