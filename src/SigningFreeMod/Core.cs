using HarmonyLib;
using MelonLoader;

#if IL2CPP
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.UI.Phone.ContactsApp;
#else
using ScheduleOne.Dialogue;
using ScheduleOne.UI.Phone.ContactsApp;
#endif

[assembly: MelonInfo(typeof(SigningFreeMod.Core), "SigningFreeMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace SigningFreeMod;

public class Core : MelonMod
{
    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(typeof(ContactsDetailPanelPatch));
        HarmonyInstance.PatchAll(typeof(DialogueTextPatch));
        HarmonyInstance.PatchAll(typeof(ChoiceTextPatch));
        LoggerInstance.Msg("SigningFreeMod loaded.");
    }
}

// Fix the typo in the phone contacts app (for unrecruited dealers).
[HarmonyPatch(typeof(ContactsDetailPanel), nameof(ContactsDetailPanel.Open))]
public static class ContactsDetailPanelPatch
{
    [HarmonyPostfix]
    public static void Postfix(ContactsDetailPanel __instance)
    {
        var label = __instance.UnlockHintLabel;
        if (label != null && label.text != null && label.text.Contains("signing free"))
            label.text = label.text.Replace("signing free", "signing fee");
    }
}

// Fix the typo in dialogue node text (e.g. Fixer's FINALIZE node for employee hiring).
// Serialized dialogue assets can contain the typo; this catches it at display time.
[HarmonyPatch(typeof(DialogueController), nameof(DialogueController.ModifyDialogueText))]
public static class DialogueTextPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref string __result)
    {
        if (__result != null && __result.Contains("signing free"))
            __result = __result.Replace("signing free", "signing fee");
    }
}

// Fix the typo in dialogue choice text, in case any choices also contain it.
[HarmonyPatch(typeof(DialogueController), nameof(DialogueController.ModifyChoiceText))]
public static class ChoiceTextPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref string __result)
    {
        if (__result != null && __result.Contains("signing free"))
            __result = __result.Replace("signing free", "signing fee");
    }
}
