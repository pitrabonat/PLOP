#!/usr/bin/env python3
"""Record the Android emulator's audio output (what the guest plays) to a WAV via the emulator gRPC
EmulatorController/streamAudio endpoint. Uses curl for HTTP/2; token = grpc.token from the running emulator's discovery .ini
(emulator started with -grpc 8554 -grpc-use-token). Never prints the token.
usage: emucapture.py <seconds> <out.wav>
"""
import os, struct, subprocess, sys, tempfile, wave

secs, out = float(sys.argv[1]), sys.argv[2]
RATE = 44100
import glob
ini = sorted(glob.glob(os.path.expanduser("~/Library/Caches/TemporaryItems/avd/running/pid_*.ini")))[-1]
token = [l.split("=", 1)[1].strip() for l in open(ini) if l.startswith("grpc.token=")][0]

def varint(v):
    b = bytearray()
    while True:
        x = v & 0x7F; v >>= 7
        b.append(x | (0x80 if v else 0))
        if not v: return bytes(b)

msg = b"\x08" + varint(RATE) + b"\x10\x01" + b"\x18\x01" + (b"\x20\x01" if os.environ.get("RT") else b"")  # 44.1 kHz, stereo, s16
tmp = tempfile.mkdtemp()
req = os.path.join(tmp, "req.bin"); open(req, "wb").write(b"\x00" + struct.pack(">I", len(msg)) + msg)
hdr = os.path.join(tmp, "h.txt")
with open(hdr, "w") as f:
    f.write("content-type: application/grpc\nte: trailers\nauthorization: Bearer %s\n" % token)
raw = os.path.join(tmp, "out.bin")
subprocess.run(["curl", "-s", "--http2-prior-knowledge", "-X", "POST", "-H", "@" + hdr, "--data-binary", "@" + req,
                "--max-time", str(secs), "-o", raw,
                "http://127.0.0.1:8554/android.emulation.control.EmulatorController/streamAudio"])
os.remove(hdr)
data = open(raw, "rb").read()

def fields(buf):
    i = 0
    while i < len(buf):
        key = 0; shift = 0
        while True:
            c = buf[i]; i += 1; key |= (c & 0x7F) << shift; shift += 7
            if c < 0x80: break
        num, wt = key >> 3, key & 7
        if wt == 0:
            v = 0; shift = 0
            while True:
                c = buf[i]; i += 1; v |= (c & 0x7F) << shift; shift += 7
                if c < 0x80: break
            yield num, v
        elif wt == 2:
            n = 0; shift = 0
            while True:
                c = buf[i]; i += 1; n |= (c & 0x7F) << shift; shift += 7
                if c < 0x80: break
            yield num, buf[i:i + n]; i += n
        else:
            raise ValueError("wire type %d" % wt)

pcm = bytearray(); i = 0; packets = 0; stamps = []; fmts = set()
while i + 5 <= len(data):
    n = struct.unpack(">I", data[i + 1:i + 5])[0]
    body = data[i + 5:i + 5 + n]; i += 5 + n
    for num, v in fields(body):
        if num == 3: pcm += v; packets += 1
        elif num == 2: stamps.append(v)
        elif num == 1: fmts.add(bytes(v).hex())
with wave.open(out, "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(bytes(pcm))
if os.environ.get("DEBUG"):
    print("formats", fmts, "timestamps", stamps[:3], stamps[-1:] if stamps else None, "span us", (stamps[-1] - stamps[0]) if stamps else 0)
print("%s: %d packets, %.2f s of audio, %d bytes stream" % (out, packets, len(pcm) / 4 / RATE, len(data)))
