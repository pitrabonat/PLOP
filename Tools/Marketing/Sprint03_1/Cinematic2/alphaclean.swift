// alphaclean <in.png> <out.png> <lo> <hi> — remaps straight alpha through smoothstep(lo, hi) to drop the faint haze
// left by keying (semi-transparent sky around a lifted prop). Colour is kept; output is premultiplied PNG.
import Foundation
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers
let a = CommandLine.arguments
let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: a[1]) as CFURL, nil)!
let img = CGImageSourceCreateImageAtIndex(src, 0, nil)!
let lo = Double(a[3])!, hi = Double(a[4])!
let w = img.width, h = img.height
let cs = CGColorSpace(name: CGColorSpace.sRGB)!
let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: cs, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
ctx.draw(img, in: CGRect(x: 0, y: 0, width: w, height: h))
let p = ctx.data!.bindMemory(to: UInt8.self, capacity: w * h * 4)
for i in 0..<(w * h) {
    let A = Double(p[i * 4 + 3]) / 255
    if A <= 0 { continue }
    var u = min(1, max(0, (A - lo) / (hi - lo))); u = u * u * (3 - 2 * u)
    let na = A * u   // keep solid interiors, kill haze
    for c in 0..<3 { p[i * 4 + c] = UInt8(min(255, (Double(p[i * 4 + c]) / A * na).rounded())) }
    p[i * 4 + 3] = UInt8((na * 255).rounded())
}
let out = ctx.makeImage()!
let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: a[2]) as CFURL, UTType.png.identifier as CFString, 1, nil)!
CGImageDestinationAddImage(d, out, nil); CGImageDestinationFinalize(d)
