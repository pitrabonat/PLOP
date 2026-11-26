#!/usr/bin/env python3
"""Rasterize the nine authored PLOP world backdrops and world-card art.

Run with the Unity project root as the optional first argument. Existing .meta
files stay in place so art changes retain their Sprite and resource references.
The painter uses the standard library and never runs in the shipped game.
"""

import math
from pathlib import Path
import random
import struct
import sys
import zlib


def rgb(value):
    return ((value >> 16) & 255, (value >> 8) & 255, value & 255)


def mix(a, b, weight):
    return tuple(round(lo + (hi - lo) * weight) for lo, hi in zip(a, b))


def easing(value):
    t = min(1.0, max(0.0, value))
    return t * t * (3.0 - 2.0 * t)


def png_chunk(tag, data):
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data))


class Canvas:
    def __init__(self, width, height, top, bottom, spotlight, light_at):
        self.width = width
        self.height = height
        self.pixels = bytearray(width * height * 3)
        self.plane(rgb(top), rgb(bottom), rgb(spotlight), light_at)

    def plane(self, top, bottom, spotlight, light_at):
        cx, cy = light_at
        for y in range(self.height):
            progress = easing(y / self.height)
            base = mix(top, bottom, progress)
            light_y = (y / self.height - cy) * 1.52
            for x in range(self.width):
                light_x = x / self.width - cx
                wash = max(0.0, 0.77 - light_x * light_x * 1.33 - light_y * light_y)
                offset = (y * self.width + x) * 3
                self.pixels[offset] = round(base[0] + (spotlight[0] - base[0]) * wash)
                self.pixels[offset + 1] = round(base[1] + (spotlight[1] - base[1]) * wash)
                self.pixels[offset + 2] = round(base[2] + (spotlight[2] - base[2]) * wash)

    def blend(self, x, y, color, alpha):
        offset = (y * self.width + x) * 3
        for channel, tone in enumerate(color):
            old = self.pixels[offset + channel]
            self.pixels[offset + channel] = round(old + (tone - old) * alpha)

    def paint(self, cx, cy, hw, hh, angle, radius, face, shade, strength=1.0, feather=1.1, bevel=0.11):
        sine, cosine = math.sin(math.radians(angle)), math.cos(math.radians(angle))
        rx = abs(cosine * hw) + abs(sine * hh) + feather
        ry = abs(sine * hw) + abs(cosine * hh) + feather
        xmin = max(0, math.floor(cx - rx))
        xmax = min(self.width, math.ceil(cx + rx))
        ymin = max(0, math.floor(cy - ry))
        ymax = min(self.height, math.ceil(cy + ry))
        shortest = min(hw, hh)

        for y in range(ymin, ymax):
            delta_y = y + 0.5 - cy
            for x in range(xmin, xmax):
                delta_x = x + 0.5 - cx
                u = delta_x * cosine + delta_y * sine
                v = delta_y * cosine - delta_x * sine
                if radius is None:
                    distance = (math.hypot(u / hw, v / hh) - 1.0) * shortest
                else:
                    ax = abs(u) - (hw - radius)
                    ay = abs(v) - (hh - radius)
                    distance = math.hypot(max(ax, 0.0), max(ay, 0.0)) + min(0.0, max(ax, ay)) - radius
                coverage = easing(0.5 - distance / feather)
                if coverage <= 0.0:
                    continue
                light = easing(0.32 + 0.48 * v / hh + 0.16 * u / hw)
                light += bevel * easing(1.0 + distance / max(1.0, shortest * 0.19))
                color = mix(face, shade, min(1.0, light))
                self.blend(x, y, color, strength * coverage)

    def round(self, x, y, w, h, radius, top, bottom, angle=0, opacity=1, feather=1.1, bevel=0.11):
        self.paint(x, y, w * 0.5, h * 0.5, angle, min(radius, w * 0.5, h * 0.5),
                   rgb(top), rgb(bottom), opacity, feather, bevel)

    def oval(self, x, y, w, h, top, bottom, angle=0, opacity=1, feather=1.1, bevel=0.11):
        self.paint(x, y, w * 0.5, h * 0.5, angle, None, rgb(top), rgb(bottom), opacity, feather, bevel)

    def mist(self, x, y, w, h, color, opacity=0.3, angle=0):
        self.oval(x, y, w, h, color, color, angle, opacity, max(9.0, min(w, h) * 0.62), 0)

    def beam(self, x, y, w, h, color, opacity=0.2, angle=0):
        self.round(x, y, w, h, w * 0.32, color, color, angle, opacity, w * 0.74, 0)

    def segment(self, x, y, end_x, end_y, width, color, opacity=1):
        angle = math.degrees(math.atan2(end_y - y, end_x - x))
        distance = math.hypot(end_x - x, end_y - y)
        self.round((x + end_x) * 0.5, (y + end_y) * 0.5, distance + width, width,
                   width * 0.5, color, color, angle, opacity, 1.2, 0)

    def save(self, destination):
        destination.parent.mkdir(parents=True, exist_ok=True)
        row_width = self.width * 3
        # PNG's left filter keeps smooth tonal fields small without affecting the RGB pixels.
        buffer = bytearray()
        for row in range(self.height):
            starts = row_width * row
            scanline = self.pixels[starts:starts + row_width]
            buffer.append(1)
            buffer.extend(scanline[0:3])
            buffer.extend((scanline[i] - scanline[i - 3]) & 255 for i in range(3, row_width))

        header = struct.pack(">2I5B", self.width, self.height, 8, 2, 0, 0, 0)
        data = b"\x89PNG\r\n\x1a\n" + png_chunk(b"IHDR", header)
        data += png_chunk(b"IDAT", zlib.compress(buffer, 9)) + png_chunk(b"IEND", b"")
        destination.write_bytes(data)


