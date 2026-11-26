// Phase 11.6 ice built from the supplied Frost art (Frost_Isolated). Open sides keep the painted bevel and
// snow caps; connected cells become one continuous sheet of the art's own cracked ice (sampled in board
// space, so cracks run on across cells). A slow glint and a few twinkles move over it; a brief shimmer
// follows a slide. Faint seams keep the grid countable.
// Renderer colour: r = neighbour mask byte, g = seed byte, b = shimmer (0..1, a Plop slid across).
Shader "PLOP/Frost"
{
    Properties
    {
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ArtTex ("Art (isolated Frost tile)", 2D) = "white" {}
        _ArtRect ("Art content rect (uv centre, uv size)", Vector) = (0.5, 0.5, 0.80, 0.79)
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
            #define PLOP_ART_TILE
            #include "PlopCommon.hlsl"

            static const float Footprint = 0.94;
            static const float Squeeze = 0.56;
            static const float Rim = 0.10;

            half4 FrostFragment(PlopVaryings i) : SV_Target
            {
                uint mask = PlopByte(i.color.r);
                float shimmer = i.color.b;
                float2 p = i.uv - 0.5;

                float4 art = PlopArtTile(p, mask, Footprint, Squeeze, Rim);
                if (art.a <= 0.002) discard;

                // Interior of the sheet: distance from every *open* edge (connected edges have no bevel).
                float2 q = abs(p);
                float ex = ((mask & (p.x >= 0.0 ? 2u : 8u)) != 0u) ? 1.0 : 0.5 * Footprint - q.x;
                float ey = ((mask & (p.y >= 0.0 ? 1u : 4u)) != 0u) ? 1.0 : 0.5 * Footprint - q.y;
                float inward = min(ex, ey);
                float interior = smoothstep(0.06, 0.13, inward);

                float2 b = i.board;
                float t = _PlopTime;
                float3 ice = PlopArtSheet(b, 0.28, 0.0).rgb;
                float3 col = lerp(art.rgb, ice, interior * 0.85);

                // Slow diagonal glint, a few twinkles and the post-slide shimmer.
                float g = frac((b.x + b.y) * 0.21 - t * 0.055);
                col += exp(-pow((g - 0.5) * 30.0, 2.0)) * 0.12 * interior;
                float2 cell = floor(b * 3.0);
                float tw = PlopHash12(cell + 17.0);
                float2 at = (cell + 0.25 + 0.5 * PlopHash22(cell)) / 3.0;
                float phase = frac(t * 0.23 + tw * 7.0);
                float star = (1.0 - smoothstep(0.0, 0.018, length(b - at))) * step(0.80, tw) * sin(3.14159 * saturate(phase * 4.0));
                col += star * 0.55 * interior;
                col += shimmer * 0.20 * interior * (0.6 + 0.4 * PlopNoise(b * 9.0 + t));
                col = lerp(col, col * 0.86, PlopSeam(p, mask) * 0.5);
                return half4(PlopOut(saturate(col)), art.a);
            }
            ENDHLSL
        }
    }
}
