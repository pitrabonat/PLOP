// plopcomp — PLOP! Sprint 03 timeline compositor (macOS AVFoundation + CoreGraphics + CoreImage, no third-party tools).
//   swiftc -O main.swift Cinematic.swift -o plopcomp
//   ./plopcomp timeline.json            → renders the MP4 named in the timeline (H.264 + AAC), plus an optional poster PNG
//   ./plopcomp timeline.json --still T  → writes one PNG frame at time T (for review and stills)
// Coordinates in timelines are output pixels, origin top-left. Times are seconds. Layers draw in array order.
import Foundation
import AVFoundation
import CoreGraphics
import CoreImage
import CoreText
import ImageIO
import UniformTypeIdentifiers

// MARK: - JSON model

struct KF: Codable { var t: Double; var x: Double?; var y: Double?; var s: Double?; var sx: Double?; var sy: Double?; var rot: Double?; var o: Double?; var ease: String? }
struct Pill: Codable { var color: String; var padX: Double?; var padY: Double?; var radius: Double? }
struct Layer: Codable {
    var type: String
    var start: Double = 0
    var end: Double = 1e9
    var fadeIn: Double? ; var fadeOut: Double?
    var opacity: Double?
    // fill
    var color: String?; var top: String?; var bottom: String?
    // image / video
    var src: String?
    var frame: [Double]?          // x, y, w, h (dest, top-left origin)
    var fit: String?              // cover | contain | stretch
    var crop: [Double]?           // source rect x,y,w,h (source pixels, top-left)
    var cropTo: [Double]?         // animated crop end (linear-eased over the layer)
    var `in`: Double?             // video: source time at layer start
    var hold: Double?             // video: freeze source at this source time (no playback)
    var rate: Double?
    var radius: Double?
    var shadow: Double?
    var blur: Double?             // blur radius for background-style layers
    var tint: String?             // multiply/overlay colour for blurred backgrounds
    var tintAmount: Double?
    var anchor: [Double]?         // image transform anchor in the image (0..1, 0..1 top-left)
    var size: [Double]?           // image base size in px (default natural)
    var kf: [KF]?                 // transform keyframes (x,y = anchor position; s/sx/sy scale; rot degrees; o opacity)
    var squash: [Double]?         // [amplitude, frequency Hz, phase] jelly squash-and-stretch
    var bob: [Double]?            // [pixels, frequency Hz, phase]
    var maskCircle: [Double]?     // cx, cy, r0, r1 (radius animated over the layer, eased) — reveal transition
    // text
    var text: String?; var font: String?; var fontSize: Double?; var x: Double?; var y: Double?
    var align: String?; var maxWidth: Double?; var tracking: Double?; var lineHeight: Double?
    var pill: Pill?; var rise: Double?; var outline: String?; var outlineWidth: Double?; var textShadow: Bool?
    // scene (procedural cinematic)
    var scene: String?; var params: [String: Double]?; var sceneOffset: Double?
}
struct Timeline: Codable {
    var width: Int; var height: Int; var fps: Double?; var duration: Double
    var output: String; var audio: String?; var bitrate: Int?; var profile: String?
    var background: String?; var fonts: [String]?; var poster: Double?; var posterOut: String?
    var layers: [Layer]
}

// MARK: - helpers