def cushion(canvas, x, y, w, h, top, bottom, stitch, angle):
    canvas.mist(x + 6, y + h * 0.44, w * 1.03, h * 0.52, bottom, 0.28, angle)
    canvas.round(x, y, w, h, h * 0.40, top, bottom, angle)
    canvas.round(x, y - 1, w * 0.85, h * 0.76, h * 0.30, stitch, stitch, angle, 0.34, 1.2, 0)
    canvas.round(x, y - 1, w * 0.82, h * 0.71, h * 0.28, top, bottom, angle, 1)
    canvas.mist(x - 8, y - h * 0.2, w * 0.55, h * 0.25, top, 0.46, angle)


def window(canvas, x, y, w, h, frame, sky, lower, cross):
    canvas.round(x + 3, y + 7, w + 10, h + 11, 22, lower, lower, opacity=0.22, feather=8)
    canvas.round(x, y, w, h, 25, frame, cross)
    inset = 14
    canvas.round(x, y, w - inset, h - inset, 20, sky, lower)
    canvas.round(x, y, w - inset, 4, 2, frame, cross, opacity=0.76)
    canvas.round(x, y, 4, h - inset, 2, frame, cross, opacity=0.76)


def dew(canvas, x, y, size):
    canvas.oval(x + 1, y + 2, size * 0.95, size, 0x549581, 0x6B9F8F, opacity=0.22, feather=2.3)
    canvas.oval(x, y, size, size * 0.94, 0xEAF9E9, 0xA7D3BC, angle=24, opacity=0.92, bevel=0.03)
    canvas.oval(x - size * 0.15, y - size * 0.17, size * 0.32, size * 0.24,
                0xFCFFF1, 0xD9F2E4, opacity=0.74, feather=1.6)


def leaf(canvas, x, y, w, h, angle, top, bottom, with_dew=False):
    canvas.oval(x + 3, y + 5, w, h, bottom, bottom, angle, 0.23, 5.8, 0)
    canvas.oval(x, y, w, h, top, bottom, angle, feather=1.4)
    theta = math.radians(angle)
    scale = w * 0.32
    vx, vy = math.cos(theta) * scale, math.sin(theta) * scale
    canvas.segment(x - vx, y - vy, x + vx, y + vy, 1.1, top, 0.70)
    canvas.mist(x - 2, y - h * 0.11, w * 0.64, h * 0.27, top, 0.42, angle)
    if with_dew:
        dew(canvas, x + w * 0.11, y - 2, 9 if w > 60 else 5)


def rain(canvas, x, y, length, opacity):
    canvas.segment(x, y, x - length * 0.32, y + length, 2.3, 0xF7FAFF, opacity)
    canvas.oval(x - length * 0.33, y + length, 2.3, 3.7,
                0xF6F8FF, 0xADBFD9, angle=16, opacity=opacity)


def toy(canvas, x, y, w, h, top, side, angle):
    canvas.mist(x + 5, y + h * 0.44, w * 1.10, h * 0.29, side, 0.42, angle)
    canvas.round(x, y + 5, w, h, h * 0.24, side, side, angle)
    canvas.round(x - 1, y - 2, w, h - 2, h * 0.24, top, side, angle)
    canvas.mist(x - w * 0.12, y - h * 0.19, w * 0.44, h * 0.38, top, 0.66, angle)


def cloud(canvas, x, y, w, h, top, lower):
    canvas.mist(x + 4, y + h * 0.23, w * 1.02, h * 0.82, lower, 0.32)
    canvas.oval(x, y + h * 0.20, w, h * 0.74, top, lower, feather=3.0)
    canvas.oval(x - w * 0.20, y - h * 0.04, w * 0.51, h * 0.88, top, lower, feather=3.0)
    canvas.oval(x + w * 0.03, y - h * 0.21, w * 0.51, h * 0.96, top, lower, feather=3.0)
    canvas.oval(x + w * 0.23, y - h * 0.02, w * 0.42, h * 0.76, top, lower, feather=3.0)
    canvas.mist(x, y - h * 0.1, w * 0.91, h * 0.67, top, 0.58)


def crescent(canvas, x, y, radius):
    canvas.mist(x, y, 2.9 * radius, 2.9 * radius, 0xFFEBCA, 0.34)
    inner_x = x + radius * 0.48
    inner_y = y - radius * 0.36
    spread = radius * 1.12
    for py in range(max(0, math.floor(y - spread)), min(canvas.height, math.ceil(y + spread))):
        for px in range(max(0, math.floor(x - spread)), min(canvas.width, math.ceil(x + spread))):
            outer = math.hypot(px + 0.50 - x, py + 0.50 - y) - radius
            cutout = math.hypot(px + 0.50 - inner_x, py + 0.50 - inner_y) - radius * 0.82
            distance = max(outer, -cutout)
            coverage = easing(0.50 - distance * 0.53)
            if coverage > 0:
                progress = easing((py - y + radius) / (2 * radius))
                color = mix(rgb(0xFFF8E9), rgb(0xEEDBBE), progress)
                canvas.blend(px, py, color, 0.90 * coverage)


def star(canvas, x, y, size, color, opacity=1):
    canvas.segment(x - size * 0.5, y, x + size * 0.5, y, max(1.6, size * 0.24), color, opacity)
    canvas.segment(x, y - size * 0.5, x, y + size * 0.5, max(1.6, size * 0.24), color, opacity)


def cozy(canvas, preview):
    if preview:
        window(canvas, 78, 53, 106, 117, 0xFFF5E7, 0xFFF2DC, 0xF0CFAF, 0xE4B699)
        canvas.beam(144, 85, 97, 165, 0xFFEDD0, 0.48, -46)
        cushion(canvas, 235, 110, 130, 76, 0xF5C6AD, 0xDDAE94, 0xB57D69, -13)
        cushion(canvas, 166, 132, 98, 53, 0xFFF0CD, 0xE7C8A6, 0xCEAD85, 9)
        return
    canvas.beam(176, 98, 112, 324, 0xFFF4DA, 0.32, -52)
    canvas.beam(221, 71, 62, 229, 0xFFF4D2, 0.24, -48)
    window(canvas, 95, 29, 120, 137, 0xFFF5E7, 0xFFF3E0, 0xF3D5B6, 0xE6C09E)
    canvas.round(101, 111, 155, 9, 4.5, 0xFFF3E2, 0xEBBFA4, opacity=0.60)
    canvas.beam(283, 1001, 568, 93, 0xEEC8AD, 0.29)
    canvas.mist(69, 1025, 235, 99, 0xC7937B, 0.29)
    cushion(canvas, 39, 995, 147, 82, 0xF5BDAD, 0xDFAD99, 0xBD8976, -16)
    cushion(canvas, 118, 1004, 102, 62, 0xFFF0CE, 0xE7C8A6, 0xCEAD85, 10)
    cushion(canvas, 532, 1004, 156, 83, 0xF5D7BD, 0xE5BA9F, 0xCA9A84, 17)
    canvas.oval(474, 995, 15, 15, 0xF8E9BD, 0xDFBE8F, opacity=0.78)


