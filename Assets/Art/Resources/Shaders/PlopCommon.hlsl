#ifndef PLOP_COMMON_INCLUDED
#define PLOP_COMMON_INCLUDED

// Shared helpers for PLOP's procedural board shaders (Lava, Frost, Loops, Breeze wind).
// Everything is analytic: no textures, no lights, no per-object material state. Per-renderer data
// travels in the SpriteRenderer colour (decoded as bytes), so all instances share one material.

#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

// Colours below are authored in sRGB (like every PLOP palette value). In a Linear project they are
// converted once at output. Per-renderer bytes arrive pre-compensated by PlopShaders.Pack (C#).
float3 PlopOut(float3 srgb)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return srgb;
#else
    return SRGBToLinear(saturate(srgb));
#endif
}

float _PlopTime;        // seconds, wrapped; frozen under Reduced Motion (set by PlopShaders.cs)
float4 _PlopBoard;      // xy: world position of cell (0,0) centre, z: cells per world unit

struct PlopAttributes
{
    float3 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct PlopVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
    float2 board : TEXCOORD1;   // board space in cells: cell (x,y) centre = (x,y)
    float2 world : TEXCOORD2;   // world XY (living-background regions map it to backdrop pixels)
    UNITY_VERTEX_OUTPUT_STEREO
};

PlopVaryings PlopVertex(PlopAttributes v)
{
    PlopVaryings o = (PlopVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    SetUpSpriteInstanceProperties();
    float3 ws = TransformObjectToWorld(v.positionOS);
    o.positionCS = TransformWorldToHClip(ws);
    o.uv = v.uv;
    o.color = (float4)v.color * (float4)unity_SpriteColor;
    o.board = (ws.xy - _PlopBoard.xy) * _PlopBoard.z;
    o.world = ws.xy;
    return o;
}

uint PlopByte(float channel) { return (uint)round(saturate(channel) * 255.0); }

float PlopHash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 PlopHash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float PlopNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(PlopHash12(i), PlopHash12(i + float2(1, 0)), u.x),
                lerp(PlopHash12(i + float2(0, 1)), PlopHash12(i + float2(1, 1)), u.x), u.y);
}

