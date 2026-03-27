# GravityBlastr Preferences Memory

Use this if the game won't remember your graphics settings, etc. between sessions.

Fixes settings not persisting between launches. The game's settings loader crashes on certain system locales, silently losing all your preferences. This mod patches the locale bug, restores saved settings on startup, and flushes changes to disk immediately so they survive crashes too.

## Features

- Fixes locale-related crash in the settings loader
- Settings restored correctly on startup
- Changes flushed to disk immediately (crash-safe)

## Compatibility

- Supports both IL2CPP and Mono branches of the game
- Savegame-safe — does not modify core save data, can be removed at any time
- Works with all other GravityBlastr mods

## Installation

### Thunderstore / r2modman
Install via mod manager. Both IL2CPP and Mono DLLs are included — [SwapperPlugin](https://thunderstore.io/c/schedule-i/p/the_croods/SwapperPlugin/) handles loading the correct one automatically.

### Nexus Mods
Download the file matching your game branch (IL2CPP or Mono) and drop the DLL into your `Mods` folder.

### Manual
Download from [GitHub Releases](https://github.com/gravityblastr/schedule-1-mods/releases), pick the DLL for your game branch, and place it in your `Mods` folder.

## About

gravityblastr makes minimal, focused mods for an experience that is close to vanilla, but better.

## Source

[GitHub](https://github.com/gravityblastr/schedule-1-mods)