def garden(canvas, preview):
    leaf_colors = [(0xD8E7BA, 0x91B48F), (0xE3EEC5, 0xA2C587), (0xB9DFCA, 0x79AF9C)]
    if preview:
        canvas.mist(166, 152, 280, 82, 0x92B494, 0.34)
        for x, y, w, h, angle, index in (
            (62, 84, 128, 47, -22, 0), (115, 62, 113, 48, -60, 2),
            (191, 82, 155, 60, -20, 1), (252, 110, 119, 49, 16, 2),
            (95, 121, 132, 36, 18, 1), (176, 124, 120, 40, 14, 0)
        ):
            leaf(canvas, x, y, w, h, angle, *leaf_colors[index], with_dew=True)
        return
    canvas.mist(502, 84, 240, 171, 0xBCD7AE, 0.22)
    canvas.mist(23, 120, 118, 128, 0xD4DDB1, 0.21)
    canvas.segment(533, -3, 467, 96, 4, 0x8AAF82, 0.56)
    for x, y, w, h, angle, index in (
        (473, 51, 74, 26, -145, 0), (518, 58, 72, 26, -52, 2),
        (453, 84, 65, 24, -146, 1), (507, 20, 74, 24, -150, 0),
        (34, 19, 98, 34, 30, 2), (59, 4, 73, 27, 32, 1)
    ):
        leaf(canvas, x, y, w, h, angle, *leaf_colors[index], with_dew=True)
    canvas.mist(46, 972, 241, 136, 0x93B686, 0.38)
    canvas.mist(521, 1001, 235, 118, 0x85AF97, 0.28)
    for x, y, w, h, angle, index in (
        (19, 964, 154, 55, -60, 1), (75, 976, 129, 50, -37, 0),
        (114, 1008, 123, 52, -22, 2), (20, 994, 142, 68, -16, 2),
        (557, 967, 156, 53, 54, 2), (516, 1003, 133, 48, 26, 1),
        (462, 1024, 119, 48, 17, 0)
    ):
        leaf(canvas, x, y, w, h, angle, *leaf_colors[index], with_dew=w < 145)
    for x, y, size in ((164, 1011, 13), (441, 995, 12), (155, 983, 7), (421, 1014, 6)):
        dew(canvas, x, y, size)


def rainy(canvas, preview):
    if preview:
        window(canvas, 101, 59, 168, 114, 0xEDF0F3, 0xE7F1F8, 0xB3C7DF, 0xC6D1E2)
        window(canvas, 246, 52, 115, 111, 0xE9EDF5, 0xE0EBF8, 0xC3CFE5, 0xC4CEDF)
        for x, y, length in ((40, 35, 19), (74, 17, 15), (132, 66, 21), (165, 22, 17),
                             (214, 13, 19), (236, 58, 16), (281, 46, 24), (72, 70, 22)):
            rain(canvas, x, y, length, 0.75)
        cushion(canvas, 227, 136, 118, 50, 0xEAE3E3, 0xB5AEC2, 0xAEA7BB, -7)
        return
    window(canvas, 72, 36, 162, 142, 0xEAEEF5, 0xECF1F9, 0xBDCBDF, 0xC2CCD9)
    window(canvas, 522, 40, 188, 154, 0xEBEFF4, 0xE7EEF9, 0xBDCADE, 0xBCCBDD)
    canvas.round(72, 116, 173, 9, 4.5, 0xF3EEF0, 0xC4C7D6, opacity=0.69)
    canvas.round(523, 124, 178, 10, 5, 0xF3EEF2, 0xC8CBD8, opacity=0.70)
    source = random.Random(204)
    for _ in range(38):
        shoulder = source.choice((source.uniform(11, 140), source.uniform(440, 567)))
        height = source.uniform(12, 135)
        rain(canvas, shoulder, height, source.uniform(5, 18), source.uniform(0.36, 0.71))
    canvas.beam(93, 149, 59, 280, 0xF6F0EB, 0.37, -40)
    canvas.beam(501, 190, 72, 285, 0xF3EBE8, 0.34, 37)
    canvas.round(288, 1011, 614, 39, 12, 0xE8DFE4, 0xC8C3D3, opacity=0.75)
    cushion(canvas, 38, 1001, 146, 54, 0xE9E4EB, 0xC4B9CD, 0xB2A6C1, -10)
    cushion(canvas, 534, 994, 153, 60, 0xE9DEE4, 0xBFADC5, 0xAB9BB2, 11)
    cushion(canvas, 480, 1011, 102, 41, 0xF8ECE6, 0xD6C4C9, 0xC2ABB4, -7)


def lamp(canvas, x, y, scale):
    canvas.mist(x, y + 15 * scale, 139 * scale, 129 * scale, 0xFFD9A0, 0.67)
    canvas.segment(x, y - 13 * scale, x, y - 57 * scale, 2.6 * scale, 0xA995AB, 0.83)
    canvas.oval(x, y + 14 * scale, 54 * scale, 17 * scale, 0xFFF0AE, 0xF6CE87, opacity=0.92)
    canvas.round(x, y, 47 * scale, 30 * scale, 13 * scale, 0xF4D1A6, 0xC68F93)
    canvas.round(x, y + 12 * scale, 49 * scale, 8 * scale, 3 * scale, 0xF4D09D, 0xD6A489)


