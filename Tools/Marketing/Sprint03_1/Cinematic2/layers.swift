// layers.swift — splits the owner's island illustration into depth layers for 2.5D compositing (Sprint 03.1).
//   layers <plate.png> <outDir>
// Each layer keeps the original pixels where it is visible. Areas hidden behind nearer layers are reconstructed by
// multigrid diffusion (smooth fill) so small parallax shifts never reveal holes. Edges against the sky are keyed
// against a reconstructed sky, not cut by hand. Outputs straight-alpha RGBA PNGs at 2x (Lanczos + light sharpening).
import Foundation
import CoreGraphics
import CoreImage
import ImageIO
import UniformTypeIdentifiers

let sRGB = CGColorSpace(name: CGColorSpace.sRGB)!

struct Img { var w: Int; var h: Int; var r: [Float]; var g: [Float]; var b: [Float] }

func load(_ path: String) -> Img {
    let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil)!
    let im = CGImageSourceCreateImageAtIndex(src, 0, nil)!
    let w = im.width, h = im.height
    var px = [UInt8](repeating: 0, count: w * h * 4)
    let ctx = CGContext(data: &px, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: sRGB, bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue)!
    ctx.draw(im, in: CGRect(x: 0, y: 0, width: w, height: h))
    var o = Img(w: w, h: h, r: .init(repeating: 0, count: w * h), g: .init(repeating: 0, count: w * h), b: .init(repeating: 0, count: w * h))
    for i in 0..<(w * h) { o.r[i] = Float(px[i * 4]) / 255; o.g[i] = Float(px[i * 4 + 1]) / 255; o.b[i] = Float(px[i * 4 + 2]) / 255 }
    return o
}

/// Polygon / ellipse mask (1 inside), top-left image coordinates, then feathered by a box blur.
func mask(_ w: Int, _ h: Int, polys: [[(Double, Double)]] = [], ellipses: [(Double, Double, Double, Double)] = [], feather: Int = 2) -> [Float] {
    var px = [UInt8](repeating: 0, count: w * h)
    let ctx = CGContext(data: &px, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w, space: CGColorSpaceCreateDeviceGray(), bitmapInfo: CGImageAlphaInfo.none.rawValue)!
    ctx.translateBy(x: 0, y: Double(h)); ctx.scaleBy(x: 1, y: -1)
    ctx.setFillColor(gray: 1, alpha: 1)
    for p in polys { ctx.beginPath(); ctx.move(to: CGPoint(x: p[0].0, y: p[0].1)); for q in p.dropFirst() { ctx.addLine(to: CGPoint(x: q.0, y: q.1)) }; ctx.closePath(); ctx.fillPath() }
    for e in ellipses { ctx.fillEllipse(in: CGRect(x: e.0 - e.2, y: e.1 - e.3, width: 2 * e.2, height: 2 * e.3)) }
    // CGContext rows are bottom-up in memory for this flipped context; flip back to top-left order
    var m = [Float](repeating: 0, count: w * h)
    for y in 0..<h { for x in 0..<w { m[y * w + x] = Float(px[y * w + x]) / 255 } }
    return feather > 0 ? blur(m, w, h, feather) : m
}

func blur(_ m: [Float], _ w: Int, _ h: Int, _ r: Int) -> [Float] {
    var a = m, b = m
    for _ in 0..<2 {
        for y in 0..<h { var s: Float = 0; var n = 0
            for x in -r..<(w + r) {
                if x + r < w { s += a[y * w + x + r]; n += 1 }
                if x - r - 1 >= 0 { s -= a[y * w + x - r - 1]; n -= 1 }
                if x >= 0 && x < w { b[y * w + x] = s / Float(max(1, n)) } } }
        for x in 0..<w { var s: Float = 0; var n = 0
            for y in -r..<(h + r) {
                if y + r < h { s += b[(y + r) * w + x]; n += 1 }
                if y - r - 1 >= 0 { s -= b[(y - r - 1) * w + x]; n -= 1 }
                if y >= 0 && y < h { a[y * w + x] = s / Float(max(1, n)) } } }
    }
    return a
}

