// mediainfo — JSON facts for QA: video size/duration/fps/codec profile/bitrate, audio format/bitrate,
// and luma stats (mean per sampled frame, longest near-black run) for videos; size + alpha for images.
import Foundation; import AVFoundation; import CoreImage; import ImageIO
func lumaOf(_ img: CGImage) -> Double {
    let w = 32, h = 32; var px = [UInt8](repeating: 0, count: w * h * 4)
    let c = CGContext(data: &px, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    c.draw(img, in: CGRect(x: 0, y: 0, width: w, height: h)); var s = 0.0
    for i in 0..<(w * h) { s += 0.2126 * Double(px[i*4]) + 0.7152 * Double(px[i*4+1]) + 0.0722 * Double(px[i*4+2]) }
    return s / Double(w * h) / 255
}
var out: [[String: Any]] = []
for p in CommandLine.arguments.dropFirst() {
    var r: [String: Any] = ["path": p]
    let ext = (p as NSString).pathExtension.lowercased()
    if ["png", "jpg", "jpeg"].contains(ext) {
        let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: p) as CFURL, nil)!
        let props = CGImageSourceCopyPropertiesAtIndex(src, 0, nil) as! [String: Any]
        r["width"] = props["PixelWidth"]; r["height"] = props["PixelHeight"]; r["hasAlpha"] = props["HasAlpha"] ?? false
        out.append(r); continue
    }
    let a = AVURLAsset(url: URL(fileURLWithPath: p))
    r["duration"] = a.duration.seconds
    if let v = a.tracks(withMediaType: .video).first {
        let sz = v.naturalSize.applying(v.preferredTransform); r["width"] = abs(sz.width); r["height"] = abs(sz.height)
        r["fps"] = v.nominalFrameRate; r["videoKbps"] = Int(v.estimatedDataRate / 1000)
        if let fd = v.formatDescriptions.first {
            let f = fd as! CMFormatDescription
            r["codec"] = String(describing: CMFormatDescriptionGetMediaSubType(f) == kCMVideoCodecType_H264 ? "H.264" : "other")
            if let ex = CMFormatDescriptionGetExtensions(f) as? [String: Any], let atoms = ex["SampleDescriptionExtensionAtoms"] as? [String: Any], let avcC = atoms["avcC"] as? Data, avcC.count > 3 {
                let prof = Int(avcC[1]), lvl = Int(avcC[3])
                r["profile"] = (prof == 100 ? "High" : prof == 77 ? "Main" : prof == 66 ? "Baseline" : "\(prof)") + " L\(Double(lvl) / 10)"
            }
        }
        // sampled luma every 0.25 s
        let gen = AVAssetImageGenerator(asset: a); gen.requestedTimeToleranceBefore = .zero; gen.requestedTimeToleranceAfter = .zero
        gen.maximumSize = CGSize(width: 64, height: 64)
        var lum: [Double] = []; var t = 0.0
        while t < a.duration.seconds - 0.05 { if let im = try? gen.copyCGImage(at: CMTime(seconds: t, preferredTimescale: 600), actualTime: nil) { lum.append(lumaOf(im)) }; t += 0.25 }
        var run = 0, best = 0; for l in lum { if l < 0.06 { run += 1; best = max(best, run) } else { run = 0 } }
        r["blackRunS"] = Double(best) * 0.25; r["firstLuma"] = lum.first ?? 0; r["lastLuma"] = lum.last ?? 0; r["minLuma"] = lum.min() ?? 0
    }
    if let au = a.tracks(withMediaType: .audio).first, let fd = au.formatDescriptions.first {
        let asbd = CMAudioFormatDescriptionGetStreamBasicDescription(fd as! CMAudioFormatDescription)!.pointee
        r["audioCh"] = asbd.mChannelsPerFrame; r["audioHz"] = asbd.mSampleRate; r["audioKbps"] = Int(au.estimatedDataRate / 1000)
        r["audioCodec"] = asbd.mFormatID == kAudioFormatMPEG4AAC ? "AAC" : "\(asbd.mFormatID)"
    } else { r["audioCh"] = 0 }
    r["bytes"] = (try? FileManager.default.attributesOfItem(atPath: p)[.size] as? Int) ?? 0
    out.append(r)
}
print(String(data: try! JSONSerialization.data(withJSONObject: out, options: [.prettyPrinted, .sortedKeys]), encoding: .utf8)!)