def attic(canvas, preview):
    if preview:
        lamp(canvas, 223, 48, 1.45)
        toy(canvas, 105, 116, 78, 63, 0xE2BDD2, 0xAD8CA8, -12)
        toy(canvas, 160, 126, 67, 47, 0xEBC7A7, 0xC49586, 10)
        toy(canvas, 79, 85, 45, 38, 0xE8D9DE, 0xB99FBA, 13)
        canvas.segment(15, 148, 310, 148, 2, 0xB79CB6, 0.25)
        return
    canvas.segment(-4, 18, 578, 18, 17, 0xC1A7BC, 0.27)
    canvas.segment(37, -10, -25, 117, 14, 0xC4ACBF, 0.37)
    canvas.segment(560, -8, 607, 133, 15, 0xB9A0B5, 0.27)
    lamp(canvas, 463, 43, 1)
    canvas.beam(392, 113, 161, 190, 0xFDE5C2, 0.30, 33)
    canvas.round(288, 1018, 618, 74, 18, 0xD4B6C1, 0xB998AF, opacity=0.49)
    canvas.segment(49, 985, 178, 1007, 1.5, 0xF1DCDE, 0.50)
    canvas.segment(415, 1007, 537, 986, 1.5, 0xF1D4D4, 0.47)
    toy(canvas, 37, 1002, 76, 70, 0xE4BED1, 0xAB8EAA, -13)
    toy(canvas, 104, 1015, 56, 46, 0xF0CCA7, 0xC69B8A, 9)
    toy(canvas, 45, 957, 47, 36, 0xEDD8DE, 0xC1A7BD, 9)
    canvas.oval(512, 998, 122, 62, 0xC6A8B7, 0xA281A2, opacity=0.80)
    canvas.oval(512, 982, 120, 48, 0xEAD0C8, 0xC4A1AA)
    canvas.oval(512, 980, 56, 21, 0xC6A4AF, 0xB590A6)
    canvas.oval(512, 978, 44, 15, 0xE6C2BC, 0xC6A1AC)


def moonlit(canvas, preview):
    if preview:
        crescent(canvas, 227, 53, 29)
        cloud(canvas, 74, 106, 172, 60, 0xF9F1F7, 0xCCCAE6)
        cloud(canvas, 228, 136, 208, 67, 0xFAF7F7, 0xCCCFE8)
        star(canvas, 96, 36, 15, 0xFBF1D2, 0.94)
        star(canvas, 175, 53, 9, 0xF9F3E2, 0.92)
        star(canvas, 289, 29, 8, 0xFFEFCE, 0.85)
        return
    crescent(canvas, 434, 52, 30)
    cloud(canvas, 53, 33, 139, 49, 0xF6F2FC, 0xC9CCE7)
    cloud(canvas, 510, 996, 210, 83, 0xFAF4FA, 0xD1D1EB)
    cloud(canvas, 38, 994, 200, 95, 0xF7F5FC, 0xCDD0E9)
    cloud(canvas, 132, 1034, 230, 90, 0xFFF8F7, 0xD3D5EE)
    cloud(canvas, 490, 1043, 268, 91, 0xFBF8FC, 0xCCD0E5)
    for x, y, size in ((142, 51, 14), (268, 31, 7), (385, 99, 10), (489, 89, 11),
                       (511, 45, 5), (108, 107, 6), (35, 935, 7), (543, 925, 11)):
        star(canvas, x, y, size, 0xFFF5D9, 0.86)
    for x, y, size in ((190, 27, 4), (373, 36, 4), (451, 112, 5), (515, 63, 4), (127, 118, 4)):
        canvas.oval(x, y, size, size * 0.93, 0xFFF9EA, 0xD8D4ED, opacity=0.84)


def ring(canvas, x, y, w, h, outer, inner, glow, angle=0):
    canvas.mist(x, y, w * 1.5, h * 1.5, glow, 0.30, angle)
    canvas.oval(x, y, w, h, outer, inner, angle, feather=1.6)
    canvas.oval(x, y, w * 0.62, h * 0.62, glow, inner, angle, 0.92, feather=2.2)
    canvas.mist(x, y, w * 0.48, h * 0.48, 0xFFFFFF, 0.25, angle)


def snowflake(canvas, x, y, size, opacity=0.9):
    for angle in (0, 60, 120):
        dx = math.cos(math.radians(angle)) * size * 0.5
        dy = math.sin(math.radians(angle)) * size * 0.5
        canvas.segment(x - dx, y - dy, x + dx, y + dy, max(1.6, size * 0.15), 0x9FC3DC, opacity)


def frosted(canvas, preview):
    ice = (0xE4F2FB, 0xA4C8E2)
    if preview:
        canvas.oval(160, 132, 250, 54, 0xBFDCEE, 0x86B2D0, opacity=0.95)
        canvas.mist(140, 124, 120, 18, 0xFFFFFF, 0.55)
        toy(canvas, 66, 116, 56, 44, ice[0], ice[1], -10)
        toy(canvas, 262, 112, 48, 40, ice[0], ice[1], 12)
        for x, y, w, h, angle in ((60, 50, 90, 30, -24), (110, 30, 80, 28, -58), (250, 44, 92, 30, 28)):
            leaf(canvas, x, y, w, h, angle, 0xE4F2F4, 0xA8CAD2, with_dew=False)
        for x, y, size in ((180, 40, 16), (212, 74, 10), (150, 82, 8), (290, 26, 9)):
            snowflake(canvas, x, y, size)
        return
    canvas.mist(470, 70, 260, 160, 0xF4FAFF, 0.40)
    canvas.segment(560, -6, 470, 104, 5, 0x9CB6C4, 0.55)
    for x, y, w, h, angle in ((478, 56, 82, 28, -140), (520, 70, 76, 26, -50), (452, 92, 70, 24, -150), (504, 24, 76, 24, -150)):
        leaf(canvas, x, y, w, h, angle, 0xE7F3F5, 0xA6C6D0, with_dew=False)
    for x, y, size in ((96, 60, 18), (160, 120, 11), (60, 150, 9), (280, 40, 12), (380, 96, 9), (220, 170, 7), (120, 930, 10), (430, 912, 12)):
        snowflake(canvas, x, y, size, 0.8)
    canvas.oval(290, 1012, 620, 120, 0xBFDCEE, 0x86B2D0, opacity=0.95)
    canvas.mist(250, 990, 360, 40, 0xFFFFFF, 0.42)
    toy(canvas, 52, 980, 84, 70, ice[0], ice[1], -12)
    toy(canvas, 112, 1000, 56, 46, 0xF2F9FD, 0xC4DCEC, 8)
    toy(canvas, 520, 985, 92, 74, ice[0], ice[1], 10)
    canvas.mist(40, 1030, 200, 60, 0xFFFFFF, 0.55)
    canvas.mist(540, 1036, 220, 60, 0xFFFFFF, 0.55)