func color(_ hex: String?, _ alpha: Double = 1) -> CGColor {
    guard var h = hex else { return CGColor(red: 1, green: 1, blue: 1, alpha: alpha) }
    if h.hasPrefix("#") { h.removeFirst() }
    var v: UInt64 = 0; Scanner(string: h).scanHexInt64(&v)
    let a = h.count == 8 ? Double(v & 0xff) / 255 : 1
    let rgb = h.count == 8 ? v >> 8 : v
    return CGColor(srgbRed: Double((rgb >> 16) & 0xff) / 255, green: Double((rgb >> 8) & 0xff) / 255, blue: Double(rgb & 0xff) / 255, alpha: a * alpha)
}
func ease(_ x: Double, _ kind: String? = "inOut") -> Double {
    let t = min(max(x, 0), 1)
    switch kind ?? "inOut" {
    case "linear": return t
    case "out": return 1 - pow(1 - t, 3)
    case "in": return t * t * t
    case "back": let c = 1.4; return 1 + (c + 1) * pow(t - 1, 3) + c * pow(t - 1, 2)
    default: return t < 0.5 ? 4 * t * t * t : 1 - pow(-2 * t + 2, 3) / 2
    }
}
let sRGB = CGColorSpace(name: CGColorSpace.sRGB)!
var imageCache: [String: CGImage] = [:]
func loadImage(_ path: String) -> CGImage {
    if let c = imageCache[path] { return c }
    guard let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil),
          let img = CGImageSourceCreateImageAtIndex(src, 0, nil) else { fatalError("cannot load image " + path) }
    imageCache[path] = img; return img
}
let ci = CIContext(options: [.workingColorSpace: sRGB, .outputColorSpace: sRGB])
func blurred(_ img: CGImage, radius: Double, maxSide: Double = 480) -> CGImage {
    let scale = min(1, maxSide / Double(max(img.width, img.height)))
    var c = CIImage(cgImage: img).transformed(by: CGAffineTransform(scaleX: scale, y: scale))
    let ext = c.extent
    c = c.clampedToExtent().applyingGaussianBlur(sigma: radius * scale).cropped(to: ext)
    return ci.createCGImage(c, from: ext, format: .RGBA8, colorSpace: sRGB)!
}

// MARK: - video sources (sequential decode, frame-at-or-before semantics; VFR safe)

final class VideoSource {
    let asset: AVURLAsset; var reader: AVAssetReader!; var output: AVAssetReaderTrackOutput!
    var current: CGImage?; var currentT = -1.0; var nextBuf: CMSampleBuffer?; var startedAt = -1.0
    let natural: CGSize
    init(_ path: String) {
        asset = AVURLAsset(url: URL(fileURLWithPath: path))
        let track = asset.tracks(withMediaType: .video)[0]
        natural = track.naturalSize.applying(track.preferredTransform)
    }
    private func open(at t: Double) {
        reader?.cancelReading()
        reader = try! AVAssetReader(asset: asset)
        reader.timeRange = CMTimeRange(start: CMTime(seconds: max(0, t - 2), preferredTimescale: 600), duration: .positiveInfinity)
        output = AVAssetReaderTrackOutput(track: asset.tracks(withMediaType: .video)[0], outputSettings: [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA])
        output.alwaysCopiesSampleData = false
        reader.add(output); reader.startReading(); startedAt = t; nextBuf = nil; current = nil; currentT = -1
    }
    func frame(at t: Double) -> CGImage? {
        if reader == nil || t < currentT - 1e-6 || t > currentT + 4 && current != nil { open(at: t) }
        while true {
            if nextBuf == nil { nextBuf = output.copyNextSampleBuffer(); if nextBuf == nil { return current } }
            let pts = CMSampleBufferGetPresentationTimeStamp(nextBuf!).seconds
            if pts > t + 1e-4 && current != nil { return current }
            if let pb = CMSampleBufferGetImageBuffer(nextBuf!) {
                let img = CIImage(cvPixelBuffer: pb)
                current = ci.createCGImage(img, from: img.extent, format: .RGBA8, colorSpace: sRGB); currentT = pts
            }
            nextBuf = nil
            if pts > t + 1e-4 { return current }
        }
    }
}
var videoSources: [String: VideoSource] = [:]   // keyed by layer index + path so overlapping uses don't thrash

// MARK: - fonts / text

func registerFonts(_ paths: [String]) {
    for p in paths { CTFontManagerRegisterFontsForURL(URL(fileURLWithPath: p) as CFURL, .process, nil) }
}
func ctFont(_ name: String?, _ size: Double) -> CTFont { CTFontCreateWithName((name ?? "Nunito") as CFString, size, nil) }

