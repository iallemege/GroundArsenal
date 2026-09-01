#!/usr/bin/env python3
"""Download extra CC0 guns that have separate parts or higher detail.

Does not touch paid Asset Store / CGTrader. OpenGameArt + Kenney only.
"""
from __future__ import print_function

import os
import re
import ssl
import sys
import zipfile

try:
    from urllib.request import Request, urlopen
except ImportError:
    from urllib2 import Request, urlopen

HERE = os.path.dirname(os.path.abspath(__file__))
DST = os.path.normpath(
    os.path.join(HERE, "..", "..", "..", "Blender", "import", "cc0_hd")
)
CTX = ssl.create_default_context()
UA = "Mozilla/5.0 GroundArsenal/1.0 (CC0 fetch)"

PAGES = (
    "https://opengameart.org/content/low-poly-animated-guns",
    "https://opengameart.org/content/weapon-pack",
    "https://opengameart.org/content/high-poly-glock-18-no-textures",
    "https://opengameart.org/content/high-poly-ak-47",
    "https://opengameart.org/content/lowpoly-pistol",
    "https://opengameart.org/content/semi-auto-shotgun-with-magazine",
    "https://opengameart.org/content/m4a1-assault-rifle",
    "https://opengameart.org/content/lightning-pump-action-rifle",
    "https://opengameart.org/content/akm",
    "https://opengameart.org/content/sci-fi-gun-pack",
)

KENNEY = (
    "https://opengameart.org/sites/default/files/weaponPack_assets.zip",
)


def get(url):
    req = Request(url, headers={"User-Agent": UA})
    return urlopen(req, context=CTX, timeout=180)


def save_url(url, path):
    print("GET", url)
    data = get(url).read()
    folder = os.path.dirname(path)
    if folder and not os.path.isdir(folder):
        os.makedirs(folder)
    with open(path, "wb") as f:
        f.write(data)
    print("  saved", path, len(data))
    return path


def unzip(path, dest):
    if not os.path.isdir(dest):
        os.makedirs(dest)
    with zipfile.ZipFile(path, "r") as z:
        z.extractall(dest)
    print("  unzip", os.path.basename(path), "->", dest)


def file_links(html):
    hrefs = re.findall(
        r'href="([^"]+\.(?:zip|obj|fbx|blend|glb|gltf|dae))"', html, re.I
    )
    out = []
    seen = set()
    for h in hrefs:
        if h.startswith("/"):
            h = "https://opengameart.org" + h
        if "opengameart.org" not in h:
            continue
        name = os.path.basename(h.split("?")[0]).lower()
        if "godot" in name or "preview" in name or "unitypackage" in name:
            continue
        if h in seen:
            continue
        seen.add(h)
        out.append(h)
    return out


def main():
    if not os.path.isdir(DST):
        os.makedirs(DST)
    for page in PAGES:
        try:
            html = get(page).read().decode("utf-8", "replace")
        except Exception as ex:
            print("page fail", page, ex)
            continue
        links = file_links(html)
        print(page, "files", len(links))
        slug = page.rstrip("/").split("/")[-1]
        dest = os.path.join(DST, slug)
        for url in links[:4]:
            name = os.path.basename(url.split("?")[0])
            path = os.path.join(dest, name)
            try:
                save_url(url, path)
                if path.lower().endswith(".zip"):
                    unzip(path, dest)
            except Exception as ex:
                print("fail", url, ex)
    print("done", DST)
    return 0


if __name__ == "__main__":
    sys.exit(main())
