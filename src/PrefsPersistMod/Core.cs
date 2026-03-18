using System;
using System.Globalization;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
#else
using ScheduleOne.DevUtilities;
#endif

[assembly: MelonInfo(typeof(PrefsPersistMod.Core), "PrefsPersistMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PrefsPersistMod;

public class Core : MelonMod
{
    internal static Core? Instance;
    private bool _settingsRestored;

    public override void OnInitializeMelon()
    {
        Instance = this;
        HarmonyInstance.PatchAll(typeof(GetDefaultUnitTypePatch));
        HarmonyInstance.PatchAll(typeof(WriteDisplaySettingsPatch));
        HarmonyInstance.PatchAll(typeof(WriteGraphicsSettingsPatch));
        HarmonyInstance.PatchAll(typeof(WriteAudioSettingsPatch));
        HarmonyInstance.PatchAll(typeof(WriteInputSettingsPatch));
        HarmonyInstance.PatchAll(typeof(WriteOtherSettingsPatch));
        LoggerInstance.Msg("PrefsPersistMod loaded.");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (_settingsRestored || !Singleton<Settings>.InstanceExists)
            return;

        _settingsRestored = true;
        var settings = Singleton<Settings>.Instance;

        // Settings.Awake() can fail if ReadDisplaySettings throws (e.g. unsupported
        // locale). Re-read and re-apply all settings from PlayerPrefs to recover.
        try
        {
            var display = settings.ReadDisplaySettings();
            settings.DisplaySettings = display;
            settings.UnappliedDisplaySettings = display;
            settings.ApplyDisplaySettings(display);

            var graphics = settings.ReadGraphicsSettings();
            settings.GraphicsSettings = graphics;
            settings.ApplyGraphicsSettings(graphics);

            var audio = settings.ReadAudioSettings();
            settings.AudioSettings = audio;
            settings.ApplyAudioSettings(audio);

            var input = settings.ReadInputSettings();
            settings.ApplyInputSettings(input);

            var other = settings.ReadOtherSettings();
            settings.ApplyOtherSettings(other);

            LoggerInstance.Msg($"Settings restored — " +
                $"Quality={graphics.GraphicsQuality} AA={graphics.AntiAliasingMode} " +
                $"FOV={graphics.FOV} Res={display.ResolutionIndex} Mode={display.DisplayMode}");
        }
        catch (Exception ex)
        {
            LoggerInstance.Error($"Failed to restore settings: {ex.Message}");
        }
    }
}

// Fix: GetDefaultUnitTypeForPlayer crashes on Invariant Culture (LCID 127)
// because RegionInfo doesn't support it. Return Metric as a safe default.
[HarmonyPatch(typeof(Settings), "GetDefaultUnitTypeForPlayer")]
public static class GetDefaultUnitTypePatch
{
    [HarmonyPrefix]
    public static bool Prefix(ref Settings.EUnitType __result)
    {
        try
        {
            var region = new RegionInfo(CultureInfo.CurrentCulture.LCID);
            var iso = region.TwoLetterISORegionName;
            __result = (iso == "US" || iso == "LR" || iso == "MM")
                ? Settings.EUnitType.Imperial
                : Settings.EUnitType.Metric;
        }
        catch
        {
            __result = Settings.EUnitType.Metric;
        }
        return false; // skip original
    }
}

[HarmonyPatch(typeof(Settings), nameof(Settings.WriteDisplaySettings))]
public static class WriteDisplaySettingsPatch
{
    [HarmonyPostfix]
    public static void Postfix(DisplaySettings settings)
    {
        PlayerPrefs.Save();
        Core.Instance?.LoggerInstance.Msg(
            $"Display settings saved — Res={settings.ResolutionIndex} Mode={settings.DisplayMode}");
    }
}

[HarmonyPatch(typeof(Settings), nameof(Settings.WriteGraphicsSettings))]
public static class WriteGraphicsSettingsPatch
{
    [HarmonyPostfix]
    public static void Postfix(GraphicsSettings settings)
    {
        PlayerPrefs.Save();
        Core.Instance?.LoggerInstance.Msg(
            $"Graphics settings saved — Quality={settings.GraphicsQuality} AA={settings.AntiAliasingMode} FOV={settings.FOV}");
    }
}

[HarmonyPatch(typeof(Settings), nameof(Settings.WriteAudioSettings))]
public static class WriteAudioSettingsPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        PlayerPrefs.Save();
        Core.Instance?.LoggerInstance.Msg("Audio settings saved.");
    }
}

[HarmonyPatch(typeof(Settings), nameof(Settings.WriteInputSettings))]
public static class WriteInputSettingsPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        PlayerPrefs.Save();
        Core.Instance?.LoggerInstance.Msg("Input settings saved.");
    }
}

[HarmonyPatch(typeof(Settings), nameof(Settings.WriteOtherSettings))]
public static class WriteOtherSettingsPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        PlayerPrefs.Save();
        Core.Instance?.LoggerInstance.Msg("Other settings saved.");
    }
}
