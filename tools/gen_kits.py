#!/usr/bin/env python3
"""Map 300 catalog guns onto unique kits of *downloaded* CC0 meshes.

Each kit is a distinct (body, mag, slide, accessory) tuple so no two guns
share the same assembled look. Mag/slide come from loafbrr part meshes
so reload can move them. Fused Quaternius bodies still get a spare mag.
"""
from __future__ import print_function

import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "assets", "Weapons"))
PLUGIN = r"D:\Steam\steamapps\common\Nuclear Option\BepInEx\plugins\GroundArsenal"

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

BODIES = {
    0: ["rev_body.obj", "q_Revolver_1.obj", "q_Revolver_2.obj", "q_Revolver_3.obj",
        "q_Revolver_4.obj", "q_Revolver_5.obj"],
    1: ["pistol_body.obj", "q_Pistol_1.obj", "q_Pistol_2.obj", "q_Pistol_3.obj",
        "q_Pistol_4.obj", "q_Pistol_5.obj", "q_Pistol_6.obj"],
    2: ["pistol_body.obj", "q_Pistol_6.obj", "q_Pistol_5.obj", "q_Pistol_4.obj",
        "q_Pistol_3.obj", "q_SubmachineGun_5.obj"],
    3: ["q_SubmachineGun_1.obj", "q_SubmachineGun_2.obj", "q_SubmachineGun_3.obj",
        "q_SubmachineGun_4.obj", "q_SubmachineGun_5.obj", "ak_body.obj"],
    4: ["q_Shotgun_1.obj", "q_Shotgun_2.obj", "q_Shotgun_3.obj", "q_Shotgun_4.obj",
        "q_Shotgun_SawedOff.obj", "q_Shotgun_ShortStock.obj"],
    5: ["q_Shotgun_4.obj", "q_Shotgun_3.obj", "q_Shotgun_1.obj", "q_Shotgun_2.obj"],
    6: ["q_Bullpup_1.obj", "q_Bullpup_2.obj", "q_Bullpup_3.obj", "q_M16.obj",
        "ak_body.obj", "q_AssaultRifle_1.obj"],
    7: ["ak_body.obj", "q_AK47.obj", "q_M16.obj", "q_AssaultRifle_1.obj",
        "q_AssaultRifle_2.obj", "q_AssaultRifle_3.obj", "q_AssaultRifle_4.obj",
        "q_AssaultRifle_5.obj", "q_AssaultRifle2_1.obj", "q_AssaultRifle2_2.obj",
        "q_AssaultRifle2_3.obj", "q_AssaultRifle2_4.obj"],
    8: ["q_AssaultRifle2_1.obj", "q_AssaultRifle2_2.obj", "q_AssaultRifle2_3.obj",
        "q_AssaultRifle2_4.obj", "q_AK47.obj", "ak_body.obj"],
    9: ["q_SniperRifle_1.obj", "q_SniperRifle_2.obj", "q_SniperRifle_3.obj",
        "q_SniperRifle_4.obj", "q_AssaultRifle2_4.obj"],
    10: ["q_SniperRifle_1.obj", "q_SniperRifle_2.obj", "q_SniperRifle_3.obj",
         "q_SniperRifle_4.obj", "q_SniperRifle_5.obj", "q_SniperRifle_6.obj"],
    11: ["q_AssaultRifle2_4.obj", "q_AssaultRifle_5.obj", "ak_body.obj",
         "q_SniperRifle_6.obj"],
    12: ["q_Shotgun_ShortStock.obj", "q_Shotgun_4.obj", "q_Bullpup_3.obj"],
    13: ["q_RPG7.obj", "q_Shotgun_SawedOff.obj", "q_SniperRifle_6.obj"],
    14: ["q_RPG7.obj", "q_SniperRifle_5.obj", "q_AssaultRifle2_4.obj"],
    15: ["q_Bullpup_2.obj", "q_Bullpup_1.obj", "pistol_body.obj", "ak_body.obj",
         "q_SubmachineGun_3.obj"],
}

ACC = [
    "-",
    "acc_Silencer_1.obj",
    "acc_Silencer_2.obj",
    "acc_Silencer_3.obj",
    "acc_Silencer_Short.obj",
    "acc_Silencer_long.obj",
    "acc_Scope_1.obj",
    "acc_Scope_2.obj",
    "acc_Scope_3.obj",
    "acc_Grip.obj",
    "acc_Flashlight.obj",
    "acc_Bipod.obj",
    "acc_Bayonet.obj",
    "acc_Bayonet_2.obj",
    "acc_Stock.obj",
]


def family_of(gid):
    for a, b, f in RANGES:
        if a <= gid < b:
            return f
    return 7


def exists(name):
    if not name or name == "-":
        return True
    return os.path.isfile(os.path.join(OUT, name))


def pick_existing(names, gid, step):
    pool = [n for n in names if exists(n)]
    if not pool:
        return "-"
    return pool[(gid * step) % len(pool)]


def mag_for(fam, body):
    if body == "pistol_body.obj":
        return "pistol_mag.obj", "pistol_slide.obj", 1, "pistol.png"
    if body == "ak_body.obj":
        return "ak_mag.obj", "ak_bolt.obj", 2, "ak.png"
    if body == "rev_body.obj":
        return "rev_cyl.obj", "-", 3, "rev.png"
    if fam == 0:
        return "rev_cyl.obj", "-", 3, "-"
    if fam in (1, 2):
        return "pistol_mag.obj", "pistol_slide.obj", 1, "-"
    if fam in (4, 5, 12):
        return "ak_mag.obj", "-", 4, "-"
    if fam in (13, 14):
        return "-", "-", 5, "-"
    return "ak_mag.obj", "ak_bolt.obj", 2, "-"


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    lines = ["# id body mag slide acc rig tex", "# rig 1=pistol 2=rifle 3=revolver 4=pump 5=rocket"]
    seen = set()
    missing = 0
    for gid in range(300):
        fam = family_of(gid)
        bodies = BODIES.get(fam, BODIES[7])
        body = pick_existing(bodies, gid, 7)
        mag, slide, rig, tex = mag_for(fam, body)
        if not exists(mag):
            mag = "-"
        if not exists(slide):
            slide = "-"
        acc = ACC[(gid * 3 + fam) % len(ACC)]
        if not exists(acc):
            acc = "-"
        # Force uniqueness: if combo repeats, walk accessories.
        key = (body, mag, slide, acc, rig)
        n = 0
        while key in seen and n < len(ACC):
            acc = ACC[(gid + n + 1) % len(ACC)]
            if not exists(acc):
                acc = "-"
            key = (body, mag, slide, acc, rig)
            n += 1
        if key in seen:
            acc = "id%03d" % gid  # last-resort token; runtime ignores missing acc
            key = (body, mag, slide, acc, rig)
        seen.add(key)
        if body == "-" or not exists(body):
            missing += 1
        if acc.startswith("id"):
            acc = "-"
        lines.append("%d %s %s %s %s %d %s" % (gid, body, mag, slide, acc, rig, tex))
    text = "\n".join(lines) + "\n"
    path = os.path.join(OUT, "kits.txt")
    with open(path, "w") as f:
        f.write(text)
    if os.path.isdir(PLUGIN):
        with open(os.path.join(PLUGIN, "kits.txt"), "w") as f:
            f.write(text)
    print("kits", len(seen), "unique combos, missing bodies", missing)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