func drawText(_ ctx: CGContext, _ L: Layer, H: Double, alpha: Double, t: Double) {
    guard let text = L.text else { return }
    let size = L.fontSize ?? 64
    let font = ctFont(L.font, size)
    let lines = text.components(separatedBy: "\n")
    let lh = (L.lineHeight ?? 1.12) * size
    var attrs: [NSAttributedString.Key: Any] = [.init(kCTFontAttributeName as String): font, .init(kCTForegroundColorAttributeName as String): color(L.color ?? "#5B3A4A", alpha)]
    if let tr = L.tracking { attrs[.init(kCTKernAttributeName as String)] = tr }
    let ctLines = lines.map { CTLineCreateWithAttributedString(NSAttributedString(string: $0, attributes: attrs)) }
    let widths = ctLines.map { CTLineGetTypographicBounds($0, nil, nil, nil) }
    let ascent = CTFontGetAscent(font), descent = CTFontGetDescent(font)
    let blockH = lh * Double(lines.count - 1) + ascent + descent
    let rise = (L.rise ?? 0) * (1 - ease(min(1, (t - L.start) / max(0.001, (L.fadeIn ?? 0.4))), "out"))
    let cx = L.x ?? 0, cyTop = (L.y ?? 0) - blockH / 2 + rise   // y = vertical centre of the block
    if let p = L.pill {
        let maxW = widths.max() ?? 0, px = p.padX ?? size * 0.6, py = p.padY ?? size * 0.32
        var x0 = cx - maxW / 2 - px
        if L.align == "left" { x0 = cx - px } else if L.align == "right" { x0 = cx - maxW - px }
        let r = CGRect(x: x0, y: H - (cyTop + blockH + py), width: maxW + 2 * px, height: blockH + 2 * py)
        ctx.saveGState()
        ctx.setShadow(offset: CGSize(width: 0, height: -size * 0.06), blur: size * 0.3, color: color("#5B3A4A", 0.18 * alpha))
        ctx.setFillColor(color(p.color, alpha)); ctx.addPath(CGPath(roundedRect: r, cornerWidth: p.radius ?? r.height / 2, cornerHeight: p.radius ?? r.height / 2, transform: nil)); ctx.fillPath()
        ctx.restoreGState()
    }
    for (i, line) in ctLines.enumerated() {
        var x = cx - widths[i] / 2
        if L.align == "left" { x = cx } else if L.align == "right" { x = cx - widths[i] }
        let baselineTop = cyTop + ascent + Double(i) * lh
        ctx.saveGState()
        if L.textShadow ?? false { ctx.setShadow(offset: CGSize(width: 0, height: -size * 0.05), blur: size * 0.25, color: color("#3A2030", 0.35 * alpha)) }
        if let oc = L.outline {
            ctx.setLineWidth(L.outlineWidth ?? size * 0.12); ctx.setLineJoin(.round); ctx.setStrokeColor(color(oc, alpha)); ctx.setTextDrawingMode(.stroke)
            ctx.textPosition = CGPoint(x: x, y: H - baselineTop); CTLineDraw(line, ctx)
            ctx.setTextDrawingMode(.fill)
        }
        ctx.textPosition = CGPoint(x: x, y: H - baselineTop); CTLineDraw(line, ctx)
        ctx.restoreGState()
    }
}

// MARK: - geometry

func fitRect(src: CGSize, into r: CGRect, mode: String?) -> CGRect {
    switch mode ?? "stretch" {
    case "cover", "contain":
        let s = mode == "cover" ? max(r.width / src.width, r.height / src.height) : min(r.width / src.width, r.height / src.height)
        let w = src.width * s, h = src.height * s
        return CGRect(x: r.midX - w / 2, y: r.midY - h / 2, width: w, height: h)
    default: return r
    }
}
func sampleKF(_ kfs: [KF], _ t: Double) -> KF {
    guard let first = kfs.first else { return KF(t: 0) }
    if t <= first.t { return first }
    var out = kfs.last!
    for i in 0..<(kfs.count - 1) where t >= kfs[i].t && t <= kfs[i + 1].t {
        let a = kfs[i], b = kfs[i + 1], u = ease((t - a.t) / max(1e-6, b.t - a.t), b.ease)
        func m(_ p: Double?, _ q: Double?) -> Double? { guard let pp = p ?? q, let qq = q ?? p else { return nil }; return pp + (qq - pp) * u }
        out = KF(t: t, x: m(a.x, b.x), y: m(a.y, b.y), s: m(a.s, b.s), sx: m(a.sx, b.sx), sy: m(a.sy, b.sy), rot: m(a.rot, b.rot), o: m(a.o, b.o))
        break
    }
    return out
}

// MARK: - render one frame

func layerAlpha(_ L: Layer, _ t: Double) -> Double {
    var a = L.opacity ?? 1
    if let fi = L.fadeIn, fi > 0 { a *= ease((t - L.start) / fi, "inOut") }
    if let fo = L.fadeOut, fo > 0 { a *= ease((L.end - t) / fo, "inOut") }
    return max(0, min(1, a))
}

