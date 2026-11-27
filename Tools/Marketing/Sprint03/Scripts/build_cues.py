#!/usr/bin/env python3
"""Regenerate Metadata/cues/<take>.tsv: real Plop.Core events for each take's replayed moves, at absolute source times.
probe --cues prints times relative to the first replay command; we add takes.tsv first_command_s."""
import csv, subprocess, os, sys
R = os.path.dirname(os.path.abspath(__file__)) + "/../.."
PACK = "/Volumes/PLOP_DEV/Unity/Projects/PLOP/Assets/Levels/Resources/Levels/PLOP_Pack_003.json"
PROBE = R + "/10_ProductionSources/LevelProbe/bin/out/probe.dll"
DOTNET = os.path.expanduser("~/.dotnet/dotnet")
for row in csv.DictReader(open(R + "/03_RawGameplay/Metadata/takes.tsv"), delimiter="\t"):
    lvl = row["level"].lstrip("L"); fc = float(row["first_command_s"])
    out = subprocess.run([DOTNET, PROBE, PACK, "--cues", lvl, row["moves"]], capture_output=True, text=True, check=True).stdout
    rows = []
    for line in out.splitlines():
        p = line.split("\t")
        if len(p) == 3:
            try: rows.append((fc + float(p[0]), p[1], p[2]))
            except ValueError: pass
    rows.sort()
    with open(R + f"/03_RawGameplay/Metadata/cues/{row['take']}.tsv", "w") as f:
        f.write("t_source\tcue\tcommand\n")
        for t, c, k in rows: f.write(f"{t:.3f}\t{c}\t{k}\n")
    print(row["take"], len(rows), rows[0][:2] if rows else "-", rows[-1][:2] if rows else "-")