def windmill(canvas, x, y, scale, angle, sails=True):
    # In-game backdrops leave the sails off: PlopAmbient.shader turns them (same hub, size and colours).
    canvas.mist(x, y + 60 * scale, 120 * scale, 70 * scale, 0x9FBF8A, 0.25)
    canvas.round(x, y + 40 * scale, 34 * scale, 80 * scale, 14 * scale, 0xF6EEDC, 0xD6C2A4)
    canvas.round(x, y + 2 * scale, 42 * scale, 22 * scale, 10 * scale, 0xE7A98F, 0xC7826C)
    if not sails:
        return
    for k in range(4):
        a = math.radians(angle + k * 90)
        ex, ey = x + math.cos(a) * 52 * scale, y + math.sin(a) * 52 * scale
        canvas.segment(x, y, ex, ey, 11 * scale, 0xFFF6E6, 0.95)
    canvas.oval(x, y, 12 * scale, 12 * scale, 0xD9B48F, 0xB68F70)


def seed_puff(canvas, x, y, size, opacity=0.85):
    canvas.mist(x, y, size * 2.2, size * 2.2, 0xFFFFFF, 0.35)
    for k in range(8):
        a = math.radians(k * 45 + 10)
        canvas.segment(x, y, x + math.cos(a) * size, y + math.sin(a) * size, 1.4, 0xFFFDF4, opacity)
    canvas.oval(x, y, size * 0.35, size * 0.35, 0xEDE2C4, 0xD2C29B, opacity=opacity)


def grass(canvas, x, y, height, lean, top, bottom):
    canvas.round(x + lean * 0.5, y - height * 0.5, 9, height, 4.5, top, bottom, lean)


def meadow(canvas, preview):
    greens = ((0xCFE5AE, 0x8FB577), (0xDDEEB9, 0xA5C68A), (0xBCDDA3, 0x7FA86E))
    if preview:
        canvas.oval(110, 150, 300, 70, 0xD4E8B4, 0xA9CA8E)
        canvas.oval(270, 156, 220, 60, 0xCBE2AB, 0x9CBF82)
        windmill(canvas, 250, 62, 0.65, 20)
        for x, y, size in ((80, 50, 10), (140, 30, 8), (190, 80, 7)):
            seed_puff(canvas, x, y, size)
        for x in range(20, 320, 22):
            grass(canvas, x, 158, 26 + (x * 7) % 14, (x % 3 - 1) * 12, *greens[x % 3])
        return
    canvas.mist(120, 60, 260, 140, 0xFFFDF0, 0.35)
    windmill(canvas, 470, 90, 1.0, 18, sails=False)
    windmill(canvas, 380, 140, 0.55, 52, sails=False)
    for x, y, size in ((90, 70, 12), (180, 140, 9), (60, 190, 7), (300, 60, 8), (240, 210, 6), (520, 920, 9), (80, 900, 8)):
        seed_puff(canvas, x, y, size)
    canvas.oval(120, 1030, 420, 140, 0xD2E7B2, 0xA2C487)
    canvas.oval(480, 1036, 380, 130, 0xC9E1A8, 0x98BC80)
    for x in range(6, 576, 17):
        grass(canvas, x, 1024 - (x * 13) % 18, 46 + (x * 7) % 26, (x % 5 - 2) * 9, *greens[x % 3])


def ember(canvas, preview):
    stones = ((0x8A6E78, 0x5C4651), (0x9B7C82, 0x6A515A))
    if preview:
        canvas.mist(160, 140, 260, 70, 0xF6A86A, 0.40)
        canvas.oval(160, 146, 170, 30, 0xF7A35E, 0xE26F4A, opacity=0.95)
        canvas.mist(160, 140, 120, 14, 0xFFE0A0, 0.6)
        toy(canvas, 60, 120, 70, 56, *stones[0], -10)
        toy(canvas, 266, 118, 64, 52, *stones[1], 12)
        for x, y, size in ((120, 60, 6), (200, 40, 5), (240, 80, 4), (90, 90, 4)):
            canvas.mist(x, y, size * 4, size * 4, 0xFFB46A, 0.45)
            canvas.oval(x, y, size, size, 0xFFD08A, 0xF2924F)
        return
    canvas.mist(288, 40, 600, 160, 0xF3B07A, 0.28)
    toy(canvas, 40, 30, 120, 96, *stones[0], 14)
    toy(canvas, 540, 40, 140, 110, *stones[1], -12)
    toy(canvas, 470, 110, 70, 56, *stones[0], 8)
    for x, y, size in ((150, 120, 5), (260, 70, 4), (330, 150, 6), (420, 60, 4), (210, 190, 4), (100, 920, 5), (470, 930, 6), (300, 900, 4)):
        canvas.mist(x, y, size * 5, size * 5, 0xFFB46A, 0.40)
        canvas.oval(x, y, size, size, 0xFFD08A, 0xF2924F)
    canvas.mist(300, 1000, 520, 140, 0xF6A266, 0.45)
    canvas.oval(300, 1008, 360, 64, 0xF8A65E, 0xDF6C48, opacity=0.95)
    canvas.mist(290, 996, 240, 22, 0xFFE2A0, 0.6)
    toy(canvas, 50, 990, 110, 90, *stones[1], -10)
    toy(canvas, 530, 996, 120, 92, *stones[0], 12)
    toy(canvas, 140, 1010, 70, 54, *stones[0], 6)


