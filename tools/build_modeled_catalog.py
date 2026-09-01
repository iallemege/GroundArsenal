#!/usr/bin/env python3
"""One catalog row per unique gun body mesh. Icons from the mesh (or Oritasy hangar art)."""
from __future__ import print_function

import os
import shutil
import struct
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
WEAP = os.path.normpath(os.path.join(HERE, "..", "assets", "Weapons"))
SRC = os.path.normpath(os.path.join(HERE, "..", "src"))
INF = os.path.normpath(os.path.join(HERE, "..", "..", "CI22XE", "assets", "Infantry"))
ORI = r"D:\Steam\steamapps\common\Nuclear Option\BepInEx\plugins\OritasyInfantry"

SKIP_PREFIX = ("acc_", "fam_")
SKIP_EXACT = {
    "pistol_mag.obj", "pistol_slide.obj",
    "ak_mag.obj", "ak_bolt.obj",
    "rev_cyl.obj", "arms.obj",
}
EXTRA = ("at4.obj", "rbs70.obj", "g11.obj")

# Oritasy hangar icons for the same modeled guns.
ORI_ICON = {
    "at4.obj": "weaponIcon_at4.png",
    "rbs70.obj": "weaponIcon_rbs70.png",
    "g11.obj": "weaponIcon_g11.png",
    "q_M16.obj": "weaponIcon_m16.png",
    "q_AK47.obj": "weaponIcon_ak47.png",
    "q_RPG7.obj": "weaponIcon_rpg7.png",
    "pistol_body.obj": "weaponIcon_pistol.png",
    "ak_body.obj": "weaponIcon_ak47.png",
}

W, H = 512, 256


def cs_str(s):
    return s.replace("\\", "\\\\").replace("\"", "\\\"")


def stem(name):
    return os.path.splitext(name)[0]


def pretty(name):
    s = stem(name)
    if s.startswith("q_"):
        s = s[2:]
    s = s.replace("_", " ")
    fix = {
        "rev body": "Revolver",
        "pistol body": "Pistol",
        "ak body": "AK",
        "at4": "AT-4",
        "rbs70": "RBS 70",
        "g11": "G11",
        "M16": "M16",
        "AK47": "AK-47",
        "RPG7": "RPG-7",
        "SubmachineGun": "SMG",
        "AssaultRifle": "Assault Rifle",
        "AssaultRifle2": "Battle Rifle",
        "SniperRifle": "Sniper",
        "Shotgun SawedOff": "Shotgun Sawed-Off",
        "Shotgun ShortStock": "Shotgun Short",
    }
    for a, b in fix.items():
        if s == a or s.startswith(a + " "):
            s = b + s[len(a):]
    return s.strip()


def classify(body):
    s = stem(body).lower()
    # fam, fire, mag, reserve, rpm, spread, dmg, pen, range, rig, magp, slide, kind
    if "revolver" in s or s == "rev_body":
        return (0, 0, 6, 48, 90, 0.012, 32, 3, 70, 3, "rev_cyl.obj", "-", "KE")
    if "pistol" in s:
        return (1, 0, 15, 75, 280, 0.014, 12, 2, 50, 1, "pistol_mag.obj", "pistol_slide.obj", "KE")
    if "submachine" in s:
        return (3, 1, 30, 150, 800, 0.016, 14, 3, 45, 2, "ak_mag.obj", "ak_bolt.obj", "KE")
    if "shotgun" in s:
        return (4, 3, 8, 40, 70, 0.055, 10, 3, 28, 4, "-", "-", "KE")
    if "bullpup" in s:
        return (6, 1, 30, 150, 750, 0.010, 20, 4, 80, 2, "ak_mag.obj", "ak_bolt.obj", "KE")
    if s == "q_m16" or "assaultrifle" in s and "2" not in s:
        pen = 4
        if "ak47" in s or s == "ak_body":
            pen = 5
        return (7, 1, 30, 150, 700, 0.009, 20, pen, 85, 2, "ak_mag.obj", "ak_bolt.obj", "KE")
    if "ak47" in s or s == "ak_body":
        return (7, 1, 30, 150, 600, 0.011, 22, 5, 80, 2, "ak_mag.obj", "ak_bolt.obj", "KE")
    if "assaultrifle2" in s:
        return (8, 0, 20, 100, 500, 0.008, 28, 6, 110, 2, "ak_mag.obj", "ak_bolt.obj", "KE")
    if "sniper" in s:
        return (10, 0, 5, 25, 40, 0.002, 36, 6, 200, 2, "ak_mag.obj", "ak_bolt.obj", "KE")
    if "rpg" in s:
        return (13, 4, 1, 4, 20, 0.010, 70, 16, 90, 5, "-", "-", "HEAT")
    if s == "at4":
        return (13, 4, 1, 3, 16, 0.008, 80, 18, 80, 5, "-", "-", "HEAT")
    if s == "rbs70":
        return (14, 4, 1, 4, 14, 0.006, 55, 15, 140, 5, "-", "-", "HE")
    if s == "g11":
        return (15, 2, 50, 200, 600, 0.008, 22, 6, 80, 2, "ak_mag.obj", "ak_bolt.obj", "KE")
    return (7, 1, 30, 120, 650, 0.012, 18, 4, 70, 2, "ak_mag.obj", "ak_bolt.obj", "KE")


