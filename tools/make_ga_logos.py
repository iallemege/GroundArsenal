# -*- coding: utf-8 -*-
import math
import os
from PIL import Image, ImageDraw, ImageFont

IAL = r"C:\Users\iallemege\.cursor\projects\g-8-034-ExportedProject\assets\c__Users_iallemege_AppData_Roaming_Cursor_User_workspaceStorage_0d265820da4ee7424eb338018c284e11_images_ial_bureau-c723ae7f-1f4b-431f-b53e-3a0de5c4ed39.png"
OUT = r"C:\Users\iallemege\.cursor\projects\g-8-034-ExportedProject\assets"
FONT = ImageFont.truetype(r"C:\Windows\Fonts\ariblk.ttf", 280)
W, H = 2400, 1350
WHITE = (255, 255, 255)
BLACK = (0, 0, 0)


def make_mark(height):
    src = Image.open(IAL).convert("L")
    px = src.load()
    w, h = src.size
    xs, ys = [], []
    for y in range(h):
        for x in range(w):
            if px[x, y] > 150:
                xs.append(x)
                ys.append(y)
    src = src.crop((min(xs), min(ys), max(xs) + 1, max(ys) + 1))
    nw = int(round(src.size[0] * (height / float(src.size[1]))))
    src = src.resize((nw, height), Image.Resampling.LANCZOS)
    rgba = Image.new("RGBA", src.size, (0, 0, 0, 0))
    sp, dp = src.load(), rgba.load()
    for y in range(src.size[1]):
        for x in range(src.size[0]):
            if sp[x, y] > 100:
                dp[x, y] = (0, 0, 0, 255)
    return rgba


def glyphs(im, y0, y1):
    px = im.load()
    w = im.size[0]
    runs = []
    on = False
    s = 0
    for x in range(w):
        hit = False
        for y in range(y0, y1):
            if px[x, y][0] > 210:
                hit = True
                break
        if hit:
            if not on:
                on = True
                s = x
        else:
            if on:
                on = False
                runs.append([s, x - 1])
    if on:
        runs.append([s, w - 1])
    merged = []
    for a, b in runs:
        if not merged or a - merged[-1][1] > 16:
            merged.append([a, b])
        else:
            merged[-1][1] = b
    return merged


def knock_crossbars(im, y0, y1):
    px = im.load()
    for a, b in glyphs(im, y0, y1):
        if b - a < 70:
            continue
        apex_x = None
        apex_y = None
        for x in range(a, b + 1):
            for y in range(y0, y1):
                if px[x, y][0] > 210:
                    if apex_y is None or y < apex_y:
                        apex_y = y
                        apex_x = x
                    break
        if apex_x is None:
            continue
        if abs(apex_x - (a + b) / 2.0) > (b - a) * 0.28:
            continue
        for y in range(y0, y1):
            segs = []
            on = False
            s = 0
            for x in range(a, b + 1):
                if px[x, y][0] > 210:
                    if not on:
                        on = True
                        s = x
                else:
                    if on:
                        on = False
                        segs.append((s, x - 1))
            if on:
                segs.append((s, b))
            if len(segs) == 1 and (segs[0][1] - segs[0][0]) > (b - a) * 0.5:
                span = segs[0][1] - segs[0][0]
                keep = max(22, int(span * 0.2))
                for x in range(segs[0][0] + keep, segs[0][1] - keep + 1):
                    px[x, y] = BLACK


