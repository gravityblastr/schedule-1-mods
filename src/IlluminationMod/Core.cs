using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.Vehicles;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.UI.Phone;
using ScheduleOne.Vehicles;
#endif

[assembly: MelonInfo(typeof(IlluminationMod.Core), "IlluminationMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace IlluminationMod;

public class Core : MelonMod
{
    // Range multipliers (increase how far light reaches, not how bright nearby surfaces are)
    private const float FlashlightRangeMultiplier = 2.0f;
    private const float HeadlightRangeMultiplier = 2.0f;
    private const float ReverseLightRangeMultiplier = 4.0f;
    private const float ReverseLightIntensityMultiplier = 3.0f;
    private const float ReverseLightSpotAngle = 120f;

    // Sticky reverse light state per vehicle
    internal static readonly Dictionary<VehicleLights, bool> StickyReverse =
        new Dictionary<VehicleLights, bool>();

    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(typeof(PhoneStartPatch));
        HarmonyInstance.PatchAll(typeof(VehicleLightsAwakePatch));
        HarmonyInstance.PatchAll(typeof(VehicleLightsUpdatePatch));
        LoggerInstance.Msg("IlluminationMod loaded.");
    }

    [HarmonyPatch(typeof(Phone), "Start")]
    public static class PhoneStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Phone __instance)
        {
#if IL2CPP
            var flashlightGO = __instance.PhoneFlashlight;
#else
            var flashlightGO = Traverse.Create(__instance)
                .Field("PhoneFlashlight").GetValue<GameObject>();
#endif
            if (flashlightGO == null) return;

            var light = flashlightGO.GetComponentInChildren<Light>(true);
            if (light != null)
            {
                light.range *= FlashlightRangeMultiplier;
            }
        }
    }

    [HarmonyPatch(typeof(VehicleLights), "Awake")]
    public static class VehicleLightsAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix(VehicleLights __instance)
        {
            // Boost headlight range
            if (__instance.headLightSources != null)
            {
                foreach (var ol in __instance.headLightSources)
                {
                    if (ol?._Light != null)
                    {
                        ol._Light.range *= HeadlightRangeMultiplier;
                        ol.MaxDistance = Mathf.Max(ol.MaxDistance, ol._Light.range * 2f);
                    }
                }
            }

            // Boost reverse light range and convert to directional spots
            if (__instance.reverseLightSources != null)
            {
                foreach (var light in __instance.reverseLightSources)
                {
                    if (light != null)
                    {
                        light.range *= ReverseLightRangeMultiplier;
                        light.intensity *= ReverseLightIntensityMultiplier;
                        if (light.type == LightType.Point)
                        {
                            light.type = LightType.Spot;
                            light.spotAngle = ReverseLightSpotAngle;
                            var vt = __instance.transform;
                            light.transform.rotation = Quaternion.LookRotation(
                                -vt.forward, vt.up);
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(VehicleLights), "Update")]
    public static class VehicleLightsUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(VehicleLights __instance)
        {
            var vehicle = __instance.GetComponent<LandVehicle>();
            if (vehicle == null) return;

            // Only apply sticky logic when local player is driving
            if (!vehicle.LocalPlayerIsDriver)
            {
                // Turn off if we were sticky when player exited
                if (StickyReverse.TryGetValue(__instance, out bool wasOn) && wasOn)
                    SetReverseLights(__instance, false);
                StickyReverse.Remove(__instance);
                return;
            }

            bool wasStickyBefore = StickyReverse.TryGetValue(__instance, out bool prev) && prev;

            // Latch on when reversing, clear on any forward throttle (like shifting to drive)
            if (vehicle.IsReversing)
                StickyReverse[__instance] = true;
            else if (vehicle.currentThrottle > 0f)
                StickyReverse[__instance] = false;

            bool shouldBeOn = StickyReverse.TryGetValue(__instance, out bool val) && val;

            // Override reverse light visuals based on sticky state
            if (!vehicle.IsReversing && (shouldBeOn != wasStickyBefore || shouldBeOn))
            {
                SetReverseLights(__instance, shouldBeOn);
            }
        }

        private static void SetReverseLights(VehicleLights vl, bool on)
        {
            // Sync the game's internal tracking flag so UpdateVisuals doesn't fight us
#if IL2CPP
            vl.reverseLightsApplied = on;
#else
            Traverse.Create(vl).Field("reverseLightsApplied").SetValue(on);
#endif

            if (vl.reverseLightMeshes != null)
            {
                var mat = on ? vl.reverseLightMat_On : vl.reverseLightMat_Off;
                if (mat != null)
                {
                    foreach (var mesh in vl.reverseLightMeshes)
                        mesh.material = mat;
                }
            }
            if (vl.reverseLightSources != null)
            {
                foreach (var light in vl.reverseLightSources)
                {
                    if (light != null)
                        light.enabled = on;
                }
            }
        }
    }
}
