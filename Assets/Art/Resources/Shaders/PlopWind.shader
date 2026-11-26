// Breeze airflow drawn on the floor in front of a fan, one quad per cell rotated to the fan direction
// (+x = downwind). Curved streaks drift downwind with a bright head, so the direction reads even when
// frozen. Renderer colour: r = (cell index * 16 + run length) byte, g = intensity (0..1), b = fan seed.
Shader "PLOP/Wind"
{
    Properties
    {
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex PlopVertex
            #pragma fragment WindFragment
            #pragma multi_compile_instancing
            #include "PlopCommon.hlsl"

            float4 _PlopWindTint;

            half4 WindFragment(PlopVaryings i) : SV_Target
            {
                uint code = PlopByte(i.color.r);
                float run = max((float)(code & 15u), 1.0);
                float index = (float)(code >> 4);
                float intensity = i.color.g;
                float seed = i.color.b * 7.0;
                float along = index + i.uv.x;           // cells downwind of the fan centre
                float across = i.uv.y - 0.5;
                float t = _PlopTime;

                float fade = smoothstep(0.30, 0.65, along) * (1.0 - smoothstep(run - 0.55, run - 0.05, along));
                if (fade <= 0.0) discard;

                // A faint corridor shows the reach; chevrons drift downwind (still readable when frozen);
                // soft curved ribbons and motes add the motion.
                float corridor = (1.0 - smoothstep(0.26, 0.36, abs(across))) * 0.10;
                float u = frac(along - t * 0.55 + seed) - 0.5;
                float chevD = abs(u + abs(across) * 0.85 - 0.02) - 0.028;
                float chevron = (1.0 - smoothstep(0.0, 0.016, chevD)) * (1.0 - smoothstep(0.13, 0.17, abs(across)));
                float streaks = 0.0;
                [unroll] for (int k = 0; k < 2; k++)
                {
                    float lane = (k * 2 - 1) * 0.25 + 0.045 * sin(along * 4.6 + t * 1.9 + k * 1.7 + seed);
                    float s = frac(along * 0.75 - t * (0.85 + 0.15 * k) + k * 0.37 + seed);
                    float head = smoothstep(0.0, 0.42, s) * (1.0 - smoothstep(0.42, 0.50, s));
                    float width = lerp(0.010, 0.030, s / 0.5);
                    float band = 1.0 - smoothstep(width * 0.4, width, abs(across - lane));
                    streaks = max(streaks, band * head);
                }
                float m = frac(along * 1.6 - t * 1.3);
                float y = (PlopHash12(float2(floor(along * 1.6 - t * 1.3), seed)) - 0.5) * 0.62;
                float mote = 1.0 - smoothstep(0.020, 0.040, length(float2((m - 0.5) * 0.62, across - y)));
                float alpha = (corridor + chevron * 0.62 + streaks * 0.48 + mote * 0.40) * fade * (0.45 + 0.55 * intensity);
                return half4(PlopOut(_PlopWindTint.rgb), saturate(alpha * _PlopWindTint.a));
            }
            ENDHLSL
        }
    }
}
