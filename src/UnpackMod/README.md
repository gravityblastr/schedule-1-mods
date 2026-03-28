# GravityBlastr Brick Unpacker

Allows unpacking bricks at a packaging station even when there's packaging (baggies/jars) in the packaging slot. Unpacking a brick doesn't use the packaging slot, so it shouldn't care whether anything is there.

## Features

- Unpack bricks without clearing the packaging slot first
- Only affects brick unpacking — normal packaging behavior is unchanged

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
