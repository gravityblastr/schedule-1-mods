using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.NPCs.Relation;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.MainMenu;
using Il2CppTMPro;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.Dialogue;
using ScheduleOne.Economy;
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.NPCs.Relation;
using ScheduleOne.Persistence;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Property;
using ScheduleOne.Quests;
using ScheduleOne.UI.MainMenu;
using TMPro;
#endif

[assembly: MelonInfo(typeof(WelcomeToTheJungleMod.Core), "GravityBlastr.WelcomeToTheJungleMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace WelcomeToTheJungleMod;

public class Core : MelonMod
{
    internal static MelonLogger.Instance Log = null!;
    internal static bool IsWtjSave;

    // Quest titles to suppress in WTJ mode. Matched against Quest.Title.
    // Kept: Sink or Swim, Down to Business, The Deep End, Unfavourable Agreements,
    // cartel arc (Deal for Benzies, Deal for Cartel, Defeat Cartel).
    internal static readonly HashSet<string> SuppressedQuestTitles = new()
    {
        "Welcome to Hyland Point",
        "Getting Started",
        "On the Grind",
        "Securing Supplies",
        "Gearing Up",
        "Expanding Operations",
        "Needin' the Green",
        "Moving Up",
        "We Need To Cook",
        "Clean Cash",
        "Connections",
        "Grow Shrooms",
        "Botanists",
        "Chemists",
        "Cleaners",
        "Packagers",
        "Handlers",
        "Dodgy Dealing",
        "Mixing Mania",
        "Keeping it Fresh",
        "Making the Rounds",
        "Packin'",
        "Vibin' on the 'Cybin",
        "Money Management",
        "Wretched Hive of Scum and Villainy",
    };

    private bool _wtjApplied;
    private bool _markerChecked;
    private float _introCompletedTime;
    private bool _subscribedToSave;
    internal static bool IsNewGame;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        HarmonyInstance.PatchAll();
        LoggerInstance.Msg("WelcomeToTheJungleMod loaded.");
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        IsWtjSave = false;
        IsNewGame = false;
        _wtjApplied = false;
        _markerChecked = false;
        _introCompletedTime = 0f;
        _subscribedToSave = false;
    }

    public override void OnUpdate()
    {
        if (!Singleton<LoadManager>.InstanceExists) return;
        var lm = Singleton<LoadManager>.Instance;
        if (!lm.IsGameLoaded) return;

        // Belt-and-suspenders: if the LoadManager prefix didn't catch the marker
        // (e.g. new game where the Postfix wrote it after StartGame was called),
        // re-check once from the loaded game folder.
        if (!_markerChecked)
        {
            _markerChecked = true;
            if (!IsWtjSave && !string.IsNullOrEmpty(lm.LoadedGameFolderPath))
            {
                string markerPath = Path.Combine(lm.LoadedGameFolderPath, "WelcomeToTheJungle.json");
                if (File.Exists(markerPath))
                {
                    Log.Msg($"Marker found via OnUpdate fallback: {markerPath}");
                    IsWtjSave = true;
                }
            }
        }

        // Hook into save events to re-write marker after the whitelist cleanup.
        if (!_subscribedToSave && IsWtjSave)
        {
            var sm = Singleton<SaveManager>.Instance;
            if (sm != null)
            {
                sm.onSaveComplete.AddListener((UnityEngine.Events.UnityAction)OnSaveComplete);
                _subscribedToSave = true;
            }
        }

        if (_wtjApplied || !IsWtjSave) return;

        // Wait for the player to exist and have completed the intro.
        if (Player.Local == null || !Player.Local.HasCompletedIntro) return;

        // On new games, the IntroManager.CharacterCreationDone coroutine runs
        // ~1s after HasCompletedIntro is set. Delay 2s so our state changes
        // stick. On reloads there's no coroutine, so apply immediately.
        if (IsNewGame)
        {
            if (_introCompletedTime == 0f)
                _introCompletedTime = Time.time;
            if (Time.time - _introCompletedTime < 2f) return;
        }

        _wtjApplied = true;
        Log.Msg("Applying WTJ state (post-intro)");
        ApplyWtjState();
    }

    internal static void ApplyWtjState()
    {
        // Destroy the RV if it isn't already.
        var prop = Singleton<PropertyManager>.Instance.GetProperty("rv");
#if IL2CPP
        var rv = prop?.TryCast<RV>();
#else
        var rv = prop as RV;
#endif
        if (rv != null && !rv.IsDestroyed)
        {
            Log.Msg("Destroying RV");
            rv.SetDestroyed();
        }

        // Clear initial dead drop cash so the player truly starts with nothing.
        foreach (var drop in DeadDrop.DeadDrops)
        {
            if (drop?.Storage != null && drop.Storage.ItemCount > 0)
            {
                Log.Msg($"Clearing dead drop: {drop.DeadDropName}");
                drop.Storage.ClearContents();
            }
        }

        // Mark all suppressed quests as Completed and untracked so they don't
        // appear in the HUD. The game shows tracked+completed quests in the panel,
        // so we must untrack them too.
        for (int i = 0; i < Quest.Quests.Count; i++)
        {
            var quest = Quest.Quests[i];
            if (quest == null) continue;
            if (!SuppressedQuestTitles.Contains(quest.Title)) continue;

            if (quest.State != EQuestState.Completed)
                quest.SetQuestState(EQuestState.Completed, false);
            if (quest.IsTracked)
                quest.SetIsTracked(false);
        }

        // Unlock Albert (seed supplier) so the player can buy growing supplies.
        // Unlock Donna (motel room landlord) and Ming (sweatshop landlord) so
        // properties are available for rent without quest progression.
        foreach (var npc in NPCManager.NPCRegistry)
        {
            if (npc == null) continue;
            string name = npc.FirstName;
            if (name != "Albert" && name != "Donna" && name != "Ming") continue;
            if (npc.RelationData != null && !npc.RelationData.Unlocked)
            {
                Log.Msg($"Unlocking {npc.FirstName}");
                npc.RelationData.Unlock(NPCRelationData.EUnlockType.Recommendation, false);
            }
        }
    }

    internal static bool IsSuppressedQuest(Quest quest)
    {
        return SuppressedQuestTitles.Contains(quest.Title);
    }

    private static void OnSaveComplete()
    {
        if (!IsWtjSave) return;
        string? folder = Singleton<LoadManager>.Instance?.LoadedGameFolderPath;
        if (string.IsNullOrEmpty(folder)) return;
        string markerPath = Path.Combine(folder, "WelcomeToTheJungle.json");
        try
        {
            File.WriteAllText(markerPath, "{\"version\":1}");
            Log.Msg($"Re-wrote marker file: {markerPath}");
        }
        catch (System.Exception ex)
        {
            Log.Warning($"Failed to re-write marker: {ex.Message}");
        }
    }
}

// ── LoadManager.StartGame ──────────────────────────────────────────────
// Detect marker file and set IsWtjSave before the load coroutine runs.
[HarmonyPatch(typeof(LoadManager), nameof(LoadManager.StartGame))]
public static class LoadManagerStartGamePatch
{
    [HarmonyPrefix]
    public static void Prefix(SaveInfo info)
    {
        if (info == null) return;
        string markerPath = Path.Combine(info.SavePath, "WelcomeToTheJungle.json");
        bool exists = File.Exists(markerPath);
        Core.Log.Msg($"LoadManager.StartGame — save={info.SavePath}, marker={exists}");
        if (exists)
            Core.IsWtjSave = true;
    }
}

// ── Quest.Begin ────────────────────────────────────────────────────────
// Block activation of suppressed quests.
[HarmonyPatch(typeof(Quest), nameof(Quest.Begin))]
public static class SuppressQuestBeginPatch
{
    [HarmonyPrefix]
    public static bool Prefix(Quest __instance)
    {
        if (!Core.IsWtjSave) return true;
        if (Core.IsSuppressedQuest(__instance))
        {
            Core.Log.Msg($"Blocking Begin for quest: {__instance.Title}");
            return false;
        }
        return true;
    }
}

// ── Quest.SetQuestState ────────────────────────────────────────────────
// Block attempts to set a suppressed quest to Active (e.g. from save load).
[HarmonyPatch(typeof(Quest), nameof(Quest.SetQuestState))]
public static class SuppressQuestActivationPatch
{
    [HarmonyPrefix]
    public static bool Prefix(Quest __instance, EQuestState state)
    {
        if (!Core.IsWtjSave) return true;
        if (state == EQuestState.Active && Core.IsSuppressedQuest(__instance))
        {
            Core.Log.Msg($"Blocking SetQuestState(Active) for quest: {__instance.Title}");
            return false;
        }
        return true;
    }
}

// ── UncleNelson.SendInitialMessage ─────────────────────────────────────
// Suppress the "go find a payphone" text message.
[HarmonyPatch(typeof(UncleNelson), nameof(UncleNelson.SendInitialMessage))]
public static class NelsonMessagePatch
{
    [HarmonyPrefix]
    public static bool Prefix() => !Core.IsWtjSave;
}

// ── SetupScreen.StartGame ──────────────────────────────────────────────
// Force skip-intro, write marker + player files so the save boots in WTJ mode.
[HarmonyPatch(typeof(SetupScreen), nameof(SetupScreen.StartGame))]
public static class SetupScreenStartGamePatch
{
    [HarmonyPrefix]
    public static void Prefix(SetupScreen __instance)
    {
        __instance.SkipIntroToggle.isOn = true;
    }

    [HarmonyPostfix]
    public static void Postfix(SetupScreen __instance)
    {
        // Read the private slotIndex field directly — more reliable than
        // capturing it via a separate Initialize patch.
        int slotIndex = Traverse.Create(__instance).Field("slotIndex").GetValue<int>();
        string savePath = Path.Combine(
            Singleton<SaveManager>.Instance.IndividualSavesContainerPath,
            "SaveGame_" + (slotIndex + 1));

        Core.Log.Msg($"SetupScreen.StartGame Postfix — slotIndex={slotIndex}, savePath={savePath}");

        if (!Directory.Exists(savePath))
        {
            Core.Log.Warning($"Save folder does not exist: {savePath}");
            return;
        }

        // Write marker file.
        string markerPath = Path.Combine(savePath, "WelcomeToTheJungle.json");
        File.WriteAllText(markerPath, "{}");
        Core.Log.Msg($"Wrote marker: {markerPath}");

        Core.IsWtjSave = true;
        Core.IsNewGame = true;
    }
}

// ── SetupScreen.Start ──────────────────────────────────────────────────
// Restyle the new-game UI: replace "Skip intro" with "Welcome to the Jungle".
[HarmonyPatch(typeof(SetupScreen), "Start")]
public static class SetupScreenStartPatch
{
    [HarmonyPostfix]
    public static void Postfix(SetupScreen __instance)
    {
        __instance.SkipIntroToggle.onValueChanged.RemoveAllListeners();
        __instance.SkipIntroToggle.isOn = true;
        __instance.SkipIntroToggle.interactable = false;

        // Hide the toggle checkbox graphic entirely.
        var toggleGraphic = __instance.SkipIntroToggle.graphic;
        if (toggleGraphic != null)
            toggleGraphic.gameObject.SetActive(false);
        var toggleBg = __instance.SkipIntroToggle.targetGraphic;
        if (toggleBg != null)
            toggleBg.gameObject.SetActive(false);

        foreach (var text in __instance.SkipIntroContainer
            .GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string lower = text.text.ToLowerInvariant();
            if (lower.Contains("recommend") || lower.Contains("new player"))
            {
                text.gameObject.SetActive(false);
            }
            else if (lower.Contains("skip") || lower.Contains("intro") || lower.Contains("tutorial"))
            {
                text.text = "Welcome to the Jungle";
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.alignment = TextAlignmentOptions.Center;
                // Expand the RectTransform to fill the container so centering works.
                var rt = text.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
                    rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
                    rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
                    rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
                }
            }
        }
    }
}

