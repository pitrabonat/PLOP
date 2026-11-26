// Translucent ice: crystalline facets with depth, a few hairline cracks, frosted rim and a slow glint.
// Connected Frost cells share one outline with faint seams that keep the grid countable.
// Renderer colour: r = neighbour mask byte, g = seed byte, b = shimmer (0..1, a Plop slid across).
Shader "PLOP/Frost"
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
            #pragma fragment FrostFragment
            #pragma multi_compile_instancing
            #include "PlopCommon.hlsl"

            half4 FrostFragment(PlopVaryings i) : SV_Target
            {
                uint mask = PlopByte(i.color.r);
                float shimmer = i.color.b;
                float2 p = i.uv - 0.5;
                const float E = 0.47, R = 0.188;

                float sd = PlopConnectedSdf(p, mask, E, R);
                float aa = max(fwidth(sd), 1e-4);
                float cover = saturate(0.5 - sd / aa);
                float shadow = 0.26 * (1.0 - smoothstep(-0.01, 0.03, PlopConnectedSdf(p - float2(0.010, -0.020), mask, E, R)));
                if (cover <= 0.0 && shadow <= 0.0) discard;

                float2 b = i.board;
                float t = _PlopTime;
                float inward = -sd;
                float2 n = PlopConnectedNormal(p, mask, E, R);
                float lit = saturate(0.5 + 0.5 * dot(n, float2(-0.57, 0.82)));

                float3 deep = float3(0.55, 0.74, 0.88);
                float3 surface = float3(0.86, 0.94, 0.99);
                float diffuse = saturate(0.55 + 0.20 * (p.y - p.x) + 0.25 * (PlopNoise(b * 1.7) - 0.5));
                float3 col = lerp(deep, surface, diffuse);

                // Facets: flat-shaded crystal cells, brighter on one side of each facet edge.
                float3 vor = PlopVoronoi(b * 2.6 + 3.7, 0.0);
                col *= 0.955 + 0.07 * vor.z;
                float facetEdge = 1.0 - smoothstep(0.0, 0.016, vor.y);
                col = lerp(col, float3(0.97, 0.99, 1.0), facetEdge * 0.26 * saturate(inward / 0.10));
                // Translucent depth: a cooler core under the surface.
                col = lerp(col, deep, 0.18 * smoothstep(0.08, 0.35, inward) * (0.5 + 0.5 * PlopNoise(b * 1.3 + 7.0)));

                // Hairline cracks on a few edges of a coarser cell pattern.
                float3 crackCells = PlopVoronoi(b * 1.35 + 11.3, 0.0);
                float crack = (1.0 - smoothstep(0.0, 0.010, crackCells.y)) * step(0.62, PlopHash12(floor(b * 1.35 + 11.3) + crackCells.z));
                col = lerp(col, float3(0.62, 0.78, 0.90), crack * 0.45 * saturate(inward / 0.08));

                // Depth: the lower right sinks into deeper blue, the upper left rim catches the light.
                float edge = 1.0 - smoothstep(0.0, 0.09, inward);
                col = lerp(col, deep * 0.94, edge * (1.0 - lit) * 0.55);
                col = lerp(col, float3(1.0, 1.0, 1.0), edge * lit * 0.70);
                // Frosted rim: fine powdery grain right at the outline.
                float frost = (1.0 - smoothstep(0.0, 0.045, inward)) * (0.55 + 0.45 * PlopNoise(b * 26.0));
                col = lerp(col, float3(0.96, 0.98, 1.0), frost * 0.55);

                // Slow diagonal glint and a brief shimmer after a slide.
                float g = frac((b.x + b.y) * 0.21 - t * 0.055);
                float glint = exp(-pow((g - 0.5) * 30.0, 2.0)) * 0.10 * saturate(inward / 0.12);
                col += glint + shimmer * 0.20 * saturate(inward / 0.10) * (0.6 + 0.4 * PlopNoise(b * 9.0 + t));
                col = lerp(col, float3(0.70, 0.83, 0.93), PlopSeam(p, mask) * 0.6);

                float4 o = PlopOverShadow(saturate(col), cover * 0.97, shadow, float3(0.33, 0.42, 0.52));
                return half4(o);
            }
            ENDHLSL
        }
    }
}