def copy_extras():
    for name in EXTRA:
        src = os.path.join(INF, name)
        if not os.path.isfile(src):
            src = os.path.join(ORI, name)
        if os.path.isfile(src):
            shutil.copyfile(src, os.path.join(WEAP, name))
            print("copied", name)


def discover():
    names = []
    for fn in sorted(os.listdir(WEAP)):
        if not fn.lower().endswith(".obj"):
            continue
        low = fn.lower()
        if any(low.startswith(p) for p in SKIP_PREFIX):
            continue
        if fn in SKIP_EXACT:
            continue
        names.append(fn)
    return names


def load_obj(path):
    verts = []
    faces = []
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        for line in f:
            if line.startswith("v "):
                p = line.split()
                if len(p) >= 4:
                    verts.append((float(p[1]), float(p[2]), float(p[3])))
            elif line.startswith("f "):
                idx = []
                for tok in line.split()[1:]:
                    a = tok.split("/")[0]
                    if a:
                        i = int(a)
                        idx.append(i - 1 if i > 0 else len(verts) + i)
                if len(idx) >= 3:
                    faces.append(idx)
    return verts, faces


def project(verts):
    if not verts:
        return []
    xs = [v[0] for v in verts]
    ys = [v[1] for v in verts]
    zs = [v[2] for v in verts]
    cx = 0.5 * (min(xs) + max(xs))
    cy = 0.5 * (min(ys) + max(ys))
    cz = 0.5 * (min(zs) + max(zs))
    sx = max(xs) - min(xs)
    sz = max(zs) - min(zs)
    # Y up, longer of X/Z as barrel
    pts = []
    for x, y, z in verts:
        if sx >= sz:
            pts.append((x - cx, y - cy))
        else:
            pts.append((z - cz, y - cy))
    return pts


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


def draw_line(buf, w, h, x0, y0, x1, y1):
    dx = abs(x1 - x0)
    dy = abs(y1 - y0)
    sx = 1 if x0 < x1 else -1
    sy = 1 if y0 < y1 else -1
    err = dx - dy
    x, y = x0, y0
    while True:
        if 0 <= x < w and 0 <= y < h:
            o = (y * w + x) * 4
            buf[o] = 255
            buf[o + 1] = 255
            buf[o + 2] = 255
            buf[o + 3] = 255
        if x == x1 and y == y1:
            break
        e2 = 2 * err
        if e2 > -dy:
            err -= dy
            x += sx
        if e2 < dx:
            err += dx
            y += sy


