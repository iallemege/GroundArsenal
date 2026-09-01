#!/usr/bin/env python3
"""One unique paint PNG per modeled gun (256x256)."""
from __future__ import print_function

import math
import os
import struct
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
WEAP = os.path.normpath(os.path.join(HERE, "..", "assets", "Weapons"))
N = 49
W = H = 256

# Distinct schemes: hue, sat, val, pattern id
SCHEMES = [
    (0.08, 0.15, 0.22, 0),   # black
    (0.10, 0.55, 0.42, 1),   # rust
    (0.12, 0.62, 0.55, 2),   # desert
    (0.28, 0.55, 0.32, 3),   # woodland
    (0.55, 0.25, 0.38, 4),   # navy
    (0.58, 0.08, 0.62, 5),   # arctic
    (0.33, 0.70, 0.28, 6),   # olive
    (0.02, 0.70, 0.38, 7),   # crimson
    (0.72, 0.18, 0.48, 8),   # urban purple
    (0.16, 0.80, 0.55, 9),   # tan
    (0.50, 0.45, 0.40, 10),  # teal
    (0.00, 0.00, 0.18, 11),  # graphite
    (0.08, 0.35, 0.70, 12),  # sand
    (0.38, 0.60, 0.22, 13),  # jungle
    (0.62, 0.55, 0.55, 14),  # ice blue
    (0.95, 0.40, 0.45, 15),  # rose steel
]


def hsv(h, s, v):
    i = int(h * 6.0)
    f = h * 6.0 - i
    p = v * (1.0 - s)
    q = v * (1.0 - f * s)
    t = v * (1.0 - (1.0 - f) * s)
    i %= 6
    if i == 0:
        r, g, b = v, t, p
    elif i == 1:
        r, g, b = q, v, p
    elif i == 2:
        r, g, b = p, v, t
    elif i == 3:
        r, g, b = p, q, v
    elif i == 4:
        r, g, b = t, p, v
    else:
        r, g, b = v, p, q
    return int(r * 255), int(g * 255), int(b * 255)


def png_write(path, w, h, rgba):
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    raw = b""
    stride = w * 4
    for y in range(h):
        raw += b"\x00" + rgba[y * stride:(y + 1) * stride]
    data = b"".join([
        b"\x89PNG\r\n\x1a\n",
        chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)),
        chunk(b"IDAT", zlib.compress(raw, 6)),
        chunk(b"IEND", b""),
    ])
    with open(path, "wb") as f:
        f.write(data)


def pat(px, py, kind, seed):
    if kind == 0:
        return (int(px / 18) + int(py / 18) + seed) % 2
    if kind == 1:
        return 1 if math.sin((px + seed * 7) * 0.12) * math.sin(py * 0.09) > 0 else 0
    if kind == 2:
        return 1 if ((px + py * 2 + seed * 3) % 28) < 10 else 0
    if kind == 3:
        return 1 if ((int(px / 12) ^ int(py / 12) ^ seed) & 1) else 0
    if kind == 4:
        return 1 if abs((px - 128) * 0.4 + math.sin(py * 0.08 + seed) * 40) < 18 else 0
    if kind == 5:
        return 1 if ((px * 3 + py + seed * 11) % 22) < 7 else 0
    if kind == 6:
        return 1 if math.sin(px * 0.2 + seed) + math.cos(py * 0.15) > 0.2 else 0
    if kind == 7:
        return 1 if ((int(px / 8) + int(py / 32) + seed) % 3) == 0 else 0
    if kind == 8:
        return 1 if ((px ^ (py + seed * 5)) & 16) else 0
    if kind == 9:
        return 1 if (int((px + seed * 9) / 20) + int(py / 8)) % 2 else 0
    if kind == 10:
        d = math.hypot(px - 128, py - 128)
        return 1 if int(d / 16 + seed) % 2 else 0
    if kind == 11:
        return 1 if (px + py * 3 + seed * 13) % 40 < 8 else 0
    if kind == 12:
        return 1 if math.sin((px - py) * 0.18 + seed) > 0 else 0
    if kind == 13:
        return 1 if ((int(px / 14) * 3 + int(py / 14) + seed) % 4) < 2 else 0
    if kind == 14:
        return 1 if ((px / 10 + seed) % 6) < 2 else 0
    return 1 if ((int(px / 16) + int(py / 6) + seed) % 2) else 0


def main():
    for i in range(N):
        h, s, v, kind = SCHEMES[i % len(SCHEMES)]
        h = (h + i * 0.071) % 1.0
        s = min(0.85, s + (i % 5) * 0.04)
        v = max(0.16, min(0.78, v + ((i * 3) % 7 - 3) * 0.03))
        r0, g0, b0 = hsv(h, s, v)
        r1, g1, b1 = hsv((h + 0.12) % 1.0, min(1.0, s + 0.1), max(0.12, v - 0.18))
        r2, g2, b2 = hsv((h + 0.55) % 1.0, 0.25, min(0.9, v + 0.22))
        buf = bytearray(W * H * 4)
        for y in range(H):
            for x in range(W):
                p = pat(x, y, kind, i)
                q = pat(x + 40, y + 17, (kind + 3) % 16, i + 9)
                if p and q:
                    r, g, b = r2, g2, b2
                elif p:
                    r, g, b = r1, g1, b1
                else:
                    r, g, b = r0, g0, b0
                # wear
                n = ((x * 13 + y * 7 + i * 31) % 23)
                if n < 2:
                    r = min(255, r + 40)
                    g = min(255, g + 35)
                    b = min(255, b + 30)
                o = (y * W + x) * 4
                buf[o] = r
                buf[o + 1] = g
                buf[o + 2] = b
                buf[o + 3] = 255
        path = os.path.join(WEAP, "livery_%02d.png" % i)
        png_write(path, W, H, buf)
        print("livery", i)
    print("OK", N)


if __name__ == "__main__":
    main()
