# X-77 Warewind Unity Bake

1. Open this folder in Unity **2022.3.62f3** (same as Nuclear Option).
2. Wait for FBX import (`Assets/MissilePack/X-75-Warewind.fbx`).
3. Menu: **Warewind → Build Nobp Bundle**.
4. Output: `UnityBake/Build/X77Warewind.nobp` (+ copy to `X77Warewind/Resources/` and game plugins).

The `.nobp` is a Unity AssetBundle loaded by **Blueprinter**. It must contain TextAsset `patch_manifest`.