def find_as(im, y0, y1):
    px = im.load()
    found = []
    for a, b in glyphs(im, y0, y1):
        if b - a < 90 or b - a > 420:
            continue
        apex = None
        for x in range(a, b + 1):
            for y in range(y0, y1):
                if px[x, y][0] > 220:
                    if apex is None or y < apex[0]:
                        apex = (y, x)
                    break
        if apex is None:
            continue
        if abs(apex[1] - (a + b) / 2.0) > (b - a) * 0.22:
            continue
        mid = y0 + int((y1 - y0) * 0.55)
        segs = []
        on = False
        s = 0
        for x in range(a, b + 1):
            if px[x, mid][0] > 220:
                if not on:
                    on = True
                    s = x
            else:
                if on:
                    on = False
                    segs.append((s, x - 1))
        if on:
            segs.append((s, b))
        if len(segs) < 2:
            continue
        peak_w = 0
        yy = min(y1 - 1, apex[0] + 8)
        run = 0
        for x in range(a, b + 1):
            if px[x, yy][0] > 220:
                run += 1
            else:
                if run > peak_w:
                    peak_w = run
                run = 0
        if run > peak_w:
            peak_w = run
        if peak_w > (b - a) * 0.38:
            continue
        bot_l = None
        bot_r = None
        for y in range(y1 - 1, y0, -1):
            if bot_l is None and px[min(im.size[0] - 1, a + 12), y][0] > 220:
                bot_l = y
            if bot_r is None and px[max(0, b - 12), y][0] > 220:
                bot_r = y
            if bot_l and bot_r:
                break
        if bot_l is None or bot_r is None:
            continue
        found.append((a, b, apex[1], apex[0], bot_l, bot_r))
    return found


def stamp(base, mark, x1, y1, x2, y2):
    ang = math.degrees(math.atan2(y2 - y1, x2 - x1))
    rot = mark.rotate(-ang, expand=True, resample=Image.Resampling.BICUBIC)
    bw, bh = base.size
    rw, rh = rot.size
    dx, dy = x2 - x1, y2 - y1
    length = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / length, dx / length
    mx, my = (x1 + x2) / 2.0, (y1 + y2) / 2.0
    bp = base.load()
    rp = rot.load()
    best = None
    for k in range(-12, 13):
        cx = mx + nx * k
        cy = my + ny * k
        x0 = int(cx - rw / 2)
        y0 = int(cy - rh / 2)
        hit = 0
        ink = 0
        for y in range(rh):
            yy = y0 + y
            if yy < 0 or yy >= bh:
                continue
            for x in range(rw):
                if rp[x, y][3] < 100:
                    continue
                ink += 1
                xx = x0 + x
                if xx < 0 or xx >= bw:
                    continue
                if bp[xx, yy][0] > 220:
                    hit += 1
        if ink and (best is None or hit > best[0]):
            best = (hit, ink, x0, y0)
    if best is None:
        return 0
    hit, ink, x0, y0 = best
    drawn = 0
    for y in range(rh):
        yy = y0 + y
        if yy < 0 or yy >= bh:
            continue
        for x in range(rw):
            if rp[x, y][3] < 100:
                continue
            xx = x0 + x
            if xx < 0 or xx >= bw:
                continue
            if bp[xx, yy][0] > 220:
                bp[xx, yy] = BLACK
                drawn += 1
    return drawn


def engrave(im, mark, y0, y1):
    total = 0
    for a, b, ax, ay, bl, br in find_as(im, y0, y1):
        legs = [((a + 18, bl - 10), (ax, ay + 14)), ((ax, ay + 14), (b - 18, br - 10))]
        for (x1, y1), (x2, y2) in legs:
            total += stamp(im, mark, x1, y1, x2, y2)
    return total