float PlopFbm(float2 p)
{
    float sum = 0.0, amp = 0.5;
    [unroll] for (int i = 0; i < 3; i++)
    {
        sum += amp * PlopNoise(p);
        p = p * 2.03 + float2(17.1, 9.2);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// x: distance to the nearest feature point, y: distance to the cell border (F2 - F1), z: cell id hash.
float3 PlopVoronoi(float2 p, float t)
{
    float2 n = floor(p), f = frac(p);
    float md = 8.0, md2 = 8.0, id = 0.0;
    [unroll] for (int j = -1; j <= 1; j++)
    [unroll] for (int i = -1; i <= 1; i++)
    {
        float2 g = float2(i, j);
        float2 o = PlopHash22(n + g);
        o = 0.5 + 0.38 * sin(t + 6.2831 * o);
        float2 r = g + o - f;
        float d = dot(r, r);
        if (d < md) { md2 = md; md = d; id = PlopHash12(n + g); }
        else if (d < md2) md2 = d;
    }
    return float3(sqrt(md), sqrt(md2) - sqrt(md), id);
}

// Mask bits (8 neighbours of the same surface): 1 N, 2 E, 4 S, 8 W, 16 NE, 32 SE, 64 SW, 128 NW.
// Signed distance (cell units, p = uv - 0.5) to a connected pool of rounded tiles. Each quadrant of the
// cell is solved on its own: a rounded outer corner, a straight run, a filleted inner corner or solid fill.
// Internal edges vanish, so neighbouring cells read as one surface; the outline matches the floor tiles.
float PlopConnectedSdf(float2 p, uint mask, float e, float r)
{
    float sx = p.x >= 0.0 ? 1.0 : -1.0;
    float sy = p.y >= 0.0 ? 1.0 : -1.0;
    float2 q = float2(p.x * sx, p.y * sy);
    uint hBit = sx > 0.0 ? 2u : 8u;
    uint vBit = sy > 0.0 ? 1u : 4u;
    uint dBit = sx > 0.0 ? (sy > 0.0 ? 16u : 32u) : (sy > 0.0 ? 128u : 64u);
    bool h = (mask & hBit) != 0u;
    bool v = (mask & vBit) != 0u;
    bool d = (mask & dBit) != 0u;
    if (h && v)
    {
        if (d) return -1.0;
        // Inner corner: the notch {x > e, y > e} rounded so the fillet ends exactly at the cell edge.
        float rin = 0.5 - e;
        float2 a = (e + rin) - q;
        float notch = length(max(a, 0.0)) + min(max(a.x, a.y), 0.0);
        return rin - notch;
    }
    if (h) return q.y - e;
    if (v) return q.x - e;
    float2 c = q - (e - r);
    return length(max(c, 0.0)) + min(max(c.x, c.y), 0.0) - r;
}

// Outward normal of the pool outline (finite differences; only meaningful near the rim).
float2 PlopConnectedNormal(float2 p, uint mask, float e, float r)
{
    const float h = 0.004;
    float dx = PlopConnectedSdf(p + float2(h, 0), mask, e, r) - PlopConnectedSdf(p - float2(h, 0), mask, e, r);
    float dy = PlopConnectedSdf(p + float2(0, h), mask, e, r) - PlopConnectedSdf(p - float2(0, h), mask, e, r);
    float2 g = float2(dx, dy);
    float len = length(g);
    return len > 1e-5 ? g / len : float2(0, 1);
}

// Faint line on an internal cell edge so the grid stays countable inside a connected pool.
float PlopSeam(float2 p, uint mask)
{
    float s = 0.0;
    if ((mask & 2u) != 0u) s = max(s, 1.0 - smoothstep(0.0, 0.010, 0.5 - p.x));
    if ((mask & 8u) != 0u) s = max(s, 1.0 - smoothstep(0.0, 0.010, p.x + 0.5));
    if ((mask & 1u) != 0u) s = max(s, 1.0 - smoothstep(0.0, 0.010, 0.5 - p.y));
    if ((mask & 4u) != 0u) s = max(s, 1.0 - smoothstep(0.0, 0.010, p.y + 0.5));
    return s;
}

// ---------------------------------------------------------------- Phase 11.6: supplied art tiles
// The art-driven surfaces (Lava, Frost) sample one isolated hand-painted tile (_ArtTex). _ArtRect maps
// "content" coordinates s (the opaque box, -0.5..0.5) to texture uv: uv = _ArtRect.xy + s * _ArtRect.zw.
#if defined(PLOP_ART_TILE)
TEXTURE2D(_ArtTex);
SAMPLER(sampler_ArtTex);
CBUFFER_START(UnityPerMaterial)
    float4 _ArtRect;
    float4 _ArtTex_TexelSize;
CBUFFER_END

// Straight-alpha art colour in sRGB (the shader maths, like the palette, works in sRGB).
float4 PlopArt(float2 s)
{
    float4 c = SAMPLE_TEXTURE2D_LOD(_ArtTex, sampler_ArtTex, _ArtRect.xy + s * _ArtRect.zw, 0);
#if !defined(UNITY_COLORSPACE_GAMMA)
    c.rgb = LinearToSRGB(c.rgb);
#endif
    return c;
}

// Blob-tile remap of one isolated tile. p: cell coordinates (-0.5..0.5); footprint: opaque width in cells;
// squeeze: content units per cell along connected sides (keeps them inside the art's interior).
// Each quadrant of the cell is solved on its own: an open side keeps the art's real rim, a connected side
// samples the interior, so rims never repeat between joined cells. At an inner corner (both sides joined,
// diagonal open) a rim "nub" continues the two neighbours' rims around the corner.
float4 PlopArtTile(float2 p, uint mask, float footprint, float squeeze, float rim)
{
    float sx = p.x >= 0.0 ? 1.0 : -1.0;
    float sy = p.y >= 0.0 ? 1.0 : -1.0;
    float2 q = abs(p);
    uint hBit = sx > 0.0 ? 2u : 8u;
    uint vBit = sy > 0.0 ? 1u : 4u;
    uint dBit = sx > 0.0 ? (sy > 0.0 ? 16u : 32u) : (sy > 0.0 ? 128u : 64u);
    bool h = (mask & hBit) != 0u;
    bool v = (mask & vBit) != 0u;
    bool d = (mask & dBit) != 0u;
    float2 s = float2(h ? q.x * squeeze : q.x / footprint, v ? q.y * squeeze : q.y / footprint);
    float4 c = PlopArt(s * float2(sx, sy));
    if (h && v && !d)
    {
        float2 o = 0.5 - q;                         // distance to the shared corner
        float gap = 0.5 - 0.5 * footprint;          // the open diagonal keeps the usual tile gap
        float rf = rim * 0.55;
        float inner = length(max(o - (rim - rf), 0.0)) - rf;
        float aa = max(fwidth(inner), 1e-4);
        float nub = saturate(0.5 - inner / aa);
        // Sample the top/bottom rim band so it lines up with the horizontal neighbour's rim.
        float2 ns = float2(q.x * squeeze, min((0.5 - o.y) / footprint, 0.485));
        float4 n = PlopArt(ns * float2(sx, sy));
        c = lerp(c, float4(n.rgb, 1.0), nub * n.a);
        // The open diagonal keeps a rounded gap, like the corner between two separate tiles.
        float g = length(max(o - (gap - 0.02), 0.0)) - 0.02;
        c.a *= saturate(g / max(fwidth(g), 1e-4) + 0.5);
    }
    return c;
}

// Board-space sample of the art's interior: a mirrored (triangle-wave) tiling, continuous across cells.
float4 PlopArtInterior(float2 board, float extent)
{
    float2 tri = abs(frac(board * 0.5) * 2.0 - 1.0);
    return PlopArt((tri - 0.5) * 2.0 * extent);
}

// Seamless interior without visible mirror axes: two mirrored layers at unrelated angles and scales, each
// gently domain-warped, blended by low-frequency noise. Continuous everywhere (no per-cell seams).
float4 PlopArtSheet(float2 board, float extent, float2 drift)
{
    float2 w = float2(PlopFbm(board * 0.9 + 1.7), PlopFbm(board * 0.9 + 8.3)) - 0.5;
    const float2x2 r1 = float2x2(0.921, -0.391, 0.391, 0.921);     // 23 degrees
    const float2x2 r2 = float2x2(0.485, 0.875, -0.875, 0.485);     // -61 degrees
    float4 a = PlopArtInterior(mul(r1, board) * 0.78 + w * 0.30 + drift, extent);
    float4 b = PlopArtInterior(mul(r2, board) * 0.91 + 3.7 - w * 0.30 - drift * 0.7, extent);
    float m = smoothstep(0.30, 0.70, PlopNoise(board * 0.62 + 2.9));
    return lerp(a, b, m);
}
#endif

// Composite a surface (colour, coverage) over its soft contact shadow.
float4 PlopOverShadow(float3 col, float cover, float shadow, float3 shadowCol)
{
    float a = cover + (1.0 - cover) * shadow;
    float3 rgb = (col * cover + shadowCol * shadow * (1.0 - cover)) / max(a, 1e-4);
    return float4(PlopOut(rgb), a);
}

#endif
