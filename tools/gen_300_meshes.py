#!/usr/bin/env python3
"""Build 300 unique high-poly textured gun OBJs (one mesh per catalog id)."""
from __future__ import print_function

import math
import os
import struct
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "assets", "Weapons"))
PLUGIN = r"D:\Steam\steamapps\common\Nuclear Option\BepInEx\plugins\GroundArsenal"

# Catalog family ranges (must match gen_catalog.py counts).
RANGES = [
    (0, 18, 0),
    (18, 42, 1),
    (42, 56, 2),
    (56, 80, 3),
    (80, 96, 4),
    (96, 108, 5),
    (108, 132, 6),
    (132, 164, 7),
    (164, 180, 8),
    (180, 196, 9),
    (196, 212, 10),
    (212, 228, 11),
    (228, 240, 12),
    (240, 256, 13),
    (256, 268, 14),
    (268, 300, 15),
]


def family_of(gid):
    for a, b, f in RANGES:
        if a <= gid < b:
            return f
    return 7


class Rng(object):
    def __init__(self, seed):
        self.s = seed & 0xFFFFFFFF

    def u(self):
        self.s = (1664525 * self.s + 1013904223) & 0xFFFFFFFF
        return self.s

    def f(self, a, b):
        return a + (self.u() / 4294967295.0) * (b - a)

    def i(self, a, b):
        if b < a:
            a, b = b, a
        return int(math.floor(self.f(a, b + 0.999)))

    def chance(self, p):
        return self.f(0.0, 1.0) < p

    def pick(self, items):
        return items[self.i(0, len(items) - 1)]


class Mesh(object):
    def __init__(self):
        self.v = []
        self.vt = []
        self.f = []

    def _tri(self, ia, ib, ic):
        self.f.append((ia, ib, ic))

    def add_box(self, cx, cy, cz, sx, sy, sz, u0=0.0, v0=0.0):
        hx, hy, hz = sx * 0.5, sy * 0.5, sz * 0.5
        p = [
            (cx - hx, cy - hy, cz - hz),
            (cx + hx, cy - hy, cz - hz),
            (cx + hx, cy + hy, cz - hz),
            (cx - hx, cy + hy, cz - hz),
            (cx - hx, cy - hy, cz + hz),
            (cx + hx, cy - hy, cz + hz),
            (cx + hx, cy + hy, cz + hz),
            (cx - hx, cy + hy, cz + hz),
        ]
        faces = (
            (0, 1, 2, 3),
            (5, 4, 7, 6),
            (4, 0, 3, 7),
            (1, 5, 6, 2),
            (3, 2, 6, 7),
            (4, 5, 1, 0),
        )
        base = len(self.v)
        for i, q in enumerate(faces):
            for k, vi in enumerate(q):
                x, y, z = p[vi]
                self.v.append((x, y, z))
                uu = u0 + (0.05 if k in (1, 2) else 0.0) + (i * 0.07) % 0.6
                vv = v0 + (0.05 if k in (2, 3) else 0.0) + (i * 0.11) % 0.6
                self.vt.append((uu, vv))
            a = base + i * 4
            self._tri(a, a + 1, a + 2)
            self._tri(a, a + 2, a + 3)

    def add_cyl(self, cx, cy, cz, axis, radius, length, segs, rings=4):
        segs = max(8, segs)
        rings = max(1, rings)
        ax = axis
        for ri in range(rings + 1):
            t = ri / float(rings)
            off = (t - 0.5) * length
            if ax == "z":
                ox, oy, oz = cx, cy, cz + off
            elif ax == "y":
                ox, oy, oz = cx, cy + off, cz
            else:
                ox, oy, oz = cx + off, cy, cz
            for si in range(segs):
                ang = (si / float(segs)) * math.pi * 2.0
                c, s = math.cos(ang), math.sin(ang)
                if ax == "z":
                    x, y, z = ox + c * radius, oy + s * radius, oz
                elif ax == "y":
                    x, y, z = ox + c * radius, oy, oz + s * radius
                else:
                    x, y, z = ox, oy + c * radius, oz + s * radius
                self.v.append((x, y, z))
                self.vt.append((si / float(segs), t))
        start = len(self.v) - (rings + 1) * segs
        for ri in range(rings):
            for si in range(segs):
                a = start + ri * segs + si
                b = start + ri * segs + ((si + 1) % segs)
                c = start + (ri + 1) * segs + si
                d = start + (ri + 1) * segs + ((si + 1) % segs)
                self._tri(a, b, d)
                self._tri(a, d, c)
        # caps
        for cap, sign in ((0, -1.0), (rings, 1.0)):
            ring0 = start + cap * segs
            cx2, cy2, cz2 = self.v[ring0]
            # average center
            sx = sy = sz = 0.0
            for si in range(segs):
                x, y, z = self.v[ring0 + si]
                sx += x
                sy += y
                sz += z
            cc = (sx / segs, sy / segs, sz / segs)
            ci = len(self.v)
            self.v.append(cc)
            self.vt.append((0.5, 0.5))
            for si in range(segs):
                a = ring0 + si
                b = ring0 + ((si + 1) % segs)
                if sign > 0:
                    self._tri(ci, a, b)
                else:
                    self._tri(ci, b, a)

    def write_obj(self, path):
        folder = os.path.dirname(path)
        if folder and not os.path.isdir(folder):
            os.makedirs(folder)
        lines = ["# GroundArsenal unique gun", "o Gun"]
        for x, y, z in self.v:
            lines.append("v %.5f %.5f %.5f" % (x, y, z))
        for u, v in self.vt:
            lines.append("vt %.4f %.4f" % (u, v))
        for a, b, c in self.f:
            lines.append("f %d/%d %d/%d %d/%d" % (
                a + 1, a + 1, b + 1, b + 1, c + 1, c + 1))
        with open(path, "w") as f:
            f.write("\n".join(lines))
            f.write("\n")
        return len(self.v), len(self.f)


