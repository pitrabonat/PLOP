// Cinematic.swift — the island scenes of the official PLOP! trailer, drawn procedurally per frame for plopcomp.
//
// Layered 2.5D on the owner's reference illustration (02_Cinematic/Source): the plate, a clean-plate patch, and the four
// Plops lifted out of it (seg.swift) are re-composited with a moving camera. Everything that moves is real motion,
// not a zoom on a still: camera dolly/pan, Plop squash/hop, rotating fan blades and portal vortex (sampled from the plate
// itself), waterfall shimmer, drifting clouds, falling petals, dust motes, light rays, lantern flicker, lava bubbles,
// wind streaks, near-camera bokeh for depth, a game Plop sprite that slides across the ice into the portal, and a
// day→dusk grade with stars for the ending.
//
// Scene names: island_wide, island_plops, island_adventure, island_dusk. t = seconds since the layer started.
import Foundation
import CoreGraphics
import CoreText

enum Cinematic {
    static let srcDir = "/Volumes/PLOP_DEV/Marketing/FinalSprint03/02_Cinematic/Source"
    static let spriteDir = "/Volumes/PLOP_DEV/Marketing/FinalSprint03/01_Brand/Sprites"
    static let PW = 1774.0, PH = 887.0
    struct Cut { let name: String; let x: Double; let y: Double; let w: Double; let h: Double }
    static let cuts: [String: Cut] = [
        "pink": Cut(name: "pink", x: 175, y: 440, w: 535, h: 400), "yellow": Cut(name: "yellow", x: 610, y: 500, w: 285, h: 270),
        "green": Cut(name: "green", x: 5, y: 415, w: 285, h: 240), "purple": Cut(name: "purple", x: 60, y: 262, w: 275, h: 182),
        "bang": Cut(name: "bang", x: 500, y: 370, w: 140, h: 115)]
    // plate landmarks
    static let fanHub = CGPoint(x: 1329, y: 514), portal = CGPoint(x: 1622, y: 488), lantern = CGPoint(x: 278, y: 212)

    static var plate: CGImage { loadImage(srcDir + "/Owner_Reference_Island_1774x887.png") }
    static func lifted(_ n: String) -> CGImage { loadImage(srcDir + "/Lifted/\(n).png") }
    static var fanCrop: CGImage = plate.cropping(to: CGRect(x: fanHub.x - 84, y: fanHub.y - 84, width: 168, height: 168))!
    static var portalCrop: CGImage = plate.cropping(to: CGRect(x: portal.x - 100, y: portal.y - 116, width: 200, height: 232))!

    // MARK: helpers (plate space is y-down after `camera`)
    static func rnd(_ i: Int, _ k: Int) -> Double { var x = UInt64(i &* 73856093 ^ k &* 19349663) &+ 0x9E3779B97F4A7C15; x ^= x >> 33; x = x &* 0xff51afd7ed558ccd; x ^= x >> 33; return Double(x % 100000) / 100000 }
    static func lerp(_ a: Double, _ b: Double, _ u: Double) -> Double { a + (b - a) * u }
    static func seg(_ t: Double, _ a: Double, _ b: Double, _ kind: String = "inOut") -> Double { ease((t - a) / max(1e-6, b - a), kind) }
    static func drawImg(_ c: CGContext, _ im: CGImage, _ r: CGRect) {
        c.saveGState(); c.translateBy(x: r.minX, y: r.maxY); c.scaleBy(x: 1, y: -1); c.interpolationQuality = .high
        c.draw(im, in: CGRect(x: 0, y: 0, width: r.width, height: r.height)); c.restoreGState()
    }
    static func glow(_ c: CGContext, _ p: CGPoint, _ r: Double, _ col: String, _ a: Double) {
        let g = CGGradient(colorsSpace: sRGB, colors: [color(col, a), color(col, 0)] as CFArray, locations: [0, 1])!
        c.drawRadialGradient(g, startCenter: p, startRadius: 0, endCenter: p, endRadius: r, options: [])
    }
    struct Cam { var x: Double; var y: Double; var z: Double }
    static func camera(_ c: CGContext, _ cam: Cam, W: Double, H: Double) -> (Double, Cam) {
        let k = H / PH * cam.z
        var cc = cam
        let hw = W / 2 / k, hh = H / 2 / k
        cc.x = min(max(cc.x, hw), PW - hw); cc.y = min(max(cc.y, hh), PH - hh)
        c.translateBy(x: W / 2, y: H / 2); c.scaleBy(x: k, y: -k); c.translateBy(x: -cc.x, y: -cc.y)
        return (k, cc)
    }
    static func toScreen(_ p: CGPoint, _ k: Double, _ cam: Cam, W: Double, H: Double) -> CGPoint {
        CGPoint(x: W / 2 + (p.x - cam.x) * k, y: H / 2 + (p.y - cam.y) * k)   // top-left origin screen coords
    }