def prism(canvas, preview):
    if preview:
        canvas.oval(160, 170, 300, 160, 0xE9E2F4, 0xC9BEE2, opacity=0.6)
        ring(canvas, 90, 90, 58, 58, 0xC6B4EE, 0xEFE6FF, 0xF9C6E2)
        ring(canvas, 236, 96, 58, 58, 0xA9D7E8, 0xE6F6FF, 0xC6F0E4)
        canvas.round(160, 128, 120, 22, 10, 0xD8CDEE, 0xB0A2CF, -18)
        for x, y, size in ((160, 40, 12), (40, 40, 8), (290, 40, 9), (180, 74, 6)):
            star(canvas, x, y, size, 0xFFF4D8, 0.9)
        return
    canvas.oval(288, -40, 640, 300, 0xEEE7F8, 0xCFC5E6, opacity=0.55)
    for k in range(7):
        a = math.radians(200 + k * 23)
        canvas.segment(288, 110, 288 + math.cos(a) * 340, 110 + math.sin(a) * 160, 3, 0xC2B6DD, 0.35)
    for x, y, size in ((80, 60, 14), (200, 40, 9), (380, 70, 12), (500, 150, 8), (130, 170, 7), (60, 930, 9), (520, 910, 11)):
        star(canvas, x, y, size, 0xFFF4D8, 0.88)
    ring(canvas, 470, 120, 62, 62, 0xC6B4EE, 0xEFE6FF, 0xF9C6E2)
    ring(canvas, 110, 990, 80, 80, 0xA9D7E8, 0xE6F6FF, 0xC6F0E4)
    canvas.round(440, 1000, 220, 34, 16, 0xDCD2F0, 0xB5A8D4, -16)
    canvas.oval(345, 975, 46, 46, 0xEAE4F7, 0xBFB2DD)
    canvas.oval(560, 1010, 60, 60, 0xF3EEFB, 0xC7BBE2, opacity=0.9)
    canvas.mist(552, 1000, 26, 26, 0xFFFFFF, 0.6)


# ---------------------------------------------------------------- Phase 11.6 enrichment
# Reference-inspired props (Documentation/ArtPack/Phase11_6/Visual_Target.png) added around the board:
# side margins, the band under the header and the floor line. The board area (about y 290-720 at the
# centre) and the HUD stay clear. Existing props keep their positions, so Ambient.json regions still match.

def flower(canvas, x, y, size, petal, centre, opacity=1.0):
    canvas.mist(x, y + size * 0.6, size * 2.2, size * 0.9, 0x7FA86E, 0.18 * opacity)
    for k in range(5):
        a = math.radians(k * 72 - 90)
        canvas.oval(x + math.cos(a) * size * 0.42, y + math.sin(a) * size * 0.42, size * 0.62, size * 0.50,
                    petal, mix_hex(petal, 0x9A6A8A, 0.25), angle=k * 72, opacity=opacity, feather=1.3)
    canvas.oval(x, y, size * 0.42, size * 0.42, centre, mix_hex(centre, 0xB07A40, 0.3), opacity=opacity)


def mix_hex(a, b, w):
    ca, cb = rgb(a), rgb(b)
    m = mix(ca, cb, w)
    return (m[0] << 16) | (m[1] << 8) | m[2]


def pine(canvas, x, y, height, body, shade, snow):
    canvas.mist(x, y + 6, height * 0.7, height * 0.16, shade, 0.30)
    canvas.round(x, y - height * 0.06, height * 0.08, height * 0.16, 3, 0xA88E86, 0x7E6862)
    for k, (w, h, dy) in enumerate(((0.62, 0.38, 0.22), (0.50, 0.34, 0.45), (0.36, 0.30, 0.66))):
        cy = y - height * dy
        for side in (-1, 1):
            canvas.segment(x, cy - height * h * 0.5, x + side * height * w * 0.5, cy + height * h * 0.35,
                           height * 0.10, body, 0.95)
        canvas.oval(x, cy + height * h * 0.15, height * w * 0.82, height * h * 0.62, body, shade, feather=1.6)
        canvas.oval(x - height * 0.03, cy - height * h * 0.18, height * w * 0.55, height * h * 0.22, snow, 0xD9E8F2,
                    opacity=0.92, feather=1.8)


def crystal(canvas, x, y, w, h, angle, top, bottom, glow=None):
    if glow is not None:
        canvas.mist(x, y - h * 0.3, w * 3.2, h * 1.4, glow, 0.30)
    canvas.round(x, y - h * 0.5, w, h, w * 0.28, top, bottom, angle, bevel=0.22)
    canvas.round(x - w * 0.18, y - h * 0.56, w * 0.22, h * 0.70, w * 0.1, 0xFFFFFF, top, angle, opacity=0.45, feather=1.4)


def cluster(canvas, x, y, scale, top, bottom, glow=None):
    for dx, h, w, a in ((-0.55, 0.62, 0.30, -24), (0.50, 0.70, 0.30, 22), (0.0, 1.0, 0.38, 0), (-0.25, 0.45, 0.24, -8), (0.28, 0.42, 0.22, 12)):
        crystal(canvas, x + dx * 36 * scale, y, w * 36 * scale, h * 70 * scale, a, top, bottom, glow if h == 1.0 else None)


