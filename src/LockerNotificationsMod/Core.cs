using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.NPCs.CharacterClasses;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.Employees;
using ScheduleOne.GameTime;
using ScheduleOne.NPCs.CharacterClasses;
#endif

[assembly: MelonInfo(typeof(LockerNotificationsMod.Core), "GravityBlastr.LockerNotificationsMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace LockerNotificationsMod;

public class Core : MelonMod
{
    internal static Fixer? CachedFixer;
    internal static readonly System.Random Rng = new();

    // After sleep ends, we delay the check so the tick cycle can deduct wages first.
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

    private bool _subscribed;

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("LockerNotificationsMod loaded.");
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        CachedFixer = null;
        CheckAfterTime = -1f;
        PreSleepUnpayable.Clear();
        _subscribed = false;
    }

    public override void OnUpdate()
    {
        // Subscribe to TimeManager.onSleepEnd once it's available
        if (!_subscribed && NetworkSingleton<TimeManager>.InstanceExists)
        {
            var tm = NetworkSingleton<TimeManager>.Instance;
            tm.onSleepEnd += new Action(OnSleepEnd);
            _subscribed = true;
        }

        if (CheckAfterTime < 0f || Time.time < CheckAfterTime) return;
        CheckAfterTime = -1f;

        CheckAllEmployees();
        PreSleepUnpayable.Clear();
    }

    /// <summary>
    /// Fires once when sleep ends (before wages are deducted).
    /// Snapshots which employees per property can't pay, then schedules a delayed check.
    /// </summary>
    private static void OnSleepEnd()
    {
        PreSleepUnpayable.Clear();

        var employees = UnityEngine.Object.FindObjectsOfType<Employee>();
        if (employees == null) return;

        foreach (var emp in employees)
        {
            if (emp.IsPayAvailable()) continue;

            var property = emp.AssignedProperty;
            var code = property?.PropertyCode;
            if (string.IsNullOrEmpty(code)) continue;

            PreSleepUnpayable.TryGetValue(code, out int count);
            PreSleepUnpayable[code] = count + 1;
        }

        CheckAfterTime = Time.time + 10f;
    }

    internal static void CheckAllEmployees()
    {
        var employees = UnityEngine.Object.FindObjectsOfType<Employee>();
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
                CachedFixer = UnityEngine.Object.FindObjectOfType<Fixer>();
            if (CachedFixer?.MSGConversation == null) return;

            var msg = string.Format(MessageTemplates[Rng.Next(MessageTemplates.Length)], propertyNames[code]);
            CachedFixer.SendTextMessage(msg);
        }
    }
}
