// Loop portal: rounded porcelain frame, luminous ring, swirling vortex with depth and orbiting motes.
// Pair identity never relies on colour alone: each pair has 1–6 beads on the frame and its own dash count
// on the rotating inner ring. Renderer colour: r = activation flash, g = blocked fizzle, b = pair byte.
Shader "PLOP/Portal"
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
            #pragma fragment PortalFragment
            #pragma multi_compile_instancing
            #include "PlopCommon.hlsl"

            float4 _PlopPortalTints[6];
            float4 _PlopPortalInk;

            half4 PortalFragment(PlopVaryings i) : SV_Target
            {
                float flash = i.color.r;
                float blocked = i.color.g;
                uint pair = min(PlopByte(i.color.b), 5u);
                float3 tint = _PlopPortalTints[pair].rgb;
                float2 p = i.uv - 0.5;
                float r = length(p);
                float a = atan2(p.y, p.x);
                float t = _PlopTime;
                float aa = max(fwidth(r), 1e-4);

                const float Outer = 0.405, Inner = 0.300;
                float shadow = 0.30 * (1.0 - smoothstep(Outer - 0.02, Outer + 0.03, length(p - float2(0.012, -0.024))));
                float frameCover = saturate((Outer - r) / aa + 0.5);
                float halo = flash * 0.55 * (1.0 - smoothstep(Outer, 0.50, r)) * step(Outer, r);
                if (frameCover <= 0.0 && shadow <= 0.0 && halo <= 0.0) discard;

                // Frame: a rounded torus lit from the upper left.
                float band = saturate((r - Inner) / (Outer - Inner));
                float2 nrm = r > 1e-4 ? p / r : float2(0, 1);
                float bulge = (band - 0.5) * 2.0;
                float lit = saturate(0.5 + 0.5 * dot(nrm * bulge, float2(-0.57, 0.82)) + 0.25 * (1.0 - abs(bulge)));
                float3 porcelain = lerp(tint * 0.78, lerp(tint, float3(1, 1, 1), 0.62), lit);
                float3 col = porcelain;

                // Pair beads on the frame (1–6), evenly spaced from the top.
                float n = (float)(pair + 1u);
                float seg = 6.2831853 / n;
                float k = round((a - 1.5707963) / seg);
                float ba = 1.5707963 + k * seg;
                float2 bc = float2(cos(ba), sin(ba)) * ((Outer + Inner) * 0.5);
                float bd = length(p - bc) - 0.034;
                float bead = saturate(0.5 - bd / aa);
                float3 ink = lerp(_PlopPortalInk.rgb, tint, 0.25);
                col = lerp(col, ink, bead * 0.92);
                col = lerp(col, float3(1, 1, 1), bead * 0.55 * (1.0 - smoothstep(0.0, 0.016, length(p - bc - float2(-0.010, 0.012)))));

                // Luminous inner ring with a slow breathing pulse.
                float pulse = 0.85 + 0.15 * sin(t * 1.9 + pair);
                float ringGlow = exp(-pow((r - Inner) / 0.022, 2.0));
                float3 light = lerp(tint, float3(1.0, 0.98, 0.94), 0.55);
                col = lerp(col, light, ringGlow * 0.85 * pulse);

                if (r < Inner)
                {
                    // Vortex: deep funnel toward the centre, spiral arms turning, motes orbiting.
                    float depth = smoothstep(0.0, Inner, r);
                    float3 well = lerp(tint * 0.52, lerp(tint, float3(1, 1, 1), 0.38), depth);
                    float swirl = sin(a * 3.0 + r * 26.0 - t * 2.3);
                    float arms = smoothstep(0.25, 0.95, swirl) * (0.35 + 0.65 * depth);
                    float3 v = lerp(well, light, arms * 0.55);
                    // Rotating dashed ring: dash count is part of the pair's design.
                    float dashes = 3.0 + (float)pair;
                    float dash = step(0.5, frac((a + t * 0.6) * dashes / 6.2831853));
                    float dashRing = exp(-pow((r - 0.235) / 0.012, 2.0)) * dash;
                    v = lerp(v, light, dashRing * 0.75);
                    for (int m = 0; m < 3; m++)
                    {
                        float ma = t * 1.25 + m * 2.0944 + pair;
                        float2 mc = float2(cos(ma), sin(ma)) * (0.15 + 0.03 * sin(t * 0.9 + m));
                        v += float3(1.0, 0.98, 0.92) * 0.85 * (1.0 - smoothstep(0.0, 0.022, length(p - mc)));
                    }
                    v += float3(1, 1, 1) * 0.30 * (1.0 - smoothstep(0.0, 0.06, r));
                    v = lerp(v, light, flash * 0.6);
                    col = lerp(col, v, saturate((Inner - r) / aa + 0.5));
                }

                // Blocked: the vortex dims and greys for a moment (no exit animation).
                float3 grey = dot(col, float3(0.3, 0.5, 0.2)) * float3(0.92, 0.92, 0.95);
                col = lerp(col, grey * 0.85, blocked * 0.65);
                col += flash * 0.15;

                float a0 = frameCover + (1.0 - frameCover) * shadow;
                float3 rgb = (saturate(col) * frameCover + float3(0.32, 0.26, 0.38) * shadow * (1.0 - frameCover)) / max(a0, 1e-4);
                rgb = lerp(rgb, light, saturate(halo / max(a0 + halo, 1e-4)));
                return half4(PlopOut(rgb), saturate(a0 + halo * (1.0 - a0)));
            }
            ENDHLSL
        }
    }
}