func renderFrame(_ ctx: CGContext, _ tl: Timeline, _ t: Double) {
    let W = Double(tl.width), H = Double(tl.height)
    ctx.setFillColor(color(tl.background ?? "#FFF6EE")); ctx.fill(CGRect(x: 0, y: 0, width: W, height: H))
    for (idx, L) in tl.layers.enumerated() where t >= L.start && t < L.end {
        var alpha = layerAlpha(L, t)
        let lt = t - L.start, dur = min(L.end, tl.duration) - L.start
        var kfv: KF? = nil
        if let k = L.kf { kfv = sampleKF(k, lt); alpha *= kfv?.o ?? 1 }
        if alpha <= 0.001 { continue }
        ctx.saveGState()
        if let mc = L.maskCircle {
            let r = mc[2] + (mc[3] - mc[2]) * ease(lt / max(0.001, dur), "inOut")
            ctx.addEllipse(in: CGRect(x: mc[0] - r, y: H - mc[1] - r, width: 2 * r, height: 2 * r)); ctx.clip()
        }
        ctx.setAlpha(alpha)
        switch L.type {
        case "fill":
            if let top = L.top, let bottom = L.bottom {
                let g = CGGradient(colorsSpace: sRGB, colors: [color(top), color(bottom)] as CFArray, locations: [0, 1])!
                ctx.drawLinearGradient(g, start: CGPoint(x: 0, y: H), end: CGPoint(x: 0, y: 0), options: [])
            } else { ctx.setFillColor(color(L.color)); ctx.fill(CGRect(x: 0, y: 0, width: W, height: H)) }
        case "image", "video":
            var img: CGImage?
            if L.type == "image" { img = loadImage(L.src!) }
            else {
                let key = "\(idx)|\(L.src!)"
                let vs = videoSources[key] ?? VideoSource(L.src!); videoSources[key] = vs
                let st = L.hold ?? ((L.in ?? 0) + lt * (L.rate ?? 1))
                img = vs.frame(at: st)
            }
            guard var im = img else { break }
            if var c = L.crop {
                if let c2 = L.cropTo { let u = ease(lt / max(0.001, dur), "inOut"); c = zip(c, c2).map { $0 + ($1 - $0) * u } }
                let r = CGRect(x: c[0], y: c[1], width: c[2], height: c[3]).integral.intersection(CGRect(x: 0, y: 0, width: im.width, height: im.height))
                if let cc = im.cropping(to: r) { im = cc }
            }
            if let b = L.blur, b > 0 { im = blurred(im, radius: b) }
            if let k = kfv, L.kf != nil {
                // sprite transform: anchor at (x,y), scale, rotation, jelly squash, bob
                let base = CGSize(width: L.size?[0] ?? Double(im.width), height: L.size?[1] ?? Double(im.height))
                let an = L.anchor ?? [0.5, 0.5]
                var sx = (k.sx ?? 1) * (k.s ?? 1), sy = (k.sy ?? 1) * (k.s ?? 1)
                if let sq = L.squash { let w = sin(2 * .pi * sq[1] * t + (sq.count > 2 ? sq[2] : 0)) * sq[0]; sx *= 1 + w; sy *= 1 - w }
                var y = k.y ?? 0
                if let bo = L.bob { y -= abs(sin(.pi * bo[1] * t + (bo.count > 2 ? bo[2] : 0))) * bo[0] }
                ctx.translateBy(x: k.x ?? 0, y: H - y)
                ctx.rotate(by: -(k.rot ?? 0) * .pi / 180)
                ctx.scaleBy(x: sx, y: sy)
                let r = CGRect(x: -an[0] * base.width, y: -(1 - an[1]) * base.height, width: base.width, height: base.height)
                if let sh = L.shadow, sh > 0 { ctx.setShadow(offset: CGSize(width: 0, height: -sh * 0.3), blur: sh, color: color("#5B3A4A", 0.25)) }
                ctx.interpolationQuality = .high; ctx.draw(im, in: r)
            } else {
                let f = L.frame ?? [0, 0, W, H]
                let dest = CGRect(x: f[0], y: H - f[1] - f[3], width: f[2], height: f[3])
                let r = fitRect(src: CGSize(width: im.width, height: im.height), into: dest, mode: L.fit)
                if let rad = L.radius, rad > 0 {
                    if let sh = L.shadow, sh > 0 {
                        ctx.saveGState(); ctx.setShadow(offset: CGSize(width: 0, height: -sh * 0.35), blur: sh, color: color("#5B3A4A", 0.28))
                        ctx.setFillColor(color(L.tint ?? "#FFFFFF")); ctx.addPath(CGPath(roundedRect: dest, cornerWidth: rad, cornerHeight: rad, transform: nil)); ctx.fillPath(); ctx.restoreGState()
                    }
                    ctx.addPath(CGPath(roundedRect: dest, cornerWidth: rad, cornerHeight: rad, transform: nil)); ctx.clip()
                } else if L.fit == "cover" { ctx.clip(to: dest) }
                ctx.interpolationQuality = .high; ctx.draw(im, in: r)
                if let tint = L.tint, L.radius == nil { ctx.setFillColor(color(tint, L.tintAmount ?? 0.3)); ctx.fill(dest) }
            }
        case "text": drawText(ctx, L, H: H, alpha: 1, t: t)
        case "scene": Cinematic.draw(ctx, name: L.scene!, t: lt + (L.sceneOffset ?? 0), W: W, H: H, p: L.params ?? [:], global: t)
        default: break
        }
        ctx.restoreGState()
    }
}