/// Multigrid membrane fill: pixels with known < 0.5 are reconstructed smoothly from their surroundings.
func fill(_ img: Img, known k0: [Float]) -> Img {
    // binary, dilated: feathered mask edges must not leak object colour into the reconstruction
    let unknownD = blur(k0.map { $0 < 0.98 ? 1 : 0 }, img.w, img.h, 3)
    let known = unknownD.map { $0 > 0.01 ? Float(0) : Float(1) }
    func level(_ r: [Float], _ g: [Float], _ b: [Float], _ k: [Float], _ w: Int, _ h: Int) -> ([Float], [Float], [Float]) {
        if w <= 8 || h <= 8 {
            var sr: Float = 0, sg: Float = 0, sb: Float = 0, sk: Float = 0
            for i in 0..<(w * h) { sr += r[i] * k[i]; sg += g[i] * k[i]; sb += b[i] * k[i]; sk += k[i] }
            let n = max(sk, 1e-4)
            var rr = r, gg = g, bb = b
            for i in 0..<(w * h) where k[i] < 0.999 { let t = k[i]; rr[i] = r[i] * t + sr / n * (1 - t); gg[i] = g[i] * t + sg / n * (1 - t); bb[i] = b[i] * t + sb / n * (1 - t) }
            return (rr, gg, bb)
        }
        let w2 = (w + 1) / 2, h2 = (h + 1) / 2
        var r2 = [Float](repeating: 0, count: w2 * h2), g2 = r2, b2 = r2, k2 = r2
        for y in 0..<h2 { for x in 0..<w2 {
            var sr: Float = 0, sg: Float = 0, sb: Float = 0, sk: Float = 0
            for dy in 0..<2 { for dx in 0..<2 { let xx = min(w - 1, 2 * x + dx), yy = min(h - 1, 2 * y + dy), i = yy * w + xx
                sr += r[i] * k[i]; sg += g[i] * k[i]; sb += b[i] * k[i]; sk += k[i] } }
            let j = y * w2 + x
            if sk > 1e-4 { r2[j] = sr / sk; g2[j] = sg / sk; b2[j] = sb / sk }
            k2[j] = min(1, sk / 4 * 1.0) > 0.05 ? min(1, sk) : 0
        } }
        let (cr, cg, cb) = level(r2, g2, b2, k2, w2, h2)
        var rr = r, gg = g, bb = b
        for y in 0..<h { for x in 0..<w { let i = y * w + x, j = min(h2 - 1, y / 2) * w2 + min(w2 - 1, x / 2)
            let t = k[i]; rr[i] = r[i] * t + cr[j] * (1 - t); gg[i] = g[i] * t + cg[j] * (1 - t); bb[i] = b[i] * t + cb[j] * (1 - t) } }
        // relax unknown pixels
        for _ in 0..<6 {
            var nr = rr, ng = gg, nb = bb
            for y in 1..<(h - 1) { for x in 1..<(w - 1) { let i = y * w + x; if k[i] > 0.999 { continue }
                let t = k[i]
                let ar = (rr[i - 1] + rr[i + 1] + rr[i - w] + rr[i + w]) / 4, ag = (gg[i - 1] + gg[i + 1] + gg[i - w] + gg[i + w]) / 4, ab = (bb[i - 1] + bb[i + 1] + bb[i - w] + bb[i + w]) / 4
                nr[i] = r[i] * t + ar * (1 - t); ng[i] = g[i] * t + ag * (1 - t); nb[i] = b[i] * t + ab * (1 - t) } }
            rr = nr; gg = ng; bb = nb
        }
        return (rr, gg, bb)
    }
    let (r, g, b) = level(img.r, img.g, img.b, known, img.w, img.h)
    return Img(w: img.w, h: img.h, r: r, g: g, b: b)
}

/// Alpha from colour difference against a reconstructed background.
func key(_ img: Img, _ bg: Img, lo: Float, hi: Float) -> [Float] {
    var a = [Float](repeating: 0, count: img.w * img.h)
    for i in 0..<a.count {
        let d = max(abs(img.r[i] - bg.r[i]), abs(img.g[i] - bg.g[i]), abs(img.b[i] - bg.b[i]))
        a[i] = min(1, max(0, (d - lo) / (hi - lo)))
    }
    return a
}

func mul(_ a: [Float], _ b: [Float]) -> [Float] { zip(a, b).map { $0 * $1 } }
func maxm(_ a: [Float], _ b: [Float]) -> [Float] { zip(a, b).map { max($0, $1) } }
func inv(_ a: [Float]) -> [Float] { a.map { 1 - $0 } }

