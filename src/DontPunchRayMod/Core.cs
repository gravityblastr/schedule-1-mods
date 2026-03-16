using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;

#if IL2CPP
using Il2CppScheduleOne.Dialogue;
#else
using ScheduleOne.Dialogue;
#endif

[assembly: MelonInfo(typeof(DontPunchRayMod.Core), "DontPunchRayMod", "1.0.0", "gravityblastr")]
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

        Core.InjectedChoices.Clear();
        foreach (var choice in __instance.Choices)
        {
            if (choice.Enabled && choice.shouldShowCheck != null && choice.ShouldShow())
                Core.InjectedChoices.Add(choice);
        }

        Core.InjectedChoices.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        for (int i = 0; i < Core.InjectedChoices.Count; i++)
        {
            existingChoices.Add(new DialogueChoiceData
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
            Traverse.Create(__instance)
                .Field("handler")
                .GetValue<DialogueHandler>()
                .InitializeDialogue(choice.Conversation);
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
