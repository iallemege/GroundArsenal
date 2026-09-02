# Ground Arsenal

Standalone **Nuclear Option** BepInEx plugin: local first-person infantry. No compile-time dependency on Oritasy.

Requires **BIA** (`BIA.Runtime_*.dll`, GUID `bia.runtime`). If Oritasy is also installed, this plugin pauses Oritasy infantry via reflection so the two FPS modes do not double-run.

Version **1.2.13**.

## Play

- Overlay only while hangar **aircraft-select** is open (not cockpit, map, or faction join). Panel sits on the **left**. **1–9** pick hotbar slot, then **Deploy Infantry**. Guns unlock with your mission **rank**.
- Buy a jet while on foot: aircraft HUD stays off until you walk up and press **U** to board.
- Field: **G** arsenal (filter by family). Click a gun to put it on the current hotbar slot.
- **1–9** / mouse wheel: hotbar. **LMB** fire, **RMB** ads (lock on launchers), **R** reload, **U** hop out / board.
- Hangar deploy uses the full hotbar. Hop-out and parachute landing use slot 1 only.
- Approach wrecks to pick up matching ammunition.

## Build

Windows, game at `d:\Steam\steamapps\common\Nuclear Option`:

```
build.bat
```

Copies `GroundArsenal.dll` and `assets/Weapons/*` into `BepInEx/plugins/`.

Fully quit Steam after installing.

## Assets

Viewmodels are downloaded CC0 meshes (loafbrr / Quaternius / byzmod3d) plus AT-4, RBS 70, and G11.

```
python tools/fetch_cc0_hd.py
F:\blender\blender.exe -b --python ..\..\Blender\scripts\export_loafbrr_parts.py
python tools/gen_kits.py
build.bat
```

Fully quit Steam after installing.

## License

Code: MIT. Weapon meshes/textures: CC0 or original analogues (not ripped commercial CAD).
