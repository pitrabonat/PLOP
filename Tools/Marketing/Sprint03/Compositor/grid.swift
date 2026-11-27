import AppKit
// grid <out> <cols> <cellW> <files...>
let a = CommandLine.arguments; let out = a[1]; let cols = Int(a[2])!; let cw = CGFloat(Double(a[3])!)
let imgs = a[4...].compactMap { NSImage(contentsOfFile: $0) }
let ch = imgs.map { cw * $0.size.height / $0.size.width }.max() ?? cw
let rows = (imgs.count + cols - 1) / cols
let W = Int(cw) * cols, H = Int(ch) * rows
let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: W, pixelsHigh: H, bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
NSGraphicsContext.saveGraphicsState(); NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
NSColor.white.setFill(); NSRect(x: 0, y: 0, width: W, height: H).fill()
for (i, im) in imgs.enumerated() { let r = i / cols, c = i % cols
  let h = cw * im.size.height / im.size.width
  im.draw(in: NSRect(x: CGFloat(c) * cw, y: CGFloat(H) - CGFloat(r + 1) * ch + (ch - h), width: cw, height: h)) }
NSGraphicsContext.restoreGraphicsState()
try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: out))
