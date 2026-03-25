# Contributing

## Prerequisites

- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- [Task](https://taskfile.dev/) (task runner)
- A GitHub personal access token (PAT) with `read:packages` scope

Game reference assemblies are hosted as private NuGet packages on GitHub Packages. The PAT is needed to restore them during build.

## Setup

```bash
export GITHUB_TOKEN=ghp_your_token_here
```

## Building

Build all mods for both Mono and IL2CPP:

```bash
task build
```

Deploy to your local game (auto-detects branch):

```bash
task deploy
```

Or build and deploy in one step:

```bash
task build-deploy
```

## Tasks

| Command | Description |
|---------|-------------|
| `task build` | Build all mods (Mono + IL2CPP) |
| `task build:mono` | Build Mono target only |
| `task build:il2cpp` | Build IL2CPP target only |
| `task deploy` | Auto-detect branch and deploy |
| `task deploy:mono` | Deploy Mono DLLs explicitly |
| `task deploy:il2cpp` | Deploy IL2CPP DLLs explicitly |
| `task deploy:clean` | Remove mod DLLs from game Mods folder |
| `task game:check` | Verify game/MelonLoader install, show active branch |

## Project structure

```
src/
  Directory.Build.props       # Shared build config (targets, defines, package refs)
  Directory.Packages.props    # Centralized NuGet version management
  GravityBlastrMods.sln       # Solution with all mod projects
  nuget.config                # GitHub Packages feed
  DeliveryNotificationsMod/   # One directory per mod
  ...
packaging/                    # NuGet packaging projects (maintainer use)
.github/workflows/            # CI: build check + release
```

Each mod is a minimal `.csproj` — just `AssemblyName` and `RootNamespace`. All shared config (target frameworks, package references, IL2CPP defines) lives in `Directory.Build.props`.

## Game branches

The game ships as **IL2CPP** (default). To switch to **Mono**: Steam > Schedule I > Properties > Betas > select **mono**.

Source code uses `#if IL2CPP` / `#else` for branch-specific logic. The build produces both `netstandard2.1` (Mono) and `net6.0` (IL2CPP) outputs.

## CI

- **Push/PR to main**: builds the solution to verify it compiles
- **Tag push (`v*`)**: builds, packages per-mod zips, and creates a GitHub Release

## Maintainer tasks

These require additional access (GitHub Packages write, private repos):

| Command | Description |
|---------|-------------|
| `task game:update` | Detect branch, package game libs as NuGet, decompile, push |
| `task game:decompile` | Decompile `Assembly-CSharp.dll` (requires [ILSpy CLI](https://github.com/icsharpcode/ILSpy)) |

### When a new game version comes out

1. **Update IL2CPP first.** Make sure Steam is on the default (IL2CPP) branch and launch the game once with MelonLoader so `Il2CppAssemblies/` is regenerated.
2. Run `task game:update`. This will:
   - Detect the game version (e.g. `0.4.5f1`)
   - Package the IL2CPP libs and push to GitHub Packages as `0.4.5.1`
   - Decompile Assembly-CSharp and commit/tag the `il2cpp` branch of `schedule-1-decompiled`
3. **Switch to Mono** in Steam (Properties > Betas > mono), then run `task game:update` again for the Mono side.
4. **Update the version** in `src/Directory.Packages.props` to the new NuGet version (e.g. `0.4.5.1`).
5. **Check for breaking changes.** The decompiled repo (`schedule-1-decompiled`) tracks diffs between game versions — review the changes on each branch to see what moved, renamed, or disappeared. This is the fastest way to find what needs updating in mod code.
6. **Build and test.** `task build` — fix any compilation errors, then deploy and test in-game.
