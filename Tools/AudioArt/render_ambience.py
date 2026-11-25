#!/usr/bin/env python3
"""Render the nine PLOP world ambience beds as mono PCM WAVs (use --only to render selected beds)."""
import argparse
from array import array
from itertools import accumulate
import math
from pathlib import Path
import random
import sys
import wave


RATE = 16_000
FRAMES = 12 * RATE
TAU = 2 * math.pi


def lowpass(signal, radius):
    padded = signal[-radius:] + signal + signal[:radius]
    integral = list(accumulate(padded, initial=0.0))
    window = 2 * radius + 1
    return [(integral[i + window] - integral[i]) / window for i in range(FRAMES)]


def air(seed):
    rng = random.Random(seed)
    white = [rng.uniform(-1, 1) for _ in range(FRAMES)]
    low = lowpass(white, 43)
    middle = lowpass(white, 12)
    high = lowpass(white, 3)
    return low, middle, high


def periodic(i, cycles, phase=0.0):
    return math.sin(TAU * cycles * i / FRAMES + phase)


def partial(i, frequency, phase=0.0):
    return periodic(i, round(frequency * FRAMES / RATE), phase)


def rustle_gate(seed, occurrences, length_min, length_max, floor):
    rng = random.Random(seed)
    envelope = [floor] * FRAMES
    for _ in range(occurrences):
        start = rng.randrange(FRAMES)
        count = int(rng.uniform(length_min, length_max) * RATE)
        strength = rng.uniform(0.12, 0.42)
        for i in range(count):
            shape = math.sin(math.pi * i / count)
            envelope[(start + i) % FRAMES] += strength * shape * shape
    return envelope


def cozy():
    low, middle, _ = air(401)
    output = []
    for i, (soft, close) in enumerate(zip(low, middle)):
        breath = 0.91 + 0.09 * periodic(i, 3) + 0.035 * periodic(i, 5, 1.1)
        room = partial(i, 109.75) + 0.36 * partial(i, 164.75, 1.7)
        output.append(breath * (0.68 * soft + 0.19 * close) + 0.011 * room)
    return output


def garden():
    low, middle, high = air(403)
    leaves = rustle_gate(404, 9, 0.57, 1.40, 0.43)
    output = []
    for i, (far, near, fine) in enumerate(zip(low, middle, high)):
        above = fine - near
        meadow = 0.28 * far + leaves[i] * (0.23 * near + 0.26 * above)
        output.append(meadow * (0.92 + 0.08 * periodic(i, 3, 0.7)))
    add_drops(output, seed=405, count=10, start_hz=675, finish_hz=460, gain=0.018)
    return output


def rain():
    low, middle, high = air(406)
    output = []
    for i, (far, rainwash, spray) in enumerate(zip(low, middle, high)):
        swell = 0.92 + 0.055 * periodic(i, 2, 0.9) + 0.025 * periodic(i, 5)
        output.append(swell * (0.18 * far + 0.53 * rainwash + 0.23 * (spray - rainwash)))
    add_drops(output, seed=407, count=110, start_hz=1110, finish_hz=545, gain=0.035)
    return output


def attic():
    low, middle, _ = air(408)
    timber = rustle_gate(409, 5, 0.6, 1.3, 0.42)
    output = []
    for i, (far, near) in enumerate(zip(low, middle)):
        grain = (0.82 + 0.18 * periodic(i, 1, 3.7)) * far + 0.10 * near
        warmth = partial(i, 130.75, 0.4) + 0.43 * partial(i, 196.0, 1.9)
        output.append(0.7 * grain + 0.020 * timber[i] * warmth)
    return output


def clouds():
    low, middle, _ = air(410)
    output = []
    for i, (soft, close) in enumerate(zip(low, middle)):
        flow = 0.72 + 0.28 * periodic(i, 2, 0.3)
        glow = partial(i, 164.75, 0.4) + 0.61 * partial(i, 246.92, 2.6)
        glass = partial(i, 329.58, 0.9) * (0.68 + 0.32 * periodic(i, 3, 1.7))
        output.append(0.39 * flow * (0.72 * soft + 0.28 * close) + 0.013 * glow + 0.008 * glass)
    return output


def pond():
    # Still frozen air with sparse, soft ice chimes.
    low, middle, _ = air(420)
    output = []
    for i, (far, near) in enumerate(zip(low, middle)):
        hush = 0.88 + 0.12 * periodic(i, 2, 1.3)
        frost = partial(i, 523.25, 0.2) + 0.5 * partial(i, 783.99, 1.1)
        output.append(hush * (0.52 * far + 0.12 * near) + 0.004 * frost * (0.5 + 0.5 * periodic(i, 3, 0.4)))
    add_drops(output, seed=421, count=14, start_hz=1320, finish_hz=1180, gain=0.012)
    return output


