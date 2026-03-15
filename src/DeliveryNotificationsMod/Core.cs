using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using ScheduleOne.Delivery;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
using ScheduleOne.UI.Phone.Delivery;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(DeliveryNotificationsMod.Core), "DeliveryNotificationsMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DeliveryNotificationsMod;

public class Core : MelonMod
{
    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(typeof(DeliveryStatusPatch));
        LoggerInstance.Msg("DeliveryNotificationsMod loaded.");
    }
}

[HarmonyPatch(typeof(DeliveryInstance), nameof(DeliveryInstance.SetStatus))]
public static class DeliveryStatusPatch
{
    private static readonly FieldInfo _entriesField =
        typeof(NotificationsManager).GetField("entries", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly Dictionary<string, DateTime> _seen = new();

    [HarmonyPrefix]
    public static void Prefix(DeliveryInstance __instance, out EDeliveryStatus __state)
    {
        __state = __instance.Status;
    }

    [HarmonyPostfix]
    public static void Postfix(DeliveryInstance __instance, EDeliveryStatus status, EDeliveryStatus __state)
    {
        // ignore Waiting and InTransit status
        if (status != EDeliveryStatus.Arrived && status != EDeliveryStatus.Completed)
            return;

        // ignore if status is not changing (e.g. when loading a save)
        if (__state == status)
            return;

        // deduplicate
        var now = DateTime.UtcNow;
        var key = $"{__instance.DeliveryID}:{status}";

        foreach (var k in new List<string>(_seen.Keys))
            if ((now - _seen[k]).TotalSeconds > 2) _seen.Remove(k);

        if (_seen.ContainsKey(key))
            return;
        _seen[key] = now;

        var manager = Singleton<NotificationsManager>.Instance;
        if (manager == null)
            return;

        // build notification
        string store = __instance.StoreName;
        string building = __instance.Destination?.PropertyName ?? __instance.DestinationCode;
        string dock = __instance.LoadingDock?.Name ?? $"Loading Dock {__instance.LoadingDockIndex + 1}";
        string verb = status == EDeliveryStatus.Arrived ? "arrived" : "completed";
        string message = $"Delivery from {store} to {building} {dock} has {verb}";

        // send notification
        var icon = PlayerSingleton<DeliveryApp>.Instance?.AppIcon;
        manager.SendNotification("Delivery Update", message, icon);
        StyleNotification(manager, status);
    }

    /// <summary>
    /// Styles our notification message to look nice
    /// </summary>
    private static void StyleNotification(NotificationsManager manager, EDeliveryStatus status)
    {
        if (_entriesField?.GetValue(manager) is not List<RectTransform> entries || entries.Count == 0)
            return;

        var entry = entries[^1];
        var container = entry.Find("Container")?.GetComponent<RectTransform>();
        var subtitleTransform = entry.Find("Container/Subtitle");
        var subtitleRect = subtitleTransform?.GetComponent<RectTransform>();
        var subtitle = subtitleTransform?.GetComponent<TextMeshProUGUI>();
        var iconImage = entry.Find("Container/AppIcon/Mask/Image")?.GetComponent<Image>();

        if (container == null || subtitleRect == null || subtitle == null)
            return;

        var titleTransform = entry.Find("Container/Title");
        var titleRect = titleTransform?.GetComponent<RectTransform>();
        // Layout tuning — adjust these to reposition elements
        const float expandHeight = 20f;         // how much taller to make the notification box
        const float expandWidth = 100f;         // how much wider to make the notification box
        const float titleAnchorY = -19f;        // title vertical pos: more negative = lower
        const float subtitleAnchorY = 26f;      // subtitle vertical pos: higher value = higher in box

        subtitle.textWrappingMode = TextWrappingModes.Normal;

        var sd = subtitleRect.sizeDelta;
        subtitleRect.sizeDelta = new Vector2(sd.x, sd.y + expandHeight);

        if (titleRect != null)
        {
            var tp = titleRect.anchoredPosition;
            titleRect.anchoredPosition = new Vector2(tp.x, titleAnchorY);
        }

        var sp = subtitleRect.anchoredPosition;
        subtitleRect.anchoredPosition = new Vector2(sp.x, subtitleAnchorY);

        var esd = entry.sizeDelta;
        entry.sizeDelta = new Vector2(esd.x + expandWidth, esd.y + expandHeight);

        // Colors match the game's positive/negative UI palette (casino, money displays)
        if (iconImage != null)
            iconImage.color = status == EDeliveryStatus.Arrived
                ? new Color32(84, 231, 23, 255)   // #54E717 - game green
                : new Color32(231, 52, 23, 255);  // game red
    }
}
