# Manual Test Script

Run through this checklist after making changes or when a new game version drops.
Test on both Mono and IL2CPP branches.

## Setup

- [ ] `task build` succeeds (both targets, 0 errors)
- [ ] `task game:check` shows correct active branch
- [ ] `task deploy` detects the right branch and deploys
- [ ] Game launches with MelonLoader, all 10 mods show "loaded" in log

## PrefsPersistMod

- [ ] Change a graphics setting (e.g. quality) — MelonLoader log shows "Graphics settings saved to disk."
- [ ] Change a display setting (e.g. resolution) and confirm — log shows "Display settings saved to disk."
- [ ] Change graphics settings, force-kill the game (Task Manager), relaunch — settings are preserved
- [ ] Change audio volume, force-kill, relaunch — volume settings are preserved

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
- [ ] Correct amounts are added to balance when laundering completes

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

## DeliveryNotificationsMod

- [ ] Delivery arrives — mod notification appears with store name, destination, dock
- [ ] Game's basic "Delivery Arrived" notification is suppressed (no duplicate)
- [ ] Delivery completes — notification appears (different color)
- [ ] Arrived notification icon is green, completed is red
- [ ] Notification text is readable (not cut off)
- [ ] No duplicate notifications for the same event
- [ ] Loading a save does NOT trigger notifications for existing delivery states

## LockerNotificationsMod

- [ ] Hire an employee, assign a locker with exactly one day's wage, sleep — text message from Manny appears
- [ ] Message shows the correct property name
- [ ] Message shows as unread notification on phone (like supplier dead drop messages)
- [ ] Multiple employees at the same property running low — only one message for that property
- [ ] Employees at different properties running low — one message per property
- [ ] Locker has enough for tomorrow after pay — no message sent
- [ ] Sleep again (new day) — notifications can fire again for the same properties
- [ ] Save, load — no spurious messages on load

## UnpackMod

- [ ] Place a brick in the output slot of a packaging station with baggies/jars in the packaging slot
- [ ] "Unpack" button is enabled (not grayed out)
- [ ] Click Unpack — brick is unpacked into loose product, packaging slot is unchanged
- [ ] Unpack a non-brick (e.g. baggie) with a full packaging slot — still correctly blocked ("Unpackaged items won't fit!")
- [ ] Unpack a brick when product slot is full — correctly blocked (ProductSlotFull)
- [ ] Packaging operations (packing loose into baggies/jars) still work normally

## Cross-Mod

- [ ] LaunderMaxMod + LaunderScaleMod: slider max matches scaled capacity
- [ ] LightPersistMod + IlluminationMod: restored lights still have boosted range
