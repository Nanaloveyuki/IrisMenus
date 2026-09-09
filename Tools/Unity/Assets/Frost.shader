Shader "Hidden/IrisMenus/Frost"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Radius ("Radius", Float) = 3
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        float _Radius;
        float4 Blur(v2f_img i, float2 axis) : SV_Target
        {
            if (_Radius < 0.01) return float4(tex2D(_MainTex, i.uv).rgb, 1);
            float2 delta = abs(_MainTex_TexelSize.xy) * axis;
            float inverseVariance = 0.5 / max(_Radius * _Radius, 0.01);
            float3 color = tex2D(_MainTex, i.uv).rgb;
            float total = 1;
            // Pair adjacent Gaussian taps using bilinear filtering: 13 fetches per axis.
            [unroll]
            for (int tap = 1; tap <= 11; tap += 2)
            {
                float a = exp(-tap * tap * inverseVariance);
                float b = exp(-(tap + 1) * (tap + 1) * inverseVariance);
                float weight = a + b;
                float offset = tap + b / max(weight, 0.000001);
                color += (tex2D(_MainTex, i.uv + delta * offset).rgb
                    + tex2D(_MainTex, i.uv - delta * offset).rgb) * weight;
                total += 2 * weight;
            }
            return float4(color / total, 1);
        }
        float4 Horizontal(v2f_img i) : SV_Target { return Blur(i, float2(1, 0)); }
        float4 Vertical(v2f_img i) : SV_Target { return Blur(i, float2(0, 1)); }
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Horizontal
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Vertical
            ENDCG
        }
    }
    Fallback Off
}