// ── DialogueController_Ming.CanBuyRoom ─────────────────────────────────
// Bypass quest gating so property rooms can be purchased without tutorial quests.
[HarmonyPatch(typeof(DialogueController_Ming), "CanBuyRoom")]
public static class RoomPurchasePatch
{
    [HarmonyPostfix]
    public static void Postfix(DialogueController_Ming __instance, ref bool __result, bool __0)
    {
        string propCode = __instance.Property != null ? __instance.Property.PropertyCode : "null";
        bool isOwned = __instance.Property != null && __instance.Property.IsOwned;
        string npcName = "unknown";
        try { npcName = Traverse.Create(__instance).Field("npc").GetValue<NPC>()?.FirstName ?? "null"; } catch { }
        int questCount = __instance.PurchaseRoomQuests != null ? __instance.PurchaseRoomQuests.Length : -1;
        Core.Log.Msg($"[RoomPurchasePatch] NPC={npcName} Property={propCode} IsOwned={isOwned} " +
                     $"IsWtj={Core.IsWtjSave} OrigResult={__result} EnabledArg={__0} QuestCount={questCount}");

        if (__instance.PurchaseRoomQuests != null)
        {
            for (int i = 0; i < __instance.PurchaseRoomQuests.Length; i++)
            {
                var q = __instance.PurchaseRoomQuests[i];
                Core.Log.Msg($"  Quest[{i}]: Title={q?.ParentQuest?.Title ?? "null"} EntryState={q?.State}");
            }
        }

        if (!Core.IsWtjSave) return;
        __result = !isOwned;
        Core.Log.Msg($"  -> Override result to {__result}");
    }
}


