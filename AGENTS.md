# AGENTS.md

This repository contains mods for [Schedule I](https://store.steampowered.com/app/3164500/Schedule_I/), a Unity game. Mods are built with [MelonLoader](https://melonwiki.xyz/) and [Harmony](https://harmony.pardeike.net/) for runtime patching.

## Project overview

- **Owner:** gravityblastr
- **GitHub repo:** `gravityblastr/schedule-1-mods`
- **License:** MIT
- **Versioning:** [Semantic versioning](https://semver.org/). Tags are `v1.2.3`. Each release notes which game version it targets.

## Game branches

Schedule I ships in two Unity backend variants:

| Branch | Target framework | Define | NuGet package |
|--------|-----------------|--------|---------------|
| **IL2CPP** (default) | `net6.0` | `IL2CPP` | `GravityBlastr.ScheduleOne.Libs.IL2CPP` |
| **Mono** | `netstandard2.1` | _(none)_ | `GravityBlastr.ScheduleOne.Libs.Mono` |

All mods multi-target both frameworks. Branch-specific code uses `#if IL2CPP` / `#else`. IL2CPP namespaces are prefixed with `Il2Cpp` (e.g. `Il2CppScheduleOne.Delivery` vs `ScheduleOne.Delivery`). When casting game objects in IL2CPP, use `.TryCast<T>()` instead of C# `as`.

## Repository structure

```
src/
  Directory.Build.props        # Shared build config (targets, defines, package refs)
  Directory.Packages.props     # Centralized NuGet version management (game lib version here)
  GravityBlastrMods.sln        # Solution with all mod projects
  nuget.config                 # GitHub Packages feed config
  <ModName>/                   # One directory per mod
    Core.cs                    # Mod entry point (extends MelonMod)
    <ModName>.csproj           # Minimal — just AssemblyName and RootNamespace
    manifest.json              # Thunderstore manifest
    README.md                  # Per-mod readme
    NEXUS.bbcode               # Nexus Mods description (gitignored)
packaging/                     # NuGet packaging projects for game libs (maintainer use)
.github/workflows/
  build.yml                    # CI: build on push/PR to main
  release.yml                  # CD: build + package + GitHub Release on v* tags
.work/                         # Gitignored workspace (see below)
  decompiled/                  # Decompiled game source (separate git repo)
  IDEAS.md                     # Ideas for new mods
```

## Mod conventions

Each mod is a single `.csproj` with minimal config — all shared settings (target frameworks, package references, IL2CPP defines) live in `Directory.Build.props`. A mod's `.csproj` only sets:

```xml
<AssemblyName>GravityBlastr.ModName</AssemblyName>
<RootNamespace>ModName</RootNamespace>
```

Assembly names follow the pattern `GravityBlastr.<ModName>`.

Every mod's `Core.cs` must:
1. Declare `[assembly: MelonInfo(...)]` and `[assembly: MelonGame("TVGS", "Schedule I")]`
2. Define a `Core` class extending `MelonMod`
3. Apply Harmony patches in `OnInitializeMelon()` via `HarmonyInstance.PatchAll()`
4. Log a confirmation message: `LoggerInstance.Msg("<ModName> loaded.")`

Use conditional imports for game namespaces:
```csharp
#if IL2CPP
using Il2CppScheduleOne.SomeNamespace;
#else
using ScheduleOne.SomeNamespace;
#endif
```

## Current mods

| Mod | Purpose |
|-----|---------|
| DeliveryNotificationsMod | Rich delivery arrival/completion notifications |
| DontPunchRayMod | Adds "Sell" option to Ray/Jeremy dialog |
| IlluminationMod | Extended flashlight/headlight range, sticky reverse lights |
| LaunderMaxMod | Auto-selects max amount in laundering UI |
| LaunderScaleMod | Scales laundering capacity with player rank |
| LightPersistMod | Saves flashlight/headlight state across loads |
| LockerNotificationsMod | Employee pay locker depletion alerts |
| PrefsPersistMod | Fixes settings not persisting on certain locales |
| SigningFreeMod | Fixes "signing free" typo to "signing fee" |
| UnpackMod | Allows brick unpacking when packaging slot is occupied |

## Task automation

Most workflows are automated via [Taskfile](https://taskfile.dev/) (`Taskfile.yml`). Key tasks:

| Command | What it does |
|---------|-------------|
| `task build` | Build all mods (both Mono + IL2CPP) |
| `task build:mono` | Build Mono target only |
| `task build:il2cpp` | Build IL2CPP target only |
| `task deploy` | Auto-detect game branch, deploy correct DLLs |
| `task build-deploy` | Build + deploy in one step |
| `task deploy:clean` | Remove mod DLLs from game Mods folder |
| `task game:check` | Verify game/MelonLoader install, show active branch |
| `task game:update` | Full game update pipeline (package libs, decompile, push) |
| `task game:launch` | Launch Schedule I via Steam |

Environment variables are loaded from `.env` (gitignored). `GITHUB_TOKEN` is required for NuGet package restore from GitHub Packages.

## Build system

- **.NET SDK 8.0+** required
- NuGet restore pulls game libraries from a private GitHub Packages feed (`nuget.pkg.github.com/gravityblastr`)
- `Directory.Packages.props` pins the game library version (e.g. `0.4.5.1`) — update this when targeting a new game version
- The solution builds both `netstandard2.1` (Mono) and `net6.0` (IL2CPP) outputs

## CI/CD

- **Push/PR to main** (`build.yml`): builds the solution in Release to verify compilation
- **Tag push matching `v*`** (`release.yml`): builds, packages per-mod zips (each containing IL2CPP/ and Mono/ subdirectories), auto-generates release notes with changed mods and commit log, creates a GitHub Release with zip artifacts

## Decompiled game code

Decompiled game source lives in `.work/decompiled/`, which is a separate git repository (`schedule-1-decompiled`) with two branches: `mono` and `il2cpp`. This repo is updated and tagged each time a new game version is released via `task game:update`.

Use this decompiled code to:
- Understand game internals when writing or debugging mods
- Review diffs between game versions to find breaking changes
- Locate the classes, methods, and fields that mods need to patch

The decompiled repo tags follow the pattern `v<nuget_version>-<branch>` (e.g. `v0.4.5.1-il2cpp`).

## Game library packaging

When a new game version drops, `task game:update` handles the full pipeline:

1. Detects the active game branch (IL2CPP or Mono) by checking for `GameAssembly.dll`
2. Reads the game version from `globalgamemanagers` (e.g. `0.4.5f1`)
3. Converts to four-part NuGet version (e.g. `0.4.5.1`)
4. Stages relevant DLLs (game assemblies, MelonLoader, Unity modules)
5. Packs a NuGet package and pushes to GitHub Packages
6. Decompiles `Assembly-CSharp.dll` using ILSpy CLI
7. Commits and tags in the decompiled repo

Run this once for IL2CPP, then switch Steam to Mono beta and run again. Then update `Directory.Packages.props` with the new version.

## `.work/` directory

The `.work/` directory is **gitignored** and serves as a local workspace for:
- `decompiled/` — the decompiled game source repo
- `IDEAS.md` — ideas for new mods (use the `new-mod` skill to add entries)
- Temporary scripts, scratch files, or agent working space

Agents can freely create and use files in `.work/` without affecting the repository.

## GitHub and issues

This project is hosted on GitHub at `gravityblastr/schedule-1-mods`. Use GitHub Issues for:
- Bug reports
- Feature requests / mod ideas
- Game version compatibility tracking

## Key files to know

| File | Why it matters |
|------|---------------|
| `src/Directory.Build.props` | All shared build config — edit this for new package refs or build settings |
| `src/Directory.Packages.props` | Game library version pin — update after `task game:update` |
| `src/nuget.config` | NuGet feed config — needs `GITHUB_TOKEN` env var |
| `Taskfile.yml` | All automation tasks |
| `TESTING.md` | Manual test checklist for all mods |
| `CONTRIBUTING.md` | Build prerequisites and developer setup |
| `.work/IDEAS.md` | New mod ideas backlog |