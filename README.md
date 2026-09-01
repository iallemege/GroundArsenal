# Ground Arsenal

Standalone **Nuclear Option** BepInEx plugin: local first-person infantry with **300 named guns** (revolver through rocket / MANPADS / sci-fi). **No compile-time dependency on Oritasy.**

If Oritasy is also installed, this plugin pauses Oritasy infantry via reflection so the two FPS modes do not double-run.

## Play

- Hangar aircraft-select overlay: **Deploy Infantry** (9 hotbar guns).
- In the field: **G** opens the 300-gun arsenal (filter by family). Click a gun to put it on the current hotbar slot.
- **1–9** / mouse wheel: hotbar. **LMB** fire, **RMB** ads, **R** reload, **U** hop out / board.
- Aircraft hop-out carries slot 1 only.

## Build

Windows, game at `d:\Steam\steamapps\common\Nuclear Option`:

```
build.bat
```

Copies `GroundArsenal.dll` and `assets/Weapons/*` into `BepInEx/plugins/`.

Fully quit Steam after installing.

## Assets

16 family meshes (`fam_00` … `fam_15`) plus albedo PNGs. CC0 / original low-poly analogues. Optional OpenGameArt **byzmod3d** M16 / AK / RPG if present under `Blender/import`. Quaternius Ultimate Guns is CC0 if you drop extra OBJs into the weapons folder.

Generate textures:

```
python tools/gen_albedos.py
python tools/gen_catalog.py
```

Meshes (Blender 5.1):

```
F:\blender\blender.exe -b --python Blender\scripts\make_arsenal_weapons.py
```

(from the ExportedProject repo that contains the Blender scripts).

## License

Code: MIT. Weapon meshes/textures: CC0 or original analogues (not ripped commercial CAD).