let ci = CIContext(options: [.workingColorSpace: sRGB])
func save(_ img: Img, alpha: [Float]?, _ path: String, scale: Double = 2) {
    let w = img.w, h = img.h
    var px = [UInt8](repeating: 0, count: w * h * 4)
    for i in 0..<(w * h) {
        let a = alpha?[i] ?? 1
        px[i * 4] = UInt8(max(0, min(255, img.r[i] * a * 255))); px[i * 4 + 1] = UInt8(max(0, min(255, img.g[i] * a * 255)))
        px[i * 4 + 2] = UInt8(max(0, min(255, img.b[i] * a * 255))); px[i * 4 + 3] = UInt8(max(0, min(255, a * 255)))
    }
    let ctx = CGContext(data: &px, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: sRGB, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    var out = ctx.makeImage()!
    if scale != 1 {
        var c = CIImage(cgImage: out)
        c = c.applyingFilter("CILanczosScaleTransform", parameters: [kCIInputScaleKey: scale, kCIInputAspectRatioKey: 1])
        c = c.applyingFilter("CISharpenLuminance", parameters: ["inputSharpness": 0.35, "inputRadius": 1.6])
        out = ci.createCGImage(c, from: CGRect(x: 0, y: 0, width: Double(w) * scale, height: Double(h) * scale), format: .RGBA8, colorSpace: sRGB)!
    }
    let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: path) as CFURL, UTType.png.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(d, out, nil); CGImageDestinationFinalize(d)
    print("  \(URL(fileURLWithPath: path).lastPathComponent)")
}

func crop(_ img: Img, _ a: [Float], _ x0: Int, _ y0: Int, _ cw: Int, _ ch: Int) -> (Img, [Float]) {
    var o = Img(w: cw, h: ch, r: .init(repeating: 0, count: cw * ch), g: .init(repeating: 0, count: cw * ch), b: .init(repeating: 0, count: cw * ch))
    var oa = [Float](repeating: 0, count: cw * ch)
    for y in 0..<ch { for x in 0..<cw { let i = (y0 + y) * img.w + x0 + x, j = y * cw + x
        o.r[j] = img.r[i]; o.g[j] = img.g[i]; o.b[j] = img.b[i]; oa[j] = a[i] } }
    return (o, oa)
}

// ---------------------------------------------------------------- geometry (plate pixels, 1774 x 887)
let plate = load(CommandLine.arguments[1])
let out = CommandLine.arguments[2]
try? FileManager.default.createDirectory(atPath: out, withIntermediateDirectories: true)
let W = plate.w, H = plate.h

let farPoly: [(Double, Double)] = [(860, 560), (960, 540), (1060, 548), (1150, 470), (1190, 420), (1250, 380), (1280, 300), (1300, 240), (1330, 200), (1330, 150), (1390, 140), (1440, 90), (1480, 70), (1560, 40), (1600, 12), (1680, 6), (1774, 0), (1774, 680), (860, 680)]
let fanPoly: [(Double, Double)] = [(1240, 470), (1262, 430), (1300, 405), (1330, 390), (1372, 390), (1412, 408), (1462, 440), (1472, 520), (1470, 600), (1452, 628), (1380, 632), (1300, 622), (1240, 614), (1226, 560)]
let portalPoly: [(Double, Double)] = [(1438, 470), (1470, 420), (1520, 350), (1560, 312), (1600, 296), (1650, 296), (1720, 326), (1774, 356), (1774, 604), (1700, 604), (1580, 604), (1470, 604), (1436, 540)]
let groundPoly: [(Double, Double)] = [(882, 628), (930, 614), (1000, 610), (1080, 620), (1120, 630), (1138, 616), (1300, 612), (1416, 612), (1418, 588), (1500, 582), (1600, 584), (1610, 560), (1680, 546), (1774, 542), (1774, 887), (882, 887)]
let fgDaisyPoly: [(Double, Double)] = [(1592, 708), (1625, 690), (1700, 700), (1774, 690), (1774, 887), (1560, 887), (1575, 800)]
let fgGrassPoly: [(Double, Double)] = [(896, 887), (902, 842), (958, 812), (1060, 800), (1180, 806), (1270, 826), (1312, 862), (1318, 887)]
let ledgeDaisyPoly: [(Double, Double)] = [(925, 600), (1005, 600), (1005, 648), (925, 648)]
let windPoly: [(Double, Double)] = [(995, 470), (1060, 430), (1160, 440), (1262, 470), (1268, 560), (1220, 600), (1100, 600), (1000, 590)]
let treePoly: [(Double, Double)] = [(0, 0), (672, 0), (672, 72), (622, 118), (540, 138), (455, 146), (380, 134), (300, 150), (244, 156), (200, 150), (0, 150)]
let lanternRect: [(Double, Double)] = [(216, 140), (342, 140), (342, 278), (216, 278)]
let petals: [[(Double, Double)]] = [[(400, 140), (478, 140), (478, 196), (400, 196)], [(124, 218), (176, 218), (176, 256), (124, 256)], [(360, 278), (410, 278), (410, 318), (360, 318)]]

