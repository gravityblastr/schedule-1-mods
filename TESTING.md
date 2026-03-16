# Manual Test Script

Run through this checklist after making changes or when a new game version drops.
Test on both Mono and IL2CPP branches.

## Setup

- [ ] `task build` succeeds (both targets, 0 errors)
- [ ] `task check-game` shows correct active branch
- [ ] `task deploy` detects the right branch and deploys
- [ ] Game launches with MelonLoader, all 8 mods show "loaded" in log

## SigningFreeMod

- [ ] Open contacts app, view an unrecruited dealer — unlock hint says "signing fee" (not "signing free")
- [ ] Talk to Fixer through hiring dialogue — text says "signing fee"

## DontPunchRayMod

- [ ] Talk to a store owner (not a customer) — "Sell" choice appears in dialogue
- [ ] Select "Sell" — transaction completes normally
- [ ] No duplicate "Sell" choices appear

## IlluminationMod

- [ ] Phone flashlight reaches noticeably further than vanilla (test in dark area)
- [ ] Vehicle headlights illuminate further than vanilla
- [ ] Reverse lights are brighter and cast a focused cone backward (not a sphere)
- [ ] Reverse, then release throttle — reverse lights stay on (sticky)
- [ ] While sticky-on, press forward throttle — reverse lights turn off
- [ ] Exit vehicle while sticky reverse lights are on — lights turn off

## LaunderScaleMod

- [ ] Below Peddler I: laundering UI shows base capacity ($20,000)
- [ ] At Peddler I+: capacity label shows a scaled value (>$20,000)
- [ ] "At capacity" message triggers at scaled limit, not base $20,000
- [ ] After confirming a launder operation, UI updates immediately (not stale)

## LaunderMaxMod

- [ ] Open laundering UI — slider and input field are pre-set to max
- [ ] Max reflects scaled capacity from LaunderScaleMod (not base $20,000)
- [ ] Can still manually adjust slider/input after auto-max

## LightPersistMod

- [ ] Turn flashlight on, save, load — flashlight is on
- [ ] Turn flashlight off, save, load — flashlight is off
- [ ] Turn vehicle headlights on, exit vehicle, save, load — headlights are on
- [ ] Multiple vehicles each remember their own headlight state
- [ ] `LightPersistMod.json` exists in save folder with correct structure
- [ ] In multiplayer: other players see restored flashlight state

## DeliveryPersistMod

- [ ] Set destination, dock, and quantities for a shop, then submit order
- [ ] Cart is NOT cleared after order — quantities remain
- [ ] Save, load, open delivery app — destination and dock are restored
- [ ] Quantities are restored after load
- [ ] Each shop remembers its own settings independently
- [ ] `DeliveryPersistMod.json` exists in save folder with correct structure
- [ ] Fresh save (no JSON file): app starts with defaults, no errors

## DeliveryNotificationsMod

- [ ] Delivery arrives — notification appears with store name, destination, dock
- [ ] Delivery completes — notification appears (different color)
- [ ] Arrived notification icon is green, completed is red
- [ ] Notification text is readable (not cut off)
- [ ] No duplicate notifications for the same event
- [ ] Loading a save does NOT trigger notifications for existing delivery states

## Cross-Mod

- [ ] LaunderMaxMod + LaunderScaleMod: slider max matches scaled capacity
- [ ] DeliveryPersistMod + DeliveryNotificationsMod: restoring quantities on load does not trigger notifications
- [ ] LightPersistMod + IlluminationMod: restored lights still have boosted range