def mushroom(canvas, x, y, size, cap, dots):
    canvas.mist(x + 3, y + 2, size * 1.3, size * 0.35, 0x6E8E68, 0.28)
    canvas.round(x, y - size * 0.32, size * 0.32, size * 0.62, size * 0.14, 0xFFF6E8, 0xE1D2BE)
    canvas.oval(x, y - size * 0.66, size, size * 0.62, cap, mix_hex(cap, 0x7A3E4A, 0.35), feather=1.4)
    for dx, dy, r in ((-0.22, -0.78, 0.13), (0.16, -0.84, 0.10), (0.30, -0.64, 0.08), (-0.05, -0.60, 0.07)):
        canvas.oval(x + dx * size, y + dy * size, r * size, r * size * 0.9, dots, 0xEADBD0, opacity=0.95)


def mug(canvas, x, y, size, body, shade, mark=True):
    canvas.mist(x + 4, y + size * 0.48, size * 1.4, size * 0.35, shade, 0.35)
    canvas.oval(x + size * 0.50, y, size * 0.42, size * 0.50, body, shade, feather=1.3)
    canvas.oval(x + size * 0.50, y, size * 0.20, size * 0.28, mix_hex(body, 0xFFFFFF, 0.2), shade, opacity=0.0)
    canvas.round(x, y, size * 0.86, size, size * 0.18, body, shade, bevel=0.18)
    canvas.oval(x, y - size * 0.47, size * 0.80, size * 0.16, mix_hex(shade, 0x6E4A3A, 0.45), shade)
    if mark:
        canvas.oval(x - size * 0.02, y + size * 0.05, size * 0.26, size * 0.26, 0xFFF4D8, 0xE8D2A8, opacity=0.95)
        canvas.oval(x + size * 0.06, y - size * 0.01, size * 0.22, size * 0.22, body, body, opacity=1.0)
    for k in range(2):
        canvas.mist(x - size * 0.12 + k * size * 0.22, y - size * (0.95 + 0.25 * k), size * 0.14, size * 0.42, 0xFFFFFF, 0.30)


def fairy_lights(canvas, x0, x1, y, sag, count, wire, bulbs):
    prev = None
    for k in range(count + 1):
        t = k / count
        px = x0 + (x1 - x0) * t
        py = y + sag * 4 * t * (1 - t)
        if prev is not None:
            canvas.segment(prev[0], prev[1], px, py, 1.6, wire, 0.55)
        prev = (px, py)
        if 0 < k < count:
            color = bulbs[k % len(bulbs)]
            canvas.mist(px, py + 6, 26, 26, color, 0.42)
            canvas.oval(px, py + 5, 7, 9, 0xFFFBEA, color, opacity=0.95)


def table_lamp(canvas, x, y, scale, shade_top, shade_bottom):
    canvas.mist(x, y - 30 * scale, 180 * scale, 160 * scale, 0xFFE2AE, 0.45)
    canvas.round(x, y + 20 * scale, 34 * scale, 16 * scale, 6 * scale, 0xE6C3A4, 0xBE9479)
    canvas.round(x, y - 8 * scale, 7 * scale, 44 * scale, 3 * scale, 0xD9B79A, 0xB08D74)
    canvas.round(x, y - 48 * scale, 66 * scale, 44 * scale, 16 * scale, shade_top, shade_bottom, bevel=0.2)
    canvas.oval(x, y - 26 * scale, 54 * scale, 8 * scale, 0xFFF3C8, 0xF4D49A, opacity=0.9)


def potted_plant(canvas, x, y, scale):
    leaves = ((0xCFE5AE, 0x8FB577), (0xBCDDA3, 0x7FA86E))
    for k, (dx, dy, w, h, a) in enumerate(((-18, -52, 58, 20, -60), (16, -58, 62, 21, -118), (-26, -30, 54, 18, -25),
                                           (24, -34, 56, 19, -155), (0, -66, 48, 18, -90))):
        leaf(canvas, x + dx * scale, y + dy * scale, w * scale, h * scale, a, *leaves[k % 2])
    canvas.round(x, y - 4 * scale, 52 * scale, 40 * scale, 12 * scale, 0xE9B9A0, 0xC68F7A, bevel=0.2)
    canvas.round(x, y - 22 * scale, 58 * scale, 10 * scale, 4 * scale, 0xF1C8AF, 0xCF9C86)


def dome(canvas, x, y, scale):
    canvas.mist(x, y, 190 * scale, 90 * scale, 0xBFB2DD, 0.32)
    canvas.round(x, y + 10 * scale, 130 * scale, 46 * scale, 10 * scale, 0xEDE6F7, 0xC4B7DE, bevel=0.2)
    canvas.oval(x, y - 16 * scale, 118 * scale, 100 * scale, 0xF4EEFB, 0xBFB0DA, feather=1.5)
    canvas.round(x + 6 * scale, y - 30 * scale, 18 * scale, 60 * scale, 8 * scale, 0x6F6496, 0x51497A, angle=-12)
    canvas.mist(x + 6 * scale, y - 34 * scale, 14 * scale, 40 * scale, 0xBFD8FF, 0.5, -12)
    canvas.round(x - 30 * scale, y - 44 * scale, 30 * scale, 10 * scale, 5 * scale, 0xFFFFFF, 0xE6DEF4, angle=-30, opacity=0.6)