    // MARK: world layers
    static func drawPlops(_ c: CGContext, t: Double, hops: [String: [Double]], squash: Double = 0.025, sleepy: Double = 0) {
        // base: original plate already drawn; pink patch only when pink moves
        if (hops["pink"] ?? []).contains(where: { t - $0 > -0.02 && t - $0 < 0.66 }) { drawImg(c, lifted("patch_pink"), CGRect(x: 0, y: 0, width: PW, height: PH)) }
        for (i, n) in ["yellow", "green", "purple", "pink"].enumerated() {
            let cu = cuts[n]!
            let base = CGPoint(x: cu.x + cu.w / 2, y: cu.y + cu.h - 8)
            // idle breathing (never shrinks below the original silhouette, so no plate edge shows)
            let ph = Double(i) * 1.7
            let br = (sin(2 * .pi * 0.55 * t + ph) + 1) / 2 * squash * (1 - 0.6 * sleepy)
            var sx = 1.012 + br, sy = 1.012 + squash - br
            var lift = 0.0
            for h0 in hops[n] ?? [] {
                let u = t - h0
                if u > 0 && u < 0.62 {
                    // anticipation squash → stretch & lift → land squash
                    if u < 0.12 { let q = u / 0.12; sx += 0.10 * q; sy -= 0.09 * q }
                    else if u < 0.42 { let q = (u - 0.12) / 0.30; lift = sin(.pi * q) * (n == "pink" ? 46 : 30); sx += 0.10 * (1 - q) - 0.05 * sin(.pi * q); sy -= 0.09 * (1 - q) - 0.08 * sin(.pi * q) }
                    else { let q = (u - 0.42) / 0.20; sx += 0.08 * sin(.pi * q); sy -= 0.07 * sin(.pi * q) }
                }
            }
            c.saveGState()
            // contact shadow when lifted
            if lift > 1 { c.setFillColor(color("#5B3A4A", 0.18 * min(1, lift / 30))); c.fillEllipse(in: CGRect(x: base.x - cu.w * 0.36, y: base.y - 10, width: cu.w * 0.72, height: 20)) }
            c.translateBy(x: base.x, y: base.y - lift); c.scaleBy(x: sx, y: sy); c.translateBy(x: -base.x, y: -base.y)
            drawImg(c, lifted("cut_" + n), CGRect(x: cu.x, y: cu.y, width: cu.w, height: cu.h))
            c.restoreGState()
        }
    }
    static func bang(_ c: CGContext, t: Double, at: Double?) {
        let cu = cuts["bang"]!
        var s = 1.0, o = 1.0
        if let a = at { let u = t - a; if u < 0 { s = 0.001; o = 0 } else { s = max(0.001, ease(u / 0.35, "back")); o = min(1, u / 0.12) } }
        c.saveGState(); c.setAlpha(o)
        let p = CGPoint(x: cu.x + 20, y: cu.y + cu.h)
        c.translateBy(x: p.x, y: p.y); c.scaleBy(x: s, y: s); c.translateBy(x: -p.x, y: -p.y)
        drawImg(c, lifted("cut_bang"), CGRect(x: cu.x, y: cu.y, width: cu.w, height: cu.h)); c.restoreGState()
    }
    static func fan(_ c: CGContext, t: Double, speed: Double) {
        let ang = t * speed * 2 * .pi
        c.saveGState(); c.addEllipse(in: CGRect(x: fanHub.x - 66, y: fanHub.y - 66, width: 132, height: 132)); c.clip()
        for k in 0..<3 {   // three samples = motion blur
            c.saveGState(); c.setAlpha(k == 0 ? 1 : 0.4)
            c.translateBy(x: fanHub.x, y: fanHub.y); c.rotate(by: ang - Double(k) * 0.12 * min(1, speed / 2)); c.translateBy(x: -fanHub.x, y: -fanHub.y)
            drawImg(c, fanCrop, CGRect(x: fanHub.x - 84, y: fanHub.y - 84, width: 168, height: 168)); c.restoreGState()
        }
        c.restoreGState()
    }
    static func vortex(_ c: CGContext, t: Double, speed: Double, pulse: Double = 1) {
        let ang = -t * speed * 2 * .pi
        c.saveGState(); c.addEllipse(in: CGRect(x: portal.x - 74, y: portal.y - 92, width: 148, height: 184)); c.clip()
        c.translateBy(x: portal.x, y: portal.y); c.rotate(by: ang); c.translateBy(x: -portal.x, y: -portal.y)
        drawImg(c, portalCrop, CGRect(x: portal.x - 100, y: portal.y - 116, width: 200, height: 232))
        c.restoreGState()
        c.saveGState(); c.setBlendMode(.screen)
        glow(c, portal, 120, "#C9A0FF", 0.22 + 0.12 * sin(t * 3.1) * pulse); c.restoreGState()
        // sparkles orbiting
        for i in 0..<10 {
            let a = rnd(i, 1) * 2 * .pi - t * (0.8 + rnd(i, 2)), r = 30 + rnd(i, 3) * 70
            let p = CGPoint(x: portal.x + cos(a) * r * 0.8, y: portal.y + sin(a) * r)
            let tw = (sin(t * 6 + Double(i)) + 1) / 2
            c.setFillColor(color("#FFFFFF", 0.35 + 0.5 * tw)); c.fillEllipse(in: CGRect(x: p.x - 2.2, y: p.y - 2.2, width: 4.4, height: 4.4))
        }
    }
    static func waterfall(_ c: CGContext, t: Double) {
        c.saveGState(); c.addRect(CGRect(x: 1492, y: 196, width: 124, height: 176)); c.clip()
        c.setLineCap(.round)
        for i in 0..<26 {
            let x = 1494 + rnd(i, 7) * 120, len = 18 + rnd(i, 8) * 30, sp = 150 + rnd(i, 9) * 90
            let y = 196 + (t * sp + rnd(i, 10) * 200).truncatingRemainder(dividingBy: 210) - 20
            c.setStrokeColor(color("#FFFFFF", 0.22 + 0.25 * rnd(i, 11))); c.setLineWidth(1.6 + rnd(i, 12) * 2)
            c.move(to: CGPoint(x: x, y: y)); c.addLine(to: CGPoint(x: x, y: y + len)); c.strokePath()
        }
        c.restoreGState()
        c.saveGState(); c.setBlendMode(.screen)
        glow(c, CGPoint(x: 1556, y: 372), 60 + 6 * sin(t * 4), "#FFFFFF", 0.35); c.restoreGState()
    }
    static func clouds(_ c: CGContext, t: Double) {
        for i in 0..<5 {
            let x = (560 + rnd(i, 20) * 700 + t * (8 + rnd(i, 21) * 10)), y = 70 + rnd(i, 22) * 200
            let s = 60 + rnd(i, 23) * 70
            for j in 0..<4 { glow(c, CGPoint(x: x + Double(j) * s * 0.55, y: y + sin(Double(j)) * s * 0.18), s, "#FFFFFF", 0.16) }
        }
    }
    static func rays(_ c: CGContext, t: Double, strength: Double) {
        c.saveGState(); c.setBlendMode(.screen)
        let o = CGPoint(x: 820, y: -160)
        for i in 0..<6 {
            let a = 1.05 + Double(i) * 0.16 + 0.03 * sin(t * 0.4 + Double(i))
            let w = 0.045 + rnd(i, 30) * 0.04, L = 1200.0
            let al = strength * (0.06 + 0.05 * (sin(t * 0.9 + Double(i) * 1.3) + 1) / 2)
            c.saveGState()
            c.move(to: o); c.addLine(to: CGPoint(x: o.x + cos(a - w) * L, y: o.y + sin(a - w) * L)); c.addLine(to: CGPoint(x: o.x + cos(a + w) * L, y: o.y + sin(a + w) * L)); c.closePath(); c.clip()
            let g = CGGradient(colorsSpace: sRGB, colors: [color("#FFF3C8", al), color("#FFF3C8", 0)] as CFArray, locations: [0, 1])!
            c.drawRadialGradient(g, startCenter: o, startRadius: 100, endCenter: o, endRadius: L, options: [])
            c.restoreGState()
        }
        c.restoreGState()
    }
    static func lanternGlow(_ c: CGContext, t: Double, boost: Double) {
        let fl = 0.85 + 0.1 * sin(t * 7.3) + 0.06 * sin(t * 13.1 + 1) + 0.04 * sin(t * 23.7)
        c.saveGState(); c.setBlendMode(.screen)
        glow(c, lantern, 70 + 30 * boost, "#FFE28A", (0.30 + 0.35 * boost) * fl)
        glow(c, CGPoint(x: 1503, y: 158), 34, "#FFD27A", 0.25 * boost * fl)   // house window
        c.restoreGState()
    }
    static func lava(_ c: CGContext, t: Double) {
        c.saveGState(); c.setBlendMode(.screen)
        glow(c, CGPoint(x: 1470, y: 780), 170, "#FF9A3C", 0.12 + 0.07 * sin(t * 2.3)); c.restoreGState()
        for i in 0..<6 {
            let per = 1.4 + rnd(i, 40), u = (t + rnd(i, 41) * per).truncatingRemainder(dividingBy: per) / per
            let p = CGPoint(x: 1370 + rnd(i, 42) * 200, y: 760 + rnd(i, 43) * 50)
            let r = 4 + u * 12
            c.setFillColor(color("#FFC35A", 0.7 * (1 - u))); c.fillEllipse(in: CGRect(x: p.x - r, y: p.y - r * 0.7, width: 2 * r, height: 1.4 * r))
            c.setStrokeColor(color("#FFF1B0", 0.6 * (1 - u))); c.setLineWidth(1.5); c.strokeEllipse(in: CGRect(x: p.x - r, y: p.y - r * 0.7, width: 2 * r, height: 1.4 * r))
        }
    }
    static func wind(_ c: CGContext, t: Double, strength: Double) {
        c.saveGState(); c.setLineCap(.round)
        for i in 0..<7 {
            let y0 = 470 + rnd(i, 50) * 110, ph = (t * (1.1 + rnd(i, 51) * 0.5) + rnd(i, 52)).truncatingRemainder(dividingBy: 1)
            let xh = 1250 - ph * 360, len = 110 + rnd(i, 53) * 80
            let a = strength * 0.55 * sin(.pi * ph)
            c.setStrokeColor(color("#FFFFFF", a)); c.setLineWidth(3 + rnd(i, 54) * 3)
            c.move(to: CGPoint(x: xh, y: y0)); c.addCurve(to: CGPoint(x: xh - len, y: y0 + 8), control1: CGPoint(x: xh - len * 0.3, y: y0 - 14), control2: CGPoint(x: xh - len * 0.7, y: y0 + 20))
            c.strokePath()
        }
        c.restoreGState()
    }
    static func petals(_ c: CGContext, t: Double, n: Int = 34) {
        for i in 0..<n {
            let per = 6 + rnd(i, 60) * 5, u = (t + rnd(i, 61) * per).truncatingRemainder(dividingBy: per) / per
            let x = rnd(i, 62) * 900 + u * 520 + 26 * sin(u * 9 + Double(i)), y = -40 + u * 900
            let rot = u * 10 + Double(i), s = 7 + rnd(i, 63) * 7
            c.saveGState(); c.translateBy(x: x, y: y); c.rotate(by: rot); c.scaleBy(x: 1, y: 0.45 + 0.4 * abs(sin(u * 14 + Double(i))))
            c.setFillColor(color(i % 3 == 0 ? "#FFC2D6" : "#FF9EBD", 0.9)); c.fillEllipse(in: CGRect(x: -s, y: -s * 0.6, width: 2 * s, height: 1.2 * s))
            c.restoreGState()
        }
    }
    static func motes(_ c: CGContext, t: Double, n: Int = 40, a: Double = 1) {
        c.saveGState(); c.setBlendMode(.screen)
        for i in 0..<n {
            let x = (rnd(i, 70) * PW + t * (6 + rnd(i, 71) * 14)).truncatingRemainder(dividingBy: PW)
            let y = 120 + rnd(i, 72) * 700 + 14 * sin(t * (0.6 + rnd(i, 73)) + Double(i))
            let tw = (sin(t * (1.5 + rnd(i, 74) * 2) + Double(i) * 2) + 1) / 2
            glow(c, CGPoint(x: x, y: y), 3 + rnd(i, 75) * 5, "#FFF6D0", a * (0.25 + 0.6 * tw))
        }
        c.restoreGState()
    }
    static func dusk(_ c: CGContext, t: Double, d: Double) {
        guard d > 0 else { return }
        c.saveGState(); c.setBlendMode(.multiply); c.setFillColor(color("#8A78C0", 0.55 * d)); c.fill(CGRect(x: 0, y: 0, width: PW, height: PH)); c.restoreGState()
        let g = CGGradient(colorsSpace: sRGB, colors: [color("#2E2A66", 0.45 * d), color("#2E2A66", 0)] as CFArray, locations: [0, 1])!
        c.drawLinearGradient(g, start: CGPoint(x: 0, y: 0), end: CGPoint(x: 0, y: 520), options: [])
        for i in 0..<60 {
            let p = CGPoint(x: 380 + rnd(i, 80) * 1000, y: 20 + rnd(i, 81) * 300)
            let tw = (sin(t * (1 + rnd(i, 82) * 2.5) + Double(i)) + 1) / 2
            let r = 1.2 + rnd(i, 83) * 2.2
            c.setFillColor(color("#FFFFFF", d * (0.25 + 0.7 * tw))); c.fillEllipse(in: CGRect(x: p.x - r, y: p.y - r, width: 2 * r, height: 2 * r))
        }
        c.saveGState(); c.setBlendMode(.screen); glow(c, CGPoint(x: 1180, y: 150), 70, "#FFF7D6", 0.55 * d); glow(c, CGPoint(x: 1180, y: 150), 26, "#FFFFFF", 0.8 * d); c.restoreGState()
    }
    static func bokeh(_ c: CGContext, t: Double, W: Double, H: Double, drift: Double, a: Double = 1) {   // screen space, near camera
        c.saveGState(); c.setBlendMode(.screen)
        for i in 0..<7 {
            let x = (rnd(i, 90) * W * 1.4 - drift * (0.6 + rnd(i, 91)) * W).truncatingRemainder(dividingBy: W * 1.4)
            let xx = x < -W * 0.2 ? x + W * 1.4 : x
            let y = rnd(i, 92) * H + 30 * sin(t * 0.5 + Double(i))
            let r = 60 + rnd(i, 93) * 120
            glow(c, CGPoint(x: xx, y: H - y), r, i % 2 == 0 ? "#FFC6DA" : "#FFF4D8", a * 0.22)
        }
        c.restoreGState()
    }
    static func zzz(_ c: CGContext, t: Double, at: CGPoint, k: Double, cam: Cam, W: Double, H: Double, a: Double) {
        for j in 0..<3 {
            let u = (t * 0.45 + Double(j) / 3).truncatingRemainder(dividingBy: 1)
            let p = toScreen(CGPoint(x: at.x + 30 * u + 10 * sin(u * 6), y: at.y - 90 * u), k, cam, W: W, H: H)
            let size = (34 + 26 * u) * k
            let font = CTFontCreateWithName("Nunito-ExtraBold" as CFString, size, nil)
            let attrs: [NSAttributedString.Key: Any] = [.init(kCTFontAttributeName as String): font, .init(kCTForegroundColorAttributeName as String): color("#FFFFFF", a * sin(.pi * u))]
            let line = CTLineCreateWithAttributedString(NSAttributedString(string: "Z", attributes: attrs))
            c.textPosition = CGPoint(x: p.x, y: H - p.y); CTLineDraw(line, c)
        }
    }
    static func gamePlop(_ c: CGContext, _ name: String, _ p: CGPoint, size: Double, sx: Double, sy: Double, rot: Double, alpha: Double) {
        let im = loadImage(spriteDir + "/\(name).png")
        c.saveGState(); c.setAlpha(alpha)
        c.translateBy(x: p.x, y: p.y); c.rotate(by: rot); c.scaleBy(x: sx, y: sy)
        c.setShadow(offset: CGSize(width: 0, height: 6), blur: 10, color: color("#5B3A4A", 0.25))
        drawImg(c, im, CGRect(x: -size / 2, y: -size * 0.9, width: size, height: size)); c.restoreGState()
    }

