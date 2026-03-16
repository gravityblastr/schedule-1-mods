# GravityBlastrMods

A collection of mods for [Schedule I](https://store.steampowered.com/app/3164500/Schedule_I/) using [MelonLoader](https://melonwiki.xyz/) and Harmony.

> Supports both the **Mono** and **IL2CPP** branches of the game.

## Quality of Life

### DeliveryNotificationsMod
Sends in-game phone notifications when deliveries arrive or complete. Includes the store name, destination, and loading dock in the message.

### DeliveryPersistMod
Remembers your delivery order settings (destination, loading dock, item quantities) after deliveries and across saves. No more re-entering orders every time you load the game, and your choices will still be present after you order.

### LaunderMaxMod
Auto-selects the maximum amount when opening the laundering interface, so you don't have to drag the slider every time.

### LightPersistMod
Saves and restores the state of your flashlight and vehicle headlights across game loads. If you left your headlights on when you saved, they'll be on when you load.

### SigningFreeMod
Fixes the "signing free" typo in the game — it should be "signing fee". Patches the contacts app, dialogue text, and dialogue choices.

## Gameplay

### DontPunchRayMod
You don't have to punch store owners anymore to sell to them. The sell option is now included in their store dialog choices.

### IlluminationMod
Improves lighting for better nighttime visibility:
- **Flashlight**: 2x range
- **Vehicle headlights**: 2x range
- **Reverse lights**: 4x range, 3x intensity, converted from point lights to directional spots
- **Sticky reverse lights**: reverse lights stay on after you stop reversing until you shift to drive

### LaunderScaleMod
Scales laundering capacity based on player rank. Starting at Peddler I, capacity increases by 5% per rank tier, up to 3x the base capacity. No more $20,000/day cap - it's now $60,000/day.

## Prerequisites

- [Schedule I](https://store.steampowered.com/app/3164500/Schedule_I/)
- [MelonLoader](https://melonwiki.xyz/) installed for Schedule I
- [.NET SDK](https://dotnet.microsoft.com/download) (for building from source)
- [Task](https://taskfile.dev/) (optional, for build/deploy commands)

### Game branches

The game ships as **IL2CPP** (default). To switch to **Mono**, right-click Schedule I in Steam > Properties > Betas > select **mono**. These mods support both branches — the build system and deploy task handle the differences automatically.

## Installing (prebuilt)

Download the `.dll` files from [Releases](../../releases) and drop them into your game's `Mods` folder:

```
C:\Program Files (x86)\Steam\steamapps\common\Schedule I\Mods\
```

## Building from source

1. Clone this repo
2. Copy required game DLLs:
   ```bash
   task copy-libs-mono      # Mono branch
   task copy-libs-il2cpp    # IL2CPP branch (run game once with MelonLoader first)
   ```

3. Build all mods (both targets):
   ```bash
   task build
   ```

4. Deploy to your game (auto-detects Mono vs IL2CPP):
   ```bash
   task deploy
   ```

Or build and deploy in one step:
```bash
task build-deploy
```

### Other tasks

| Command | Description |
|---------|-------------|
| `task build-mono` | Build Mono target only |
| `task build-il2cpp` | Build IL2CPP target only |
| `task deploy-mono` | Deploy Mono DLLs explicitly |
| `task deploy-il2cpp` | Deploy IL2CPP DLLs explicitly |
| `task clean` | Remove mod DLLs from the game's Mods folder |
| `task check-game` | Verify game and MelonLoader installation, show active branch |
| `task decompile` | Decompile `Assembly-CSharp.dll` (requires [ILSpy CLI](https://github.com/icsharpcode/ILSpy)) |

## License

[MIT](LICENSE)
