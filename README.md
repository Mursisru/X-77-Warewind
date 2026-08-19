# X-77 Warewind

[![Version](https://img.shields.io/badge/version-1.0.0-blue)](https://github.com/Mursisru/X-77-Warewind/releases)
[![BepInEx](https://img.shields.io/badge/BepInEx-5-green)](https://docs.bepinex.dev/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

BepInEx plugin that adds the **X-77 Warewind** two-stage hypersonic cruise missile to [Nuclear Option](https://store.steampowered.com/app/2654120/Nuclear_Option/).

> [!IMPORTANT]
> **Requires [Blueprinter](https://github.com/nikkorap/NOBlueprinter-Releases)** (`com.nikkorap.blueprinter`). Install `Blueprinter.dll` into `BepInEx/plugins/` before this mod.

## Features

- Drop / loft / 50 km cruise / dive; solid booster + ramjet sustainer
- Optical HUD, 700 kg HE, 2800 kg launch
- Own flares ×50 and EW capacitor; add-only Darkreach / Alkyon HE Piledriver slots
- Content bundle `X77Warewind.nobp` (`WarewindVisual`)

Json keys stay `missilepack_x77_warewind` / `MissilePack_X77_Warewind_single` for existing loadouts.

## Install

1. Install BepInEx 5 and Blueprinter.
2. Copy the `X-77-Warewind/` folder into `BepInEx/plugins/X-77-Warewind/`:
   - `X77Warewind.dll`
   - `X77Warewind.nobp`
   - `PreviewWarewind.png`
   - `Textures/Warewind/` (runtime material maps)
3. Launch the game and select **X-77 Warewind** on Piledriver HE pylons.

## Build

```powershell
dotnet build .\X77Warewind\X77Warewind.csproj -c Release
```

Release output auto-deploys to `BepInEx/plugins/X-77-Warewind/`.

Unity bake (mesh `.nobp`):

```text
Open UnityBake/ in Unity 2022.3.62f3 → Warewind → Build Nobp Bundle
```

## Model source

Blender export (canonical): [`Models/X-75-Warewind.fbx`](Models/X-75-Warewind.fbx)  
Unity import copy: `UnityBake/Assets/MissilePack/X-75-Warewind.fbx`

## Contributors

See [CONTRIBUTORS.md](CONTRIBUTORS.md).

## License

MIT — see [LICENSE](LICENSE).
