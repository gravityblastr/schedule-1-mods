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

[assembly: MelonInfo(typeof(LockerNotificationsMod.Core), "GravityBlastr.LockerNotificationsMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace LockerNotificationsMod;

public class Core : MelonMod
{
    internal static Fixer? CachedFixer;
    internal static readonly System.Random Rng = new();

    // After OnSleepEnd, we delay the check so the tick cycle can deduct wages first.
    // OnSleepEnd fires before wages are deducted; the tick processes them shortly after.
    internal static float CheckAfterTime = -1f;
    // Count of employees per property that were already unable to pay BEFORE today's
    // wages were deducted. We only notify if MORE employees can't pay after deduction.
    internal static readonly Dictionary<string, int> PreSleepUnpayable = new();

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
        CheckAfterTime = -1f;
        PreSleepUnpayable.Clear();
    }

    public override void OnUpdate()
    {
        if (CheckAfterTime < 0f || Time.time < CheckAfterTime) return;
        CheckAfterTime = -1f;

        CheckAllEmployees();
        PreSleepUnpayable.Clear();
    }

    internal static void CheckAllEmployees()
    {
        var employees = Object.FindObjectsOfType<Employee>();
        if (employees == null) return;

        // Count how many employees per property can't pay now (after wages deducted)
        var postSleepUnpayable = new Dictionary<string, int>();
        var propertyNames = new Dictionary<string, string>();

        foreach (var emp in employees)
        {
            if (emp.IsPayAvailable()) continue;

            var property = emp.AssignedProperty;
            if (property == null) continue;

            var propertyCode = property.PropertyCode;
            if (string.IsNullOrEmpty(propertyCode)) continue;

            postSleepUnpayable.TryGetValue(propertyCode, out int count);
            postSleepUnpayable[propertyCode] = count + 1;
            propertyNames[propertyCode] = property.PropertyName;
        }

        // Only notify for properties where MORE employees can't pay than before sleep
        foreach (var (code, postCount) in postSleepUnpayable)
        {
            PreSleepUnpayable.TryGetValue(code, out int preCount);
            if (postCount <= preCount) continue;

            if (CachedFixer == null)
                CachedFixer = Object.FindObjectOfType<Fixer>();
            if (CachedFixer?.MSGConversation == null) return;

            var msg = string.Format(MessageTemplates[Rng.Next(MessageTemplates.Length)], propertyNames[code]);
            CachedFixer.SendTextMessage(msg);
        }
    }
}

/// <summary>
/// When sleep ends, count employees per property that are already unable to pay,
/// then schedule a delayed check. The delay allows the tick cycle to process wage
/// deductions before we check balances. Only properties where the unpayable count
/// INCREASES after deduction trigger a notification.
/// </summary>
[HarmonyPatch(typeof(Employee), "OnSleepEnd")]
public static class SleepEndPatch
{
    [HarmonyPostfix]
    public static void Postfix(Employee __instance)
    {
        // Count employees per property that can't pay BEFORE wages are deducted.
        if (!__instance.IsPayAvailable())
        {
            var prop = __instance.AssignedProperty;
            var code = prop?.PropertyCode;
            if (!string.IsNullOrEmpty(code))
            {
                Core.PreSleepUnpayable.TryGetValue(code, out int count);
                Core.PreSleepUnpayable[code] = count + 1;
            }
        }

        // Only schedule once per sleep (fires per employee)
        if (Core.CheckAfterTime > 0f) return;
        Core.CheckAfterTime = Time.time + 10f;
    }
}
