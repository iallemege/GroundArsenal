# Ground Arsenal

Standalone **Nuclear Option** BepInEx infantry plugin. **No compile-time Oritasy, Blueprinter, BIA, or Icarus reference.** Soft-depends Oritasy only so it can pause that pack’s FPS if both are installed.

Version **1.1.0**.

## Honest gun count

| Source | Count |
| --- | --- |
| Quaternius Ultimate Guns (CC0) | 43 bodies |
| loafbrr pistol / AK / revolver (CC0) | 3 |
| Original infantry meshes (AT-4, RBS 70, G11) | 3 |
| **Selectable catalog** | **49** |

Accessories (`acc_*`) and family placeholders are not extra guns. There is no 300-gun CC0 download.

## Play

Hangar aircraft-select overlay is always available:

- Toggle **Infantry ENABLED / DISABLED** to run this system alone (config: `Infantry.Enabled`).
- Pick hotbar guns. **Slot 1 = carry-on** (hop-out / chute).
- **DEPLOY** charges the **sum of unique guns** on the hotbar (same `Allocation` millions as aircraft). Pistol 0.12M … assault rifle 0.45M … AT-4 1.45M … RBS 70 2.80M. Hop-out does not charge again.
- In the field: **G** arsenal, **1–9** / wheel, **LMB** fire, **RMB** ads, **R** reload, **U** board, **'** armor table (every encyclopedia vehicle / ship / aircraft).
- Hits always spark at the impact point. Shot streaks are **off** (`Combat.ShowTracers = false`).
- Bottom overlay: FPS + live spark count (`Debug.ShowPerf`).

Config file: `BepInEx/config/com.iallemege.groundarsenal.cfg`

| Key | Default | Meaning |
| --- | --- | --- |
| Infantry.Enabled | true | Master switch |
| Infantry.PauseOritasyFps | true | If Oritasy is present, skip its infantry Tick/Draw |
| Combat.ShowTracers | false | Optional streak |
| Debug.ShowPerf | true | FPS overlay |

To test **independence / performance**: leave this plugin on, set `PauseOritasyFps = true` (or disable `OritasyInfantry.dll`), deploy, watch the FPS line. Ground Arsenal does not reference Blueprinter 2.0.0.

## Install

1. Drop `GroundArsenal.dll` into `BepInEx/plugins/`.
2. Drop the `GroundArsenal/` folder (weapon OBJ / PNG / `kits.txt`) next to the DLL.
3. **Fully quit Steam**, then start the game.

## Build

Windows, game at `d:\Steam\steamapps\common\Nuclear Option`:

```
build.bat
```

C# 5 / `csc`. Copies the DLL and `assets/Weapons/*` into plugins.

## License

Code: MIT. Weapon meshes/textures: CC0 (Quaternius, loafbrr) or original infantry analogues. See `assets/LICENSE_ASSETS.txt`.
