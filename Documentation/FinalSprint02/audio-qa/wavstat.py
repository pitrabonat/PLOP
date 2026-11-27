import wave,struct,sys,math
for p in sys.argv[1:]:
    w=wave.open(p); n=w.getnframes(); d=w.readframes(n)
    x=struct.unpack("<%dh"%(len(d)//2),d) if d else ()
    pk=max((abs(v) for v in x),default=0); rms=math.sqrt(sum(v*v for v in x)/max(1,len(x)))
    print("%s: %.2f s, peak %.1f dBFS, rms %.1f dBFS" % (p.split("/")[-1], n/w.getframerate(), 20*math.log10(max(pk,1)/32768), 20*math.log10(max(rms,1e-9)/32768)))
