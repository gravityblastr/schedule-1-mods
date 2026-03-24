using HarmonyLib;
using MelonLoader;
using UnityEngine.UI;

#if IL2CPP
using Il2CppTMPro;
using Il2CppScheduleOne.UI;
#else
using TMPro;
using ScheduleOne.UI;
#endif

[assembly: MelonInfo(typeof(LaunderMaxMod.Core), "LaunderMaxMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace LaunderMaxMod;

public class Core : MelonMod
{
    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(typeof(OpenAmountSelectorPatch));
        LoggerInstance.Msg("LaunderMaxMod loaded.");
    }
}

[HarmonyPatch(typeof(LaunderingInterface), nameof(LaunderingInterface.OpenAmountSelector))]
public static class OpenAmountSelectorPatch
{
    [HarmonyPostfix]
    public static void Postfix(LaunderingInterface __instance)
    {
#if IL2CPP
        int max = __instance.maxLaunderAmount;
        __instance.selectedAmountToLaunder = max;
        __instance.amountSlider.SetValueWithoutNotify(max);
        __instance.amountInputField.SetTextWithoutNotify(max.ToString());
#else
        var t = Traverse.Create(__instance);
        int max = t.Property("maxLaunderAmount").GetValue<int>();
        t.Field("selectedAmountToLaunder").SetValue(max);
        t.Field("amountSlider").GetValue<Slider>().SetValueWithoutNotify(max);
        t.Field("amountInputField").GetValue<TMP_InputField>().SetTextWithoutNotify(max.ToString());
#endif
    }
}