def render_icon(obj_path, png_path):
    verts, faces = load_obj(obj_path)
    pts = project(verts)
    buf = bytearray(W * H * 4)
    if not pts:
        png_write(png_path, W, H, buf)
        return
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    minx, maxx = min(xs), max(xs)
    miny, maxy = min(ys), max(ys)
    sx = max(maxx - minx, 1e-6)
    sy = max(maxy - miny, 1e-6)
    pad_x, pad_y = 0.10, 0.22
    inner_w = W * (1.0 - 2.0 * pad_x)
    inner_h = H * (1.0 - 2.0 * pad_y)
    sc = min(inner_w / sx, inner_h / sy)
    cx = 0.5 * (minx + maxx)
    cy = 0.5 * (miny + maxy)

    def to_px(p):
        x = int((p[0] - cx) * sc + W * 0.5)
        y = int(H - ((p[1] - cy) * sc + H * 0.5))
        return x, y

    seen = set()
    for face in faces:
        n = len(face)
        for i in range(n):
            a, b = face[i], face[(i + 1) % n]
            if a > b:
                a, b = b, a
            key = (a, b)
            if key in seen:
                continue
            seen.add(key)
            if a < 0 or b < 0 or a >= len(pts) or b >= len(pts):
                continue
            x0, y0 = to_px(pts[a])
            x1, y1 = to_px(pts[b])
            draw_line(buf, W, H, x0, y0, x1, y1)
    png_write(png_path, W, H, buf)


def try_copy_ori_icon(body, dest):
    src_name = ORI_ICON.get(body)
    if not src_name:
        return False
    for folder in (ORI, INF):
        src = os.path.join(folder, src_name)
        if os.path.isfile(src):
            shutil.copyfile(src, dest)
            return True
    return False


def write_kits(guns):
    path = os.path.join(WEAP, "kits.txt")
    lines = ["# id body mag slide acc rig tex icon", "# one row per unique body mesh"]
    for g in guns:
        lines.append("%d %s %s %s - %d - %s" % (
            g["id"], g["body"], g["magp"], g["slide"], g["rig"], g["icon"]))
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("wrote", path, len(guns))


