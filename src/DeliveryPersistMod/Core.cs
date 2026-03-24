using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

#if IL2CPP
using System.Text.Json;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI.Phone.Delivery;
using SProperty = Il2CppScheduleOne.Property.Property;
#else
using Newtonsoft.Json;
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
using ScheduleOne.UI.Phone.Delivery;
using SProperty = ScheduleOne.Property.Property;
#endif

[assembly: MelonInfo(typeof(DeliveryPersistMod.Core), "DeliveryPersistMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DeliveryPersistMod;

public class Core : MelonMod
{
    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(typeof(ResetCartPatch));
        HarmonyInstance.PatchAll(typeof(StartPatch));
        HarmonyInstance.PatchAll(typeof(RefreshDestinationUIPatch));
        LoggerInstance.Msg("DeliveryPersistMod loaded.");
    }

    // --- Save-file persistence ---
    // Writes a standalone JSON file inside the game's save folder.
    // The game ignores unknown files, so removing the mod leaves saves intact.

    private const string FileName = "DeliveryPersistMod.json";
    private static bool _subscribedToSave;
    private static bool _subscribedToLoad;

    private class ShopState
    {
        public string Dest { get; set; } = "";
        public int Dock { get; set; }
        public Dictionary<string, int> Qty { get; set; } = new Dictionary<string, int>();
    }

    // Loaded once from disk on game load; never overwritten from disk again during the session.
    private static Dictionary<string, ShopState>? _savedData;

    private static string? GetFilePath()
    {
        string? folder = Singleton<LoadManager>.Instance?.LoadedGameFolderPath;
        return string.IsNullOrEmpty(folder) ? null : Path.Combine(folder, FileName);
    }

    private static Dictionary<string, ShopState> ReadFromDisk()
    {
        string? path = GetFilePath();
        if (path == null || !File.Exists(path))
            return new Dictionary<string, ShopState>();
        try
        {
            string json = File.ReadAllText(path);
#if IL2CPP
            return JsonSerializer.Deserialize<Dictionary<string, ShopState>>(json)
                   ?? new Dictionary<string, ShopState>();
#else
            return JsonConvert.DeserializeObject<Dictionary<string, ShopState>>(json)
                   ?? new Dictionary<string, ShopState>();
#endif
        }
        catch
        {
            return new Dictionary<string, ShopState>();
        }
    }

    private static string SerializeToJson(object obj)
    {
#if IL2CPP
        return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
#else
        return JsonConvert.SerializeObject(obj, Formatting.Indented);
#endif
    }

    /// <summary>
    /// Returns saved state for a shop. Only reads from disk once per game load.
    /// </summary>
    public static (string destCode, int dock, Dictionary<string, int> quantities) GetSavedState(string shopName)
    {
        if (_savedData == null)
            _savedData = ReadFromDisk();

        if (!_savedData.TryGetValue(shopName, out var state))
            return ("", 0, new Dictionary<string, int>());

        return (state.Dest, state.Dock, new Dictionary<string, int>(state.Qty));
    }

    /// <summary>
    /// Called on game save. Reads current UI state from all live DeliveryShops and writes to disk.
    /// </summary>
    private static void OnGameSave()
    {
        var app = PlayerSingleton<DeliveryApp>.Instance;
        if (app == null) return;

        var shops = app.GetComponentsInChildren<DeliveryShop>(includeInactive: true);
        var data = new Dictionary<string, ShopState>();

        foreach (var shop in shops)
        {
            var state = new ShopState();

#if IL2CPP
            state.Dest = shop.destinationProperty?.PropertyCode ?? "";
            state.Dock = shop.loadingDockIndex;
            var entries = shop.listingEntries;
#else
            var t = Traverse.Create(shop);
            var dest = t.Field("destinationProperty").GetValue<SProperty>();
            state.Dest = dest?.PropertyCode ?? "";
            state.Dock = t.Field("loadingDockIndex").GetValue<int>();
            var entries = t.Field("listingEntries").GetValue<List<ListingEntry>>();
#endif
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    if (e.SelectedQuantity > 0)
                        state.Qty[e.MatchingListing.Item.ID] = e.SelectedQuantity;
                }
            }

            data[shop.MatchingShopInterfaceName] = state;
        }

        string? path = GetFilePath();
        if (path == null) return;
        try
        {
            File.WriteAllText(path, SerializeToJson(data));
            _savedData = data;
        }
        catch (Exception ex)
        {
            Melon<Core>.Logger.Warning($"Failed to write {FileName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Called after all save data is loaded. Items are now unlocked, so quantities and
    /// destinations can be restored. Destinations must be restored eagerly here — not
    /// deferred to RefreshDestinationUI — because a save can fire before the player
    /// opens the delivery app, which would overwrite good data with empty fields.
    /// </summary>
    private static void OnLoadComplete()
    {
        var app = PlayerSingleton<DeliveryApp>.Instance;
        if (app == null) return;

        var shops = app.GetComponentsInChildren<DeliveryShop>(includeInactive: true);
        foreach (var shop in shops)
        {
            var (destCode, dock, quantities) = GetSavedState(shop.MatchingShopInterfaceName);

            // Restore destination property and dock
            if (!string.IsNullOrEmpty(destCode))
            {
                SProperty? prop = null;
                foreach (var p in SProperty.OwnedProperties)
                {
                    if (p.PropertyCode == destCode) { prop = p; break; }
                }
                if (prop != null)
                {
#if IL2CPP
                    shop.destinationProperty = prop;
                    shop.loadingDockIndex = dock;
#else
                    var t = Traverse.Create(shop);
                    t.Field("destinationProperty").SetValue(prop);
                    t.Field("loadingDockIndex").SetValue(dock);
#endif
                }
            }

            // Restore quantities
            if (quantities.Count == 0) continue;

#if IL2CPP
            var entries = shop.listingEntries;
#else
            var entries = Traverse.Create(shop).Field("listingEntries").GetValue<List<ListingEntry>>();
#endif
            foreach (var entry in entries)
            {
                if (quantities.TryGetValue(entry.MatchingListing.Item.ID, out int qty))
                    entry.SetQuantity(qty, notify: false);
            }

#if IL2CPP
            shop.RefreshCart();
#else
            Traverse.Create(shop).Method("RefreshCart").GetValue();
#endif
        }
    }

    internal static void EnsureHooks()
    {
        if (!_subscribedToSave)
        {
            var sm = Singleton<SaveManager>.Instance;
            if (sm != null)
            {
                sm.onSaveComplete.AddListener((UnityEngine.Events.UnityAction)OnGameSave);
                _subscribedToSave = true;
            }
        }
        if (!_subscribedToLoad)
        {
            var lm = Singleton<LoadManager>.Instance;
            if (lm != null)
            {
                lm.onLoadComplete.AddListener((UnityEngine.Events.UnityAction)OnLoadComplete);
                _subscribedToLoad = true;
            }
        }
    }

    /// <summary>Reset cached data so it will be re-read from the new save's file.</summary>
    internal static void InvalidateCache()
    {
        _savedData = null;
    }
}

/// <summary>
/// Skips clearing quantities after order submit; still refreshes the UI labels.
/// </summary>
[HarmonyPatch(typeof(DeliveryShop), "ResetCart")]
public static class ResetCartPatch
{
    [HarmonyPrefix]
    public static bool Prefix(DeliveryShop __instance)
    {
#if IL2CPP
        __instance.RefreshCart();
        __instance.RefreshOrderButton();
#else
        var t = Traverse.Create(__instance);
        t.Method("RefreshCart").GetValue();
        t.Method("RefreshOrderButton").GetValue();
#endif
        return false; // skip original
    }
}

/// <summary>
/// Subscribe to save/load hooks on first DeliveryShop.Start. Invalidate cache for fresh read.
/// </summary>
[HarmonyPatch(typeof(DeliveryShop), "Start")]
public static class StartPatch
{
    [HarmonyPostfix]
    public static void Postfix(DeliveryShop __instance)
    {
        Core.EnsureHooks();
        Core.InvalidateCache();
    }
}

/// <summary>
/// Before RefreshDestinationUI rebuilds the dropdown, pre-set destinationProperty
/// from saved state so the method's own match-restore logic picks it up.
/// </summary>
[HarmonyPatch(typeof(DeliveryShop), "RefreshDestinationUI")]
public static class RefreshDestinationUIPatch
{
    [HarmonyPrefix]
    public static void Prefix(DeliveryShop __instance)
    {
        // Only restore if nothing is selected yet (don't override user's in-session choice)
#if IL2CPP
        if (__instance.destinationProperty != null) return;
#else
        if (Traverse.Create(__instance).Field("destinationProperty").GetValue<SProperty>() != null) return;
#endif

        var (destCode, dock, _) = Core.GetSavedState(__instance.MatchingShopInterfaceName);
        if (string.IsNullOrEmpty(destCode)) return;

        SProperty? prop = null;
        foreach (var p in SProperty.OwnedProperties)
        {
            if (p.PropertyCode == destCode) { prop = p; break; }
        }
        if (prop == null) return;

#if IL2CPP
        __instance.destinationProperty = prop;
        __instance.loadingDockIndex = dock;
#else
        var t = Traverse.Create(__instance);
        t.Field("destinationProperty").SetValue(prop);
        t.Field("loadingDockIndex").SetValue(dock);
#endif
    }
}
