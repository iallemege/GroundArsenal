#!/usr/bin/env python3
"""Write 512x512 albedo PNGs (no deps) for 16 weapon families."""
from __future__ import print_function

import os
import struct
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "assets", "Weapons"))
ORITASY = r"D:\Steam\steamapps\common\Nuclear Option\BepInEx\plugins\OritasyInfantry"
ASSETS9 = os.path.normpath(os.path.join(HERE, "..", "..", "CI22XE", "assets", "Infantry"))

FAMILIES = [
    ("fam_00", (70, 68, 64), (28, 26, 24), (140, 120, 70)),
    ("fam_01", (36, 36, 38), (18, 18, 20), (90, 90, 92)),
    ("fam_02", (42, 40, 38), (20, 18, 16), (110, 80, 40)),
    ("fam_03", (32, 34, 36), (14, 16, 18), (70, 90, 80)),
    ("fam_04", (92, 78, 52), (40, 32, 20), (160, 140, 100)),
    ("fam_05", (48, 46, 42), (22, 20, 18), (120, 100, 60)),
    ("fam_06", (40, 44, 38), (18, 22, 16), (80, 100, 70)),
    ("fam_07", (38, 40, 36), (16, 18, 14), (90, 95, 80)),
    ("fam_08", (46, 52, 38), (20, 24, 16), (110, 90, 50)),
    ("fam_09", (34, 36, 40), (14, 16, 20), (80, 70, 50)),
    ("fam_10", (30, 32, 34), (12, 12, 14), (70, 70, 60)),
    ("fam_11", (52, 48, 40), (24, 22, 16), (100, 90, 50)),
    ("fam_12", (70, 62, 40), (30, 26, 16), (130, 110, 60)),
    ("fam_13", (58, 72, 42), (24, 32, 18), (90, 80, 40)),
    ("fam_14", (50, 56, 48), (20, 24, 20), (80, 90, 70)),
    ("fam_15", (28, 48, 72), (10, 20, 40), (40, 220, 255)),
]


def chunk(tag, data):
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
    payload = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
    payload += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    payload += chunk(b"IEND", b"")
    folder = os.path.dirname(path)
    if folder and not os.path.isdir(folder):
        os.makedirs(folder)
    with open(path, "wb") as f:
        f.write(payload)


def lerp(a, b, t):
    return int(a + (b - a) * t)


def make_fn(base, dark, accent):
    def rgb(x, y):
        n = ((x * 13 + y * 31 + x * y) & 255) / 255.0
        band = 1.0 if ((x // 28) + (y // 56)) % 2 == 0 else 0.0
        rail = 1.0 if (y % 64) < 6 else 0.0
        t = 0.55 + 0.25 * n + 0.12 * band
        r = lerp(dark[0], base[0], t)
        g = lerp(dark[1], base[1], t)
        b = lerp(dark[2], base[2], t)
        if rail > 0.5:
            r = lerp(r, accent[0], 0.45)
            g = lerp(g, accent[1], 0.45)
            b = lerp(b, accent[2], 0.45)
        scratch = 1.0 if ((x * 17 + y * 9) & 63) == 0 else 0.0
        if scratch:
            r = min(255, r + 40)
            g = min(255, g + 40)
            b = min(255, b + 40)
        return r, g, b

    return rgb


def copy_to(src, dest_dir, name):
    if not dest_dir:
        return
    if not os.path.isdir(dest_dir):
        try:
            os.makedirs(dest_dir)
        except Exception:
            return
    dest = os.path.join(dest_dir, name)
    with open(src, "rb") as f:
        data = f.read()
    with open(dest, "wb") as f:
        f.write(data)
    print("copy", dest)


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    for name, base, dark, accent in FAMILIES:
        path = os.path.join(OUT, name + ".png")
        write_png(path, 512, 512, make_fn(base, dark, accent))
        print("wrote", path)

    map9 = {
        "rifle.png": "fam_07.png",
        "ak47.png": "fam_08.png",
        "pistol.png": "fam_01.png",
        "shotgun.png": "fam_04.png",
        "sniper.png": "fam_10.png",
        "at4.png": "fam_13.png",
        "rpg7.png": "fam_13.png",
        "rbs70.png": "fam_14.png",
        "g11.png": "fam_15.png",
    }
    for dest_name, src_name in map9.items():
        src = os.path.join(OUT, src_name)
        copy_to(src, ORITASY, dest_name)
        copy_to(src, ASSETS9, dest_name)
    return 0


if __name__ == "__main__":
    sys.exit(main())