def png_chunk(tag, data):
    crc = zlib.crc32(tag + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)


def write_png(path, w, h, rgb_fn):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            r, g, b = rgb_fn(x, y)
            raw.extend((r & 255, g & 255, b & 255))
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)
    payload = b"\x89PNG\r\n\x1a\n" + png_chunk(b"IHDR", ihdr)
    payload += png_chunk(b"IDAT", zlib.compress(bytes(raw), 6))
    payload += png_chunk(b"IEND", b"")
    folder = os.path.dirname(path)
    if folder and not os.path.isdir(folder):
        os.makedirs(folder)
    with open(path, "wb") as f:
        f.write(payload)


def lerp(a, b, t):
    return int(a + (b - a) * t)


def make_png_fn(gid, fam, rng):
    palettes = [
        ((72, 68, 62), (28, 26, 24), (150, 120, 70)),
        ((38, 38, 40), (16, 16, 18), (90, 90, 95)),
        ((44, 40, 36), (18, 16, 14), (120, 80, 40)),
        ((32, 36, 38), (12, 14, 16), (70, 95, 80)),
        ((96, 78, 48), (40, 30, 18), (170, 140, 90)),
        ((50, 48, 42), (20, 18, 16), (130, 100, 55)),
        ((40, 46, 38), (16, 20, 14), (85, 110, 70)),
        ((36, 40, 34), (14, 16, 12), (95, 100, 80)),
        ((48, 54, 38), (18, 22, 14), (120, 90, 45)),
        ((34, 36, 42), (12, 14, 18), (85, 70, 45)),
        ((30, 32, 34), (10, 10, 12), (75, 75, 60)),
        ((54, 48, 38), (22, 20, 14), (110, 90, 45)),
        ((74, 62, 38), (28, 24, 14), (140, 110, 55)),
        ((58, 74, 40), (22, 30, 16), (95, 80, 35)),
        ((50, 58, 48), (18, 22, 18), (80, 95, 70)),
        ((24, 50, 78), (8, 18, 40), (40, 230, 255)),
    ]
    base, dark, accent = palettes[fam]
    shift = rng.i(-18, 18)
    base = tuple(max(0, min(255, c + shift)) for c in base)
    stripe = rng.i(16, 48)
    rail_h = rng.i(3, 9)

    def rgb(x, y):
        n = ((x * (13 + gid % 7) + y * (31 + gid % 11) + x * y) & 255) / 255.0
        band = 1.0 if ((x // stripe) + (y // (stripe * 2))) % 2 == 0 else 0.0
        rail = 1.0 if (y % 64) < rail_h else 0.0
        t = 0.50 + 0.28 * n + 0.14 * band
        r = lerp(dark[0], base[0], t)
        g = lerp(dark[1], base[1], t)
        b = lerp(dark[2], base[2], t)
        if rail > 0.5:
            r = lerp(r, accent[0], 0.5)
            g = lerp(g, accent[1], 0.5)
            b = lerp(b, accent[2], 0.5)
        if ((x * 17 + y * 9 + gid) & 63) == 0:
            r = min(255, r + 45)
            g = min(255, g + 45)
            b = min(255, b + 45)
        if fam == 15:
            glow = 1.0 if (x + y + gid) % 37 < 3 else 0.0
            if glow:
                r, g, b = accent
        return r, g, b

    return rgb


def greebles(m, rng, x, y, z, w, h, d, n):
    for i in range(n):
        m.add_box(
            x + rng.f(-w, w),
            y + rng.f(-h * 0.4, h * 0.4),
            z + rng.f(-d, d),
            rng.f(0.004, 0.014),
            rng.f(0.003, 0.010),
            rng.f(0.006, 0.022),
            rng.f(0, 0.5),
            rng.f(0, 0.5),
        )


def muzzle_device(m, rng, z_muzzle, radius, kind):
    segs = rng.i(16, 32)
    if kind == 0:
        m.add_cyl(0, 0.012, z_muzzle - 0.012, "z", radius * 1.35, 0.024, segs, 3)
        for i in range(rng.i(3, 8)):
            ang = i / 8.0 * math.pi * 2
            m.add_box(math.cos(ang) * radius * 1.1, 0.012 + math.sin(ang) * radius * 0.2,
                      z_muzzle - 0.01, 0.004, 0.004, 0.016)
    elif kind == 1:
        m.add_cyl(0, 0.012, z_muzzle - 0.06, "z", radius * 1.55, 0.12, segs, 6)
        m.add_cyl(0, 0.012, z_muzzle - 0.12, "z", radius * 1.25, 0.02, segs, 2)
    elif kind == 2:
        m.add_cyl(0, 0.012, z_muzzle - 0.01, "z", radius * 1.2, 0.02, segs, 2)
    elif kind == 3:
        m.add_cyl(0, 0.012, z_muzzle - 0.03, "z", radius * 1.8, 0.05, max(8, segs // 2), 3)


def optic(m, rng, z, kind):
    segs = rng.i(16, 28)
    if kind == 0:
        m.add_box(0, 0.055, z, 0.012, 0.018, 0.028)
        m.add_box(0, 0.068, z + 0.02, 0.004, 0.008, 0.008)
    elif kind == 1:
        m.add_box(0, 0.062, z, 0.022, 0.018, 0.032)
        m.add_box(0, 0.062, z + 0.018, 0.018, 0.014, 0.006)
    elif kind == 2:
        m.add_cyl(0, 0.070, z, "z", rng.f(0.014, 0.020), rng.f(0.10, 0.20), segs, 5)
        m.add_cyl(0, 0.070, z + rng.f(0.04, 0.08), "z", rng.f(0.018, 0.026), 0.03, segs, 2)
        m.add_cyl(0, 0.070, z - rng.f(0.04, 0.08), "z", rng.f(0.016, 0.022), 0.025, segs, 2)
        m.add_box(0, 0.048, z, 0.010, 0.012, 0.04)
    elif kind == 3:
        m.add_box(0, 0.068, z, 0.040, 0.032, 0.055)
        m.add_box(0, 0.068, z + 0.03, 0.036, 0.026, 0.008)


def build_gun(gid):
    fam = family_of(gid)
    rng = Rng(gid * 7919 + 104729 + fam * 13)
    m = Mesh()
    segs = 28 + (gid % 17)  # 28..44 unique tessellation
    rings = 5 + (gid % 8)

    if fam == 0:
        bl = rng.f(0.10, 0.28)
        cr = rng.f(0.018, 0.028)
        m.add_cyl(0, 0.028, -0.08 - bl * 0.5, "z", rng.f(0.007, 0.012), bl, segs, rings)
        m.add_cyl(0, 0.028, -0.05, "x", cr, rng.f(0.038, 0.055), rng.i(10, 16), 3)
        for i in range(rng.i(5, 8)):
            ang = i / 6.0 * math.pi * 2 + gid * 0.1
            m.add_cyl(math.cos(ang) * cr * 0.45, 0.028 + math.sin(ang) * cr * 0.45,
                      -0.05, "x", 0.004, 0.04, 10, 2)
        m.add_box(0, 0.012, -0.02, 0.016, 0.028, 0.07)
        m.add_box(0, -0.040, 0.01, rng.f(0.014, 0.020), rng.f(0.048, 0.070), rng.f(0.020, 0.032))
        m.add_box(0, 0.040, 0.00, 0.006, 0.014, 0.012)
        m.add_box(0, 0.042, -0.08 - bl, 0.004, 0.008, 0.010)
        if rng.chance(0.55):
            muzzle_device(m, rng, -0.08 - bl, 0.009, rng.i(0, 2))
        greebles(m, rng, 0, 0.02, -0.04, 0.01, 0.01, 0.03, rng.i(4, 10))

    elif fam in (1, 2):
        sl = rng.f(0.09, 0.16)
        m.add_box(0, 0.030, -0.06, rng.f(0.014, 0.020), rng.f(0.016, 0.024), sl)
        nser = rng.i(4, 12)
        for i in range(nser):
            m.add_box(0, 0.042, -0.02 - i * (sl / nser), 0.015, 0.003, 0.006)
        m.add_box(0, 0.010, -0.04, 0.014, 0.016, sl * 0.7)
        m.add_box(0, -0.038, 0.00, rng.f(0.013, 0.018), rng.f(0.045, 0.065), rng.f(0.018, 0.028))
        m.add_box(0, -0.022, 0.00, rng.f(0.010, 0.015), rng.f(0.028, 0.050), rng.f(0.014, 0.022))
        m.add_cyl(0, 0.026, -0.06 - sl * 0.5, "z", rng.f(0.005, 0.009), sl * 0.55, segs, rings)
        m.add_box(0, -0.008, -0.05, 0.018, 0.004, 0.028)
        if fam == 2:
            m.add_box(0, -0.070, -0.02, rng.f(0.012, 0.016), rng.f(0.055, 0.090), rng.f(0.016, 0.024))
            m.add_box(0, 0.010, 0.04, 0.008, 0.008, rng.f(0.06, 0.12))
        if rng.chance(0.6):
            muzzle_device(m, rng, -0.06 - sl * 0.55, 0.007, rng.i(0, 3))
        if rng.chance(0.5):
            optic(m, rng, -0.05, rng.i(0, 1))
        greebles(m, rng, 0, 0.02, -0.05, 0.01, 0.008, 0.04, rng.i(5, 12))

    elif fam == 3:
        rec = rng.f(0.12, 0.18)
        bar = rng.f(0.12, 0.22)
        m.add_box(0, 0.014, -0.08, rng.f(0.020, 0.026), rng.f(0.024, 0.032), rec)
        m.add_cyl(0, 0.016, -0.08 - rec * 0.5 - bar * 0.5, "z", rng.f(0.007, 0.010), bar, segs, rings)
        m.add_cyl(0, 0.016, -0.08 - rec * 0.35, "z", rng.f(0.011, 0.015), bar * 0.45, max(12, segs // 2), 4)
        m.add_box(0, -0.050, -0.06, rng.f(0.011, 0.016), rng.f(0.055, 0.090), rng.f(0.014, 0.022))
        m.add_box(0, -0.040, -0.04, 0.015, 0.050, 0.022)
        if rng.chance(0.7):
            m.add_box(0, 0.004, 0.06, rng.f(0.014, 0.020), rng.f(0.014, 0.022), rng.f(0.08, 0.16))
            m.add_box(0, 0.000, 0.14, 0.022, 0.032, 0.016)
        optic(m, rng, -0.10, rng.i(0, 2) if rng.chance(0.7) else 0)
        muzzle_device(m, rng, -0.08 - rec * 0.5 - bar, 0.008, rng.i(0, 3))
        greebles(m, rng, 0, 0.02, -0.08, 0.012, 0.01, 0.05, rng.i(8, 16))

    elif fam in (4, 5):
        bar = rng.f(0.32, 0.58) if fam == 4 else rng.f(0.28, 0.46)
        m.add_cyl(0, 0.028, -0.20 - bar * 0.5, "z", rng.f(0.012, 0.018), bar, segs, rings + 2)
        m.add_cyl(0, 0.006, -0.18 - bar * 0.3, "z", rng.f(0.008, 0.012), bar * 0.55, max(12, segs - 8), 4)
        m.add_box(0, 0.016, -0.10, 0.022, 0.028, rng.f(0.08, 0.12))
        m.add_box(0, 0.004, -0.22, rng.f(0.018, 0.024), rng.f(0.016, 0.022), rng.f(0.07, 0.12))
        m.add_box(0, 0.000, 0.04, rng.f(0.016, 0.022), rng.f(0.022, 0.030), rng.f(0.10, 0.16))
        m.add_box(0, -0.010, 0.14, 0.024, 0.040, 0.018)
        m.add_box(0, -0.040, -0.08, 0.016, 0.050, 0.022)
        if fam == 5:
            m.add_box(0, -0.050, -0.12, rng.f(0.016, 0.022), rng.f(0.055, 0.085), rng.f(0.028, 0.045))
        m.add_box(0, 0.044, -0.20 - bar, 0.004, 0.008, 0.010)
        muzzle_device(m, rng, -0.20 - bar, 0.015, 3 if rng.chance(0.5) else 0)
        greebles(m, rng, 0, 0.02, -0.14, 0.012, 0.01, 0.06, rng.i(6, 14))

    elif fam in (6, 7, 8, 9):
        rec = rng.f(0.14, 0.22)
        bar = rng.f(0.18, 0.28) if fam == 6 else rng.f(0.26, 0.40)
        if fam == 8:
            bar = rng.f(0.30, 0.42)
        if fam == 9:
            bar = rng.f(0.38, 0.55)
        m.add_box(0, 0.014, -0.10, rng.f(0.022, 0.028), rng.f(0.028, 0.038), rec)
        m.add_cyl(0, 0.016, -0.10 - rec * 0.45 - bar * 0.5, "z", rng.f(0.007, 0.011), bar, segs, rings + 2)
        m.add_box(0, 0.008, -0.18, rng.f(0.018, 0.026), rng.f(0.016, 0.024), rng.f(0.08, 0.16))
        m.add_box(0, -0.055, -0.08, rng.f(0.012, 0.018), rng.f(0.060, 0.095), rng.f(0.016, 0.028))
        m.add_box(0, -0.042, -0.06, 0.016, 0.052, 0.022)
        m.add_box(0, 0.004, 0.08, rng.f(0.016, 0.022), rng.f(0.016, 0.026), rng.f(0.10, 0.18))
        m.add_box(0, 0.000, 0.18, rng.f(0.020, 0.026), rng.f(0.030, 0.042), 0.016)
        m.add_box(0, 0.044, -0.12, 0.012, 0.006, rng.f(0.10, 0.18))
        m.add_cyl(0, 0.030, -0.16 - bar * 0.3, "z", 0.004, bar * 0.5, 12, 3)
        optic(m, rng, -0.14, 2 if fam >= 9 else rng.i(0, 2))
        if rng.chance(0.45):
            m.add_box(0.0, -0.020, -0.22, rng.f(0.040, 0.070), 0.008, 0.008)
        muzzle_device(m, rng, -0.10 - rec * 0.45 - bar, 0.009, rng.i(0, 2))
        greebles(m, rng, 0, 0.02, -0.12, 0.014, 0.012, 0.07, rng.i(10, 22))

    elif fam == 10:
        bar = rng.f(0.55, 0.85)
        m.add_cyl(0, 0.018, -0.20 - bar * 0.5, "z", rng.f(0.008, 0.012), bar, segs, rings + 4)
        muzzle_device(m, rng, -0.20 - bar, 0.012, 0)
        m.add_box(0, 0.006, -0.08, 0.020, 0.028, 0.16)
        m.add_box(0, -0.040, -0.06, 0.014, 0.045, 0.040)
        m.add_box(0, -0.004, 0.08, 0.018, 0.024, 0.14)
        m.add_box(0, 0.022, 0.06, 0.014, 0.012, 0.08)
        m.add_box(0.020, 0.028, -0.04, 0.028, 0.008, 0.008)
        optic(m, rng, -0.12, 2)
        m.add_box(0, -0.022, -0.28, rng.f(0.045, 0.070), 0.008, 0.008)
        greebles(m, rng, 0, 0.02, -0.10, 0.012, 0.01, 0.06, rng.i(8, 16))

    elif fam == 11:
        bar = rng.f(0.32, 0.50)
        m.add_box(0, 0.016, -0.12, rng.f(0.026, 0.034), rng.f(0.032, 0.042), rng.f(0.16, 0.24))
        m.add_cyl(0, 0.018, -0.22 - bar * 0.5, "z", rng.f(0.009, 0.013), bar, segs, rings + 3)
        m.add_cyl(0, 0.018, -0.28, "z", rng.f(0.014, 0.020), bar * 0.4, max(12, segs // 2), 4)
        m.add_box(rng.f(0.03, 0.06), 0.000, -0.10, rng.f(0.045, 0.070), rng.f(0.035, 0.050), rng.f(0.06, 0.10))
        m.add_box(0, -0.046, -0.08, 0.016, 0.055, 0.024)
        m.add_box(0, 0.004, 0.08, 0.020, 0.024, 0.14)
        m.add_box(0, 0.050, -0.14, 0.008, 0.028, 0.08)
        m.add_box(0, -0.024, -0.32, rng.f(0.050, 0.080), 0.010, 0.010)
        muzzle_device(m, rng, -0.22 - bar, 0.012, 0)
        greebles(m, rng, 0, 0.02, -0.14, 0.016, 0.012, 0.08, rng.i(12, 24))

    elif fam == 12:
        tube = rng.f(0.22, 0.36)
        m.add_cyl(0, 0.020, -0.16 - tube * 0.5, "z", rng.f(0.024, 0.032), tube, segs, rings)
        m.add_cyl(0, 0.020, -0.08, "z", rng.f(0.028, 0.036), 0.05, segs, 3)
        m.add_box(0, -0.048, -0.06, 0.016, 0.060, 0.024)
        m.add_box(0, 0.000, 0.06, 0.018, 0.022, 0.12)
        m.add_box(0, 0.062, -0.12, 0.012, 0.040, 0.04)
        m.add_box(0, 0.070, -0.22, 0.008, 0.050, 0.004)
        greebles(m, rng, 0, 0.02, -0.16, 0.02, 0.015, 0.08, rng.i(6, 12))

    elif fam in (13, 14):
        tube = rng.f(0.70, 1.15) if fam == 14 else rng.f(0.55, 0.85)
        rad = rng.f(0.038, 0.056)
        m.add_cyl(0, 0.020, -0.20 - tube * 0.5, "z", rad, tube, segs, rings + 3)
        if fam == 13:
            m.add_cyl(0, 0.020, -0.20 - tube, "z", rad * 1.15, rng.f(0.12, 0.20), segs, 4)
            m.add_cyl(0, 0.020, -0.20 - tube - 0.10, "z", rad * 0.45, 0.08, max(12, segs // 2), 3)
            m.add_box(0, -0.040, -0.10, 0.022, 0.018, 0.08)
        else:
            m.add_box(0, 0.000, -0.10, 0.060, 0.070, 0.20)
            m.add_box(0, 0.120, -0.28, 0.050, 0.070, 0.14)
            m.add_cyl(0, 0.120, -0.40, "z", 0.038, 0.045, segs, 3)
            m.add_box(0, -0.110, 0.00, 0.10, 0.016, 0.055)
            m.add_box(0, -0.160, -0.04, 0.012, 0.11, 0.012)
        m.add_box(0, -0.080, -0.16, 0.018, 0.080, 0.030)
        m.add_box(0, 0.078, -0.40, 0.012, 0.045, 0.040)
        greebles(m, rng, 0, 0.03, -0.30, 0.03, 0.02, 0.15, rng.i(8, 18))

    else:
        rec = rng.f(0.18, 0.26)
        bar = rng.f(0.16, 0.30)
        m.add_box(0, 0.012, -0.10, rng.f(0.024, 0.034), rng.f(0.040, 0.052), rec)
        m.add_box(0, 0.008, -0.22, 0.024, 0.032, 0.10)
        m.add_cyl(0, 0.014, -0.28 - bar * 0.5, "z", rng.f(0.008, 0.014), bar, segs, rings)
        ncoil = rng.i(2, 5)
        for i in range(ncoil):
            m.add_cyl(0, 0.014, -0.30 - i * (bar / ncoil), "z", rng.f(0.014, 0.020), 0.028, segs, 3)
        m.add_box(0, 0.062, -0.12, 0.012, 0.028, 0.10)
        optic(m, rng, -0.22, 3)
        m.add_box(0, -0.048, -0.06, 0.014, 0.055, 0.022)
        m.add_box(0, 0.000, 0.08, 0.022, 0.030, 0.08)
        m.add_box(0, 0.040, -0.04, rng.f(0.018, 0.026), rng.f(0.020, 0.030), rng.f(0.030, 0.050))
        greebles(m, rng, 0, 0.02, -0.14, 0.016, 0.015, 0.08, rng.i(10, 20))

    # Unique ID nubs so no two meshes can be identical.
    m.add_box(
        rng.f(-0.012, 0.012),
        rng.f(0.000, 0.020),
        rng.f(-0.04, 0.02),
        0.003 + (gid % 9) * 0.0007,
        0.003 + ((gid // 9) % 9) * 0.0007,
        0.004 + ((gid // 81) % 9) * 0.0008,
    )
    return m, fam, rng


def copy_to(src, dest_dir):
    if not dest_dir:
        return
    if not os.path.isdir(dest_dir):
        try:
            os.makedirs(dest_dir)
        except Exception:
            return
    dest = os.path.join(dest_dir, os.path.basename(src))
    with open(src, "rb") as f:
        data = f.read()
    with open(dest, "wb") as f:
        f.write(data)


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    total_v = 0
    total_f = 0
    for gid in range(300):
        mesh, fam, rng = build_gun(gid)
        stem = "gun_%03d" % gid
        obj = os.path.join(OUT, stem + ".obj")
        png = os.path.join(OUT, stem + ".png")
        nv, nf = mesh.write_obj(obj)
        write_png(png, 384, 384, make_png_fn(gid, fam, rng))
        copy_to(obj, PLUGIN)
        copy_to(png, PLUGIN)
        total_v += nv
        total_f += nf
        if gid % 25 == 0 or gid == 299:
            print("gun", gid, "fam", fam, "verts", nv, "tris", nf)
    print("OK 300 unique guns avg verts", int(total_v / 300.0), "avg tris", int(total_f / 300.0))
    return 0


if __name__ == "__main__":
    sys.exit(main())
