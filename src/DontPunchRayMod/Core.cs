using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

#if IL2CPP
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.Economy;
#else
using ScheduleOne.Dialogue;
using ScheduleOne.Economy;
#endif

[assembly: MelonInfo(typeof(DontPunchRayMod.Core), "GravityBlastr.DontPunchRayMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DontPunchRayMod;

public class Core : MelonMod
{
    /// <summary>
    /// Choices most recently injected into an override dialogue ENTRY node.
    /// Read by the callback and check patches to handle player selection.
    /// </summary>
    internal static List<DialogueController.DialogueChoice> InjectedChoices = [];

    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(typeof(ModifyChoiceListPatch));
        HarmonyInstance.PatchAll(typeof(ChoiceCallbackPatch));
        HarmonyInstance.PatchAll(typeof(CheckChoicePatch));
        LoggerInstance.Msg("DontPunchRayMod loaded.");
    }
}

// When the dialogue system shows an override container's ENTRY node, inject any
// programmatic choices (e.g. Customer's "offer deal" / "free sample") that would
// normally only appear in the generic dialogue path.
[HarmonyPatch(typeof(DialogueController), nameof(DialogueController.ModifyChoiceList))]
public static class ModifyChoiceListPatch
{
    [HarmonyPostfix]
    public static void Postfix(
        DialogueController __instance,
        string dialogueLabel,
        ref List<DialogueChoiceData> existingChoices)
    {
        if (DialogueHandler.activeDialogue == __instance.GenericDialogue)
            return;
        if (dialogueLabel != "ENTRY")
            return;

        // Only inject choices for NPCs that are Customers (store vendors).
        // DialogueController lives on a child "Dialogue" GameObject, so search up
        // the hierarchy. Other NPCs (e.g. Donna Martin) don't have a Customer
        // component and shouldn't get choices injected. (fixes #10)
        if (__instance.GetComponentInParent<Customer>() == null)
            return;

        Core.InjectedChoices.Clear();
        foreach (var choice in __instance.Choices)
        {
            if (choice.Enabled && choice.shouldShowCheck != null && choice.ShouldShow())
                Core.InjectedChoices.Add(choice);
        }

        Core.InjectedChoices.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        // Under IL2CPP, the ref List<DialogueChoiceData> parameter is not marshalled
        // correctly by Harmony. Instead, modify handler.CurrentChoices directly —
        // that's the actual list the caller uses after ModifyChoiceList returns.
#if IL2CPP
        var choiceList = __instance.handler.CurrentChoices;
#else
        var choiceList = existingChoices;
#endif

        // Build a set of existing choice texts so we can skip duplicates.
        // Override dialogues may already contain choices that also exist as
        // programmatic choices (e.g. "You wanna buy something?"). (fixes #9)
        var existingTexts = new HashSet<string>();
        foreach (var existing in choiceList)
            existingTexts.Add(existing.ChoiceText);

        for (int i = 0; i < Core.InjectedChoices.Count; i++)
        {
            if (existingTexts.Contains(Core.InjectedChoices[i].ChoiceText))
                continue;

            choiceList.Add(new DialogueChoiceData
            {
                ChoiceText = Core.InjectedChoices[i].ChoiceText,
                ChoiceLabel = "DPRAY_" + i,
                ShowWorldspaceDialogue = Core.InjectedChoices[i].ShowWorldspaceDialogue
            });
        }
    }
}

// When the player picks one of our injected choices, fire its onChoosen event
// and optionally start its conversation.
[HarmonyPatch(typeof(DialogueController), nameof(DialogueController.ChoiceCallback))]
public static class ChoiceCallbackPatch
{
    [HarmonyPostfix]
    public static void Postfix(DialogueController __instance, string choiceLabel)
    {
        if (!choiceLabel.StartsWith("DPRAY_"))
            return;

        int index = int.Parse(choiceLabel.Substring("DPRAY_".Length));
        if (index < 0 || index >= Core.InjectedChoices.Count)
            return;

        var choice = Core.InjectedChoices[index];
        choice.onChoosen?.Invoke();
        if (choice.Conversation != null)
        {
#if IL2CPP
            __instance.handler.InitializeDialogue(choice.Conversation);
#else
            Traverse.Create(__instance)
                .Field("handler")
                .GetValue<DialogueHandler>()
                .InitializeDialogue(choice.Conversation);
#endif
        }
    }
}

// Validate our injected choices via their isValidCheck delegate.
[HarmonyPatch(typeof(DialogueController), nameof(DialogueController.CheckChoice))]
public static class CheckChoicePatch
{
    [HarmonyPostfix]
    public static void Postfix(
        DialogueController __instance,
        string choiceLabel,
        ref string invalidReason,
        ref bool __result)
    {
        if (!choiceLabel.StartsWith("DPRAY_"))
            return;

        int index = int.Parse(choiceLabel.Substring("DPRAY_".Length));
        if (index >= 0 && index < Core.InjectedChoices.Count)
        {
            __result = Core.InjectedChoices[index].IsValid(out invalidReason);
        }
    }
}