// MARK: - main

let args = CommandLine.arguments
let tlURL = URL(fileURLWithPath: args[1])
var tl = try JSONDecoder().decode(Timeline.self, from: Data(contentsOf: tlURL))
FileManager.default.changeCurrentDirectoryPath(tlURL.deletingLastPathComponent().path)
registerFonts(tl.fonts ?? [])
let fps = tl.fps ?? 30
let W = tl.width, H = tl.height

func makeContext(_ data: UnsafeMutableRawPointer?, _ bpr: Int) -> CGContext {
    CGContext(data: data, width: W, height: H, bitsPerComponent: 8, bytesPerRow: bpr, space: sRGB,
              bitmapInfo: CGImageAlphaInfo.premultipliedFirst.rawValue | CGBitmapInfo.byteOrder32Little.rawValue)!
}
func writePNG(_ ctx: CGContext, _ path: String) {
    // Stills are written as opaque 24-bit RGB (no alpha): App Store and Google Play reject screenshots/graphics with alpha.
    let src = ctx.makeImage()!
    let oc = CGContext(data: nil, width: src.width, height: src.height, bitsPerComponent: 8, bytesPerRow: 0, space: sRGB,
                       bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue)!
    oc.draw(src, in: CGRect(x: 0, y: 0, width: src.width, height: src.height))
    let img = oc.makeImage()!
    let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: path) as CFURL, UTType.png.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(d, img, nil); CGImageDestinationFinalize(d)
}

if let i = args.firstIndex(of: "--still") {
    let t = Double(args[i + 1])!; let out = args.count > i + 2 ? args[i + 2] : tl.output.replacingOccurrences(of: ".mp4", with: String(format: "_%05.2f.png", t))
    let ctx = makeContext(nil, 0); renderFrame(ctx, tl, t); writePNG(ctx, out); print("still \(out)"); exit(0)
}

let outURL = URL(fileURLWithPath: tl.output)
try? FileManager.default.removeItem(at: outURL)
try FileManager.default.createDirectory(at: outURL.deletingLastPathComponent(), withIntermediateDirectories: true)
let writer = try AVAssetWriter(outputURL: outURL, fileType: .mp4)
let profile = (tl.profile ?? "").hasPrefix("high40") ? AVVideoProfileLevelH264High40 : AVVideoProfileLevelH264HighAutoLevel
// "high40cbr": constant bit rate (App Store App Preview asks for 10–12 Mbps; VBR undershoots on flat pastel content)
var compression: [String: Any] = [AVVideoAverageBitRateKey: tl.bitrate ?? 12_000_000, AVVideoProfileLevelKey: profile,
                                  AVVideoExpectedSourceFrameRateKey: fps, AVVideoMaxKeyFrameIntervalKey: Int(fps), AVVideoAllowFrameReorderingKey: true]
