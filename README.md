# GravityBlastrMods

A collection of mods for [Schedule I](https://store.steampowered.com/app/3164500/Schedule_I/) using [MelonLoader](https://melonwiki.xyz/).

> Supports both the **Mono** and **IL2CPP** branches of the game.

## Mods

All mods can be installed separately or together. All are savegame-safe — they don't touch core save data, so you can remove them at any time without breaking anything.

| Mod | What it does |
|-----|-------------|
| **DeliveryNotificationsMod** | Sends phone notifications when deliveries arrive or complete, with store name, destination, and loading dock. |
| **DeliveryPersistMod** | Remembers your delivery order settings (destination, dock, quantities) after deliveries and across saves. |
| **DontPunchRayMod** | Adds a sell option to Ray and Jeremy's dialog — no more punching required. |
| **IlluminationMod** | Better nighttime visibility: 2x flashlight range, 2x headlight range, 4x reverse light range, and sticky reverse lights. |
| **LaunderMaxMod** | Auto-selects the maximum amount when opening the laundering interface. |
| **LaunderScaleMod** | Scales laundering capacity with player rank — up to 3x base ($60k/day instead of $20k). |
| **LightPersistMod** | Saves flashlight and headlight state across game loads. |
| **LockerNotificationsMod** | Texts you when employee pay depletes a locker below tomorrow's need. One message per property per day. |
| **PrefsPersistMod** | Fixes settings (like graphics etc) not persisting between launches on certain system locales. |
| **SigningFreeMod** | Fixes the "signing free" typo — it's "signing fee". |
| **UnpackMod** | Lets you unpack bricks at a packaging station even when packaging is in the slot. |

## Requirements

- [Schedule I](https://store.steampowered.com/app/3164500/Schedule_I/)
- [MelonLoader](https://melonwiki.xyz/) installed for Schedule I

## Installation

Download the `.zip` for each mod you want from [Releases](../../releases). Each zip contains both IL2CPP and Mono DLLs — pick the one matching your game branch and drop it into your `Mods` folder:

```
C:\Program Files (x86)\Steam\steamapps\common\Schedule I\Mods\
```

Not sure which branch you're on? The game ships as **IL2CPP** by default. If you haven't changed anything in Steam betas, use the IL2CPP DLL.

### Versioning

Releases use [semantic versioning](https://semver.org/) (e.g. `v1.0.0`). Each release notes which game version it was built and tested against. Mods may work with other game versions, but only the listed version has been tested.

## Uninstalling

Delete the mod's `.dll` from your `Mods` folder. That's it — no save cleanup needed.

## Philosophy

- Minimal, single-purpose mods that do one thing well
- No malware, tracking, ads, mining, or any bullshit like that. Ever.
- Open source, open book
- Vibe coded, human approved
- This game is great and you should buy it

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for build instructions and development setup.

## License

[MIT](LICENSE)
