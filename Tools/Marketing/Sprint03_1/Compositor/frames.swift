import AVFoundation; import AppKit
// frames.swift <video> <outprefix> <t1> <t2> ... — writes JPEGs at the given seconds (negative = from end), prints duration.
let a = CommandLine.arguments; let asset = AVURLAsset(url: URL(fileURLWithPath: a[1]))
let dur = CMTimeGetSeconds(asset.duration); print(String(format: "%@ %.2fs", (a[1] as NSString).lastPathComponent, dur))
let g = AVAssetImageGenerator(asset: asset); g.appliesPreferredTrackTransform = true
g.maximumSize = CGSize(width: 360, height: 780); g.requestedTimeToleranceBefore = .zero; g.requestedTimeToleranceAfter = .zero
for (i, s) in a.dropFirst(3).enumerated() { var t = Double(s)!; if t < 0 { t = dur + t }
  guard let img = try? g.copyCGImage(at: CMTime(seconds: t, preferredTimescale: 600), actualTime: nil) else { print("no frame at \(t)"); continue }
  let rep = NSBitmapImageRep(cgImage: img); try! rep.representation(using: .jpeg, properties: [:])!.write(to: URL(fileURLWithPath: "\(a[2])_\(i).jpg")) }
