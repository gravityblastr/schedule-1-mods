using HarmonyLib;
using MelonLoader;

#if IL2CPP
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;
#else
using ScheduleOne.ObjectScripts;
using ScheduleOne.Product;
#endif

[assembly: MelonInfo(typeof(UnpackMod.Core), "GravityBlastr.UnpackMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UnpackMod;

public class Core : MelonMod
{
    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(typeof(GetStatePatch));
        LoggerInstance.Msg("UnpackMod loaded.");
    }
}

/// <summary>
/// When unpacking a brick, skip the "packaging slot full" check.
/// Bricks don't return packaging to the slot (unlike jars/baggies),
/// so the check is unnecessary and blocks valid unpack operations.
/// </summary>
[HarmonyPatch(typeof(PackagingStation), nameof(PackagingStation.GetState))]
public static class GetStatePatch
{
    [HarmonyPostfix]
    public static void Postfix(
        PackagingStation __instance,
        PackagingStation.EMode mode,
        ref PackagingStation.EState __result)
    {
        if (mode != PackagingStation.EMode.Unpackage) return;
        if (__result != PackagingStation.EState.PackageSlotFull) return;

#if IL2CPP
        var product = __instance.OutputSlot.ItemInstance?.TryCast<ProductItemInstance>();
#else
        var product = __instance.OutputSlot.ItemInstance as ProductItemInstance;
#endif
        if (product?.AppliedPackaging?.ID != "brick") return;

        // Brick doesn't use the packaging slot — only check product slot capacity
        int quantity = product.AppliedPackaging.Quantity;
#if IL2CPP
        var copy = __instance.OutputSlot.ItemInstance!.GetCopy(1)!.TryCast<ProductItemInstance>()!;
#else
        var copy = __instance.OutputSlot.ItemInstance.GetCopy(1) as ProductItemInstance;
#endif
        copy!.SetPackaging(null);

        __result = __instance.ProductSlot.GetCapacityForItem(copy) < quantity
            ? PackagingStation.EState.ProductSlotFull
            : PackagingStation.EState.CanBegin;
    }
}
