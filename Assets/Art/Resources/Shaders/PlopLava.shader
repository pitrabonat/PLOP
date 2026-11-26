// Phase 11.6 molten pool built from the supplied Lava art (Lava_Isolated). Open sides keep the painted
// cooled-stone rim; connected cells join into one pool (no stone between them). The molten area is replaced
// by a board-space flow of the art's own magma: a slow domain warp drags it, crack seams pulse with heat and
// a few bubbles rise and pop. Renderer colour: r = neighbour mask byte, g = seed byte, b = heat flash (0..1).
Shader "PLOP/Lava"
{
    Properties
    {
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ArtTex ("Art (isolated Lava tile)", 2D) = "white" {}
        _ArtRect ("Art content rect (uv centre, uv size)", Vector) = (0.5, 0.5, 0.84, 0.82)
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
            #define PLOP_ART_TILE
            #include "PlopCommon.hlsl"

            static const float Footprint = 0.94;
            static const float Squeeze = 0.50;     // connected sides sample |s| <= 0.25: inside the stones
            static const float Rim = 0.17;         // painted stone rim width in cells

            float Bubble(float2 p, float seed, float k, float t, out float ring)
            {
                float period = 3.1 + 1.9 * k + seed * 1.3;
                float phase = PlopHash12(float2(seed * 37.0, k * 11.0)) * period;
                float cycle = floor((t + phase) / period);
                float u = frac((t + phase) / period);
                float2 c = (PlopHash22(float2(seed * 53.0 + k * 7.0, cycle)) - 0.5) * 0.40;
                float dist = length(p - c);
                float grow = saturate(u / 0.20);
                float r = 0.016 + 0.036 * grow;
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

                float4 art = PlopArtTile(p, mask, Footprint, Squeeze, Rim);
                if (art.a <= 0.002) discard;

                // Molten pixels of the painting (orange/yellow; the stones are neutral grey-violet).
                float molten = saturate((art.r - art.b - 0.30) / 0.22);

                float t = _PlopTime;
                float2 b = i.board;
                float2 warp = float2(PlopFbm(b * 1.15 + float2(0.0, t * 0.10)), PlopFbm(b * 1.15 + float2(5.2, -t * 0.08))) - 0.5;
                float3 flow = PlopArtSheet(b * 0.85 + warp * 0.42, 0.24, float2(t * 0.030, t * 0.018)).rgb;
                // Heat pulses travel through the flow; bright crack seams breathe a little more.
                float heat = PlopFbm(b * 1.6 - float2(t * 0.17, t * 0.11));
                float bright = smoothstep(0.62, 0.86, dot(flow, float3(0.30, 0.59, 0.11)));
                flow *= 0.90 + 0.22 * heat;
                flow += float3(0.22, 0.11, 0.0) * bright * (0.55 + 0.45 * sin(t * 1.6 + heat * 6.2831));

                float depth = molten * saturate(1.0 - max(abs(p.x), abs(p.y)) * 1.4 + 0.3);
                for (int k = 0; k < 2; k++)
                {
                    float ring;
                    float bubble = Bubble(p, seed + 0.13 * k, k, t, ring) * depth;
                    flow = lerp(flow, float3(1.0, 0.80, 0.42), bubble);
                    flow = lerp(flow, float3(1.0, 0.95, 0.76), 0.6 * bubble * smoothstep(0.55, 1.0, bubble));
                    flow = lerp(flow, float3(1.0, 0.85, 0.45), ring * 0.65 * depth);
                }

                float3 col = lerp(art.rgb, flow, molten);
                // Warm light spills onto the stones next to the magma.
                col += float3(0.10, 0.04, 0.0) * (1.0 - molten) * saturate(art.r - art.b) * (0.6 + 0.4 * heat);
                col = lerp(col, col * 0.88, PlopSeam(p, mask) * 0.35 * molten);
                // Heat flash when a Plop touches the pool.
                col = lerp(col, float3(1.0, 0.88, 0.60), flash * 0.55 * molten);
                return half4(PlopOut(saturate(col)), art.a);
            }
            ENDHLSL
        }
    }
}
