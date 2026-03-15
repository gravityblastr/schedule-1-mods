using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI.Phone;
using ScheduleOne.Vehicles;
using ScheduleOne.Vision;
using UnityEngine;

[assembly: MelonInfo(typeof(LightPersistMod.Core), "LightPersistMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace LightPersistMod;

public class Core : MelonMod
{
    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(typeof(PhoneStartPatch));
        LoggerInstance.Msg("LightPersistMod loaded.");
    }

    private const string FileName = "LightPersistMod.json";
    private static bool _subscribedToSave;
    private static bool _subscribedToLoad;

    private class LightState
    {
        public bool Flashlight;
        public Dictionary<string, bool> VehicleHeadlights = new Dictionary<string, bool>();
    }

    private static LightState? _savedData;

    private static string? GetFilePath()
    {
        string? folder = Singleton<LoadManager>.Instance?.LoadedGameFolderPath;
        return string.IsNullOrEmpty(folder) ? null : Path.Combine(folder, FileName);
    }

    private static LightState ReadFromDisk()
    {
        string? path = GetFilePath();
        if (path == null || !File.Exists(path))
            return new LightState();
        try
        {
            return JsonConvert.DeserializeObject<LightState>(File.ReadAllText(path))
                   ?? new LightState();
        }
        catch
        {
            return new LightState();
        }
    }

    private static void OnGameSave()
    {
        var state = new LightState();

        // Flashlight
        var phone = PlayerSingleton<Phone>.Instance;
        if (phone != null)
            state.Flashlight = phone.FlashlightOn;

        // Vehicle headlights
        var vm = NetworkSingleton<VehicleManager>.Instance;
        if (vm != null)
        {
            foreach (var vehicle in vm.AllVehicles)
            {
                if (vehicle == null || vehicle.GUID == Guid.Empty) continue;
                var lights = vehicle.GetComponent<VehicleLights>();
                if (lights != null)
                    state.VehicleHeadlights[vehicle.GUID.ToString()] = lights.HeadlightsOn;
            }
        }

        string? path = GetFilePath();
        if (path == null) return;
        try
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(state, Formatting.Indented));
            _savedData = state;
        }
        catch (Exception ex)
        {
            Melon<Core>.Logger.Warning($"Failed to write {FileName}: {ex.Message}");
        }
    }

    private static void OnLoadComplete()
    {
        _savedData = ReadFromDisk();

        // Restore flashlight
        if (_savedData.Flashlight)
        {
            var phone = PlayerSingleton<Phone>.Instance;
            if (phone != null && !phone.FlashlightOn)
            {
                // Set the property via Traverse (protected setter)
                Traverse.Create(phone).Property("FlashlightOn").SetValue(true);

                // Sync the visual light to other players
                Player.Local?.SetFlashlightOn_Server(true);

                // Restore visibility attribute so NPCs react correctly
                var visAttr = Traverse.Create(phone).Field("flashlightVisibility")
                    .GetValue<VisibilityAttribute>();
                if (visAttr != null)
                {
                    visAttr.pointsChange = 10f;
                    visAttr.multiplier = 1.5f;
                }
            }
        }

        // Restore vehicle headlights
        if (_savedData.VehicleHeadlights.Count > 0)
        {
            var vm = NetworkSingleton<VehicleManager>.Instance;
            if (vm != null)
            {
                foreach (var vehicle in vm.AllVehicles)
                {
                    if (vehicle == null || vehicle.GUID == Guid.Empty) continue;
                    string key = vehicle.GUID.ToString();
                    if (_savedData.VehicleHeadlights.TryGetValue(key, out bool on) && on)
                    {
                        var lights = vehicle.GetComponent<VehicleLights>();
                        if (lights != null)
                            lights.HeadlightsOn = true;
                    }
                }
            }
        }
    }

    internal static void EnsureHooks()
    {
        if (!_subscribedToSave)
        {
            var sm = Singleton<SaveManager>.Instance;
            if (sm != null)
            {
                sm.onSaveComplete.AddListener(OnGameSave);
                _subscribedToSave = true;
            }
        }
        if (!_subscribedToLoad)
        {
            var lm = Singleton<LoadManager>.Instance;
            if (lm != null)
            {
                lm.onLoadComplete.AddListener(OnLoadComplete);
                _subscribedToLoad = true;
            }
        }
    }

    internal static void InvalidateCache()
    {
        _savedData = null;
    }
}

[HarmonyPatch(typeof(Phone), "Start")]
public static class PhoneStartPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        Core.EnsureHooks();
        Core.InvalidateCache();
    }
}
