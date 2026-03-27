using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

#if IL2CPP
using Il2CppTMPro;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.UI;
#else
using TMPro;
using ScheduleOne.DevUtilities;
using ScheduleOne.Levelling;
using ScheduleOne.Money;
using ScheduleOne.Property;
using ScheduleOne.UI;
#endif

[assembly: MelonInfo(typeof(LaunderScaleMod.Core), "GravityBlastr.LaunderScaleMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace LaunderScaleMod;

public class Core : MelonMod
{
    // Stores the true original LaunderCapacity per business, set once on Initialize.
    // GetScaledCapacity reads from here instead of the live field, which prevents
    // compounding: the RefreshLaunderButton patch temporarily writes the scaled value
    // to the field, and if it were read back by GetScaledCapacity it would re-scale
    // an already-scaled value on every MinPass call.
    private static readonly Dictionary<Business, float> _baseCapacity = [];

    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(typeof(AppliedLaunderLimitPatch));
        HarmonyInstance.PatchAll(typeof(InitializePatch));
        HarmonyInstance.PatchAll(typeof(OpenPatch));
        HarmonyInstance.PatchAll(typeof(RefreshLaunderButtonPatch));
        HarmonyInstance.PatchAll(typeof(CreateEntryPatch));
        LoggerInstance.Msg("LaunderScaleMod loaded.");
    }

    internal static void UpdateCapacityLabel(LaunderingInterface ui)
    {
        float scaled = GetScaledCapacity(ui.business);
#if IL2CPP
        ui.launderCapacityLabel.text = MoneyManager.FormatAmount(scaled);
#else
        Traverse.Create(ui)
            .Field("launderCapacityLabel")
            .GetValue<TextMeshProUGUI>()
            .text = MoneyManager.FormatAmount(scaled);
#endif
    }

    public static void CacheOriginalCapacity(Business b)
    {
        if (!_baseCapacity.ContainsKey(b))
            _baseCapacity[b] = b.LaunderCapacity;
    }

    /// <summary>
    /// Returns the scaled launder capacity for a business based on player rank.
    /// - Below Peddler I: original capacity unchanged.
    /// - Peddler I and above: +5% per rank tier at or above Peddler I (1/20 per tier).
    /// - Capped at 3x the original capacity.
    /// </summary>
    public static float GetScaledCapacity(Business b)
    {
        // Always use the cached original — never the live field, which may be
        // temporarily set to the scaled value by RefreshLaunderButtonPatch.
        float original = _baseCapacity.TryGetValue(b, out float cached) ? cached : b.LaunderCapacity;

        var lm = NetworkSingleton<LevelManager>.Instance;
        if (lm == null) return original;

        var current = new FullRank(lm.Rank, lm.Tier);
        var peddlerI = new FullRank(ERank.Peddler, 1);

        if (current < peddlerI) return original;

        int tiersAbove = current.GetRankIndex() - peddlerI.GetRankIndex() + 1;
        float scaled = original * (1f + tiersAbove / 20f);
        return Mathf.Min(scaled, original * 3f);
    }
}

// Scales the effective remaining launder capacity, which drives the slider max and
// the amount selector in both this mod and LaunderMaxMod.
[HarmonyPatch(typeof(Business), nameof(Business.appliedLaunderLimit), MethodType.Getter)]
public static class AppliedLaunderLimitPatch
{
    [HarmonyPostfix]
    public static void Postfix(Business __instance, ref float __result)
    {
        __result = Core.GetScaledCapacity(__instance) - __instance.currentLaunderTotal;
    }
}

// Caches the original LaunderCapacity on Initialize (LevelManager may not be loaded
// yet here, but we need the base value before anything can modify the field).
[HarmonyPatch(typeof(LaunderingInterface), nameof(LaunderingInterface.Initialize))]
public static class InitializePatch
{
    [HarmonyPostfix]
    public static void Postfix(LaunderingInterface __instance)
    {
        Core.CacheOriginalCapacity(__instance.business);
    }
}

// Updates the capacity label each time the UI opens — LevelManager is always
// available by this point, so GetScaledCapacity returns the correct value.
[HarmonyPatch(typeof(LaunderingInterface), nameof(LaunderingInterface.Open))]
public static class OpenPatch
{
    [HarmonyPostfix]
    public static void Postfix(LaunderingInterface __instance)
    {
        Core.UpdateCapacityLabel(__instance);
    }
}

// When an operation is confirmed, StartLaunderingOperation is a ServerRpc — it doesn't
// add the operation locally until the server round-trips back via ObserversRpc. The
// vanilla ConfirmAmount calls UpdateTimeline/UpdateCurrentTotal/RefreshLaunderButton
// immediately (before the operation exists), so they show stale data until the next
// MinPass. This patch refreshes the UI when the operation actually arrives.
[HarmonyPatch(typeof(LaunderingInterface), "CreateEntry")]
public static class CreateEntryPatch
{
    [HarmonyPostfix]
    public static void Postfix(LaunderingInterface __instance)
    {
        if (__instance.isOpen)
        {
#if IL2CPP
            __instance.UpdateTimeline();
            __instance.UpdateCurrentTotal();
            __instance.RefreshLaunderButton();
#else
            Traverse.Create(__instance).Method("UpdateTimeline").GetValue();
            Traverse.Create(__instance).Method("UpdateCurrentTotal").GetValue();
            Traverse.Create(__instance).Method("RefreshLaunderButton").GetValue();
#endif
            Core.UpdateCapacityLabel(__instance);
        }
    }
}

// Temporarily swaps in the scaled capacity so the at-capacity button/message check
// uses the correct limit, then restores the original field value.
[HarmonyPatch(typeof(LaunderingInterface), "RefreshLaunderButton")]
public static class RefreshLaunderButtonPatch
{
    [HarmonyPrefix]
    public static void Prefix(LaunderingInterface __instance, out float __state)
    {
        __state = __instance.business.LaunderCapacity;
        __instance.business.LaunderCapacity = Core.GetScaledCapacity(__instance.business);
    }

    [HarmonyPostfix]
    public static void Postfix(LaunderingInterface __instance, float __state)
    {
        __instance.business.LaunderCapacity = __state;
    }
}
