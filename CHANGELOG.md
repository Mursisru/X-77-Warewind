# Changelog

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
