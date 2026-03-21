using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

#if IL2CPP
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.NPCs.CharacterClasses;
#else
using ScheduleOne.Employees;
using ScheduleOne.NPCs.CharacterClasses;
#endif

[assembly: MelonInfo(typeof(LockerNotificationsMod.Core), "LockerNotificationsMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace LockerNotificationsMod;

public class Core : MelonMod
{
    internal static readonly HashSet<string> NotifiedProperties = new();
    internal static Fixer? CachedFixer;
    internal static readonly System.Random Rng = new();

    internal static readonly string[] MessageTemplates =
    {
        "Heads up. Lockers at the {0} are running low - your crew might not get paid tomorrow.",
        "Yo, the lockers at the {0} are almost dry. Might want to top them off.",
        "Just a heads up - the {0} lockers can't cover tomorrow's wages.",
        "Money's tight at the {0}. Your people aren't gonna pay themselves.",
        "Running low at the {0}. If the lockers don't get refilled, someone's not getting paid.",
    };

    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(typeof(SetIsPaidPatch));
        HarmonyInstance.PatchAll(typeof(SleepEndPatch));
        LoggerInstance.Msg("LockerNotificationsMod loaded.");
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        CachedFixer = null;
        NotifiedProperties.Clear();
    }
}

/// <summary>
/// After an employee's daily wage is deducted and they're marked as paid,
/// check if their locker has enough left for tomorrow. If not, send a
/// text message from the Fixer (Manny) — one per property per day.
/// </summary>
[HarmonyPatch(typeof(Employee), nameof(Employee.SetIsPaid))]
public static class SetIsPaidPatch
{
    [HarmonyPostfix]
    public static void Postfix(Employee __instance)
    {
        var home = __instance.GetHome();
        if (home == null) return;

        // Still enough cash for tomorrow — no notification needed
        if (home.GetCashSum() >= __instance.DailyWage) return;

        var property = __instance.AssignedProperty;
        if (property == null) return;

        var propertyCode = property.PropertyCode;
        if (string.IsNullOrEmpty(propertyCode)) return;

        // Only one notification per property per day
        if (!Core.NotifiedProperties.Add(propertyCode)) return;

        // Find the Fixer NPC (Manny) to send the text message
        if (Core.CachedFixer == null)
            Core.CachedFixer = Object.FindObjectOfType<Fixer>();
        if (Core.CachedFixer?.MSGConversation == null) return;

        var propertyName = property.PropertyName;
        var msg = string.Format(Core.MessageTemplates[Core.Rng.Next(Core.MessageTemplates.Length)], propertyName);
        Core.CachedFixer.SendTextMessage(msg);
    }
}

/// <summary>
/// Reset the per-property notification tracking at the start of each new day.
/// </summary>
[HarmonyPatch(typeof(Employee), "OnSleepEnd")]
public static class SleepEndPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        Core.NotifiedProperties.Clear();
    }
}
