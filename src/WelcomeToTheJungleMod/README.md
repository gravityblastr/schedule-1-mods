# WelcomeToTheJungleMod

A hardcore "cold start" mode for Schedule I. No Uncle Nelson guidance, no dead-drop cash, no hand-holding — you spawn in Hyland Point with a destroyed RV and zero dollars.

## What it does

When **Welcome to the Jungle Mode** is enabled in mod settings, starting a new game:

- Skips the tutorial/prologue entirely
- Destroys the RV on arrival (it already blew up)
- Suppresses all tutorial guidance quests (payphone, dead drops, "how to grow/cook/launder" missions)
- Keeps the late-game story arc intact (Sink or Swim pressure, The Deep End, cartel quests)

The goal is to discover the game's systems through exploration rather than hand-holding.

## Setup

1. Enable **Welcome to the Jungle Mode** in MelonLoader mod preferences
2. Start a new game — the mode is embedded in that save permanently
3. Disable the setting again for future new games if desired

The mode is save-specific: a `WelcomeToTheJungle.json` marker is written to the save folder on creation. Existing saves are never affected.

## Compatibility

Tested on Schedule I v0.4.5f1 (IL2CPP and Mono).