def meadow():
    # Gentle gusts that swell and settle; seeds rustling in grass.
    low, middle, high = air(422)
    gusts = rustle_gate(423, 7, 1.1, 2.6, 0.38)
    output = []
    for i, (far, near, fine) in enumerate(zip(low, middle, high)):
        breeze = 0.30 * far + gusts[i] * (0.30 * near + 0.18 * (fine - near))
        output.append(breeze * (0.90 + 0.10 * periodic(i, 2, 0.2)))
    return output


def ember():
    # A warm, low cavern hum with soft, slow crackles.
    low, middle, _ = air(424)
    output = []
    for i, (far, near) in enumerate(zip(low, middle)):
        warmth = partial(i, 98.0, 0.3) + 0.45 * partial(i, 146.83, 2.2)
        pulse = 0.86 + 0.14 * periodic(i, 3, 0.8)
        output.append(pulse * (0.58 * far + 0.08 * near) + 0.018 * warmth)
    add_drops(output, seed=425, count=26, start_hz=420, finish_hz=260, gain=0.016)
    return output


def prism():
    # Glassy shimmer under a quiet dome.
    low, middle, _ = air(426)
    output = []
    for i, (far, near) in enumerate(zip(low, middle)):
        shimmer = partial(i, 659.25, 0.5) * (0.6 + 0.4 * periodic(i, 4, 1.2)) + 0.6 * partial(i, 987.77, 2.4) * (0.5 + 0.5 * periodic(i, 3))
        glow = partial(i, 220.0, 0.1) + 0.5 * partial(i, 329.63, 1.4)
        output.append(0.40 * (0.70 * far + 0.15 * near) + 0.006 * shimmer + 0.009 * glow)
    return output


def add_drops(signal, seed, count, start_hz, finish_hz, gain):
    rng = random.Random(seed)
    for _ in range(count):
        start = rng.randrange(FRAMES)
        length = int(rng.uniform(0.041, 0.105) * RATE)
        pitch = rng.uniform(0.70, 1.56)
        amplitude = gain * rng.uniform(0.30, 1.0)
        for i in range(length):
            position = i / length
            rise_and_fall = math.sin(math.pi * position)
            frequency = pitch * (start_hz + (finish_hz - start_hz) * position * 0.5)
            phase = TAU * (i / RATE) * frequency
            wave_sample = math.sin(phase) + 0.13 * math.sin(phase * 2.13)
            signal[(start + i) % FRAMES] += amplitude * rise_and_fall * rise_and_fall * wave_sample


def finish(signal, rms_db):
    offset = sum(signal) / FRAMES
    centred = [sample - offset for sample in signal]
    current_rms = math.sqrt(sum(sample * sample for sample in centred) / FRAMES)
    multiplier = (10 ** (rms_db / 20)) / current_rms
    samples = [round(32767 * sample * multiplier) for sample in centred]
    if max(map(abs, samples)) >= 32767:
        raise ValueError("Mastering introduced clipping.")
    # The sequence is cyclic already. Choose a naturally smooth cut without a silent taper.
    def discontinuity(i):
        before = samples[i - 1] - samples[i - 2]
        after = samples[(i + 1) % FRAMES] - samples[i]
        return max(abs(samples[i] - samples[i - 1]), abs(after - before))
    first = min(range(1, FRAMES), key=discontinuity)
    return array("h", samples[first:] + samples[:first])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path,
                        default=Path(__file__).resolve().parents[2] / "Assets/Audio/Resources/Ambience")
    parser.add_argument("--only", nargs="*", default=None, help="bed names to render (default: all)")
    arguments = parser.parse_args()
    output = arguments.output
    output.mkdir(parents=True, exist_ok=True)
    for name, render, level in (
        ("cozy-nest", cozy, -36.0),
        ("dewdrop-garden", garden, -35.0),
        ("rainy-window", rain, -35.5),
        ("twilight-attic", attic, -37.0),
        ("moonlit-clouds", clouds, -36.0),
        ("frosted-pond", pond, -37.0),
        ("breezy-meadow", meadow, -35.5),
        ("ember-hollow", ember, -36.5),
        ("prism-observatory", prism, -37.0),
    ):
        if arguments.only and name not in arguments.only:
            continue
        samples = finish(render(), level)
        if sys.byteorder == "big":
            samples.byteswap()
        path = output / (name + ".wav")
        with wave.open(str(path), "wb") as writer:
            writer.setnchannels(1)
            writer.setsampwidth(2)
            writer.setframerate(RATE)
            writer.writeframes(samples.tobytes())
        print(path)


if __name__ == "__main__":
    main()