def write_catalog(guns):
    path = os.path.join(SRC, "GaCatalog.cs")
    fam_en = [
        "Revolver", "Pistol", "Machine pistol", "SMG",
        "Shotgun", "Auto shotgun", "Carbine", "Assault rifle",
        "Battle rifle", "DMR", "Sniper", "LMG",
        "Grenade launcher", "Rocket", "ATGM / MANPADS", "Energy",
    ]
    out = []
    a = out.append
    a("using UnityEngine;")
    a("")
    a("namespace GroundArsenal")
    a("{")
    a("    internal sealed class GaGun")
    a("    {")
    a("        internal int Id;")
    a("        internal string En;")
    a("        internal string Zh;")
    a("        internal int Family;")
    a("        internal int Fire;")
    a("        internal int Mag;")
    a("        internal int Reserve;")
    a("        internal float Rpm;")
    a("        internal float Spread;")
    a("        internal float Damage;")
    a("        internal float Pen;")
    a("        internal float Range;")
    a("        internal Color Tint;")
    a("        internal Color Tracer;")
    a("        internal string Icon;")
    a("        internal string Kind;")
    a("        internal string Body;")
    a("    }")
    a("")
    a("    internal static class GaCatalog")
    a("    {")
    a("        internal const int Count = %d;" % len(guns))
    a("        internal const int FamilyCount = 16;")
    a("        internal const int HotbarSize = 9;")
    a("        internal static readonly GaGun[] All = Build();")
    a("")
    a("        internal static readonly string[] FamilyEn = new string[]")
    a("        {")
    a("            \"" + "\", \"".join(fam_en[:8]) + "\",")
    a("            \"" + "\", \"".join(fam_en[8:]) + "\"")
    a("        };")
    a("")
    a("        internal static GaGun Get(int id)")
    a("        {")
    a("            if (id < 0 || id >= Count)")
    a("                return All[0];")
    a("            return All[id];")
    a("        }")
    a("")
    a("        internal static string Name(GaGun g, bool zh)")
    a("        {")
    a("            if (g == null)")
    a("                return \"?\";")
    a("            return zh ? g.Zh : g.En;")
    a("        }")
    a("")
    a("        internal static int FindBody(string body)")
    a("        {")
    a("            if (string.IsNullOrEmpty(body))")
    a("                return 0;")
    a("            for (int i = 0; i < Count; i++)")
    a("            {")
    a("                if (All[i].Body == body)")
    a("                    return i;")
    a("            }")
    a("            return 0;")
    a("        }")
    a("")
    a("        internal static bool FamilyHas(int fam)")
    a("        {")
    a("            for (int i = 0; i < Count; i++)")
    a("            {")
    a("                if (All[i].Family == fam)")
    a("                    return true;")
    a("            }")
    a("            return false;")
    a("        }")
    a("")
    a("        internal static string FireName(int fire, bool zh)")
    a("        {")
    a("            if (fire == 1) return zh ? \"\\u5168\\u81ea\\u52a8\" : \"Auto\";")
    a("            if (fire == 2) return zh ? \"\\u70b9\\u5c04\" : \"Burst\";")
    a("            if (fire == 3) return zh ? \"\\u9730\\u5f39\" : \"Shotgun\";")
    a("            if (fire == 4) return zh ? \"\\u706b\\u7bad\" : \"Rocket\";")
    a("            if (fire == 5) return zh ? \"\\u80fd\\u91cf\" : \"Energy\";")
    a("            return zh ? \"\\u5355\\u53d1\" : \"Semi\";")
    a("        }")
    a("")
    a("        private static GaGun[] Build()")
    a("        {")
    a("            GaGun[] a = new GaGun[Count];")
    for g in guns:
        a("            a[%d] = G(%d, \"%s\", \"%s\", %d, %d, %d, %d, %sf, %sf, %sf, %sf, %sf, \"%s\", \"%s\", \"%s\");" % (
            g["id"], g["id"], cs_str(g["en"]), cs_str(g["en"]),
            g["fam"], g["fire"], g["mag"], g["reserve"],
            fmt(g["rpm"]), fmt(g["spread"]), fmt(g["dmg"]), fmt(g["pen"]), fmt(g["rng"]),
            cs_str(g["icon"]), cs_str(g["kind"]), cs_str(g["body"])))
    a("            return a;")
    a("        }")
    a("")
    a("        private static GaGun G(int id, string en, string zh, int fam, int fire,")
    a("            int mag, int reserve, float rpm, float spread, float dmg, float pen,")
    a("            float range, string icon, string kind, string body)")
    a("        {")
    a("            GaGun x = new GaGun();")
    a("            x.Id = id;")
    a("            x.En = en;")
    a("            x.Zh = zh;")
    a("            x.Family = fam;")
    a("            x.Fire = fire;")
    a("            x.Mag = mag;")
    a("            x.Reserve = reserve;")
    a("            x.Rpm = rpm;")
    a("            x.Spread = spread;")
    a("            x.Damage = dmg;")
    a("            x.Pen = pen;")
    a("            x.Range = range;")
    a("            x.Tint = Color.white;")
    a("            x.Tracer = new Color(1f, 0.82f, 0.35f);")
    a("            x.Icon = icon;")
    a("            x.Kind = kind;")
    a("            x.Body = body;")
    a("            return x;")
    a("        }")
    a("    }")
    a("}")
    a("")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out))
    print("wrote", path)


def fmt(v):
    if isinstance(v, float):
        s = ("%.4f" % v).rstrip("0").rstrip(".")
        if "." not in s:
            s += ".0"
        return s
    return str(v)


def main():
    copy_extras()
    bodies = discover()
    guns = []
    for i, body in enumerate(bodies):
        fam, fire, mag, reserve, rpm, spread, dmg, pen, rng, rig, magp, slide, kind = classify(body)
        icon = "weaponIcon_" + stem(body) + ".png"
        guns.append({
            "id": i,
            "body": body,
            "en": pretty(body),
            "fam": fam,
            "fire": fire,
            "mag": mag,
            "reserve": reserve,
            "rpm": rpm,
            "spread": spread,
            "dmg": dmg,
            "pen": pen,
            "rng": rng,
            "rig": rig,
            "magp": magp,
            "slide": slide,
            "kind": kind,
            "icon": icon,
        })
        dest = os.path.join(WEAP, icon)
        if try_copy_ori_icon(body, dest):
            print("icon copy", body)
        else:
            render_icon(os.path.join(WEAP, body), dest)
            print("icon draw", body)
    write_kits(guns)
    write_catalog(guns)
    print("MODELED", len(guns))


if __name__ == "__main__":
    main()