    // MARK: shots
    static func draw(_ ctx: CGContext, name: String, t: Double, W: Double, H: Double, p: [String: Double], global: Double) {
        let c = ctx
        var cam: Cam
        switch name {
        case "island_wide":       // shot 1: little world — pull back from the lantern to the whole island, light rays, petals
            let u = seg(t, 0, p["dur"] ?? 5.33)
            cam = Cam(x: lerp(470, 887, u), y: lerp(250, 443, u), z: lerp(1.55, 1.0, u))
        case "island_plops":      // shot 2: meet the Plops — push in on the group, each one hops in turn
            let u = seg(t, 0, p["dur"] ?? 2.67)
            cam = Cam(x: lerp(400, 470, u), y: lerp(560, 575, u), z: lerp(1.45, 1.62, u))
        case "island_adventure":  // shot 3: a Plop slides over the ice past the fan into the portal; camera dives in
            let d = p["dur"] ?? 2.67
            let u1 = seg(t, 0, d * 0.62), u2 = seg(t, d * 0.62, d, "in")
            cam = Cam(x: lerp(lerp(1060, 1430, u1), 1622, u2), y: lerp(lerp(600, 560, u1), 488, u2), z: lerp(lerp(1.5, 1.75, u1), 5.0, u2))
        default:                  // island_dusk: ending — wide, slow push, day turns to dusk
            let u = seg(t, 0, p["dur"] ?? 8)
            cam = Cam(x: lerp(860, 887, u), y: lerp(470, 443, u), z: lerp(1.12, 1.0, u))
        }
        if let x = p["cx"], let y = p["cy"], let z = p["cz"] { cam = Cam(x: x, y: y, z: z) }   // fixed camera for stills
        let duskAmt = name == "island_dusk" ? seg(t, 0.2, 2.4) : 0
        c.saveGState()
        let (k, cc) = camera(c, cam, W: W, H: H)
        drawImg(c, plate, CGRect(x: 0, y: 0, width: PW, height: PH))
        clouds(c, t: global)
        waterfall(c, t: global)
        fan(c, t: global, speed: name == "island_adventure" ? 2.6 : 1.3)
        vortex(c, t: global, speed: name == "island_adventure" ? lerp(0.35, 1.4, seg(t, 1.2, 2.67, "in")) : 0.25)
        lava(c, t: global)
        wind(c, t: global, strength: name == "island_adventure" ? 1 : 0.55)
        var hops: [String: [Double]] = [:]
        if name == "island_plops" { hops = ["pink": [0.25], "yellow": [0.8], "green": [1.25], "purple": [1.7]] }
        if name == "island_wide" { hops = ["pink": [3.6]] }
        drawPlops(c, t: global, hops: hops, sleepy: duskAmt)
        if name == "island_plops" { bang(c, t: t, at: 0.32) } else if name != "island_dusk" { bang(c, t: t, at: nil) }
        if name == "island_adventure" {
            let d = p["dur"] ?? 2.67
            // slide across the ice (0–0.9 s), hop onto the stones (0.9–1.3), pulled into the portal (1.3–1.9)
            var pos: CGPoint, s = 1.0, rot = 0.0, al = 1.0, sx = 1.0, sy = 1.0
            if t < 0.9 { let u = seg(t, 0, 0.9, "out"); pos = CGPoint(x: lerp(980, 1265, u), y: lerp(735, 672, u)); sx = 1.12 - 0.12 * u; sy = 0.9 + 0.1 * u }
            else if t < 1.3 { let u = (t - 0.9) / 0.4; pos = CGPoint(x: lerp(1265, 1500, u), y: lerp(672, 600, u) - sin(.pi * u) * 70); sy = 1 + 0.12 * sin(.pi * u); sx = 1 - 0.08 * sin(.pi * u) }
            else { let u = seg(t, 1.3, min(1.95, d), "in"); pos = CGPoint(x: lerp(1500, portal.x, u), y: lerp(600, portal.y + 40, u)); s = 1 - 0.92 * u; rot = u * 7; al = 1 - 0.6 * u }
            if t < 1.95 { gamePlop(c, "Plop_Mint", pos, size: 120 * s, sx: sx, sy: sy, rot: rot, alpha: al) }
            if t < 0.9 {   // ice slide sparkle trail
                for j in 0..<6 { let q = max(0, t - Double(j) * 0.05); let u = seg(q, 0, 0.9, "out")
                    c.setFillColor(color("#FFFFFF", 0.5 * (1 - Double(j) / 6))); c.fillEllipse(in: CGRect(x: lerp(980, 1265, u) - 50 - 3, y: lerp(735, 672, u) - 10, width: 6, height: 6)) }
            }
        }
        rays(c, t: global, strength: name == "island_dusk" ? 1 - duskAmt : (name == "island_wide" ? 1.3 : 0.9))
        dusk(c, t: global, d: duskAmt)
        lanternGlow(c, t: global, boost: 0.3 + duskAmt)
        petals(c, t: global, n: name == "island_dusk" ? 14 : 34)
        motes(c, t: global, a: name == "island_dusk" ? 1.4 : 0.8)
        c.restoreGState()
        if name == "island_dusk" && duskAmt > 0.3 {
            for n in ["pink", "yellow", "green"] { let cu = cuts[n]!; zzz(c, t: global + Double(n.count), at: CGPoint(x: cu.x + cu.w * 0.72, y: cu.y + 10), k: k, cam: cc, W: W, H: H, a: min(1, (duskAmt - 0.3) * 2)) }
        }
        // near-camera bokeh for depth (moves faster than the plate)
        bokeh(c, t: global, W: W, H: H, drift: (cc.x / PW) * 1.6 + t * 0.03, a: name == "island_dusk" ? 0.6 : 1)
        // portal dive: vortex fills the frame, then flashes white for the reveal into gameplay
        if name == "island_adventure" {
            let d = p["dur"] ?? 2.67
            let u = seg(t, d - 0.75, d, "in")
            if u > 0 {
                let im = loadImage(spriteDir + "/Portal_Vortex.png")
                c.saveGState(); c.setAlpha(min(1, u * 1.6))
                c.translateBy(x: W / 2, y: H / 2); c.rotate(by: -t * 5.5); let s = lerp(500, 2600, u)
                c.draw(im, in: CGRect(x: -s / 2, y: -s / 2, width: s, height: s)); c.restoreGState()
                c.setFillColor(color("#FFF6EE", max(0, (u - 0.7) / 0.3))); c.fill(CGRect(x: 0, y: 0, width: W, height: H))
            }
        }
        // vignette
        let vg = CGGradient(colorsSpace: sRGB, colors: [color("#3A2030", 0), color("#3A2030", name == "island_dusk" ? 0.32 : 0.16)] as CFArray, locations: [0.55, 1])!
        c.drawRadialGradient(vg, startCenter: CGPoint(x: W / 2, y: H / 2), startRadius: 0, endCenter: CGPoint(x: W / 2, y: H / 2), endRadius: hypot(W, H) / 2, options: [.drawsAfterEndLocation])
    }
}