def enrich(key, canvas):
    if key == "cozy-nest":
        fairy_lights(canvas, -10, 586, 40, 24, 14, 0xC9A28C, (0xFFD58F, 0xFFC0B0, 0xFFE6A6))
        table_lamp(canvas, 512, 912, 1.0, 0xF6CDB4, 0xDDA48C)
        mug(canvas, 172, 966, 34, 0xF3B7C4, 0xCF8FA0)
    elif key == "frosted-pond":
        for x, y, h in ((28, 950, 150), (92, 968, 104), (548, 946, 160), (486, 972, 96)):
            pine(canvas, x, y, h, 0xA9C9C4, 0x6F9A9A, 0xF7FBFF)
        cluster(canvas, 170, 1000, 0.75, 0xE6F5FF, 0x8DBAE0, 0xD8F0FF)
        cluster(canvas, 412, 1004, 0.55, 0xEAF6FF, 0x9AC2E4, 0xD8F0FF)
    elif key == "breezy-meadow":
        canvas.oval(288, 958, 700, 90, 0xDCECC0, 0xB7D29C, opacity=0.7)
        for k, x in enumerate(range(26, 560, 39)):
            y = 990 + (k * 17) % 26
            petal = (0xF6B8C8, 0xFFF0A8, 0xFFFFFF, 0xC9B6EE)[k % 4]
            flower(canvas, x, y, 13 + (k * 5) % 6, petal, 0xF6C96B if petal != 0xFFF0A8 else 0xF2A85C)
        for x in (44, 118):
            canvas.round(x, 930, 8, 52, 3, 0xE9D7BC, 0xC2A98A)
        canvas.segment(30, 916, 132, 916, 5, 0xE5D0B2, 0.95)
        canvas.segment(30, 936, 132, 936, 5, 0xE5D0B2, 0.95)
    elif key == "ember-hollow":
        canvas.mist(0, 300, 120, 700, 0x5C4651, 0.35)
        canvas.mist(576, 300, 120, 700, 0x5C4651, 0.35)
        cluster(canvas, 96, 968, 0.80, 0xFFD58A, 0xE48A3E, 0xFFB46A)
        cluster(canvas, 486, 972, 0.95, 0xFFDE9C, 0xE07B3A, 0xFFB46A)
        cluster(canvas, 548, 214, 0.45, 0xFFD58A, 0xE48A3E, 0xFFB46A)
        canvas.segment(170, 1040, 230, 985, 9, 0xFFB466, 0.55)
        canvas.segment(360, 1040, 330, 990, 7, 0xFFB466, 0.50)
    elif key == "prism-observatory":
        dome(canvas, 470, 975, 0.9)
        cluster(canvas, 92, 962, 0.85, 0xE9DDFF, 0x9C86D8, 0xF2C8F0)
        cluster(canvas, 40, 214, 0.45, 0xE3F4FF, 0x8FB8E2, 0xC6F0E4)
        for x, y, size in ((250, 935, 7), (330, 960, 9), (560, 860, 8), (24, 860, 7)):
            star(canvas, x, y, size, 0xFFF4D8, 0.85)
    elif key == "dewdrop-garden":
        mushroom(canvas, 430, 1000, 46, 0xF19AA6, 0xFFF3EC)
        mushroom(canvas, 470, 1012, 30, 0xF6B2A0, 0xFFF3EC)
        for k, x in enumerate((210, 250, 290, 336, 380)):
            flower(canvas, x, 1004 + (k % 2) * 10, 12, (0xD9C6F2, 0xF6B8C8, 0xFFFFFF)[k % 3], 0xF6C96B, 0.9)
    elif key == "rainy-window":
        potted_plant(canvas, 196, 1000, 1.0)
        mug(canvas, 404, 986, 38, 0xF4E6D6, 0xCDB9A6)
        for x, y, r in ((60, 70, 5), (110, 40, 4), (520, 60, 5), (560, 100, 4), (470, 30, 3)):
            canvas.oval(x, y, r, r * 1.3, 0xFFFFFF, 0xB9CCE3, opacity=0.75)
    elif key == "twilight-attic":
        fairy_lights(canvas, -10, 586, 36, 26, 12, 0xB79CB6, (0xFFD58F, 0xFFC6A6, 0xFFE9A6, 0xF6C2E0))
        canvas.mist(288, 1000, 220, 70, 0xFFE2A8, 0.30)
        star(canvas, 288, 985, 26, 0xFFE7A8, 0.85)
        canvas.oval(288, 985, 22, 22, 0xFFF4C8, 0xF2CC80, opacity=0.95)
    elif key == "moonlit-clouds":
        cloud(canvas, 12, 520, 120, 44, 0xF7F4FC, 0xCDD0E9)
        cloud(canvas, 566, 420, 130, 46, 0xF9F5FB, 0xD0D2EA)
        cloud(canvas, 560, 760, 110, 40, 0xF9F5FB, 0xD0D2EA)
        for x, y, size in ((30, 640, 8), (548, 580, 6), (24, 350, 6), (552, 300, 9), (300, 930, 7)):
            star(canvas, x, y, size, 0xFFF5D9, 0.82)


WORLDS = (
    ("cozy-nest", 0xFBEBDC, 0xF5DBCA, 0xFFF6E7, (0.26, 0.10), cozy),
    ("dewdrop-garden", 0xE1EFE2, 0xD6E3CA, 0xF5F8DF, (0.31, 0.13), garden),
    ("rainy-window", 0xDCE7F4, 0xE3DCE7, 0xF0F1EF, (0.39, 0.16), rainy),
    ("twilight-attic", 0xEBDFED, 0xD6BFD1, 0xFAE8DB, (0.66, 0.11), attic),
    ("moonlit-clouds", 0xDCE3F7, 0xD0D8F1, 0xF2EEF9, (0.72, 0.12), moonlit),
    ("frosted-pond", 0xDCECF7, 0xC9DFEE, 0xF6FBFF, (0.30, 0.12), frosted),
    ("breezy-meadow", 0xEDF5DC, 0xDDEBC5, 0xFCFBE9, (0.70, 0.10), meadow),
    ("ember-hollow", 0xB08A86, 0x8E6B6E, 0xFBD1A6, (0.50, 0.12), ember),
    ("prism-observatory", 0xE7E1F5, 0xD9D2EC, 0xFFF4FA, (0.50, 0.10), prism),
)


def main(root, only=None):
    resources = root / "Assets" / "Art" / "Resources"
    for key, top, floor, light, light_at, compose in WORLDS:
        if only and key not in only:
            continue
        backdrop = Canvas(576, 1024, top, floor, light, light_at)
        compose(backdrop, False)
        enrich(key, backdrop)
        backdrop.save(resources / "WorldBackdrops" / f"{key}.png")

        preview = Canvas(320, 160, top, floor, light, light_at)
        compose(preview, True)
        preview.save(root / "Assets" / "Art" / "WorldPreviews" / f"{key}.png")


if __name__ == "__main__":
    # Usage: render_worlds.py [project root] [world ids...]  (no ids: all nine)
    main(Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2], sys.argv[2:] or None)
