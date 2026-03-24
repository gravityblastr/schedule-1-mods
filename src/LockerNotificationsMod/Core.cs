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

    // After OnSleepEnd, we delay the check so the tick cycle can deduct wages first.
    // OnSleepEnd fires before wages are deducted; the tick processes them shortly after.
    internal static float CheckAfterTime = -1f;
    // Properties that were already unable to pay BEFORE today's wages were deducted.
    // We only notify for properties that become unable to pay after deduction.
    internal static readonly HashSet<string> AlreadyEmptyProperties = new();

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
        HarmonyInstance.PatchAll(typeof(SleepEndPatch));
        LoggerInstance.Msg("LockerNotificationsMod loaded.");
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        CachedFixer = null;
        NotifiedProperties.Clear();
        CheckAfterTime = -1f;
        AlreadyEmptyProperties.Clear();
    }

    public override void OnUpdate()
    {
        if (CheckAfterTime < 0f || Time.time < CheckAfterTime) return;
        CheckAfterTime = -1f;

        NotifiedProperties.Clear();
        CheckAllEmployees();
        AlreadyEmptyProperties.Clear();
    }

    internal static void CheckAllEmployees()
    {
        var employees = Object.FindObjectsOfType<Employee>();
        if (employees == null) return;

        foreach (var emp in employees)
        {
            // IsPayAvailable() calls GetHome() in native code where virtual
            // dispatch works correctly on both Mono and IL2CPP.
            // After wages are deducted, IsPayAvailable() == false means the
            // locker can't cover tomorrow's wages.
            if (emp.IsPayAvailable()) continue;

            var property = emp.AssignedProperty;
            if (property == null) continue;

            var propertyCode = property.PropertyCode;
            if (string.IsNullOrEmpty(propertyCode)) continue;

            // Skip properties that were already empty before today's wages
            if (AlreadyEmptyProperties.Contains(propertyCode)) continue;

            if (!NotifiedProperties.Add(propertyCode)) continue;

            if (CachedFixer == null)
                CachedFixer = Object.FindObjectOfType<Fixer>();
            if (CachedFixer?.MSGConversation == null) return;

            var propertyName = property.PropertyName;
            var msg = string.Format(MessageTemplates[Rng.Next(MessageTemplates.Length)], propertyName);
            CachedFixer.SendTextMessage(msg);
        }
    }
}

/// <summary>
/// When sleep ends, record which properties are already unable to pay, then
/// schedule a delayed check. The delay allows the tick cycle to process wage
/// deductions before we check balances. Only properties that become unable
/// to pay AFTER deduction trigger a notification.
/// </summary>
[HarmonyPatch(typeof(Employee), "OnSleepEnd")]
public static class SleepEndPatch
{
    [HarmonyPostfix]
    public static void Postfix(Employee __instance)
    {
        // Record properties that can't pay BEFORE wages are deducted.
        // These were already empty and shouldn't trigger a new notification.
        if (!__instance.IsPayAvailable())
        {
            var prop = __instance.AssignedProperty;
            var code = prop?.PropertyCode;
            if (!string.IsNullOrEmpty(code))
                Core.AlreadyEmptyProperties.Add(code);
        }

        // Only schedule once per sleep (fires per employee)
        if (Core.CheckAfterTime > 0f) return;
        Core.CheckAfterTime = Time.time + 10f;
    }
}
