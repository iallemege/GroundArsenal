#!/usr/bin/env python3
"""Download CC0 gun packs (Quaternius Ultimate Guns on OpenGameArt)."""
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
DST = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Blender", "import", "cc0_guns"))
PAGE = "https://opengameart.org/content/low-poly-guns-pack"
CTX = ssl.create_default_context()
UA = "Mozilla/5.0 GroundArsenal/1.0"


def get(url):
    req = Request(url, headers={"User-Agent": UA})
    return urlopen(req, context=CTX, timeout=120)


def main():
    if not os.path.isdir(DST):
        os.makedirs(DST)
    print("fetch page", PAGE)
    html = get(PAGE).read().decode("utf-8", "replace")
    hrefs = re.findall(r'href="([^"]+\.zip)"', html, re.I)
    print("zip links", hrefs)
    if not hrefs:
        print("no zip on page")
        return 1
    h = hrefs[0]
    url = h if h.startswith("http") else ("https://opengameart.org" + h)
    zpath = os.path.join(DST, "ultimate_gun_pack_by_quaternius.zip")
    print("download", url)
    data = get(url).read()
    with open(zpath, "wb") as f:
        f.write(data)
    print("saved", zpath, len(data))
    with zipfile.ZipFile(zpath, "r") as z:
        z.extractall(DST)
        print("extracted", len(z.namelist()), "files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