def figure(kind, ax, ay):
    layer = Image.new("RGB", (W, H), BLACK)
    d = ImageDraw.Draw(layer)
    if kind == "infantry":
        d.ellipse((ax - 22, ay - 92, ax + 22, ay - 50), fill=WHITE)
        d.polygon(
            [(ax - 40, ay - 8), (ax - 30, ay - 46), (ax + 30, ay - 46), (ax + 48, ay - 8), (ax + 36, ay + 2), (ax - 32, ay + 2)],
            fill=WHITE,
        )
        d.rectangle((ax + 6, ay - 42, ax + 96, ay - 32), fill=WHITE)
        d.rectangle((ax + 18, ay - 54, ax + 28, ay - 32), fill=WHITE)
    elif kind == "airborne":
        d.pieslice((ax - 86, ay - 176, ax + 86, ay - 28), 200, 340, fill=WHITE)
        d.line((ax - 58, ay - 78, ax - 10, ay - 6), fill=WHITE, width=4)
        d.line((ax + 58, ay - 78, ax + 10, ay - 6), fill=WHITE, width=4)
        d.ellipse((ax - 14, ay - 30, ax + 14, ay - 4), fill=WHITE)
        d.polygon(
            [(ax - 26, ay + 2), (ax - 18, ay - 16), (ax + 18, ay - 16), (ax + 26, ay + 2), (ax + 18, ay + 8), (ax - 18, ay + 8)],
            fill=WHITE,
        )
    else:
        d.ellipse((ax - 18, ay - 84, ax + 18, ay - 50), fill=WHITE)
        d.arc((ax - 30, ay - 96, ax + 30, ay - 54), 200, 340, fill=WHITE, width=5)
        d.polygon(
            [(ax - 42, ay - 6), (ax - 32, ay - 42), (ax + 32, ay - 42), (ax + 42, ay - 6), (ax + 30, ay + 4), (ax - 30, ay + 4)],
            fill=WHITE,
        )
        d.line((ax - 62, ay - 18, ax - 32, ay - 40), fill=WHITE, width=5)
        d.line((ax + 62, ay - 18, ax + 32, ay - 40), fill=WHITE, width=5)
        d.ellipse((ax - 74, ay - 30, ax - 52, ay - 8), fill=WHITE)
        d.ellipse((ax + 52, ay - 30, ax + 74, ay - 8), fill=WHITE)
    return layer


def build(kind, filename):
    im = Image.new("RGB", (W, H), BLACK)
    d = ImageDraw.Draw(im)
    top = "GROUND"
    bot = "ARSENAL"
    tb = d.textbbox((0, 0), top, font=FONT)
    bb = d.textbbox((0, 0), bot, font=FONT)
    tw, th = tb[2] - tb[0], tb[3] - tb[1]
    bw, bh = bb[2] - bb[0], bb[3] - bb[1]
    y_top = 180
    y_bot = 620
    d.text(((W - tw) // 2, y_top), top, font=FONT, fill=WHITE)
    d.text(((W - bw) // 2, y_bot), bot, font=FONT, fill=WHITE)
    knock_crossbars(im, y_top - 10, y_top + th + 30)
    knock_crossbars(im, y_bot - 10, y_bot + bh + 40)
    ases = find_as(im, y_bot - 10, y_bot + bh + 50)
    if not ases:
        raise SystemExit("no A on " + kind)
    last = ases[-1]
    ax, ay = last[2], last[3]
    fig = figure(kind, ax, ay - 6)
    fp = fig.load()
    bp = im.load()
    for y in range(H):
        for x in range(W):
            if fp[x, y][0] > 200:
                bp[x, y] = WHITE
    d = ImageDraw.Draw(im)
    px = im.load()
    bottom = 0
    for y in range(H - 1, 0, -1):
        if any(px[x, y][0] > 200 for x in range(0, W, 3)):
            bottom = y
            break
    d.line((180, bottom + 28, W - 180, bottom + 28), fill=WHITE, width=4)
    mark = make_mark(34)
    ink = engrave(im, mark, y_top - 10, y_top + th + 30)
    ink += engrave(im, mark, y_bot - 10, y_bot + bh + 50)
    path = os.path.join(OUT, filename)
    im.save(path)
    print(kind, "As", [(a[0], a[1]) for a in ases], "ink", ink, path)


if __name__ == "__main__":
    build("infantry", "GroundArsenal-logo-infantry.png")
    build("airborne", "GroundArsenal-logo-airborne.png")
    build("groundcrew", "GroundArsenal-logo-groundcrew.png")
