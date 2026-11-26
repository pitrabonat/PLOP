// Living World backgrounds (Phase 11.5): animated mid-layer props (windmill sails, swaying grass, a slow glow,
// light rays, a lamp beam, drifting clouds) drawn only inside small region quads listed per World in
// Resources/Worlds/Ambient.json, never full screen. Particles are a separate mesh (PlopAmbientParticles).
// Coordinates are the authored backdrop's pixel space (576 x 1024, y down) so props line up with painted ones;
// _PlopArtRect maps world XY to it. Everything fades out over the board (_PlopClear, in board cells).
// Renderer colour: r = World mode byte. Motion comes from _PlopAmbient.x only; _PlopAmbient.w is the opacity.
Shader "PLOP/Ambient"
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
            #pragma fragment AmbientFragment
            #pragma multi_compile_instancing
            #include "PlopCommon.hlsl"

            float4 _PlopAmbient;   // x: ambient time (s), y: density scale (Battery Saver < 1), z: 1 = moving
            float4 _PlopClear;     // board rect in cells: xy min, zw max (incl. tray padding)
            float4 _PlopArtRect;   // backdrop: x world min X, y world max Y, z/w backdrop pixels per world unit
            float4 _PlopAmbientTint;   // primary particle colour (sRGB)
            float4 _PlopAmbientTint2;  // secondary colour (sRGB)

            static const float2 ArtSize = float2(576.0, 1024.0);

            struct Acc { float3 pc; float a; };

            void Over(inout Acc acc, float3 c, float a)
            {
                a = saturate(a);
                acc.pc = acc.pc * (1.0 - a) + c * a;
                acc.a = acc.a * (1.0 - a) + a;
            }

            float Capsule(float2 p, float2 a, float2 b, float r)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h) - r;
            }

            void Windmill(inout Acc acc, float2 q, float2 hub, float s, float angle)
            {
                float2 p = q - hub;
                if (dot(p, p) > 3600.0 * s * s) return;   // outside the sail circle: nothing to draw
                float spar = 1e5, cloth = 0.0, lattice = 0.0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float a = radians(angle + k * 90.0);
                    float2 dir = float2(cos(a), sin(a));
                    float along = dot(p, dir), across = dot(p, float2(-dir.y, dir.x));
                    // Wooden spar, and a lattice sail frame on one side of it (a windmill, never a fan).
                    spar = min(spar, Capsule(p, float2(0, 0), dir * 56.0 * s, 1.7 * s));
                    float2 b = float2(along - 35.0 * s, across - 7.5 * s);
                    float2 e = abs(b) - float2(20.0 * s, 6.5 * s);
                    float box = length(max(e, 0.0)) + min(max(e.x, e.y), 0.0) - 0.8 * s;
                    float inSail = 1.0 - smoothstep(-0.6, 0.6, box);
                    cloth = max(cloth, inSail);
                    float bars = 1.0 - smoothstep(0.35, 1.0, abs(frac(along / (8.0 * s)) - 0.5) * 8.0 * s);
                    lattice = max(lattice, inSail * bars);
                }
                Over(acc, lerp(float3(1.0, 0.965, 0.902), float3(0.851, 0.776, 0.667), lattice * 0.85), cloth * 0.92);
                Over(acc, float3(0.80, 0.66, 0.52), 1.0 - smoothstep(-0.6, 0.6, spar));
                float hubD = length(p) - 7.0 * s;
                Over(acc, lerp(float3(0.851, 0.706, 0.561), float3(0.714, 0.561, 0.439), saturate(p.y / (7.0 * s) * 0.5 + 0.5)),
                     1.0 - smoothstep(-0.6, 0.6, hubD));
            }

            half4 AmbientFragment(PlopVaryings i) : SV_Target
            {
                // Clean centre: nothing over the board or its tray.
                float2 lo = _PlopClear.xy, hi = _PlopClear.zw;
                float2 outside = max(max(lo - i.board, i.board - hi), 0.0);
                float clear = smoothstep(0.0, 0.9, length(outside));
                if (hi.x > lo.x && clear <= 0.001) return half4(0, 0, 0, 0);
                if (hi.x <= lo.x) clear = 1.0;

                uint mode = PlopByte(i.color.r);
                float opacity = _PlopAmbient.w;
                float t = _PlopAmbient.x;
                float2 q = float2((i.world.x - _PlopArtRect.x) * _PlopArtRect.z, (_PlopArtRect.y - i.world.y) * _PlopArtRect.w);
                float3 tint = _PlopAmbientTint.rgb, tint2 = _PlopAmbientTint2.rgb;
                Acc acc; acc.pc = 0; acc.a = 0;

                if (mode == 2u)
                {
                    // Breezy Meadow: turning sails on the painted towers, and grass swaying along the bottom.
                    if (q.y < 260.0)
                    {
                        Windmill(acc, q, float2(470.0, 90.0), 1.0, 18.0 + t * 20.0);
                        Windmill(acc, q, float2(380.0, 140.0), 0.55, 52.0 + t * 26.0);
                    }
                    else
                    {
                        float col = floor(q.x / 29.0);
                        float h = PlopHash12(float2(col, 3.0));
                        float baseX = (col + 0.5) * 29.0;
                        float height = 34.0 + h * 26.0;
                        float lean = sin(t * 1.1 + col * 0.45) * 7.0 + (h - 0.5) * 8.0;
                        float d = Capsule(q, float2(baseX, 1030.0), float2(baseX + lean, 1030.0 - height),
                                          lerp(3.6, 1.2, saturate((1030.0 - q.y) / height)));
                        float blade = 1.0 - smoothstep(-0.6, 0.6, d);
                        Over(acc, lerp(tint2, float3(0.62, 0.78, 0.53), saturate((1030.0 - q.y) / height)), blade * 0.9);
                    }
                }
                else if (mode == 3u)
                {
                    // Ember Hollow: a slow, deep glow along the bottom edge. No crust, cells or bright orange
                    // surface, so it never reads as Lava.
                    float glow = exp(-max(0.0, ArtSize.y - q.y - 30.0 - 14.0 * sin(q.x * 0.018 + t * 0.35)) / 85.0);
                    Over(acc, tint2, glow * (0.20 + 0.08 * sin(t * 0.5)));
                }
                else if (mode == 4u)
                {
                    // Prism Observatory: slow pastel light rays from the dome.
                    float2 d = q - float2(288.0, -60.0);
                    float a = atan2(d.x, d.y);
                    float rays = pow(saturate(0.5 + 0.5 * sin(a * 9.0 + t * 0.12)), 6.0) * exp(-length(d) / 420.0);
                    float3 hue = 0.5 + 0.5 * cos(6.2832 * (a * 0.35 + t * 0.02 + float3(0.0, 0.33, 0.67)));
                    Over(acc, lerp(float3(1, 1, 1), hue, 0.45), rays * 0.22 * (1.0 - smoothstep(330.0, 400.0, q.y)));
                }
                else if (mode == 7u)
                {
                    // Twilight Attic: a soft beam from the lamp, breathing slowly.
                    float2 dir = float2(-0.3554, 0.9347);
                    float2 d = q - float2(463.0, 60.0);
                    float along = dot(d, dir), across = abs(dot(d, float2(-dir.y, dir.x)));
                    float beam = step(0.0, along) * (1.0 - smoothstep(0.0, 40.0 + along * 0.30, across))
                               * (1.0 - smoothstep(360.0, 540.0, along));
                    Over(acc, tint2, beam * (0.24 + 0.04 * sin(t * 0.4)));
                }
                else if (mode == 8u)
                {
                    // Moonlit Clouds: soft clouds drifting slowly across two bands.
                    float2 cp = float2((q.x + t * 7.0) / 170.0, q.y / 80.0);
                    float band = exp(-pow((q.y - 230.0) / 90.0, 2.0)) + exp(-pow((q.y - 880.0) / 70.0, 2.0));
                    float cloud = smoothstep(0.52, 0.72, PlopFbm(cp)) * band;
                    Over(acc, tint2, cloud * 0.40);
                }

                float alpha = acc.a * clear * opacity;
                float3 rgb = acc.pc / max(acc.a, 1e-4);
                return half4(PlopOut(rgb), alpha);
            }
            ENDHLSL
        }
    }
}
