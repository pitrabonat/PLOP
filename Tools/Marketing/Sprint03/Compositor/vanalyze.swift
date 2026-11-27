// vanalyze <video.mp4> [out.tsv] — per decoded frame: pts, mean luma (whole frame), mean abs diff vs previous frame in the
// board band (25–80 % of height). Prints a summary: first visible frame, and motion onsets (diff bursts after quiet).
import AVFoundation; import CoreImage
let url = URL(fileURLWithPath: CommandLine.arguments[1])
let asset = AVURLAsset(url: url); let track = asset.tracks(withMediaType: .video)[0]
let reader = try! AVAssetReader(asset: asset)
let out = AVAssetReaderTrackOutput(track: track, outputSettings: [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA])
reader.add(out); reader.startReading()
let gw = 72, gh = 156
let ctx = CGContext(data: nil, width: gw, height: gh, bitsPerComponent: 8, bytesPerRow: gw * 4, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
let cictx = CIContext()
var prev: [Double]? = nil; var rows = "pts\tluma\tdiff\n"; var series: [(Double, Double, Double)] = []
while let sb = out.copyNextSampleBuffer() {
  guard let pb = CMSampleBufferGetImageBuffer(sb) else { continue }
  let pts = CMSampleBufferGetPresentationTimeStamp(sb).seconds
  let ci = CIImage(cvPixelBuffer: pb); let img = cictx.createCGImage(ci, from: ci.extent)!
  ctx.interpolationQuality = .medium; ctx.draw(img, in: CGRect(x: 0, y: 0, width: gw, height: gh))
  let p = ctx.data!.bindMemory(to: UInt8.self, capacity: gw * gh * 4)
  var g = [Double](repeating: 0, count: gw * gh); var sum = 0.0
  for i in 0..<(gw * gh) { let v = (0.299 * Double(p[i*4]) + 0.587 * Double(p[i*4+1]) + 0.114 * Double(p[i*4+2])) / 255; g[i] = v; sum += v }
  var d = 0.0
  if let q = prev { var n = 0; for y in Int(Double(gh) * 0.20)..<Int(Double(gh) * 0.75) { for x in 0..<gw { d += abs(g[y*gw+x] - q[y*gw+x]); n += 1 } }; d /= Double(n) }
  prev = g; series.append((pts, sum / Double(gw * gh), d)); rows += String(format: "%.3f\t%.4f\t%.5f\n", pts, sum / Double(gw * gh), d)
}
if CommandLine.arguments.count > 2 { try! rows.write(toFile: CommandLine.arguments[2], atomically: true, encoding: .utf8) }
let visible = series.first { $0.0 > 1.5 && $0.1 > 0.5 }?.0 ?? -1
var onsets: [Double] = []; var lastActive = -10.0
for (t, l, d) in series where t > visible + 0.6 && l > 0.5 {
  if d > 0.0018 { if t - lastActive > 0.3 { onsets.append(t) }; lastActive = t }
}
let dur = CMTimeGetSeconds(asset.duration)
print(String(format: "%@\tdur %.2f\tvisible %.2f\tonsets %@", url.lastPathComponent, dur, visible, onsets.map { String(format: "%.2f", $0) }.joined(separator: ",")))