if tl.profile == "high40cbr" { compression.removeValue(forKey: AVVideoAverageBitRateKey); compression["ConstantBitRate"] = tl.bitrate ?? 11_000_000 }
let vIn = AVAssetWriterInput(mediaType: .video, outputSettings: [
    AVVideoCodecKey: AVVideoCodecType.h264, AVVideoWidthKey: W, AVVideoHeightKey: H,
    AVVideoColorPropertiesKey: [AVVideoColorPrimariesKey: AVVideoColorPrimaries_ITU_R_709_2, AVVideoTransferFunctionKey: AVVideoTransferFunction_ITU_R_709_2, AVVideoYCbCrMatrixKey: AVVideoYCbCrMatrix_ITU_R_709_2],
    AVVideoCompressionPropertiesKey: compression])
vIn.expectsMediaDataInRealTime = false
let adaptor = AVAssetWriterInputPixelBufferAdaptor(assetWriterInput: vIn, sourcePixelBufferAttributes: [
    kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA, kCVPixelBufferWidthKey as String: W, kCVPixelBufferHeightKey as String: H])
writer.add(vIn)

var aIn: AVAssetWriterInput?; var aReader: AVAssetReader?; var aOut: AVAssetReaderTrackOutput?
if let audio = tl.audio {
    let asset = AVURLAsset(url: URL(fileURLWithPath: audio))
    aReader = try AVAssetReader(asset: asset)
    aReader!.timeRange = CMTimeRange(start: .zero, duration: CMTime(seconds: tl.duration, preferredTimescale: 44100))
    aOut = AVAssetReaderTrackOutput(track: asset.tracks(withMediaType: .audio)[0], outputSettings: [AVFormatIDKey: kAudioFormatLinearPCM, AVLinearPCMBitDepthKey: 16, AVLinearPCMIsFloatKey: false, AVLinearPCMIsBigEndianKey: false, AVLinearPCMIsNonInterleaved: false, AVSampleRateKey: 44100, AVNumberOfChannelsKey: 2])
    aReader!.add(aOut!); aReader!.startReading()
    aIn = AVAssetWriterInput(mediaType: .audio, outputSettings: [AVFormatIDKey: kAudioFormatMPEG4AAC, AVSampleRateKey: 44100, AVNumberOfChannelsKey: 2, AVEncoderBitRateKey: 256_000])
    aIn!.expectsMediaDataInRealTime = false; writer.add(aIn!)
}
writer.startWriting(); writer.startSession(atSourceTime: .zero)

var audioDone = aIn == nil
func pumpAudio(until t: Double) {
    guard let aIn = aIn, !audioDone else { return }
    while aIn.isReadyForMoreMediaData {
        guard let sb = aOut!.copyNextSampleBuffer() else { aIn.markAsFinished(); audioDone = true; return }
        aIn.append(sb)
        if CMSampleBufferGetPresentationTimeStamp(sb).seconds > t { return }
    }
}

let frames = Int((tl.duration * fps).rounded())
let started = Date()
for f in 0..<frames {
    let t = Double(f) / fps
    pumpAudio(until: t + 1)
    while !vIn.isReadyForMoreMediaData { pumpAudio(until: t + 2); usleep(500) }
    var pb: CVPixelBuffer?
    CVPixelBufferPoolCreatePixelBuffer(nil, adaptor.pixelBufferPool!, &pb)
    CVPixelBufferLockBaseAddress(pb!, [])
    let ctx = makeContext(CVPixelBufferGetBaseAddress(pb!), CVPixelBufferGetBytesPerRow(pb!))
    renderFrame(ctx, tl, t)
    if let p = tl.poster, abs(t - p) < 0.5 / fps, let po = tl.posterOut { writePNG(ctx, po) }
    CVPixelBufferUnlockBaseAddress(pb!, [])
    adaptor.append(pb!, withPresentationTime: CMTime(value: CMTimeValue(f), timescale: CMTimeScale(fps)))
    if f % Int(fps * 5) == 0 { FileHandle.standardError.write("  \(String(format: "%.0f", t))s".data(using: .utf8)!) }
}
vIn.markAsFinished()
while !audioDone { pumpAudio(until: 1e9); usleep(1000) }
let sem = DispatchSemaphore(value: 0)
writer.finishWriting { sem.signal() }; sem.wait()
if writer.status != .completed { print("FAILED: \(writer.error?.localizedDescription ?? "?")"); exit(1) }
print(String(format: "\nwrote %@  %dx%d  %.2fs  %d frames  (%.0fs)", tl.output, W, H, tl.duration, frames, Date().timeIntervalSince(started)))