print("masks")
let mFar = mask(W, H, polys: [farPoly], feather: 3)
let mFan = mask(W, H, polys: [fanPoly], feather: 2)
let mPortal = mask(W, H, polys: [portalPoly], feather: 2)
let mMid = maxm(mFan, mPortal)
let mGround = mask(W, H, polys: [groundPoly], feather: 2)
let mFgDaisy = mask(W, H, polys: [fgDaisyPoly], feather: 3)
let mFgGrass = mask(W, H, polys: [fgGrassPoly], feather: 3)
let mFg = maxm(mFgDaisy, mFgGrass)
let mWind = mask(W, H, polys: [windPoly], feather: 6)
let mTree = mask(W, H, polys: [treePoly] + petals, feather: 4)
let mLantern = mask(W, H, polys: [lanternRect], feather: 2)
let mLeftJunk = mask(W, H, polys: [[(0, 330), (880, 330), (880, 887), (0, 887)]], feather: 0)   // owner's original Plops (not used)

print("sky")
var unknownSky = maxm(maxm(maxm(mFar, mMid), maxm(mGround, mTree)), maxm(mWind, mLeftJunk))
unknownSky = maxm(unknownSky, maxm(mLantern, mask(W, H, polys: [[(0, 90), (310, 90), (310, 345), (0, 345)]], feather: 0)))
let sky = fill(plate, known: inv(unknownSky))
save(sky, alpha: nil, out + "/L0_sky.png")

print("far")
// far layer: original where visible; reconstructed behind fan / portal / ground / wind swirl; edge keyed against the sky
let farKey = key(plate, sky, lo: 0.03, hi: 0.10)
let farFill = fill(plate, known: inv(maxm(maxm(mMid, mGround), mWind)))
var farA = mul(mFar, maxm(blur(farKey, W, H, 1), maxm(mMid, mGround)))
farA = mul(farA, inv(mWind))
farA = mul(farA, inv(mask(W, H, polys: [[(0, 0), (882, 0), (882, 887), (0, 887)]], feather: 0)))
save(farFill, alpha: farA, out + "/L1_far.png")

print("mid")
let midFill = fill(plate, known: inv(mGround))
save(midFill, alpha: mul(mMid, maxm(inv(mGround), mMid)), out + "/L2_mid.png")
// rotating parts: fan blades and portal vortex as separate discs (straight crops; drawn over the mid layer)
let (bladesImg, bladesA) = crop(plate, mask(W, H, ellipses: [(1333, 513, 61, 68)], feather: 2), 1263, 437, 140, 152)
save(bladesImg, alpha: bladesA, out + "/P_fan_blades.png")
let (vortexImg, vortexA) = crop(plate, mask(W, H, ellipses: [(1617, 488, 90, 92)], feather: 3), 1508, 377, 214, 218)
save(vortexImg, alpha: vortexA, out + "/P_portal_vortex.png")

print("ground")
let groundFill = fill(plate, known: inv(mFg))
save(groundFill, alpha: mGround, out + "/L3_ground.png")
let (ledgeImg, ledgeA) = crop(plate, mask(W, H, polys: [ledgeDaisyPoly], feather: 3), 920, 595, 90, 58)
save(ledgeImg, alpha: ledgeA, out + "/P_ledge_daisies.png")

print("foreground")
save(plate, alpha: mFg, out + "/L4_fg.png")

print("tree + lantern")
let treeBg = fill(plate, known: inv(maxm(mTree, mLantern)))
let treeKey = blur(key(plate, treeBg, lo: 0.035, hi: 0.11), W, H, 1)
let treeA = mul(mul(maxm(mTree, mLantern), treeKey), inv(mLantern))
let (tImg, tA) = crop(plate, treeA, 0, 0, 680, 350)
save(tImg, alpha: tA, out + "/P_tree.png")
let lanternKey = blur(key(plate, treeBg, lo: 0.06, hi: 0.15), W, H, 1)
let (lImg, lA) = crop(plate, mul(mLantern, lanternKey), 212, 112, 132, 168)
save(lImg, alpha: lA, out + "/P_lantern.png")
print("done")
