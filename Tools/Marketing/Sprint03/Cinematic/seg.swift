// seg — lifts the four Plops (and the "!" marks) out of the owner's reference plate by hue/saturation region growing,
// hole filling and feathering, then builds a clean plate (push-pull fill under the dilated masks).
// Output: <out>/cut_<name>.png (RGBA, cropped to bbox), <out>/cuts.json (bbox per cut), <out>/plate_clean.png, <out>/mask_all.png
import Foundation; import CoreGraphics; import ImageIO; import UniformTypeIdentifiers
let a = CommandLine.arguments; let out = a[2]
try? FileManager.default.createDirectory(atPath: out, withIntermediateDirectories: true)
let s = CGImageSourceCreateWithURL(URL(fileURLWithPath: a[1]) as CFURL, nil)!; let img = CGImageSourceCreateImageAtIndex(s, 0, nil)!
let W = img.width, H = img.height
var px = [UInt8](repeating: 0, count: W * H * 4)
let cs = CGColorSpace(name: CGColorSpace.sRGB)!
let c0 = CGContext(data: &px, width: W, height: H, bitsPerComponent: 8, bytesPerRow: W * 4, space: cs, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
c0.draw(img, in: CGRect(x: 0, y: 0, width: W, height: H))
func hsv(_ i: Int) -> (Double, Double, Double) {
    let r = Double(px[i*4])/255, g = Double(px[i*4+1])/255, b = Double(px[i*4+2])/255
    let mx = max(r, g, b), mn = min(r, g, b), d = mx - mn
    var h = 0.0
    if d > 1e-6 { if mx == r { h = 60 * fmod((g - b) / d, 6) } else if mx == g { h = 60 * ((b - r) / d + 2) } else { h = 60 * ((r - g) / d + 4) } }
    if h < 0 { h += 360 }
    return (h, mx == 0 ? 0 : d / mx, mx)
}
func hd(_ a: Double, _ b: Double) -> Double { let d = abs(a - b).truncatingRemainder(dividingBy: 360); return min(d, 360 - d) }
struct Spec { let name: String; let box: (Int, Int, Int, Int); let seed: (Int, Int); let hue: Double; let tol: Double; let minS: Double; let minV: Double; var maxS: Double = 1 }
let specs = [
    Spec(name: "pink", box: (175, 440, 535, 400), seed: (450, 700), hue: 350, tol: 22, minS: 0.30, minV: 0.45),
    Spec(name: "yellow", box: (610, 500, 285, 270), seed: (745, 690), hue: 42, tol: 18, minS: 0.35, minV: 0.55),
    Spec(name: "green", box: (5, 415, 285, 240), seed: (150, 590), hue: 100, tol: 28, minS: 0.08, minV: 0.6, maxS: 0.5),
    Spec(name: "purple", box: (60, 262, 275, 182), seed: (215, 380), hue: 305, tol: 30, minS: 0.18, minV: 0.5),
    Spec(name: "bang", box: (500, 370, 140, 115), seed: (525, 405), hue: 32, tol: 18, minS: 0.4, minV: 0.6),
]
var all = [Float](repeating: 0, count: W * H)
var json: [[String: Any]] = []
var pinkMask = [Float](repeating: 0, count: W * H)
for sp in specs {
    let (bx, by, bw, bh) = sp.box
    var m = [UInt8](repeating: 0, count: bw * bh)
    for y in 0..<bh { for x in 0..<bw { let i = (by + y) * W + bx + x; let (h, s, v) = hsv(i)
        if hd(h, sp.hue) <= sp.tol && s >= sp.minS && s <= sp.maxS && v >= sp.minV { m[y * bw + x] = 1 } } }
    // keep components: for "bang" keep all, else the component holding the seed
    func flood(_ grid: inout [UInt8], _ sx: Int, _ sy: Int, _ from: UInt8, _ to: UInt8) {
        var st = [(sx, sy)]
        while let (x, y) = st.popLast() { if x < 0 || y < 0 || x >= bw || y >= bh || grid[y * bw + x] != from { continue }
            grid[y * bw + x] = to; st += [(x+1, y), (x-1, y), (x, y+1), (x, y-1)] }
    }
    if sp.name != "bang" {
        flood(&m, sp.seed.0 - bx, sp.seed.1 - by, 1, 2)
        for i in 0..<m.count { m[i] = m[i] == 2 ? 1 : 0 }
        // close small gaps (dilate 3, erode 3), then fill holes not connected to the border
        func morph(_ g: [UInt8], _ r: Int, _ dil: Bool) -> [UInt8] { var o = g
            for y in 0..<bh { for x in 0..<bw { var v: UInt8 = dil ? 0 : 1
                outer: for dy in -r...r { for dx in -r...r where dx*dx+dy*dy <= r*r { let xx = x+dx, yy = y+dy
                    let gv: UInt8 = (xx < 0 || yy < 0 || xx >= bw || yy >= bh) ? 0 : g[yy*bw+xx]
                    if dil && gv == 1 { v = 1; break outer }; if !dil && gv == 0 { v = 0; break outer } } }
                o[y*bw+x] = v } }; return o }
        m = morph(morph(m, 4, true), 4, false)
        var bg = m.map { $0 == 0 ? UInt8(1) : UInt8(0) }
        for x in 0..<bw { flood(&bg, x, 0, 1, 2); flood(&bg, x, bh-1, 1, 2) }
        for y in 0..<bh { flood(&bg, 0, y, 1, 2); flood(&bg, bw-1, y, 1, 2) }
        for i in 0..<m.count where bg[i] == 1 { m[i] = 1 }
        m = morph(m, 2, true)
    }
    // feather: 3x3 box blur twice
    var f = m.map { Float($0) }
    for _ in 0..<2 { var g = f; for y in 1..<(bh-1) { for x in 1..<(bw-1) { var acc: Float = 0
        for dy in -1...1 { for dx in -1...1 { acc += f[(y+dy)*bw + x+dx] } }; g[y*bw+x] = acc / 9 } }; f = g }
    // write RGBA cut (premultiplied)
    var cut = [UInt8](repeating: 0, count: bw * bh * 4); var area = 0
    for y in 0..<bh { for x in 0..<bw { let a = f[y*bw+x]; let i = (by + y) * W + bx + x
        if a > 0.5 { area += 1 }
        all[i] = max(all[i], a); if sp.name == "pink" { pinkMask[i] = a }
        for c in 0..<3 { cut[(y*bw+x)*4+c] = UInt8(Float(px[i*4+c]) * a) }; cut[(y*bw+x)*4+3] = UInt8(a * 255) } }
    let cc = CGContext(data: &cut, width: bw, height: bh, bitsPerComponent: 8, bytesPerRow: bw*4, space: cs, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: "\(out)/cut_\(sp.name).png") as CFURL, UTType.png.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(d, cc.makeImage()!, nil); CGImageDestinationFinalize(d)
    json.append(["name": sp.name, "x": bx, "y": by, "w": bw, "h": bh, "area": area])
    print(sp.name, "area", area)
}
try! JSONSerialization.data(withJSONObject: json, options: .prettyPrinted).write(to: URL(fileURLWithPath: out + "/cuts.json"))
// clean plate: hole = dilated mask (6 px); push-pull fill
var hole = [Bool](repeating: false, count: W*H)
for y in 0..<H { for x in 0..<W where all[y*W+x] > 0.05 { for dy in -7...7 { for dx in -7...7 { let xx = x+dx, yy = y+dy
    if xx >= 0 && yy >= 0 && xx < W && yy < H { hole[yy*W+xx] = true } } } } }
// pyramid of (premult colour, weight)
struct Lvl { var w: Int; var h: Int; var c: [Float]; var a: [Float] }
var L0 = Lvl(w: W, h: H, c: [Float](repeating: 0, count: W*H*3), a: [Float](repeating: 0, count: W*H))
for i in 0..<(W*H) where !hole[i] { L0.a[i] = 1; for c in 0..<3 { L0.c[i*3+c] = Float(px[i*4+c]) } }
var pyr = [L0]
while pyr.last!.w > 2 && pyr.last!.h > 2 {
    let p = pyr.last!; let w = (p.w+1)/2, h = (p.h+1)/2
    var n = Lvl(w: w, h: h, c: [Float](repeating: 0, count: w*h*3), a: [Float](repeating: 0, count: w*h))
    for y in 0..<p.h { for x in 0..<p.w { let i = y*p.w+x, j = (y/2)*w + x/2; n.a[j] += p.a[i]; for c in 0..<3 { n.c[j*3+c] += p.c[i*3+c] } } }
    for j in 0..<(w*h) where n.a[j] > 0 { for c in 0..<3 { n.c[j*3+c] /= n.a[j] }; n.a[j] = min(1, n.a[j]) }
    pyr.append(n)
}
for k in stride(from: pyr.count-2, through: 0, by: -1) {
    let up = pyr[k+1]
    for y in 0..<pyr[k].h { for x in 0..<pyr[k].w { let i = y*pyr[k].w+x
        if pyr[k].a[i] < 1 {
            // bilinear from coarser level
            let fx = (Double(x) + 0.5) / 2 - 0.5, fy = (Double(y) + 0.5) / 2 - 0.5
            let x0 = max(0, min(up.w-1, Int(floor(fx)))), y0 = max(0, min(up.h-1, Int(floor(fy)))), x1 = min(up.w-1, x0+1), y1 = min(up.h-1, y0+1)
            let tx = Float(max(0, min(1, fx - Double(x0)))), ty = Float(max(0, min(1, fy - Double(y0))))
            let wa = pyr[k].a[i]
            for c in 0..<3 {
                let v = (up.c[(y0*up.w+x0)*3+c]*(1-tx) + up.c[(y0*up.w+x1)*3+c]*tx)*(1-ty) + (up.c[(y1*up.w+x0)*3+c]*(1-tx) + up.c[(y1*up.w+x1)*3+c]*tx)*ty
                pyr[k].c[i*3+c] = pyr[k].c[i*3+c] * wa + v * (1 - wa)
            }
            pyr[k].a[i] = 1
        } } }
}
var outPx = px
for i in 0..<(W*H) where hole[i] { for c in 0..<3 { outPx[i*4+c] = UInt8(max(0, min(255, pyr[0].c[i*3+c]))) }; outPx[i*4+3] = 255 }
let oc = CGContext(data: &outPx, width: W, height: H, bitsPerComponent: 8, bytesPerRow: W*4, space: cs, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
let d2 = CGImageDestinationCreateWithURL(URL(fileURLWithPath: out + "/plate_clean.png") as CFURL, UTType.png.identifier as CFString, 1, nil)!
CGImageDestinationAddImage(d2, oc.makeImage()!, nil); CGImageDestinationFinalize(d2)
var mk = all.map { UInt8($0 * 255) }
let mc = CGContext(data: &mk, width: W, height: H, bitsPerComponent: 8, bytesPerRow: W, space: CGColorSpaceCreateDeviceGray(), bitmapInfo: 0)!
let d3 = CGImageDestinationCreateWithURL(URL(fileURLWithPath: out + "/mask_all.png") as CFURL, UTType.png.identifier as CFString, 1, nil)!
CGImageDestinationAddImage(d3, mc.makeImage()!, nil); CGImageDestinationFinalize(d3)
// patch: clean plate where the pink Plop (dilated 12 px) was, transparent elsewhere
var patch = [UInt8](repeating: 0, count: W*H*4)
for y in 0..<H { for x in 0..<W { var m: Float = 0
    for dy in stride(from: -12, through: 12, by: 3) { for dx in stride(from: -12, through: 12, by: 3) { let xx = x+dx, yy = y+dy
        if xx >= 0 && yy >= 0 && xx < W && yy < H && pinkMask[yy*W+xx] > 0.5 { m = 1 } } }
    if m > 0 { let i = (y*W+x)*4; for c in 0..<3 { patch[i+c] = outPx[i+c] }; patch[i+3] = 255 } } }
let pc = CGContext(data: &patch, width: W, height: H, bitsPerComponent: 8, bytesPerRow: W*4, space: cs, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
let d4 = CGImageDestinationCreateWithURL(URL(fileURLWithPath: out + "/patch_pink.png") as CFURL, UTType.png.identifier as CFString, 1, nil)!
CGImageDestinationAddImage(d4, pc.makeImage()!, nil); CGImageDestinationFinalize(d4)
print("clean plate + pink patch written")
