// Living World particles (Phase 11.5): snow, embers, seeds, pollen, rain, stars and motes as one small static
// mesh of quads (at most a few dozen per World). The vertex shader moves each quad from its seed and the shared
// ambient clock (no per-object Update, no CPU particle simulation) and collapses any particle over the board or
// beyond the Battery Saver density to zero area, so only the particles' own pixels are ever shaded.
// Coordinates: backdrop pixel space (576 x 1024, y down); the mesh is a child of the backdrop sprite.
// Per-vertex data: uv0 = corner xy, spawn zw (0..1) · uv1 = size px, alpha, velocity px/s
//                  uv2 = sway px, phase, shape, tint · uv3 = band y0, y1 (0..1), twinkle speed, rank (0..1)
Shader "PLOP/AmbientParticles"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex ParticleVertex
            #pragma fragment ParticleFragment
            #include "PlopCommon.hlsl"

            float4 _PlopAmbient;       // x: time, y: density, z: moving, w: opacity
            float4 _PlopClear;         // board rect in cells (min xy, max zw)
            float4 _PlopArtSize;       // xy: backdrop sprite size in its local units
            float4 _PlopAmbientTint;
            float4 _PlopAmbientTint2;

            static const float2 Art = float2(576.0, 1024.0);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                float4 look : TEXCOORD1;    // x alpha, y shape, z tint, w twinkle
                float phase : TEXCOORD2;
            };

            float WrapRange(float v, float lo, float span) { return lo + (v - lo) - span * floor((v - lo) / span); }

            float3 ArtToLocal(float2 p)
            {
                return float3((p.x / Art.x - 0.5) * _PlopArtSize.x, (0.5 - p.y / Art.y) * _PlopArtSize.y, 0.0);
            }

            Varyings ParticleVertex(Attributes v)
            {
                Varyings o = (Varyings)0;
                float t = _PlopAmbient.x;
                float size = v.uv1.x;
                float2 velocity = v.uv1.zw;
                float sway = v.uv2.x, phase = v.uv2.y, shape = v.uv2.z;
                float y0 = v.uv3.x * Art.y, y1 = v.uv3.y * Art.y;
                float margin = size * 3.0 + 12.0;

                float2 p = v.uv0.zw * float2(Art.x, y1 - y0) + float2(0.0, y0) + velocity * t;
                p.x = WrapRange(p.x, -margin, Art.x + 2.0 * margin);
                p.y = WrapRange(p.y, y0 - margin, (y1 - y0) + 2.0 * margin);
                p.x += sin(t * (0.5 + phase) + phase * 6.2832) * sway;

                float2 corner = v.uv0.xy;
                float2 offset = corner * size;
                if (shape > 1.5 && shape < 2.5)   // streak: stretched along its velocity
                {
                    float2 dir = normalize(velocity + float2(0.0, 1e-3));
                    offset = dir * corner.y * size * 4.0 + float2(-dir.y, dir.x) * corner.x * size * 0.45;
                }

                // Clean centre and density: hide the whole particle (zero area, nothing shaded).
                float3 centreWS = TransformObjectToWorld(ArtToLocal(p));
                float2 b = (centreWS.xy - _PlopBoard.xy) * _PlopBoard.z;
                float2 outside = max(max(_PlopClear.xy - b, b - _PlopClear.zw), 0.0);
                float clear = (_PlopClear.z > _PlopClear.x) ? smoothstep(0.0, 0.9, length(outside)) : 1.0;
                float band = 1.0;
                if (y1 - y0 < Art.y - 1.0) band = smoothstep(y0, y0 + 40.0, p.y) * (1.0 - smoothstep(y1 - 40.0, y1, p.y));
                float alpha = v.uv1.y * clear * band * _PlopAmbient.w;
                bool hidden = alpha <= 0.002 || v.uv3.w > _PlopAmbient.y;

                float3 ws = TransformObjectToWorld(ArtToLocal(p + offset));
                o.positionCS = hidden ? float4(-4.0, -4.0, 0.0, 1.0) : TransformWorldToHClip(ws);
                o.corner = corner;
                o.look = float4(alpha, shape, v.uv2.w, v.uv3.z);
                o.phase = phase;
                return o;
            }

            half4 ParticleFragment(Varyings i) : SV_Target
            {
                float2 c = i.corner;
                float r = length(c);
                int shape = (int)round(i.look.y);
                float a;
                float3 col = i.look.z > 0.5 ? _PlopAmbientTint2.rgb : _PlopAmbientTint.rgb;
                if (shape == 0)        a = 1.0 - smoothstep(0.35, 1.0, r);                                   // dot / mote / ember
                else if (shape == 1)                                                                          // star / glint
                {
                    float2 d = abs(c);
                    a = (1.0 - smoothstep(0.12, 0.38, r))
                      + 0.7 * (1.0 - smoothstep(0.0, 0.09, min(d.x, d.y))) * (1.0 - smoothstep(0.3, 1.0, max(d.x, d.y)));
                }
                else if (shape == 2)   a = (1.0 - smoothstep(0.3, 1.0, abs(c.x))) * (1.0 - smoothstep(0.55, 1.0, abs(c.y)));  // rain streak
                else if (shape == 3)   a = (1.0 - smoothstep(0.85, 1.0, r)) * (0.45 + 0.55 * smoothstep(0.5, 0.95, r)); // bokeh
                else if (shape == 4)                                                                          // seed puff
                {
                    float ang = atan2(c.y, c.x);
                    a = (1.0 - smoothstep(0.14, 0.28, r)) + (1.0 - smoothstep(0.45, 1.0, r)) * (0.30 + 0.30 * cos(ang * 8.0));
                }
                else if (shape == 5)                                                                          // snowflake
                {
                    float ang = atan2(c.y, c.x);
                    float arms = 1.0 - smoothstep(0.04, 0.13, abs(sin(ang * 3.0)) * r);
                    a = max(arms * (1.0 - smoothstep(0.7, 1.0, r)), 1.0 - smoothstep(0.12, 0.32, r));
                }
                else                                                                                          // water drop
                {
                    float hl = 1.0 - smoothstep(0.0, 0.28, length(c - float2(-0.3, 0.35)));
                    col = lerp(col, float3(1, 1, 1), hl);
                    a = (1.0 - smoothstep(0.75, 1.0, r)) * (0.55 + 0.45 * hl);
                }
                if (i.look.w > 0.0) a *= 0.35 + 0.65 * (0.5 + 0.5 * sin(_PlopAmbient.x * i.look.w + i.phase * 40.0));
                return half4(PlopOut(col), saturate(a) * i.look.x);
            }
            ENDHLSL
        }
    }
}
