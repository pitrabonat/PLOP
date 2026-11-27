// lift — Vision foreground-instance masks for the reference plate: writes one RGBA cut-out PNG per instance + mask, and prints bounds.
import Foundation; import Vision; import CoreImage; import ImageIO; import UniformTypeIdentifiers
let a = CommandLine.arguments; let url = URL(fileURLWithPath: a[1]); let out = a[2]
try? FileManager.default.createDirectory(atPath: out, withIntermediateDirectories: true)
let src = CGImageSourceCreateWithURL(url as CFURL, nil)!; let img = CGImageSourceCreateImageAtIndex(src, 0, nil)!
let req = VNGenerateForegroundInstanceMaskRequest()
let h = VNImageRequestHandler(cgImage: img)
try h.perform([req])
guard let r = req.results?.first else { print("no result"); exit(1) }
let ctx = CIContext()
func save(_ ci: CIImage, _ name: String) {
    let cg = ctx.createCGImage(ci, from: CGRect(x: 0, y: 0, width: img.width, height: img.height))!
    let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: out + "/" + name) as CFURL, UTType.png.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(d, cg, nil); CGImageDestinationFinalize(d)
}
print("instances:", r.allInstances.count)
for i in r.allInstances {
    let m = try r.generateScaledMaskForImage(forInstances: IndexSet(integer: i), from: h)
    let mi = CIImage(cvPixelBuffer: m)
    // bounds
    let ext = mi.extent; print("instance", i, "mask", ext.size)
    save(mi, "mask_\(i).png")
    let cut = try r.generateMaskedImage(ofInstances: IndexSet(integer: i), from: h, croppedToInstancesExtent: false)
    save(CIImage(cvPixelBuffer: cut), "cut_\(i).png")
}
