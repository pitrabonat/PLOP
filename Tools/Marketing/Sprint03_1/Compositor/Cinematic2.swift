// Cinematic2.swift — PLOP! official trailer V2 cinematic shots (Sprint 03.1).
// 2.5D: the owner's island illustration split into depth layers (sky / far / mid / ground / foreground + tree, lantern,
// fan blades, portal vortex) by Cinematic2/layers.swift; a camera with per-layer parallax and depth of field; and the
// game's own Plop rig (expressions, blinks, hops, slides, warps, sleep) rendered in Unity by MarketingCapture.cs and
// composited with contact shadows, ice reflections and per-shot relighting. The night ending is a separate composition.
import Foundation
import CoreGraphics
import CoreImage
import CoreText

enum C2 {
    static let base = "/Volumes/PLOP_DEV/Marketing/FinalSprint03_1/01_CinematicSources"
    static let L = base + "/Layers/"
    static let R = base + "/RigRenders/"

    // ------------------------------------------------------------------ utilities
    static func clamp(_ x: Double, _ a: Double = 0, _ b: Double = 1) -> Double { min(b, max(a, x)) }
    static func lerp(_ a: Double, _ b: Double, _ u: Double) -> Double { a + (b - a) * u }
    static func seg(_ t: Double, _ a: Double, _ b: Double, _ k: String = "inOut") -> Double { ease((t - a) / max(1e-6, b - a), k) }
    static func smooth(_ x: Double) -> Double { let t = clamp(x); return t * t * (3 - 2 * t) }
    static func rnd(_ i: Int, _ k: Int) -> Double { var x = UInt64(bitPattern: Int64(i &* 73856093 ^ k &* 19349663)) &+ 0x9E3779B97F4A7C15; x ^= x >> 33; x = x &* 0xff51afd7ed558ccd; x ^= x >> 33; return Double(x % 100000) / 100000 }

    // blurred-image cache (depth of field). Key: path|radius bucket
    static var blurCache: [String: CGImage] = [:]
    static var blurOrder: [String] = []
    static func blurredImage(_ path: String, _ img: CGImage, radiusPx r: Double) -> CGImage {
        if r < 0.6 { return img }
        let q = (r * 2).rounded() / 2
        let key = path + "|" + String(q)
        if let c = blurCache[key] { return c }
        let down = max(1, q / 2.5)
        var c = CIImage(cgImage: img)
        let ext = c.extent
        c = c.transformed(by: CGAffineTransform(scaleX: 1 / down, y: 1 / down))
        c = c.clampedToExtent().applyingGaussianBlur(sigma: q / down).cropped(to: c.extent)
        c = c.transformed(by: CGAffineTransform(scaleX: down, y: down))
        let out = ci.createCGImage(c, from: ext, format: .RGBA8, colorSpace: sRGB)!
        blurCache[key] = out; blurOrder.append(key)
        if blurOrder.count > 40 { blurCache.removeValue(forKey: blurOrder.removeFirst()) }
        return out
    }

