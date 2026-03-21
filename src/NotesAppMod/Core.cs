using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI.Phone;
#else
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
using ScheduleOne.UI.Phone;
#endif

[assembly: MelonInfo(typeof(NotesAppMod.Core), "NotesAppMod", "1.0.0", "gravityblastr")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace NotesAppMod;

// ─── Data model ──────────────────────────────────────────────────────────────

public class Note
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string LastModified { get; set; } = "";
    public bool IsReadOnly { get; set; }
}

// ─── Persistence ─────────────────────────────────────────────────────────────

internal static class NotesStore
{
    private static List<Note>? _notes;
    private static bool _subscribedToSave;
    private static bool _subscribedToLoad;

    internal static void EnsureHooks()
    {
        if (!_subscribedToSave)
        {
            var sm = Singleton<SaveManager>.Instance;
            if (sm != null)
            {
                sm.onSaveComplete.AddListener((UnityAction)OnSave);
                _subscribedToSave = true;
            }
        }

        if (!_subscribedToLoad)
        {
            var lm = Singleton<LoadManager>.Instance;
            if (lm != null)
            {
                lm.onLoadComplete.AddListener((UnityAction)OnLoad);
                _subscribedToLoad = true;
            }
        }
    }

    internal static void InvalidateCache() => _notes = null;

    private static string GetFilePath()
    {
        var folder = Singleton<LoadManager>.Instance?.LoadedGameFolderPath;
        if (string.IsNullOrEmpty(folder)) return "";
        return Path.Combine(folder, "NotesAppMod.json");
    }

    internal static List<Note> GetNotes()
    {
        if (_notes == null) Load();
        return _notes!;
    }

    internal static Note? GetNote(string id) => GetNotes().FirstOrDefault(n => n.Id == id);

    internal static Note CreateNote(string title, string body, bool readOnly = false)
    {
        var note = new Note
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            Body = body,
            LastModified = DateTime.UtcNow.ToString("o"),
            IsReadOnly = readOnly
        };
        GetNotes().Add(note);
        return note;
    }

    internal static bool UpdateNote(string id, string? title = null, string? body = null)
    {
        var note = GetNote(id);
        if (note == null) return false;
        if (title != null) note.Title = title;
        if (body != null) note.Body = body;
        note.LastModified = DateTime.UtcNow.ToString("o");
        return true;
    }

    internal static bool DeleteNote(string id)
    {
        var note = GetNote(id);
        if (note == null) return false;
        return GetNotes().Remove(note);
    }

    private static void Load()
    {
        _notes = new List<Note>();
        var path = GetFilePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
#if IL2CPP
            _notes = System.Text.Json.JsonSerializer.Deserialize<List<Note>>(json) ?? new List<Note>();
#else
            _notes = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Note>>(json) ?? new List<Note>();
#endif
        }
        catch (Exception ex)
        {
            Core.Log("Failed to load notes: " + ex.Message);
            _notes = new List<Note>();
        }
    }

    internal static void Save()
    {
        if (_notes == null) return;
        var path = GetFilePath();
        if (string.IsNullOrEmpty(path)) return;
        try
        {
#if IL2CPP
            var json = System.Text.Json.JsonSerializer.Serialize(_notes,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
#else
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(_notes, Newtonsoft.Json.Formatting.Indented);
#endif
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Core.Log("Failed to save notes: " + ex.Message);
        }
    }

    private static void OnSave() => Save();

    private static void OnLoad()
    {
        InvalidateCache();
        NotesApp.Instance?.RefreshNoteList();
    }
}

// ─── Public API for other mods ───────────────────────────────────────────────

/// <summary>
/// Public API for other mods to interact with the Notes system.
/// Notes created with <c>readOnly: true</c> cannot be edited or deleted by the player.
/// </summary>
public static class NotesAPI
{
    /// <summary>Create a note. Returns the new note's ID.</summary>
    public static string CreateNote(string title, string body, bool readOnly = false)
    {
        var note = NotesStore.CreateNote(title, body, readOnly);
        NotesApp.Instance?.RefreshNoteList();
        return note.Id;
    }

    /// <summary>Get a note by ID, or null if not found.</summary>
    public static Note? GetNote(string id) => NotesStore.GetNote(id);

    /// <summary>Get all notes, ordered by most recently modified.</summary>
    public static IReadOnlyList<Note> GetAllNotes() =>
        NotesStore.GetNotes().OrderByDescending(n => n.LastModified).ToList().AsReadOnly();

    /// <summary>Update a note's title and/or body. Returns false if not found.</summary>
    public static bool UpdateNote(string id, string? title = null, string? body = null)
    {
        var result = NotesStore.UpdateNote(id, title, body);
        if (result) NotesApp.Instance?.RefreshNoteList();
        return result;
    }

    /// <summary>Delete a note. Returns false if not found.</summary>
    public static bool DeleteNote(string id)
    {
        var result = NotesStore.DeleteNote(id);
        if (result) NotesApp.Instance?.RefreshNoteList();
        return result;
    }
}

// ─── MelonMod entry ──────────────────────────────────────────────────────────

public class Core : MelonMod
{
    internal static Core? Instance;

    public override void OnInitializeMelon()
    {
        Instance = this;
        HarmonyInstance.PatchAll(typeof(HomeScreenStartPatch));
        LoggerInstance.Msg("NotesAppMod loaded. (build 6)");
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        NotesApp.Cleanup();
    }

    internal static void Log(string msg) => Instance?.LoggerInstance.Msg(msg);
    internal static void LogWarning(string msg) => Instance?.LoggerInstance.Warning(msg);
}

// ─── Harmony patches ─────────────────────────────────────────────────────────

/// <summary>
/// Inject NotesApp into the phone after the HomeScreen initialises.
/// </summary>
[HarmonyPatch(typeof(HomeScreen), "Start")]
public static class HomeScreenStartPatch
{
    [HarmonyPostfix]
    public static void Postfix(HomeScreen __instance)
    {
        try
        {
            NotesStore.EnsureHooks();
            NotesApp.Initialize(__instance);
            Core.Log("NotesApp injected into phone.");
        }
        catch (Exception ex)
        {
            Core.LogWarning("Failed to inject NotesApp: " + ex);
        }
    }
}
