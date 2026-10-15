# Ground Arsenal

First-person infantry for **Nuclear Option** (BepInEx). **1.0 is the first stable release**; **1.0.5** is the current patch (fixes the startup crash on installs with only Ground Arsenal + BIA, and the Infantry chip closing the infantry menu again after a death). Earlier GitHub tags (1.1.x / 1.2.x) were test builds.

Requires **[BIA Runtime](https://github.com/iallemege/BIA)** (`bia.runtime`). The zip already includes `BIA.Runtime_1.0.25.dll`.

## Install

Unzip into `BepInEx/plugins/` so you have:

- `plugins/GroundArsenal.dll`
- `plugins/GroundArsenal/` (meshes, liveries, icons, `kits.txt`)
- `plugins/BIA.Runtime_1.0.25.dll` (skip if you already have a current BIA Runtime)

Do not nest a `BepInEx/` folder inside the zip extract.

## Play

Hangar **aircraft-select**: left overlay, **Infantry** chip. Pick kit / AIR lift / spawn field, then **DEPLOY**.

- **T** board / leave vehicles and cargo aircraft
- **WASD** drive (driver seat). **F1–F4** seats
- AIR: **LIFT** tab picks the insert aircraft at **half airframe cost**, or paradrop for free
- **H** buy a ground vehicle, **B** buy aircraft (AIR / SEA)
- **LMB** fire, **RMB** ads, **R** reload

If Oritasy is also installed, Ground Arsenal pauses Oritasy infantry so the two FPS modes do not double-run.

## Build

Windows, game at `d:\Steam\steamapps\common\Nuclear Option`:

```
build.bat
```

Copies `GroundArsenal.dll` and `assets/Weapons/*` into `BepInEx/plugins/`.

## License

Code: MIT. Weapon meshes/textures: CC0 or original analogues (not ripped commercial CAD).
