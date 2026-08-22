# Changelog

## [1.0.1] - 2026-08-22

### Fixed

- Intermittent post-launch AAM-36 identity/visual leak on shared vanilla AAM2 shell (multi-pylon / lost Pending spawn token)
- Shared `WeaponInfo` + `sortWeapons` on mount spawn; rescue `Claim` when AAM2 shell spawns within Fire window
- Force Warewind `weaponPrefab` on Fire; bootstrap resolves AAM2 only (no generic AAM fallback)

## [1.0.0] - 2026-08-19

First public release.

### Added

- X-77 Warewind standalone BepInEx plugin (`com.mursisru.x77warewind`)
- Two-stage hypersonic flight profile: Drop → Align → Loft → Cruise → Dive
- Stage-1 TWR punch (10× for 5 s), optical guidance, 700 kg HE, 2800 kg launch mass
- Custom `WarewindVisual` bundle (`X77Warewind.nobp`) with Blender 1:1 materials
- Motor exhaust FX (TBM booster) and trail particles on engine sockets
- Combat HUD weapon preview (`PreviewWarewind.png`)
- Add-only Darkreach / Alkyon HE Piledriver slots; Alkyon bay fit with dorsal sink
- Flares (15 km gate), EW jam, survivability overrides, HUD range tune

### Notes

- Requires **BepInEx 5** and **Blueprinter**
- Json keys unchanged: `missilepack_x77_warewind` / `MissilePack_X77_Warewind_single`

## [1.1.0] - 2026-08-17

### Changed

- Split out of MissilePack into a standalone X-77 Warewind plugin (`com.mursisru.x77warewind`)
- Plugin folder `BepInEx/plugins/X-77-Warewind/`, bundle `X77Warewind.nobp`

Json keys are unchanged (`missilepack_x77_warewind`).

### Added (from MissilePack 1.1.0)

- Two-stage hypersonic (Optical HUD, 700 kg HE, mass 2800 kg)
- Shared vanilla AAM2 `unitPrefab` spawn contract; stamp `WarewindVisual` after Spawn
- Fire-and-forget: Drop → Loft → 50 km cruise → Dive
- Dual motors, stage-1 mesh sep, flares / EW, DockingPort eject
