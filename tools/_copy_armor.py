#!/usr/bin/env python3
import os
import re

root = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "..", "CI22XE", "src"))
out = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "src"))

math_src = os.path.join(root, "ModularArmorMathService.cs")
cat_src = os.path.join(root, "ArmorCatalogService.cs")
math_dst = os.path.join(out, "GaArmorMath.cs")
cat_dst = os.path.join(out, "GaArmorCatalog.cs")

t = open(math_src, "r", encoding="utf-8").read()
t = t.replace("namespace Oritasy", "namespace GroundArsenal")
t = t.replace("internal static class ModularArmorMathService", "internal static class GaArmorMath")
open(math_dst, "w", encoding="utf-8", newline="\n").write(t)
print("wrote", math_dst)

t = open(cat_src, "r", encoding="utf-8").read()
t = t.replace("namespace Oritasy", "namespace GroundArsenal")
t = t.replace("ModularArmorMathService", "GaArmorMath")
t = t.replace("InfantryHud.CatalogOpen", "GaHud.CatalogOpen")
t = re.sub(
    r'UiLang\.T\(\s*"((?:\\.|[^"])*)"\s*,\s*"(?:\\.|[^"])*"\s*\)',
    r'"\1"',
    t,
)
open(cat_dst, "w", encoding="utf-8", newline="\n").write(t)
print("wrote", cat_dst, "uilang left", "UiLang" in t)
