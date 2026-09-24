# Graphix

Lightweight MelonLoader mod for Unity games — graphics settings, FPS limiter and in-game HUD.

## Features
- Resolution & screen mode (windowed / borderless / exclusive fullscreen)
- Shadows, MSAA, anisotropic filtering, mipmaps, soft particles
- Precise FPS limiter (hybrid sleep + spin)
- In-game HUD (F4): FPS, frametime, 1% low, 0.1% low, GPU stats via NVML
- Auto-preset recommendation logged at startup

## Requirements
- MelonLoader 0.6+ (0.7.x recommended)
- Unity game with MelonLoader installed
- Windows for full GPU stats (NVIDIA only)

## Installation
1. Install MelonLoader into your game.
2. Drop `Graphix.dll` into the `Mods` folder.
3. Launch the game — config is created at `UserData/MelonPreferences.cfg`.
4. Edit the config or press F4 in-game to toggle the HUD.

## License
MIT
