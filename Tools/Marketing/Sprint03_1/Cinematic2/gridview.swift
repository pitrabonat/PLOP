// gridview in.png out.png x y w h scale step — crop with a labelled coordinate grid (for authoring layer polygons)
import Foundation; import CoreGraphics; import ImageIO; import UniformTypeIdentifiers; import CoreText
let a = CommandLine.arguments
let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: a[1]) as CFURL, nil)!; let img = CGImageSourceCreateImageAtIndex(src, 0, nil)!
let x = Double(a[3])!, y = Double(a[4])!, w = Double(a[5])!, h = Double(a[6])!, s = Double(a[7])!, step = Double(a[8])!
let W = Int(w * s), H = Int(h * s)
let ctx = CGContext(data: nil, width: W, height: H, bitsPerComponent: 8, bytesPerRow: 0, space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue)!
ctx.interpolationQuality = .high
ctx.draw(img, in: CGRect(x: -x * s, y: Double(H) - (Double(img.height) - y) * s, width: Double(img.width) * s, height: Double(img.height) * s))
ctx.setLineWidth(1)
var g = (x / step).rounded(.up) * step
while g < x + w { let px = (g - x) * s; ctx.setStrokeColor(CGColor(red: 0, green: 0.3, blue: 1, alpha: 0.55)); ctx.stroke(CGRect(x: px, y: 0, width: 0.01, height: Double(H)))
  let l = CTLineCreateWithAttributedString(NSAttributedString(string: "\(Int(g))", attributes: [kCTFontAttributeName as NSAttributedString.Key: CTFontCreateWithName("Menlo" as CFString, 11, nil), kCTForegroundColorAttributeName as NSAttributedString.Key: CGColor(red: 0, green: 0, blue: 0.8, alpha: 1)]))
  ctx.textPosition = CGPoint(x: px + 2, y: Double(H) - 12); CTLineDraw(l, ctx); g += step }
g = (y / step).rounded(.up) * step
while g < y + h { let py = Double(H) - (g - y) * s; ctx.setStrokeColor(CGColor(red: 1, green: 0.1, blue: 0, alpha: 0.55)); ctx.stroke(CGRect(x: 0, y: py, width: Double(W), height: 0.01))
  let l = CTLineCreateWithAttributedString(NSAttributedString(string: "\(Int(g))", attributes: [kCTFontAttributeName as NSAttributedString.Key: CTFontCreateWithName("Menlo" as CFString, 11, nil), kCTForegroundColorAttributeName as NSAttributedString.Key: CGColor(red: 0.8, green: 0, blue: 0, alpha: 1)]))
  ctx.textPosition = CGPoint(x: 2, y: py + 2); CTLineDraw(l, ctx); g += step }
let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: a[2]) as CFURL, UTType.png.identifier as CFString, 1, nil)!
CGImageDestinationAddImage(d, ctx.makeImage()!, nil); CGImageDestinationFinalize(d)