    // rig frame sequences
    struct Meta: Decodable { struct F: Decodable { var t: Double; var root: [Double]; var center: [Double]; var asleep: Bool }
        var ppu: Double; var width: Double; var height: Double; var frames: [F] }
    static var metas: [String: Meta] = [:]
    static func meta(_ seq: String) -> Meta {
        if let m = metas[seq] { return m }
        let m = try! JSONDecoder().decode(Meta.self, from: Data(contentsOf: URL(fileURLWithPath: R + seq + "/meta.json")))
        metas[seq] = m; return m
    }
    static var frameCache: [String: CGImage] = [:]
    static var frameOrder: [String] = []
    static func rigFrame(_ seq: String, _ t: Double) -> (CGImage, Meta.F, Meta) {
        let m = meta(seq)
        let i = max(0, min(m.frames.count - 1, Int((t * 30).rounded(.down))))
        let path = R + seq + String(format: "/f_%04d.png", i)
        if frameCache[path] == nil {
            frameCache[path] = loadImageUncached(path); frameOrder.append(path)
            if frameOrder.count > 60 { frameCache.removeValue(forKey: frameOrder.removeFirst()) }
        }
        return (frameCache[path]!, m.frames[i], m)
    }
    static func loadImageUncached(_ p: String) -> CGImage {
        let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: p) as CFURL, nil)!
        return CGImageSourceCreateImageAtIndex(src, 0, nil)!
    }

    // ------------------------------------------------------------------ camera over the plate (plate px, 1774 x 887)
    struct Cam { var cx: Double; var cy: Double; var z: Double }
    /// Parallax: a layer with factor f follows the camera move from the shot's reference camera by f.
    static func eff(_ cam: Cam, _ ref: Cam, _ f: Double) -> Cam {
        Cam(cx: ref.cx + (cam.cx - ref.cx) * f, cy: ref.cy + (cam.cy - ref.cy) * f, z: ref.z * pow(cam.z / ref.z, f))
    }
    static func toScreen(_ x: Double, _ y: Double, _ c: Cam, _ W: Double, _ H: Double) -> CGPoint {
        CGPoint(x: (x - c.cx) * c.z + W / 2, y: H - ((y - c.cy) * c.z + H / 2))
    }
    /// Draws an image whose top-left sits at plate (x0, y0) with `scale` image px per plate px.
    static func plateImage(_ ctx: CGContext, _ path: String, x0: Double, y0: Double, scale: Double = 2, cam: Cam, W: Double, H: Double, blur: Double = 0, alpha: Double = 1) {
        var img = loadImage(path)
        if blur > 0.6 { img = blurredImage(path, img, radiusPx: blur / cam.z * scale) }
        let w = Double(img.width) / scale, h = Double(img.height) / scale
        let tl = toScreen(x0, y0, cam, W, H)
        let r = CGRect(x: tl.x, y: tl.y - h * cam.z, width: w * cam.z, height: h * cam.z)
        if r.maxX < 0 || r.minX > W || r.maxY < 0 || r.minY > H { return }
        ctx.saveGState(); ctx.setAlpha(alpha); ctx.interpolationQuality = .high; ctx.draw(img, in: r); ctx.restoreGState()
    }
    /// Draws an image rotated about a plate pivot (pivot given in plate px; image top-left at x0,y0). Squeeze: the rotation
    /// happens in an un-foreshortened space (sx) so a disc seen at 3/4 view keeps its ellipse.
    static func plateRotated(_ ctx: CGContext, _ path: String, x0: Double, y0: Double, pivot: (Double, Double), angle: Double, squeezeX: Double = 1, scale: Double = 2, cam: Cam, W: Double, H: Double, alpha: Double = 1, blur: Double = 0) {
        var img = loadImage(path)
        if blur > 0.6 { img = blurredImage(path, img, radiusPx: blur / cam.z * scale) }
        let p = toScreen(pivot.0, pivot.1, cam, W, H)
        let w = Double(img.width) / scale * cam.z, h = Double(img.height) / scale * cam.z
        let ox = (x0 - pivot.0) * cam.z, oyTop = (y0 - pivot.1) * cam.z
        ctx.saveGState(); ctx.setAlpha(alpha); ctx.interpolationQuality = .high
        ctx.translateBy(x: p.x, y: p.y)
        ctx.scaleBy(x: squeezeX, y: 1); ctx.rotate(by: -angle); ctx.scaleBy(x: 1 / squeezeX, y: 1)
        ctx.draw(img, in: CGRect(x: ox, y: -oyTop - h, width: w, height: h))
        ctx.restoreGState()
    }

    /// A rig frame placed with its root ground point at plate (gx, gy); u = plate px per rig cell.
    static func rig(_ ctx: CGContext, _ seq: String, _ t: Double, gx: Double, gy: Double, u: Double, cam: Cam, W: Double, H: Double,
                    breathe: Double = 0, alpha: Double = 1, blur: Double = 0, extraScale: (Double, Double) = (1, 1), reflect: Double = 0, relight: ((CGContext, CGRect) -> Void)? = nil, follow: Bool = false) {
        let (img0, f, m) = rigFrame(seq, t)
        var img = img0
        let k = u / m.ppu * cam.z                     // output px per image px
        if blur > 0.6 { img = blurredImage(R + seq + "#\(Int(t * 30))", img, radiusPx: blur / k) }
        let leftWorld = f.center[0] - m.width / m.ppu / 2, topWorld = f.center[1] + m.height / m.ppu / 2
        let rootX = follow ? f.center[0] : f.root[0]
        let ax = (rootX - leftWorld) * m.ppu, ay = (topWorld - (f.root[1] - 0.345)) * m.ppu   // root ground point in image px (top-left origin)
        let s = toScreen(gx, gy, cam, W, H)
        var sy = extraScale.1, sx = extraScale.0
        if breathe > 0 { let b = sin(2 * .pi * t / 3.4) * 0.011 * breathe; sy *= 1 + b; sx *= 1 - b * 0.5 }
        let rect = CGRect(x: -ax * k * sx, y: -(m.height - ay) * k * sy, width: m.width * k * sx, height: m.height * k * sy)
        if reflect > 0 {
            ctx.saveGState(); ctx.translateBy(x: s.x, y: s.y); ctx.scaleBy(x: 1, y: -1)
            ctx.setAlpha(alpha * reflect); ctx.interpolationQuality = .high
            ctx.clip(to: CGRect(x: -W, y: 0, width: 2 * W, height: m.height * k * 0.55))
            ctx.draw(img, in: rect); ctx.restoreGState()
        }
        ctx.saveGState(); ctx.translateBy(x: s.x, y: s.y); ctx.setAlpha(alpha); ctx.interpolationQuality = .high
        if let rl = relight {
            // relight in the image's own pixel space (offscreen), then masked by its alpha — no seams in the main frame
            let iw = img.width, ih = img.height
            let oc = CGContext(data: nil, width: iw, height: ih, bitsPerComponent: 8, bytesPerRow: 0, space: sRGB, bitmapInfo: CGImageAlphaInfo.premultipliedFirst.rawValue | CGBitmapInfo.byteOrder32Little.rawValue)!
            let ir = CGRect(x: 0, y: 0, width: iw, height: ih)
            oc.draw(img, in: ir); rl(oc, ir)
            oc.setBlendMode(.destinationIn); oc.draw(img, in: ir)
            ctx.draw(oc.makeImage()!, in: rect)
        } else { ctx.draw(img, in: rect) }
        ctx.restoreGState()
    }

    static func glow(_ ctx: CGContext, _ p: CGPoint, _ r: Double, _ hex: String, _ a: Double) {
        let g = CGGradient(colorsSpace: sRGB, colors: [color(hex, a), color(hex, 0)] as CFArray, locations: [0, 1])!
        ctx.drawRadialGradient(g, startCenter: p, startRadius: 0, endCenter: p, endRadius: r, options: [])
    }

    // ------------------------------------------------------------------ environment pieces (day island)
    static let FAN_PIVOT = (1333.0, 513.0), VORTEX_PIVOT = (1617.0, 488.0)

    static func island(_ ctx: CGContext, t: Double, cam: Cam, ref: Cam, W: Double, H: Double, fanSpeed: Double, fanAngle: Double, vortexAngle: Double,
                       focus: Double = 1, dof: Double = 0, between: ((Double) -> Void)? = nil) {
        // depth factors: sky .35, far .6, mid .85, ground 1, fg 1.35. Blur grows with distance from the focus depth.
        func b(_ f: Double) -> Double { dof * abs(f - focus) }
        let sky = eff(cam, ref, 0.35), far = eff(cam, ref, 0.6), mid = eff(cam, ref, 0.85), gnd = eff(cam, ref, 1.0)
        plateImage(ctx, L + "L0_sky.png", x0: 0, y0: 0, cam: sky, W: W, H: H, blur: b(0.35))
        plateImage(ctx, L + "L1_far.png", x0: 0, y0: 0, cam: far, W: W, H: H, blur: b(0.6))
        waterfall(ctx, t: t, cam: far, W: W, H: H, blur: b(0.6))
        houseGlow(ctx, t: t, cam: far, W: W, H: H)
        plateImage(ctx, L + "L2_mid.png", x0: 0, y0: 0, cam: mid, W: W, H: H, blur: b(0.85))
        // fan blades: rotate in the disc's own plane (3/4 view squeeze), motion blur at speed
        let copies = fanSpeed > 2 ? 5 : 1
        for i in 0..<copies {
            let a = fanAngle - Double(i) * min(0.5, fanSpeed * 0.012)
            plateRotated(ctx, L + "P_fan_blades.png", x0: 1263, y0: 437, pivot: FAN_PIVOT, angle: a, squeezeX: 0.9, cam: mid, W: W, H: H, alpha: copies == 1 ? 1 : (i == 0 ? 0.55 : 0.28), blur: b(0.85))
        }
        // portal vortex: slow spin + breathing glow
        plateRotated(ctx, L + "P_portal_vortex.png", x0: 1508, y0: 377, pivot: VORTEX_PIVOT, angle: vortexAngle, squeezeX: 0.98, cam: mid, W: W, H: H, blur: b(0.85))
        let vp = toScreen(VORTEX_PIVOT.0, VORTEX_PIVOT.1, mid, W, H)
        ctx.saveGState(); ctx.setBlendMode(.screen); glow(ctx, vp, 70 * mid.z, "#D9A6FF", 0.18 + 0.06 * sin(t * 2.3)); ctx.restoreGState()
        between?(0.9)
        plateImage(ctx, L + "L3_ground.png", x0: 0, y0: 0, cam: gnd, W: W, H: H, blur: b(1.0))
        lava(ctx, t: t, cam: gnd, W: W, H: H)
        // the little daisies on the ledge sway (overlay of themselves, rotated a few tenths of a degree)
        plateRotated(ctx, L + "P_ledge_daisies.png", x0: 920, y0: 595, pivot: (965, 646), angle: 0.035 * sin(t * 1.7), cam: gnd, W: W, H: H, blur: b(1.0))
        between?(1.0)
    }

    static func foreground(_ ctx: CGContext, t: Double, cam: Cam, ref: Cam, W: Double, H: Double, focus: Double, dof: Double) {
        let fg = eff(cam, ref, 1.35)
        // bottom-right daisy + grass mound sway around their bases
        plateRotated(ctx, L + "L4_fg.png", x0: 0, y0: 0, pivot: (1690, 900), angle: 0.012 * sin(t * 1.3 + 0.4), cam: fg, W: W, H: H, blur: dof * abs(1.35 - focus))
    }

    static func waterfall(_ ctx: CGContext, t: Double, cam: Cam, W: Double, H: Double, blur: Double) {
        // flow: two phase-offset copies of the falling water slide down and cross-fade (seamless)
        let img = loadImage(L + "L1_far.png")
        let rect = CGRect(x: 1488 * 2, y: 190 * 2, width: 128 * 2, height: 186 * 2)
        guard let crop = img.cropping(to: rect) else { return }
        let period = 1.1, travel = 26.0
        for k in 0..<2 {
            let ph = (t / period + Double(k) * 0.5).truncatingRemainder(dividingBy: 1)
            let a = 1 - abs(ph * 2 - 1)
            let tl = toScreen(1488, 190 + ph * travel - travel * 0.5, cam, W, H)
            let clipTL = toScreen(1490, 196, cam, W, H), clipBR = toScreen(1614, 372, cam, W, H)
            ctx.saveGState(); ctx.clip(to: CGRect(x: clipTL.x, y: clipBR.y, width: clipBR.x - clipTL.x, height: clipTL.y - clipBR.y))
            ctx.setAlpha(0.55 * a); ctx.interpolationQuality = .high
            ctx.draw(crop, in: CGRect(x: tl.x, y: tl.y - 186 * cam.z, width: 128 * cam.z, height: 186 * cam.z))
            ctx.restoreGState()
        }
        // foam at the base
        let foot = toScreen(1552, 374, cam, W, H)
        ctx.saveGState(); ctx.setBlendMode(.screen)
        for i in 0..<5 { let ph = (t * 0.9 + Double(i) / 5).truncatingRemainder(dividingBy: 1)
            glow(ctx, CGPoint(x: foot.x + (rnd(i, 3) - 0.5) * 90 * cam.z * 0.5, y: foot.y + ph * 6 * cam.z), (5 + 8 * ph) * cam.z, "#FFFFFF", 0.22 * (1 - ph)) }
        ctx.restoreGState()
    }
    static func houseGlow(_ ctx: CGContext, t: Double, cam: Cam, W: Double, H: Double) {
        ctx.saveGState(); ctx.setBlendMode(.screen)
        glow(ctx, toScreen(1505, 160, cam, W, H), 18 * cam.z, "#FFD98A", 0.25 + 0.05 * sin(t * 3.1))
        ctx.restoreGState()
    }
    static func lava(_ ctx: CGContext, t: Double, cam: Cam, W: Double, H: Double) {
        ctx.saveGState(); ctx.setBlendMode(.screen)
        for i in 0..<6 {
            let per = 1.6 + rnd(i, 1) * 1.2, ph = ((t + rnd(i, 2) * per) / per).truncatingRemainder(dividingBy: 1)
            let x = 1420 + rnd(i, 3) * 200, y = 780 + rnd(i, 4) * 50
            let p = toScreen(x, y - ph * 6, cam, W, H)
            glow(ctx, p, (3 + 7 * sin(.pi * ph)) * cam.z, "#FFE08A", 0.55 * sin(.pi * ph))
        }
        glow(ctx, toScreen(1500, 800, cam, W, H), 120 * cam.z, "#FF9A4A", 0.08 + 0.03 * sin(t * 2))
        ctx.restoreGState()
    }

    /// Soft wind ribbons travelling along a path (fan mouth → loop → across the ice).
    static func wind(_ ctx: CGContext, t: Double, strength s: Double, cam: Cam, W: Double, H: Double, path pts: [(Double, Double)]) {
        if s <= 0.01 { return }
        let P = pts.map { toScreen($0.0, $0.1, cam, W, H) }
        let path = CGMutablePath(); path.move(to: P[0])
        var i = 1; while i + 2 < P.count + 1 && i + 1 < P.count { path.addQuadCurve(to: CGPoint(x: (P[i].x + P[i + 1].x) / 2, y: (P[i].y + P[i + 1].y) / 2), control: P[i]); i += 1 }
        path.addLine(to: P.last!)
        ctx.saveGState(); ctx.setLineCap(.round)
        for k in 0..<4 {
            let off = Double(k) * 9 * cam.z - 13 * cam.z
            ctx.saveGState(); ctx.translateBy(x: 0, y: off * 0.6)
            ctx.addPath(path)
            ctx.setLineWidth((6 - Double(k)) * cam.z * 0.55)
            let dash = 120.0 * cam.z, gap = 220.0 * cam.z
            ctx.setLineDash(phase: -t * 900 * cam.z * 0.4 - Double(k) * 70, lengths: [dash, gap])
            ctx.setStrokeColor(color("#FFFFFF", (0.42 - Double(k) * 0.07) * s))
            ctx.strokePath(); ctx.restoreGState()
        }
        ctx.restoreGState()
    }

    /// Procedural portal tunnel (screen space) used when the camera passes through the portal and for the match cut.
    static func tunnel(_ ctx: CGContext, t: Double, cx: Double, cy: Double, r: Double, alpha: Double) {
        if alpha <= 0 { return }
        ctx.saveGState(); ctx.setAlpha(alpha)
        let g = CGGradient(colorsSpace: sRGB, colors: [color("#FFF2FF"), color("#E7A6FF"), color("#9A5BE6"), color("#5B2BA8"), color("#3A1A72")] as CFArray, locations: [0, 0.12, 0.42, 0.75, 1])!
        let c = CGPoint(x: cx, y: cy)
        ctx.addEllipse(in: CGRect(x: cx - r, y: cy - r, width: 2 * r, height: 2 * r)); ctx.clip()
        ctx.drawRadialGradient(g, startCenter: c, startRadius: 0, endCenter: c, endRadius: r, options: [.drawsAfterEndLocation])
        // spiral arms
        ctx.setLineCap(.round)
        for arm in 0..<5 {
            let path = CGMutablePath()
            for j in 0...60 {
                let u = Double(j) / 60, ang = Double(arm) * 2 * .pi / 5 + t * 2.6 + u * 4.2
                let rr = r * (0.04 + 0.96 * u)
                let p = CGPoint(x: cx + cos(ang) * rr, y: cy + sin(ang) * rr)
                if j == 0 { path.move(to: p) } else { path.addLine(to: p) }
            }
            ctx.addPath(path); ctx.setLineWidth(r * 0.07); ctx.setStrokeColor(color("#F6CFFF", 0.35)); ctx.strokePath()
            ctx.addPath(path); ctx.setLineWidth(r * 0.022); ctx.setStrokeColor(color("#FFFFFF", 0.55)); ctx.strokePath()
        }
        for i in 0..<40 {   // stars streaming outward
            let ph = (t * 0.6 + rnd(i, 9)).truncatingRemainder(dividingBy: 1)
            let ang = rnd(i, 7) * 2 * .pi + t * 0.8, rr = r * ph
            glow(ctx, CGPoint(x: cx + cos(ang) * rr, y: cy + sin(ang) * rr), r * 0.012 * (0.5 + ph * 2), "#FFFFFF", 0.8 * ph)
        }
        ctx.restoreGState()
    }

    /// Soft out-of-focus foreground leaves (procedural) framing a corner.
    static func leaves(_ ctx: CGContext, t: Double, W: Double, H: Double, corner: Int, blur: Double, a: Double = 1) {
        let n = 7
        for i in 0..<n {
            let ang = (corner == 0 ? -0.6 : 0.9) + Double(i) * 0.28 + 0.05 * sin(t * 1.3 + Double(i))
            let len = 260 + rnd(i, 2) * 160, wid = 85 + rnd(i, 3) * 40
            let base = corner == 0 ? CGPoint(x: -40 + rnd(i, 4) * 120, y: -40 + rnd(i, 5) * 60) : CGPoint(x: W + 40 - rnd(i, 4) * 120, y: -40 + rnd(i, 5) * 60)
            ctx.saveGState(); ctx.translateBy(x: base.x, y: base.y); ctx.rotate(by: ang + (corner == 0 ? 0.5 : -0.5))
            let p = CGMutablePath(); p.move(to: .zero)
            p.addQuadCurve(to: CGPoint(x: len, y: 0), control: CGPoint(x: len * 0.5, y: wid))
            p.addQuadCurve(to: .zero, control: CGPoint(x: len * 0.5, y: -wid * 0.6))
            ctx.addPath(p); ctx.clip()
            let g = CGGradient(colorsSpace: sRGB, colors: [color("#6FAE5A", a), color("#A8D57A", a), color("#D8EFA0", a * 0.9)] as CFArray, locations: [0, 0.6, 1])!
            ctx.drawLinearGradient(g, start: .zero, end: CGPoint(x: len, y: wid * 0.3), options: [])
            ctx.restoreGState()
        }
        _ = blur
    }

    /// Procedural grass blades at the focus plane (sharp, swaying), drawn in screen space along a baseline.
    static func grass(_ ctx: CGContext, t: Double, x0: Double, x1: Double, y: Double, h: Double, n: Int, seed: Int, wind: Double = 1, colorTop: String = "#CFE88E", colorBase: String = "#7DB55A", alpha: Double = 1) {
        for i in 0..<n {
            let x = x0 + (x1 - x0) * (Double(i) + rnd(i, seed)) / Double(n)
            let hh = h * (0.55 + 0.6 * rnd(i, seed + 1)), w = 7 + 7 * rnd(i, seed + 2)
            let sway = (0.09 + 0.05 * rnd(i, seed + 3)) * sin(t * (1.5 + rnd(i, seed + 4)) + Double(i) * 0.7) * wind + (rnd(i, seed + 5) - 0.5) * 0.5
            let tip = CGPoint(x: x + sin(sway) * hh, y: y + cos(sway) * hh)
            let p = CGMutablePath(); p.move(to: CGPoint(x: x - w / 2, y: y))
            p.addQuadCurve(to: tip, control: CGPoint(x: x - w * 0.2 + sin(sway) * hh * 0.4, y: y + hh * 0.55))
            p.addQuadCurve(to: CGPoint(x: x + w / 2, y: y), control: CGPoint(x: x + w * 0.35 + sin(sway) * hh * 0.45, y: y + hh * 0.5))
            ctx.saveGState(); ctx.addPath(p); ctx.clip()
            let g = CGGradient(colorsSpace: sRGB, colors: [color(colorBase, alpha), color(colorTop, alpha)] as CFArray, locations: [0, 1])!
            ctx.drawLinearGradient(g, start: CGPoint(x: x, y: y), end: tip, options: [])
            ctx.restoreGState()
        }
    }

    static func grade(_ ctx: CGContext, W: Double, H: Double, warm: Double, lift: Double) {
        // gentle morning light: warm top-left wash + soft vignette (no bloom, no flash)
        ctx.saveGState(); ctx.setBlendMode(.softLight)
        let g = CGGradient(colorsSpace: sRGB, colors: [color("#FFD9A8", 0.45 * warm), color("#FFD9A8", 0)] as CFArray, locations: [0, 1])!
        ctx.drawRadialGradient(g, startCenter: CGPoint(x: W * 0.1, y: H * 1.05), startRadius: 0, endCenter: CGPoint(x: W * 0.1, y: H * 1.05), endRadius: W * 1.1, options: [])
        ctx.restoreGState()
        if lift < 1 { ctx.saveGState(); ctx.setFillColor(color("#2E1F3A", (1 - lift))); ctx.setBlendMode(.multiply); ctx.fill(CGRect(x: 0, y: 0, width: W, height: H)); ctx.restoreGState() }
        ctx.saveGState()
        let v = CGGradient(colorsSpace: sRGB, colors: [color("#3A2340", 0), color("#3A2340", 0.22)] as CFArray, locations: [0.55, 1])!
        ctx.drawRadialGradient(v, startCenter: CGPoint(x: W / 2, y: H / 2), startRadius: 0, endCenter: CGPoint(x: W / 2, y: H / 2), endRadius: W * 0.72, options: [.drawsAfterEndLocation])
        ctx.restoreGState()
    }

    static func rays(_ ctx: CGContext, t: Double, W: Double, H: Double, a: Double) {
        ctx.saveGState(); ctx.setBlendMode(.screen)
        for i in 0..<4 {
            let x = W * (0.05 + Double(i) * 0.11) + sin(t * 0.4 + Double(i)) * 20
            let p = CGMutablePath(); p.move(to: CGPoint(x: x - 30, y: H + 10)); p.addLine(to: CGPoint(x: x + 60, y: H + 10))
            p.addLine(to: CGPoint(x: x + 520 + Double(i) * 40, y: -10)); p.addLine(to: CGPoint(x: x + 300 + Double(i) * 40, y: -10)); p.closeSubpath()
            ctx.saveGState(); ctx.addPath(p); ctx.clip()
            let g = CGGradient(colorsSpace: sRGB, colors: [color("#FFF1D6", 0.10 * a), color("#FFF1D6", 0)] as CFArray, locations: [0, 1])!
            ctx.drawLinearGradient(g, start: CGPoint(x: x, y: H), end: CGPoint(x: x + 400, y: H * 0.15), options: [])
            ctx.restoreGState()
        }
        ctx.restoreGState()
    }

    // ------------------------------------------------------------------ shots
    static func draw(_ ctx: CGContext, name: String, t: Double, W: Double, H: Double, p: [String: Double]) {
        switch name {
        case "v2_world": world(ctx, t: t, W: W, H: H)
        case "v2_hero": hero(ctx, t: t, W: W, H: H)
        case "v2_adventure": adventure(ctx, t: t, W: W, H: H)
        case "v2_tunnel": tunnel(ctx, t: t, cx: lerp(p["x0"] ?? W / 2, p["x1"] ?? W / 2, seg(t, 0, p["dur"] ?? 1)), cy: H - lerp(p["y0"] ?? H / 2, p["y1"] ?? H / 2, seg(t, 0, p["dur"] ?? 1)),
                              r: lerp(p["r0"] ?? W, p["r1"] ?? W, seg(t, 0, p["dur"] ?? 1, "inOut")), alpha: 1)
        case "v2_night": night(ctx, t: t, W: W, H: H, p: p)
        case "v2_studio": studio(ctx, t: t, W: W, H: H, p: p)
        case "v2_thumb": thumb(ctx, W: W, H: H, p: p)
        default: break
        }
    }

    /// SHOT 01 — the world awakens (2.0 s). Boom down + slow push over the layered island (parallax + depth of field);
    /// the blossom canopy and its lantern hang in the near foreground and sway; waterfall flows; fan idles; the pink Plop
    /// sleeps on the grassy ledge (eyes closed, breathing). Opens on a fully lit frame — no black.
    static let ID = 0.0
    static func flat(_ W: Double, _ H: Double) -> Cam { Cam(cx: W / 2, cy: H / 2, z: 1) }
    static func world(_ ctx: CGContext, t: Double, W: Double, H: Double) {
        let ref = Cam(cx: 1322, cy: 400, z: 2.12)
        let u = seg(t, 0, 2.2, "linear")
        let cam = Cam(cx: lerp(1322, 1310, u), cy: lerp(400, 452, smooth(u)), z: lerp(2.12, 2.2, u))
        island(ctx, t: t + 3, cam: cam, ref: ref, W: W, H: H, fanSpeed: 0.6, fanAngle: t * 0.4, vortexAngle: -t * 0.35, focus: 1.0, dof: 3, between: { f in
            if f == 1.0 {
                let g = eff(cam, ref, 1.0)
                contactShadow(ctx, toScreen(1000, 624, g, W, H), w: 70 * g.z, a: 0.22)
                rig(ctx, "hero_wake", 0.0, gx: 1000, gy: 624, u: 88, cam: g, W: W, H: H, breathe: 1)
            }
        })
        foreground(ctx, t: t, cam: cam, ref: ref, W: W, H: H, focus: 1.0, dof: 3)
        // near foreground: blossom canopy + lantern (factor 1.5 parallax, defocused)
        let k = 1.5 * ref.z / 2.0
        let tc = Cam(cx: 621 + (cam.cx - ref.cx) * k, cy: 335 + (cam.cy - ref.cy) * k, z: 2.0 * pow(cam.z / ref.z, 1.5))
        plateRotated(ctx, L + "P_tree_c.png", x0: 0, y0: 0, pivot: (240, 40), angle: 0.01 * sin(t * 1.1), cam: tc, W: W, H: H, blur: 5)
        plateRotated(ctx, L + "P_lantern_c.png", x0: 212, y0: 112, pivot: (291, 128), angle: 0.045 * sin(t * 1.6 + 0.3), cam: tc, W: W, H: H, blur: 3.5)
        ctx.saveGState(); ctx.setBlendMode(.screen); glow(ctx, toScreen(279, 212, tc, W, H), 150, "#FFD48A", 0.16); ctx.restoreGState()
        rays(ctx, t: t, W: W, H: H, a: 1)
        grade(ctx, W: W, H: H, warm: 1, lift: lerp(0.9, 1, seg(t, 0, 1.2)))
    }

    static func contactShadow(_ ctx: CGContext, _ p: CGPoint, w: Double, a: Double) {
        ctx.saveGState(); ctx.setBlendMode(.multiply)
        ctx.translateBy(x: p.x, y: p.y); ctx.scaleBy(x: 1, y: 0.28)
        let g = CGGradient(colorsSpace: sRGB, colors: [color("#6B4A6E", a), color("#6B4A6E", 0)] as CFArray, locations: [0, 1])!
        ctx.drawRadialGradient(g, startCenter: .zero, startRadius: 0, endCenter: .zero, endRadius: w, options: [])
        ctx.restoreGState()
    }

    /// Soft moss mound in the illustration's style (rounded scalloped edge, yellow-green, lit from above) with a few
    /// rounded grass tufts that sway. `top` is the CG y (bottom-origin) of the scalloped edge.
    static func moss(_ ctx: CGContext, t: Double, top: Double, W: Double, r: Double, seed: Int, light: String = "#D4E77C", mid: String = "#A9CF5C", dark: String = "#7FAE4C", tufts: Int = 18, alpha: Double = 1) {
        let p = CGMutablePath()
        p.addRect(CGRect(x: -20, y: -20, width: W + 40, height: top - r * 0.5 + 20))
        var x = -r
        var i = 0
        while x < W + r { let rr = r * (0.8 + 0.4 * rnd(i, seed)); p.addEllipse(in: CGRect(x: x - rr, y: top - rr * 1.15 + 8 * rnd(i, seed + 1), width: 2 * rr, height: 2 * rr)); x += r * 1.25; i += 1 }
        ctx.saveGState(); ctx.setAlpha(alpha); ctx.beginTransparencyLayer(auxiliaryInfo: nil)
        ctx.addPath(p); ctx.clip()
        let g = CGGradient(colorsSpace: sRGB, colors: [color(dark), color(mid), color(light)] as CFArray, locations: [0, 0.6, 1])!
        ctx.drawLinearGradient(g, start: CGPoint(x: 0, y: top - 3.2 * r), end: CGPoint(x: 0, y: top + r * 0.2), options: [.drawsBeforeStartLocation, .drawsAfterEndLocation])
        // soft sheen near the top edge
        ctx.setBlendMode(.screen)
        let sh = CGGradient(colorsSpace: sRGB, colors: [color("#FFFBD0", 0), color("#FFFBD0", 0.35)] as CFArray, locations: [0, 1])!
        ctx.drawLinearGradient(sh, start: CGPoint(x: 0, y: top - r * 0.9), end: CGPoint(x: 0, y: top + r * 0.3), options: [])
        // dappled moss texture: soft light/dark spots, denser and smaller toward the far edge
        ctx.setBlendMode(.softLight)
        for k in 0..<160 {
            let v = rnd(k, seed + 20), y = top - r * 0.6 - pow(rnd(k, seed + 21), 0.7) * (top + 20)
            let rad = r * (0.3 + 0.8 * (1 - y / max(1, top))) * (0.6 + v)
            ctx.setFillColor(color(v > 0.5 ? "#FFFFE0" : "#5E8E3A", 0.10))
            ctx.fillEllipse(in: CGRect(x: rnd(k, seed + 22) * W - rad, y: y - rad * 0.5, width: 2 * rad, height: rad))
        }
        ctx.endTransparencyLayer(); ctx.restoreGState()
        // rounded tufts on the rim
        for j in 0..<tufts {
            let tx = W * (Double(j) + rnd(j, seed + 7)) / Double(tufts), base = top - r * (0.2 + 0.4 * rnd(j, seed + 9))
            for b in 0..<4 {
                let hh = r * (0.45 + 0.6 * rnd(j * 4 + b, seed + 8)), ww = r * (0.16 + 0.1 * rnd(j * 4 + b, seed + 10))
                let sway = 0.22 * (Double(b) - 1.5) + 0.07 * sin(t * 1.8 + Double(j) + Double(b))
                let bx = tx + (Double(b) - 1.5) * ww * 0.8
                let tip = CGPoint(x: bx + sin(sway) * hh, y: base + cos(sway) * hh)
                let bl = CGMutablePath(); bl.move(to: CGPoint(x: bx - ww / 2, y: base))
                bl.addQuadCurve(to: tip, control: CGPoint(x: bx - ww * 0.6 + sin(sway) * hh * 0.5, y: base + hh * 0.7))
                bl.addQuadCurve(to: CGPoint(x: bx + ww / 2, y: base), control: CGPoint(x: bx + ww * 0.6 + sin(sway) * hh * 0.5, y: base + hh * 0.6))
                ctx.saveGState(); ctx.setAlpha(alpha); ctx.addPath(bl); ctx.clip()
                let gg = CGGradient(colorsSpace: sRGB, colors: [color(mid), color(light)] as CFArray, locations: [0, 1])!
                ctx.drawLinearGradient(gg, start: CGPoint(x: bx, y: base), end: tip, options: [])
                ctx.restoreGState()
            }
        }
    }

    /// SHOT 02 — meet our hero (3.33 s). Macro close-up: island defocused behind, a mossy ledge at the focus plane, the
    /// pink Plop wakes (eyes open, blink, smile, squash-and-stretch hop) with a contact shadow; the mint friend reacts behind.
    static func hero(_ ctx: CGContext, t: Double, W: Double, H: Double) {
        let ref = Cam(cx: 1205, cy: 520, z: 5.4)
        let cam = Cam(cx: lerp(1205, 1216, t / 3.4), cy: 520, z: lerp(5.4, 5.55, t / 3.4))
        let sky = eff(cam, ref, 0.35), far = eff(cam, ref, 0.6), mid = eff(cam, ref, 0.85)
        plateImage(ctx, L + "L0_sky.png", x0: 0, y0: 0, cam: sky, W: W, H: H, blur: 22)
        plateImage(ctx, L + "L1_far.png", x0: 0, y0: 0, cam: far, W: W, H: H, blur: 20)
        plateImage(ctx, L + "L2_mid.png", x0: 0, y0: 0, cam: mid, W: W, H: H, blur: 16)
        plateRotated(ctx, L + "P_fan_blades.png", x0: 1263, y0: 437, pivot: FAN_PIVOT, angle: t * 0.5, squeezeX: 0.9, cam: mid, W: W, H: H, blur: 16)
        let id = flat(W, H)
        let px = (cam.cx - ref.cx) * 2.0   // near layers drift a little faster than the background
        // back ledge (soft, hazier) with the friend
        let back = renderOffscreen(W: W, H: H) { c in
            moss(c, t: t, top: 400, W: W, r: 46, seed: 3, light: "#E3EDA6", mid: "#C3DB86", dark: "#A9C77A", tufts: 22)
            contactShadow(c, CGPoint(x: 1330 - px * 0.6, y: 356), w: 140, a: 0.2)
            rig(c, "mint_react", t, gx: 1330 - px * 0.6, gy: H - 356, u: 300, cam: id, W: W, H: H)
        }
        let bi = CIImage(cgImage: back).clampedToExtent().applyingGaussianBlur(sigma: 3.2).cropped(to: CGRect(x: 0, y: 0, width: W, height: H))
        ctx.draw(ci.createCGImage(bi, from: CGRect(x: 0, y: 0, width: W, height: H), format: .RGBA8, colorSpace: sRGB)!, in: CGRect(x: 0, y: 0, width: W, height: H))
        // front ledge (sharp) with the hero
        moss(ctx, t: t, top: 250, W: W, r: 64, seed: 11, tufts: 16)
        contactShadow(ctx, CGPoint(x: 760 - px, y: 200), w: 300, a: 0.3)
        rig(ctx, "hero_wake", t, gx: 760 - px, gy: H - 200, u: 640, cam: id, W: W, H: H, breathe: t < 0.45 ? 1 : 0)
        // out-of-focus daisies in the very near foreground (bottom-right corner)
        // (no foreground daisies: keep the macro frame clean)
        grade(ctx, W: W, H: H, warm: 0.8, lift: 1)
    }

    /// SHOT 03 + 04 — wind from the fan loops round, the hero slides across the ice into the portal; the camera follows
    /// and pushes into the vortex (4.0 s: 0–2.67 adventure, 2.67–4.0 portal).
    static let slidePath: [(Double, Double)] = [(985, 716), (1110, 700), (1235, 676), (1320, 648), (1420, 618), (1520, 600), (1592, 596)]
    static func pathPoint(_ s: Double) -> (Double, Double, Double) {   // s in rig cells (0…7) → plate point + local scale
        let u = clamp(s / 7) * Double(slidePath.count - 1)
        let i = min(slidePath.count - 2, Int(u)), f = u - Double(i)
        let a = slidePath[i], b = slidePath[i + 1]
        let y = lerp(a.1, b.1, f)
        return (lerp(a.0, b.0, f), y, lerp(150, 112, clamp((716 - y) / 120)))
    }
    static func adventure(_ ctx: CGContext, t: Double, W: Double, H: Double) {
        let ref = Cam(cx: 1255, cy: 560, z: 2.62)
        let (_, f0, _) = rigFrame("hero_adventure", min(t, 3.36))
        let s = f0.root[0]
        let (px, py, pu) = pathPoint(s)
        // camera: settle on the fan, follow the slide, then push into the vortex
        let follow = seg(t, 0.9, 2.5)
        var cam = Cam(cx: lerp(1255, 1430, follow), cy: lerp(560, 520, follow), z: lerp(2.62, 2.9, follow))
        let push = seg(t, 2.4, 3.6, "inOut")
        if push > 0 { cam = Cam(cx: lerp(cam.cx, VORTEX_PIVOT.0, smooth(push * 1.4)), cy: lerp(cam.cy, VORTEX_PIVOT.1, smooth(push * 1.4)), z: cam.z * pow(8.5 / cam.z, push)) }
        let fanSpeed = 22 * seg(t, 0.15, 0.75)
        let fanAngle = t < 0.15 ? 0 : 0.6 * pow(t - 0.15, 2) * 22 / 1.2
        let vortexSpin = -t * (0.5 + 2.5 * push)
        island(ctx, t: t + 5, cam: cam, ref: ref, W: W, H: H, fanSpeed: fanSpeed, fanAngle: fanAngle, vortexAngle: vortexSpin, focus: 1.0, dof: push > 0 ? 0 : 2, between: { f in
            if f == 1.0 {
                let g = eff(cam, ref, 1.0)
                // warp: shrink spinning into the vortex centre
                let warp = seg(t, 2.5, 3.2)
                let gx = lerp(px, VORTEX_PIVOT.0, warp), gy = lerp(py, VORTEX_PIVOT.1 + 20, warp)
                let onIce = py > 615 ? 0.22 : 0
                rig(ctx, "hero_adventure", min(t, 3.36), gx: gx, gy: gy, u: pu, cam: g, W: W, H: H, reflect: onIce, follow: true)
            }
        })
        wind(ctx, t: t, strength: seg(t, 0.35, 0.7) * (1 - seg(t, 2.2, 2.6)), cam: eff(cam, ref, 0.95), W: W, H: H,
             path: [(1262, 520), (1160, 470), (1040, 490), (985, 580), (1000, 650), (1080, 690), (1220, 672), (1330, 640)])
        foreground(ctx, t: t, cam: cam, ref: ref, W: W, H: H, focus: 1, dof: push > 0 ? 0 : 2)
        // through the portal: procedural tunnel takes over the frame
        let through = seg(t, 2.95, 3.6)
        if through > 0 {
            let vp = toScreen(VORTEX_PIVOT.0, VORTEX_PIVOT.1, eff(cam, ref, 0.85), W, H)
            tunnel(ctx, t: t, cx: lerp(Double(vp.x), W / 2, through), cy: lerp(Double(vp.y), H / 2, through), r: lerp(85 * cam.z, 1250, through), alpha: min(1, through * 2.5))
        }
        grade(ctx, W: W, H: H, warm: 0.8 * (1 - through), lift: 1)
    }

    // ------------------------------------------------------------------ night (SHOT 09 + end card background)
    static func night(_ ctx: CGContext, t: Double, W: Double, H: Double, p: [String: Double]) {
        let endAt = p["end"] ?? 4.0
        if t >= endAt {
            // end card: the same night, focus pulled into the distance and gently dimmed
            let u = seg(t, endAt, endAt + 0.8)
            let img = renderOffscreen(W: W, H: H) { c in nightScene(c, t: t, W: W, H: H) }
            var ciImg = CIImage(cgImage: img)
            ciImg = ciImg.clampedToExtent().applyingGaussianBlur(sigma: 14 * u).cropped(to: CGRect(x: 0, y: 0, width: W, height: H))
            let out = ci.createCGImage(ciImg, from: CGRect(x: 0, y: 0, width: W, height: H), format: .RGBA8, colorSpace: sRGB)!
            ctx.draw(out, in: CGRect(x: 0, y: 0, width: W, height: H))
            ctx.saveGState(); ctx.setFillColor(color("#1E1730", 0.28 * u)); ctx.fill(CGRect(x: 0, y: 0, width: W, height: H)); ctx.restoreGState()
            return
        }
        nightScene(ctx, t: t, W: W, H: H)
    }

    static func renderOffscreen(W: Double, H: Double, _ body: (CGContext) -> Void) -> CGImage {
        let c = CGContext(data: nil, width: Int(W), height: Int(H), bitsPerComponent: 8, bytesPerRow: 0, space: sRGB, bitmapInfo: CGImageAlphaInfo.premultipliedFirst.rawValue | CGBitmapInfo.byteOrder32Little.rawValue)!
        body(c); return c.makeImage()!
    }

    static func cloudShape(_ pts: [(Double, Double, Double)]) -> CGPath {
        let p = CGMutablePath()
        for (x, y, r) in pts { p.addEllipse(in: CGRect(x: x - r, y: y - r, width: 2 * r, height: 2 * r)) }
        return p
    }

    static func nightScene(_ ctx: CGContext, t: Double, W: Double, H: Double) {
        let push = seg(t, 0, 5.5, "linear")
        let z = lerp(1.0, 1.07, push)
        func layerT(_ f: Double, _ body: () -> Void) {
            ctx.saveGState(); let zz = pow(z, f); ctx.translateBy(x: W / 2, y: H * 0.42); ctx.scaleBy(x: zz, y: zz); ctx.translateBy(x: -W / 2 - push * 20 * f, y: -H * 0.42); body(); ctx.restoreGState()
        }
        // sky
        let sky = CGGradient(colorsSpace: sRGB, colors: [color("#2C2752"), color("#4F4382"), color("#8C74B2"), color("#D9A9C4")] as CFArray, locations: [0, 0.45, 0.78, 1])!
        ctx.drawLinearGradient(sky, start: CGPoint(x: 0, y: H), end: CGPoint(x: 0, y: H * 0.18), options: [.drawsAfterEndLocation])
        layerT(0.2) {
            for i in 0..<90 {
                let x = rnd(i, 1) * W, y = H * (0.42 + 0.58 * rnd(i, 2)), r = 0.8 + 1.8 * rnd(i, 3) * rnd(i, 3)
                let tw = 0.55 + 0.45 * sin(t * (1 + rnd(i, 4) * 2) + rnd(i, 5) * 6)
                ctx.setFillColor(color("#FFF6E0", 0.75 * tw)); ctx.fillEllipse(in: CGRect(x: x - r, y: y - r, width: 2 * r, height: 2 * r))
            }
            // moon: crescent with a soft halo
            let m = CGPoint(x: W * 0.8, y: H * 0.8)
            glow(ctx, m, 300, "#F7E2C0", 0.22)
            // soft full moon (gradient disc, faint maria) — no hard crescent edge
            ctx.saveGState()
            ctx.addEllipse(in: CGRect(x: m.x - 54, y: m.y - 54, width: 108, height: 108)); ctx.clip()
            let mg = CGGradient(colorsSpace: sRGB, colors: [color("#FFF8E6"), color("#F4E2C2")] as CFArray, locations: [0, 1])!
            ctx.drawRadialGradient(mg, startCenter: CGPoint(x: m.x - 14, y: m.y + 14), startRadius: 0, endCenter: m, endRadius: 58, options: [.drawsAfterEndLocation])
            for (dx, dy, r) in [(-14.0, 8.0, 13.0), (16.0, -10.0, 9.0), (6.0, 20.0, 7.0)] { ctx.setFillColor(color("#E8D2AE", 0.45)); ctx.fillEllipse(in: CGRect(x: m.x + dx - r, y: m.y + dy - r, width: 2 * r, height: 2 * r)) }
            ctx.restoreGState()
        }
        // far cloud range
        layerT(0.45) {
            var fpts: [(Double, Double, Double)] = []
            for i in 0..<14 { let x: Double = Double(i) * 160 - 60; let y: Double = H * 0.36 + 30 * sin(Double(i) * 1.7); let r: Double = 120 + 40 * rnd(i, 8); fpts.append((x, y, r)) }
            let far = cloudShape(fpts)
            ctx.saveGState(); ctx.addPath(far); ctx.clip()
            let g = CGGradient(colorsSpace: sRGB, colors: [color("#7E6AA8"), color("#A48CC8")] as CFArray, locations: [0, 1])!
            ctx.drawLinearGradient(g, start: CGPoint(x: 0, y: H * 0.2), end: CGPoint(x: 0, y: H * 0.5), options: [.drawsBeforeStartLocation, .drawsAfterEndLocation])
            ctx.restoreGState()
        }
        // cloud bed (where the Nests rest): moonlit top edge
        layerT(0.85) {
            var pts: [(Double, Double, Double)] = []
            for i in 0..<12 { let x: Double = Double(i) * 190 - 80; let y: Double = H * 0.26 + 26 * sin(Double(i) * 2.1); let r: Double = 170 + 50 * rnd(i, 11); pts.append((x, y, r)) }
            pts.append((W / 2, -200, 700))
            let bed = cloudShape(pts)
            ctx.saveGState(); ctx.addPath(bed); ctx.clip()
            let g = CGGradient(colorsSpace: sRGB, colors: [color("#8E79BE"), color("#B7A2DE"), color("#E5DAFA")] as CFArray, locations: [0, 0.7, 1])!
            ctx.drawLinearGradient(g, start: CGPoint(x: 0, y: 0), end: CGPoint(x: 0, y: H * 0.47), options: [.drawsBeforeStartLocation, .drawsAfterEndLocation])
            ctx.setBlendMode(.screen); glow(ctx, CGPoint(x: W * 0.8, y: H * 0.5), 900, "#C9B8F2", 0.25)
            ctx.restoreGState()
        }
        // the Plops in their pillow Nests: night relight (cool ambient, moon rim from the right, lantern warmth from the left)
        func relight(_ c: CGContext, _ r: CGRect) {
            c.setBlendMode(.multiply); c.setFillColor(color("#A49CD0")); c.fill(r)
            c.setBlendMode(.screen)
            let g = CGGradient(colorsSpace: sRGB, colors: [color("#FFC98A", 0.30), color("#FFC98A", 0)] as CFArray, locations: [0, 1])!
            c.drawLinearGradient(g, start: CGPoint(x: r.minX, y: r.midY), end: CGPoint(x: r.midX, y: r.midY), options: [])
            let m = CGGradient(colorsSpace: sRGB, colors: [color("#DCD6FF", 0), color("#DCD6FF", 0.22)] as CFArray, locations: [0, 1])!
            c.drawLinearGradient(m, start: CGPoint(x: r.midX, y: r.midY), end: CGPoint(x: r.maxX, y: r.maxY), options: [])
        }
        let flat = flat(W, H)
        layerT(0.95) {
            rig(ctx, "sleep_mint", t, gx: 600, gy: H - 352, u: 215, cam: flat, W: W, H: H, breathe: asleep("sleep_mint", t), blur: 1.2, relight: relight)
            rig(ctx, "sleep_lilac", t, gx: 1370, gy: H - 360, u: 215, cam: flat, W: W, H: H, breathe: asleep("sleep_lilac", t), blur: 1.2, relight: relight)
        }
        layerT(1.0) {
            rig(ctx, "sleep_honey", t, gx: 760, gy: H - 205, u: 290, cam: flat, W: W, H: H, breathe: asleep("sleep_honey", t), relight: relight)
            rig(ctx, "sleep_pink", t, gx: 1185, gy: H - 190, u: 320, cam: flat, W: W, H: H, breathe: asleep("sleep_pink", t), relight: relight)
            // subtle z's only above Plops whose eyes are already closed
            for (seq, x, y, s) in [("sleep_pink", 1215.0, 655.0, 1.0), ("sleep_honey", 790.0, 660.0, 0.8)] {
                let a = asleep(seq, t)
                if a <= 0 { continue }
                let since = t - asleepSince(seq)
                for k in 0..<3 {
                    let ph = ((since - Double(k) * 0.8) / 2.4)
                    if ph < 0 || ph > 1 { continue }
                    let size = (40 + 22 * ph) * s
                    let alpha = 0.55 * sin(.pi * ph) * a
                    let line = CTLineCreateWithAttributedString(NSAttributedString(string: "z", attributes: [.init(kCTFontAttributeName as String): ctFont("Nunito-ExtraBold", size), .init(kCTForegroundColorAttributeName as String): color("#FFF6E8", alpha)]))
                    ctx.textPosition = CGPoint(x: x + ph * 40 * s + sin(ph * 5) * 8, y: H - y + ph * 120 * s); CTLineDraw(line, ctx)
                }
            }
        }
        // foreground: blossom branch and lantern, relit for night (lantern stays warm and emissive)
        layerT(1.3) {
            let cam = Cam(cx: 621, cy: 335, z: 2.0)
            ctx.saveGState()
            // canopy only: loose petals below the branches would read as dark blobs once night-tinted → soft vertical fade
            let keep = toScreen(0, 122, cam, W, H), drop = toScreen(0, 162, cam, W, H)
            let mc = CGContext(data: nil, width: Int(W), height: Int(H), bitsPerComponent: 8, bytesPerRow: 0, space: CGColorSpaceCreateDeviceGray(), bitmapInfo: CGImageAlphaInfo.none.rawValue)!
            let mg = CGGradient(colorsSpace: CGColorSpaceCreateDeviceGray(), colors: [CGColor(gray: 1, alpha: 1), CGColor(gray: 0, alpha: 1)] as CFArray, locations: [0, 1])!
            mc.drawLinearGradient(mg, start: keep, end: drop, options: [.drawsBeforeStartLocation, .drawsAfterEndLocation])
            ctx.clip(to: CGRect(x: 0, y: 0, width: W, height: H), mask: mc.makeImage()!)
            ctx.beginTransparencyLayer(auxiliaryInfo: nil)
            plateRotated(ctx, L + "P_tree_c.png", x0: 0, y0: 0, pivot: (240, 60), angle: 0.01 * sin(t * 0.9), cam: cam, W: W, H: H, blur: 3)
            ctx.setBlendMode(.sourceAtop); ctx.setFillColor(color("#4B3F78", 0.62)); ctx.fill(CGRect(x: 0, y: 0, width: W, height: H))
            ctx.endTransparencyLayer(); ctx.restoreGState()
            plateRotated(ctx, L + "P_lantern_c.png", x0: 212, y0: 112, pivot: (291, 128), angle: 0.04 * sin(t * 1.2), cam: cam, W: W, H: H, blur: 1.5)
            let lp = toScreen(279, 212, cam, W, H)
            ctx.saveGState(); ctx.setBlendMode(.screen); glow(ctx, lp, 260, "#FFC46E", 0.28 + 0.03 * sin(t * 4.1)); glow(ctx, lp, 70, "#FFE2A8", 0.35); ctx.restoreGState()
        }
        // vignette
        let v = CGGradient(colorsSpace: sRGB, colors: [color("#120C22", 0), color("#120C22", 0.35)] as CFArray, locations: [0.5, 1])!
        ctx.drawRadialGradient(v, startCenter: CGPoint(x: W / 2, y: H * 0.45), startRadius: 0, endCenter: CGPoint(x: W / 2, y: H * 0.45), endRadius: W * 0.75, options: [.drawsAfterEndLocation])
    }
    static func asleep(_ seq: String, _ t: Double) -> Double {
        let m = meta(seq); let i = max(0, min(m.frames.count - 1, Int(t * 30)))
        return m.frames[i].asleep ? 1 : 0
    }
    static func asleepSince(_ seq: String) -> Double {
        let m = meta(seq); return m.frames.first(where: { $0.asleep })?.t ?? 99
    }

    // ------------------------------------------------------------------ SHOT 08: the hero tries looks (Character Studio rig)
    static func studio(_ ctx: CGContext, t: Double, W: Double, H: Double, p: [String: Double]) {
        let beat = p["beat"] ?? 0.6667
        let looks = ["look_bow", "look_flower", "look_crown", "look_nightcap", "look_star"]
        let k = min(looks.count - 1, Int(t / beat))
        // soft studio backdrop: lilac → cream, a round floor pad with a contact shadow
        let g = CGGradient(colorsSpace: sRGB, colors: [color("#F3E6FA"), color("#FFF6EE")] as CFArray, locations: [0, 1])!
        ctx.drawLinearGradient(g, start: CGPoint(x: 0, y: H), end: CGPoint(x: 0, y: 0), options: [])
        let cx = p["cx"] ?? W / 2, floorY = p["floor"] ?? H * 0.22
        glow(ctx, CGPoint(x: cx, y: H - (H - floorY) + 40), 520, "#FFFFFF", 0.65)
        ctx.saveGState(); ctx.setFillColor(color("#EADCF2")); ctx.fillEllipse(in: CGRect(x: cx - 330, y: floorY - 70, width: 660, height: 140)); ctx.restoreGState()
        let u = p["u"] ?? 560
        rig(ctx, looks[k], t, gx: cx, gy: H - floorY, u: u, cam: flat(W, H), W: W, H: H)
        // a single soft sparkle on each change (no particle spray)
        let since = t - Double(k) * beat
        if k > 0 && since < 0.35 {
            let a = 1 - since / 0.35
            ctx.saveGState(); ctx.setBlendMode(.screen); glow(ctx, CGPoint(x: cx + 150, y: H - floorY + u * 0.95), 90 * (0.6 + since), "#FFFFFF", 0.7 * a); ctx.restoreGState()
        }
    }

    // ------------------------------------------------------------------ thumbnails (single frames, 2560x1440 design)
    static func thumb(_ ctx: CGContext, W: Double, H: Double, p: [String: Double]) {
        let kind = Int(p["kind"] ?? 0)
        let id = flat(W, H)
        if kind == 0 {
            // A — hero mascot: bright island behind (defocused), mossy ledge, a large happy pink Plop with contact shadow
            let cam = Cam(cx: 1390, cy: 430, z: 2.35)
            island(ctx, t: 2, cam: cam, ref: cam, W: W, H: H, fanSpeed: 0, fanAngle: 0.3, vortexAngle: 0, focus: 1.6, dof: 4)
            rays(ctx, t: 0.5, W: W, H: H, a: 1)
            moss(ctx, t: 0.6, top: 250, W: W, r: 64, seed: 11, tufts: 16)
            contactShadow(ctx, CGPoint(x: 680, y: 205), w: 330, a: 0.32)
            rig(ctx, "still_pink_happy", 0, gx: 680, gy: H - 205, u: 700, cam: id, W: W, H: H)
            grade(ctx, W: W, H: H, warm: 0.7, lift: 1)
        } else if kind == 2 {
            // C — expressive mascot beside the real board
            contactShadow(ctx, CGPoint(x: p["x"] ?? 1500, y: p["floor"] ?? 150), w: 210, a: 0.25)
            rig(ctx, "still_pink_excited", 0, gx: p["x"] ?? 1500, gy: H - (p["floor"] ?? 150), u: p["u"] ?? 470, cam: id, W: W, H: H)
        }
    }
}
