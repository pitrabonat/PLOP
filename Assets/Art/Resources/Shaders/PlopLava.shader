// Stylised molten pool: domain-warped flow, drifting crust plates with glowing cracks, occasional
// bubbles and a cooled-stone rim. Connected Lava cells share one outline (see PlopConnectedSdf).
// Renderer colour: r = neighbour mask byte, g = seed byte, b = heat flash (0..1).
Shader "PLOP/Lava"
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
            #pragma fragment LavaFragment
            #pragma multi_compile_instancing
            #include "PlopCommon.hlsl"

            float Bubble(float2 p, float seed, float k, float t, out float ring)
            {
                float period = 3.1 + 1.9 * k + seed * 1.3;
                float phase = PlopHash12(float2(seed * 37.0, k * 11.0)) * period;
                float cycle = floor((t + phase) / period);
                float u = frac((t + phase) / period);
                float2 c = (PlopHash22(float2(seed * 53.0 + k * 7.0, cycle)) - 0.5) * 0.46;
                float dist = length(p - c);
                float grow = saturate(u / 0.20);
                float r = 0.018 + 0.042 * grow;
                float body = (u < 0.20) ? 1.0 - smoothstep(r - 0.008, r, dist) : 0.0;
                float pop = saturate((u - 0.20) / 0.09);
                ring = (u >= 0.20 && u < 0.29) ? (1.0 - smoothstep(0.0, 0.010, abs(dist - (r + 0.05 * pop)))) * (1.0 - pop) : 0.0;
                return body * (0.35 + 0.65 * smoothstep(0.0, 0.6, (c.y - p.y + r) / (2.0 * r)));
            }

            half4 LavaFragment(PlopVaryings i) : SV_Target
            {
                uint mask = PlopByte(i.color.r);
                float seed = PlopByte(i.color.g) / 255.0;
                float flash = i.color.b;
                float2 p = i.uv - 0.5;
                const float E = 0.47, R = 0.188;

                float sd = PlopConnectedSdf(p, mask, E, R);
                float aa = max(fwidth(sd), 1e-4);
                float cover = saturate(0.5 - sd / aa);
                float shadow = 0.32 * (1.0 - smoothstep(-0.01, 0.035, PlopConnectedSdf(p - float2(0.012, -0.024), mask, E, R)));
                if (cover <= 0.0 && shadow <= 0.0) discard;

                float t = _PlopTime;
                float2 b = i.board;
                // Flow: a slow domain warp drags the whole pool, so neighbouring cells move as one.
                float2 w = float2(PlopFbm(b * 1.1 + float2(0.0, t * 0.10)), PlopFbm(b * 1.1 + float2(5.2, -t * 0.08)));
                float molten = PlopFbm(b * 2.2 + w * 1.9 + float2(t * 0.05, t * 0.03));
                float streak = sin((b.x * 0.8 + b.y * 0.6) * 9.0 + molten * 7.0 - t * 1.1);

                float3 deep = float3(0.55, 0.12, 0.07);
                float3 red = float3(0.86, 0.25, 0.09);
                float3 orange = float3(0.98, 0.52, 0.15);
                float3 yellow = float3(1.00, 0.82, 0.40);
                float heat = saturate(molten * 1.3 - 0.20 + 0.08 * streak);
                float3 col = lerp(red, orange, smoothstep(0.18, 0.60, heat));
                col = lerp(col, yellow, smoothstep(0.62, 0.92, heat) * 0.9);

                // Cooled crust only drifts over part of the pool; its seams glow.
                float cooled = smoothstep(0.46, 0.66, PlopFbm(b * 0.85 + float2(-t * 0.025, t * 0.018) + 3.1));
                float3 vor = PlopVoronoi(b * 1.7 + (w - 0.5) * 1.3 + float2(t * 0.04, -t * 0.02), t * 0.3);
                float plate = smoothstep(0.05, 0.15, vor.y) * cooled;
                float3 crust = lerp(deep, red * 0.78, 0.25 + 0.45 * vor.z);
                col = lerp(col, crust, plate * 0.82);
                float seamGlow = (1.0 - smoothstep(0.0, 0.10, vor.y)) * cooled;
                col = lerp(col, lerp(orange, yellow, 0.55), seamGlow * 0.55);

                float depth = saturate(-sd / 0.16);
                for (int k = 0; k < 2; k++)
                {
                    float ring;
                    float bubble = Bubble(p, seed + 0.13 * k, k, t, ring) * depth * (1.0 - plate);
                    col = lerp(col, lerp(orange, yellow, 0.6), bubble);
                    col = lerp(col, float3(1.0, 0.94, 0.74), 0.7 * bubble * smoothstep(0.55, 1.0, bubble));
                    col = lerp(col, yellow, ring * 0.65 * depth);
                }
                // Amber glow toward the middle of the pool.
                col += float3(0.30, 0.14, 0.02) * smoothstep(0.10, 0.45, -sd) * (1.0 - plate) * 0.6;

                // Cooled stone rim with a warm inner lip; lit from the upper left like every board piece.
                float inward = -sd;
                float2 n = PlopConnectedNormal(p, mask, E, R);
                float lit = saturate(0.5 + 0.5 * dot(n, float2(-0.57, 0.82)));
                float3 stone = lerp(float3(0.45, 0.33, 0.31), float3(0.80, 0.66, 0.58), lit);
                stone *= 0.92 + 0.10 * PlopNoise(b * 30.0);
                float rim = 1.0 - smoothstep(0.050, 0.072, inward);
                float lip = smoothstep(0.050, 0.072, inward) * (1.0 - smoothstep(0.072, 0.15, inward));
                col = lerp(col, deep * 0.8, lip * 0.35);
                col += float3(0.45, 0.16, 0.03) * lip * 0.35;
                col = lerp(col, stone, rim);
                col = lerp(col, col * 0.86, PlopSeam(p, mask) * 0.55);

                // Heat flash when a Plop touches the pool.
                col = lerp(col, float3(1.0, 0.88, 0.60), flash * 0.55 * (1.0 - rim));

                float4 o = PlopOverShadow(col, cover, shadow, float3(0.30, 0.16, 0.14));
                return half4(o);
            }
            ENDHLSL
        }
    }
}
