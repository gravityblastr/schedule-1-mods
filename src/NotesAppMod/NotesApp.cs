using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI.Phone;
#else
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI.Phone;
#endif

namespace NotesAppMod;

/// <summary>
/// Standalone phone app that does NOT inherit from App&lt;T&gt; to avoid
/// PlayerSingleton / FishNet requirements. Manually integrates with the
/// phone system instead.
/// </summary>
public class NotesApp : MonoBehaviour
{
    public static NotesApp? Instance { get; private set; }

    // ── State ────────────────────────────────────────────────────────────
    private bool _isOpen;
    private string? _currentNoteId;
    private Font? _font;
    private Button? _appIconButton;

    // ── UI root ──────────────────────────────────────────────────────────
    private GameObject? _container;
    private RectTransform? _notifContainer;
    private Text? _notifText;

    // ── List view ────────────────────────────────────────────────────────
    private GameObject? _listView;
    private RectTransform? _noteListContent;
    private Text? _emptyLabel;

    // ── Detail view ──────────────────────────────────────────────────────
    private GameObject? _detailView;
    private InputField? _titleInput;
    private InputField? _bodyInput;
    private Text? _modifiedLabel;
    private GameObject? _deleteButton;
    private Text? _detailHeaderText;

    // ── Body scroll state ────────────────────────────────────────────────
    private RectTransform? _bodyRect;
    private RectTransform? _bodyTextRect;
    private Image? _scrollHandleImg;
    private RectTransform? _scrollHandleRect;
    private RectTransform? _scrollTrackRect;
    private int _scrollLine;
    private const int LinesPerScroll = 3;

    private static readonly FieldInfo? DrawStartField =
        typeof(InputField).GetField("m_DrawStart", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? DrawEndField =
        typeof(InputField).GetField("m_DrawEnd", BindingFlags.NonPublic | BindingFlags.Instance);

    // ── Appearance ───────────────────────────────────────────────────────
    private static readonly Color BgColor = new(0.08f, 0.08f, 0.12f, 0.95f);
    private static readonly Color HeaderColor = new(0.12f, 0.12f, 0.18f, 1f);
    private static readonly Color EntryColor = new(0.14f, 0.14f, 0.20f, 1f);
    private static readonly Color EntryHoverColor = new(0.20f, 0.20f, 0.28f, 1f);
    private static readonly Color InputBgColor = new(0.10f, 0.10f, 0.15f, 0.9f);
    private static readonly Color AccentColor = new(0.35f, 0.65f, 0.95f, 1f);
    private static readonly Color DeleteColor = new(0.85f, 0.25f, 0.25f, 1f);
    private static readonly Color TextColor = Color.white;
    private static readonly Color SubtextColor = new(0.6f, 0.6f, 0.7f, 1f);

    // ─────────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    public static void Initialize(HomeScreen homeScreen)
    {
        if (Instance != null) return;

        var appsCanvas = PlayerSingleton<AppsCanvas>.Instance;
        if (appsCanvas == null)
        {
            Core.LogWarning("AppsCanvas not found, cannot create NotesApp.");
            return;
        }

        // Grab a font from an existing UI text element
        var existingText = homeScreen.GetComponentInChildren<Text>(true);
        var font = existingText != null ? existingText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Create app container as child of AppsCanvas
        var go = new GameObject("NotesApp");
        go.transform.SetParent(appsCanvas.canvas.transform, false);
        Instance = go.AddComponent<NotesApp>();
        Instance._font = font;
        Instance.BuildUI();
        Instance._container!.SetActive(false);

        // Create home screen icon
        Instance.CreateHomeScreenIcon(homeScreen);

        // Register for phone events
        var phone = PlayerSingleton<Phone>.Instance;
        if (phone != null)
        {
#if IL2CPP
            phone.closeApps += (Il2CppSystem.Action)Instance.Close;
            phone.onPhoneOpened += (Il2CppSystem.Action)Instance.OnPhoneOpened;
#else
            phone.closeApps = (Action)Delegate.Combine(phone.closeApps, new Action(Instance.Close));
            phone.onPhoneOpened = (Action)Delegate.Combine(phone.onPhoneOpened, new Action(Instance.OnPhoneOpened));
#endif
        }

        // Register exit listener (Escape key handling)
        GameInput.RegisterExitListener(
            (GameInput.ExitDelegate)Instance.OnExit, 1);
    }

    public static void Cleanup()
    {
        if (Instance != null)
        {
            GameInput.DeregisterExitListener(
                (GameInput.ExitDelegate)Instance.OnExit);
        }
        Instance = null;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Home screen icon
    // ─────────────────────────────────────────────────────────────────────

    private void CreateHomeScreenIcon(HomeScreen homeScreen)
    {
        // Get private fields from HomeScreen via Traverse
        var prefab = Traverse.Create(homeScreen).Field("appIconPrefab").GetValue<GameObject>();
        var container = Traverse.Create(homeScreen).Field("appIconContainer").GetValue<RectTransform>();
        var appIcons = Traverse.Create(homeScreen).Field("appIcons").GetValue<List<Button>>();
        var uiPanel = Traverse.Create(homeScreen).Field("uiPanel").GetValue<object>();

        if (prefab == null || container == null)
        {
            Core.LogWarning("Could not find HomeScreen icon prefab/container.");
            return;
        }

        var iconGO = UnityEngine.Object.Instantiate(prefab, container);
        var rect = iconGO.GetComponent<RectTransform>();

        // Set icon image — create a simple notepad sprite
        var iconSprite = CreateNotesSprite();
        rect.Find("Mask/Image").GetComponent<Image>().sprite = iconSprite;
        rect.Find("Label").GetComponent<Text>().text = "Notes";

        // Grab notification badge and set up for note count display
        var notifTransform = rect.Find("Notifications");
        if (notifTransform != null)
        {
            _notifContainer = notifTransform.GetComponent<RectTransform>();
            _notifText = notifTransform.Find("Text")?.GetComponent<Text>();
        }
        UpdateNoteBadge();

        // Wire up click
        _appIconButton = rect.GetComponent<Button>();
        _appIconButton.onClick.AddListener((UnityEngine.Events.UnityAction)ToggleOpen);

        // Register with HomeScreen's icon list
        appIcons?.Add(_appIconButton);

        // Register with UIPanel for gamepad navigation.
        // IL2CPP reflection is incompatible with standard GetMethod, so
        // we only do this on the Mono branch; gamepad nav is non-critical.
#if !IL2CPP
        try
        {
            if (uiPanel != null)
            {
                var addMethod = uiPanel.GetType().GetMethod("AddSelectable");
                var selectable = iconGO.GetComponents<Component>();
                foreach (var s in selectable)
                {
                    if (s.GetType().Name == "UISelectable")
                    {
                        addMethod?.Invoke(uiPanel, new object[] { s });
                        break;
                    }
                }
            }
        }
        catch { /* gamepad nav not critical */ }
#endif
    }

    private static Sprite CreateNotesSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];

        // Teal/green background (matching game icon style)
        var bg = new Color(0.18f, 0.62f, 0.52f, 1f);
        var white = Color.white;

        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

        // Draw a white blocky notepad shape (page with folded corner)
        // Page body: x 16-48, y 10-52
        for (int y = 10; y <= 52; y++)
            for (int x = 16; x <= 48; x++)
                pixels[y * size + x] = white;

        // Folded corner: cut triangle at top-right (x 40-48, y 44-52)
        for (int y = 44; y <= 52; y++)
            for (int x = 40 + (y - 44); x <= 48; x++)
                pixels[y * size + x] = bg;

        // Fold line (diagonal)
        for (int i = 0; i <= 8; i++)
        {
            int x = 40 + i;
            int y = 44 + i;
            if (x < size && y < size)
                pixels[y * size + x] = new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        // Draw 4 horizontal "text" lines on the page (darker than bg)
        var lineColor = new Color(0.14f, 0.50f, 0.42f, 1f);
        int[] lineYs = { 18, 24, 30, 36 };
        int[] lineWidths = { 26, 22, 26, 16 };
        for (int i = 0; i < lineYs.Length; i++)
            for (int x = 20; x < 20 + lineWidths[i]; x++)
                pixels[lineYs[i] * size + x] = lineColor;

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
    }

    private static Sprite CreateTrashSprite()
    {
        const int s = 24;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color[s * s];
        var c = Color.clear;
        var w = Color.white;

        for (int i = 0; i < px.Length; i++) px[i] = c;

        // Lid: horizontal bar at top (y=20-21, x=6-17)
        for (int y = 20; y <= 21; y++)
            for (int x = 6; x <= 17; x++)
                px[y * s + x] = w;
        // Lid handle (y=22-23, x=9-14)
        for (int y = 22; y <= 23; y++)
            for (int x = 9; x <= 14; x++)
                px[y * s + x] = w;
        // Body: rectangle (y=4-19, x=7-16)
        for (int y = 4; y <= 19; y++)
            for (int x = 7; x <= 16; x++)
                px[y * s + x] = w;
        // Cut vertical slots in body (x=9, x=12, x=14 — clear)
        for (int y = 6; y <= 17; y++)
        {
            px[y * s + 9] = c;
            px[y * s + 12] = c;
            px[y * s + 14] = c;
        }

        tex.SetPixels(px);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, s, s), Vector2.one * 0.5f);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Open / Close
    // ─────────────────────────────────────────────────────────────────────

    private void OnPhoneOpened()
    {
        // Re-apply landscape orientation if our app is still open when phone reopens
        if (_isOpen)
        {
            PlayerSingleton<Phone>.Instance?.SetIsHorizontal(true);
            PlayerSingleton<Phone>.Instance?.SetLookOffsetMultiplier(0.6f);
        }
    }

    private void ToggleOpen() => SetOpen(!_isOpen);

    private void Close()
    {
        if (_isOpen) SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        if (open && Phone.ActiveApp != null && Phone.ActiveApp != _container)
            return; // Another app is already open

        _isOpen = open;

        // Mirror App<T>.SetOpen behaviour
        PlayerSingleton<AppsCanvas>.Instance?.SetIsOpen(open);
        PlayerSingleton<HomeScreen>.Instance?.SetIsOpen(!open);

        if (open)
        {
            Phone.ActiveApp = _container;
            // Landscape orientation (same as Contacts, Delivery, etc.)
            PlayerSingleton<Phone>.Instance?.SetIsHorizontal(true);
            PlayerSingleton<Phone>.Instance?.SetLookOffsetMultiplier(0.6f);
            ShowListView();
        }
        else
        {
            SaveCurrentNote();
            if (Phone.ActiveApp == _container)
                Phone.ActiveApp = null;
            PlayerSingleton<Phone>.Instance?.SetIsHorizontal(false);
            PlayerSingleton<Phone>.Instance?.SetLookOffsetMultiplier(1f);
            // Clear typing flag in case an input was focused when closing
            GameInput.IsTyping = false;
        }

        _container?.SetActive(open);
    }

    private void Update()
    {
        if (!_isOpen) return;

        // Suppress game key bindings (flashlight, etc.) while an InputField is focused.
        var isFocused = (_titleInput != null && _titleInput.isFocused)
                     || (_bodyInput != null && _bodyInput.isFocused);
        GameInput.IsTyping = isFocused;

        // Body scroll — mouse wheel + scrollbar update
        UpdateBodyScroll();
    }

    private void UpdateBodyScroll()
    {
        if (_bodyRect == null || _bodyTextRect == null || _bodyInput == null
            || _detailView == null || !_detailView.activeSelf)
            return;

        // Use preferredHeight of the text component to know total content height
        var bodyText = _bodyInput.textComponent;
        if (bodyText == null) return;

        float bodyH = _bodyRect.rect.height;

        // Compute true text height using FULL input text (InputField only feeds
        // the visible portion to textComponent.text, so preferredHeight is useless).
        string fullText = _bodyInput.text ?? "";
        float textH;
        if (fullText.Length > 0 && _bodyTextRect!.rect.width > 0)
        {
            var settings = bodyText.GetGenerationSettings(
                new Vector2(_bodyTextRect.rect.width, 0));
            textH = bodyText.cachedTextGeneratorForLayout
                .GetPreferredHeight(fullText, settings) / bodyText.pixelsPerUnit + 8;
        }
        else
        {
            textH = 0;
        }
        float overflow = Mathf.Max(0, textH - bodyH);

        if (overflow <= 0)
        {
            // All content fits — hide scrollbar
            if (_scrollTrackRect != null) _scrollTrackRect.gameObject.SetActive(false);
            return;
        }

        // Show scrollbar
        if (_scrollTrackRect != null) _scrollTrackRect.gameObject.SetActive(true);

        // Build line-start index table from full text
        var lineStarts = new List<int> { 0 };
        for (int i = 0; i < fullText.Length; i++)
            if (fullText[i] == '\n' && i + 1 < fullText.Length)
                lineStarts.Add(i + 1);
        int totalLines = lineStarts.Count;

        // Mouse wheel scrolling — adjusts m_DrawStart via reflection
        // without affecting focus, selection, or caret position.
        float scroll = UnityEngine.Input.mouseScrollDelta.y;
        if (scroll != 0 && DrawStartField != null)
        {
            _scrollLine += scroll > 0 ? -LinesPerScroll : LinesPerScroll;
            _scrollLine = Mathf.Clamp(_scrollLine, 0, Mathf.Max(0, totalLines - 1));

            int newDrawStart = lineStarts[_scrollLine];
            DrawStartField.SetValue(_bodyInput, newDrawStart);
            _bodyInput.ForceLabelUpdate();
        }

        // Update scrollbar handle
        if (_scrollHandleRect != null && _scrollTrackRect != null)
        {
            float trackH = _scrollTrackRect.rect.height;
            float handleH = Mathf.Max(20, trackH * Mathf.Clamp01(bodyH / textH));
            _scrollHandleRect.sizeDelta = new Vector2(0, handleH);

            // Position handle based on current scroll line
            float scrollFraction = totalLines > 1
                ? (float)_scrollLine / (totalLines - 1)
                : 0f;
            float handleTravel = trackH - handleH;
            _scrollHandleRect.anchoredPosition = new Vector2(0, -scrollFraction * handleTravel);
        }
    }

    private void OnExit(ExitAction exit)
    {
        if (exit.Used || !_isOpen) return;
        if (!PlayerSingleton<Phone>.InstanceExists || !PlayerSingleton<Phone>.Instance.IsOpen)
            return;

        exit.Used = true;

        if (_detailView != null && _detailView.activeSelf)
        {
            SaveCurrentNote();
            ShowListView();
        }
        else
        {
            SetOpen(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  UI Construction
    // ─────────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Main container — fills the phone screen
        _container = gameObject;
        var containerRect = _container.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        // Background
        var bg = _container.AddComponent<Image>();
        bg.color = BgColor;

        BuildListView();
        BuildDetailView();

        _detailView!.SetActive(false);
    }

    // ── List View ────────────────────────────────────────────────────────

    private void BuildListView()
    {
        _listView = CreateChild("ListView", _container!.transform);
        Stretch(_listView.GetComponent<RectTransform>());

        // Header bar
        var header = CreateChild("Header", _listView.transform);
        var headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = Vector2.one;
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.sizeDelta = new Vector2(0, 40);
        var headerBg = header.AddComponent<Image>();
        headerBg.color = HeaderColor;

        // Title text
        var titleGO = CreateChild("Title", header.transform);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(12, 0);
        titleRect.offsetMax = new Vector2(-50, 0);
        var titleText = titleGO.AddComponent<Text>();
        titleText.text = "Notes";
        titleText.font = _font;
        titleText.fontSize = 20;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = TextColor;
        titleText.alignment = TextAnchor.MiddleLeft;

        // New note button (+)
        var newBtnGO = CreateChild("NewBtn", header.transform);
        var newBtnRect = newBtnGO.GetComponent<RectTransform>();
        newBtnRect.anchorMin = new Vector2(1, 0);
        newBtnRect.anchorMax = Vector2.one;
        newBtnRect.pivot = new Vector2(1, 0.5f);
        newBtnRect.sizeDelta = new Vector2(38, 0);
        newBtnRect.anchoredPosition = new Vector2(-4, 0);
        var newBtnImg = newBtnGO.AddComponent<Image>();
        newBtnImg.color = AccentColor;
        var newBtn = newBtnGO.AddComponent<Button>();
        newBtn.targetGraphic = newBtnImg;
        newBtn.onClick.AddListener((UnityEngine.Events.UnityAction)OnNewNote);

        var newBtnLabel = CreateChild("Label", newBtnGO.transform);
        Stretch(newBtnLabel.GetComponent<RectTransform>());
        var newBtnText = newBtnLabel.AddComponent<Text>();
        newBtnText.text = "+";
        newBtnText.font = _font;
        newBtnText.fontSize = 24;
        newBtnText.fontStyle = FontStyle.Bold;
        newBtnText.color = Color.white;
        newBtnText.alignment = TextAnchor.MiddleCenter;

        // Scroll view for notes list
        var scrollGO = CreateChild("Scroll", _listView.transform);
        var scrollRect = scrollGO.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(0, 0);
        scrollRect.offsetMax = new Vector2(0, -40); // below header
        var scrollView = scrollGO.AddComponent<ScrollRect>();
        scrollView.horizontal = false;
        scrollView.movementType = ScrollRect.MovementType.Clamped;
        scrollGO.AddComponent<RectMask2D>();

        // Scrollbar for list
        var listSbGO = CreateChild("Scrollbar", scrollGO.transform);
        var listSbRect = listSbGO.GetComponent<RectTransform>();
        listSbRect.anchorMin = new Vector2(1, 0);
        listSbRect.anchorMax = Vector2.one;
        listSbRect.pivot = new Vector2(1, 0.5f);
        listSbRect.sizeDelta = new Vector2(6, 0);
        listSbRect.anchoredPosition = new Vector2(-1, 0);
        var listSb = listSbGO.AddComponent<Scrollbar>();
        listSb.direction = Scrollbar.Direction.BottomToTop;

        var listSbHandleArea = CreateChild("HandleArea", listSbGO.transform);
        Stretch(listSbHandleArea.GetComponent<RectTransform>());
        var listSbHandle = CreateChild("Handle", listSbHandleArea.transform);
        Stretch(listSbHandle.GetComponent<RectTransform>());
        var listSbHandleImg = listSbHandle.AddComponent<Image>();
        listSbHandleImg.color = new Color(0.4f, 0.4f, 0.5f, 0.6f);
        listSb.handleRect = listSbHandle.GetComponent<RectTransform>();
        listSb.targetGraphic = listSbHandleImg;
        scrollView.verticalScrollbar = listSb;
        scrollView.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // Content container with vertical layout
        var contentGO = CreateChild("Content", scrollGO.transform);
        _noteListContent = contentGO.GetComponent<RectTransform>();
        _noteListContent.anchorMin = new Vector2(0, 1);
        _noteListContent.anchorMax = Vector2.one;
        _noteListContent.pivot = new Vector2(0.5f, 1);
        _noteListContent.sizeDelta = new Vector2(0, 0);
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.padding = new RectOffset(6, 6, 4, 4);
        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollView.content = _noteListContent;

        // Empty state label
        var emptyGO = CreateChild("Empty", scrollGO.transform);
        var emptyRect = emptyGO.GetComponent<RectTransform>();
        emptyRect.anchorMin = new Vector2(0, 0.4f);
        emptyRect.anchorMax = new Vector2(1, 0.6f);
        emptyRect.offsetMin = Vector2.zero;
        emptyRect.offsetMax = Vector2.zero;
        _emptyLabel = emptyGO.AddComponent<Text>();
        _emptyLabel.text = "No notes yet.\nTap + to create one.";
        _emptyLabel.font = _font;
        _emptyLabel.fontSize = 16;
        _emptyLabel.color = SubtextColor;
        _emptyLabel.alignment = TextAnchor.MiddleCenter;
    }

    // ── Detail View ──────────────────────────────────────────────────────

    private void BuildDetailView()
    {
        _detailView = CreateChild("DetailView", _container!.transform);
        Stretch(_detailView.GetComponent<RectTransform>());

        // Header — same height as list view (40)
        var header = CreateChild("Header", _detailView.transform);
        var headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = Vector2.one;
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.sizeDelta = new Vector2(0, 40);
        var headerBg = header.AddComponent<Image>();
        headerBg.color = HeaderColor;

        // Back button — same width as + button (38)
        var backGO = CreateChild("BackBtn", header.transform);
        var backRect = backGO.GetComponent<RectTransform>();
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = new Vector2(0, 1);
        backRect.pivot = new Vector2(0, 0.5f);
        backRect.sizeDelta = new Vector2(38, 0);
        backRect.anchoredPosition = new Vector2(4, 0);
        var backImg = backGO.AddComponent<Image>();
        backImg.color = AccentColor;
        var backBtn = backGO.AddComponent<Button>();
        backBtn.targetGraphic = backImg;
        backBtn.onClick.AddListener((UnityEngine.Events.UnityAction)OnBackToList);

        var backLabel = CreateChild("Label", backGO.transform);
        Stretch(backLabel.GetComponent<RectTransform>());
        var backText = backLabel.AddComponent<Text>();
        backText.text = "<";
        backText.font = _font;
        backText.fontSize = 22;
        backText.fontStyle = FontStyle.Bold;
        backText.color = Color.white;
        backText.alignment = TextAnchor.MiddleCenter;

        // Delete button — top right, same size as back/+ buttons (38)
        _deleteButton = CreateChild("DeleteBtn", header.transform);
        var delRect = _deleteButton.GetComponent<RectTransform>();
        delRect.anchorMin = new Vector2(1, 0);
        delRect.anchorMax = Vector2.one;
        delRect.pivot = new Vector2(1, 0.5f);
        delRect.sizeDelta = new Vector2(38, 0);
        delRect.anchoredPosition = new Vector2(-4, 0);
        var delImg = _deleteButton.AddComponent<Image>();
        delImg.color = DeleteColor;
        var delBtn = _deleteButton.AddComponent<Button>();
        delBtn.targetGraphic = delImg;
        delBtn.onClick.AddListener((UnityEngine.Events.UnityAction)OnDeleteNote);

        // Trash icon (drawn as a small sprite)
        var delIconGO = CreateChild("Icon", _deleteButton.transform);
        Stretch(delIconGO.GetComponent<RectTransform>(), 6, 6, 6, 6);
        var delIconImg = delIconGO.AddComponent<Image>();
        delIconImg.sprite = CreateTrashSprite();
        delIconImg.preserveAspect = true;
        delIconImg.color = Color.white;

        // Header title — offset past back button with extra spacing, before delete
        var hTitleGO = CreateChild("HeaderTitle", header.transform);
        var hTitleRect = hTitleGO.GetComponent<RectTransform>();
        hTitleRect.anchorMin = Vector2.zero;
        hTitleRect.anchorMax = Vector2.one;
        hTitleRect.offsetMin = new Vector2(50, 0);
        hTitleRect.offsetMax = new Vector2(-46, 0);
        _detailHeaderText = hTitleGO.AddComponent<Text>();
        _detailHeaderText.text = "Edit Note";
        _detailHeaderText.font = _font;
        _detailHeaderText.fontSize = 18;
        _detailHeaderText.fontStyle = FontStyle.Bold;
        _detailHeaderText.color = TextColor;
        _detailHeaderText.alignment = TextAnchor.MiddleLeft;

        // All elements absolutely positioned — no VerticalLayoutGroup.
        // Header is 40px at top. Title is 28px below header.
        // Body fills between title and footer. Footer is 16px at bottom.

        const float headerH = 40f;
        const float titleH = 28f;
        const float footerH = 16f;
        const float pad = 8f;
        const float gap = 3f;

        // Title input — anchored below header
        var titleGO = CreateChild("TitleInput", _detailView.transform);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = Vector2.one;
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.offsetMin = new Vector2(pad, 0);
        titleRect.offsetMax = new Vector2(-pad, -(headerH + gap));
        titleRect.sizeDelta = new Vector2(titleRect.sizeDelta.x, titleH);

        var titleBg = titleGO.AddComponent<Image>();
        titleBg.color = InputBgColor;

        var titleTextGO = CreateChild("Text", titleGO.transform);
        Stretch(titleTextGO.GetComponent<RectTransform>(), 8, 8, 2, 2);
        var titleText = titleTextGO.AddComponent<Text>();
        titleText.font = _font;
        titleText.fontSize = 15;
        titleText.color = TextColor;
        titleText.supportRichText = false;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Truncate;

        var titlePhGO = CreateChild("Placeholder", titleGO.transform);
        Stretch(titlePhGO.GetComponent<RectTransform>(), 8, 8, 2, 2);
        var titlePhText = titlePhGO.AddComponent<Text>();
        titlePhText.font = _font;
        titlePhText.fontSize = 15;
        titlePhText.fontStyle = FontStyle.Italic;
        titlePhText.color = new Color(1, 1, 1, 0.25f);
        titlePhText.text = "Note title...";
        titlePhText.alignment = TextAnchor.MiddleLeft;

        _titleInput = titleGO.AddComponent<InputField>();
        _titleInput.textComponent = titleText;
        _titleInput.placeholder = titlePhText;
        _titleInput.lineType = InputField.LineType.SingleLine;
        _titleInput.characterLimit = 200;

        // Body input — fills space between title and footer
        float bodyTop = headerH + gap + titleH + gap * 2; // extra gap below title
        float bodyBottom = footerH + gap;

        var bodyGO = CreateChild("BodyInput", _detailView.transform);
        _bodyRect = bodyGO.GetComponent<RectTransform>();
        _bodyRect.anchorMin = Vector2.zero;
        _bodyRect.anchorMax = Vector2.one;
        _bodyRect.offsetMin = new Vector2(pad, bodyBottom);
        _bodyRect.offsetMax = new Vector2(-pad, -bodyTop);

        var bodyBg = bodyGO.AddComponent<Image>();
        bodyBg.color = InputBgColor;
        bodyGO.AddComponent<RectMask2D>();

        // Text child — stretches to fill body with padding
        var bodyTextGO = CreateChild("Text", bodyGO.transform);
        _bodyTextRect = bodyTextGO.GetComponent<RectTransform>();
        Stretch(_bodyTextRect, 8, 14, 4, 4); // 14 right for scrollbar room
        var bodyText = bodyTextGO.AddComponent<Text>();
        bodyText.font = _font;
        bodyText.fontSize = 15;
        bodyText.color = TextColor;
        bodyText.supportRichText = false;
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Truncate;

        // Placeholder
        var bodyPhGO = CreateChild("Placeholder", bodyGO.transform);
        Stretch(bodyPhGO.GetComponent<RectTransform>(), 8, 14, 4, 4);
        var bodyPhText = bodyPhGO.AddComponent<Text>();
        bodyPhText.font = _font;
        bodyPhText.fontSize = 15;
        bodyPhText.fontStyle = FontStyle.Italic;
        bodyPhText.color = new Color(1, 1, 1, 0.25f);
        bodyPhText.text = "Write your note...";
        bodyPhText.alignment = TextAnchor.UpperLeft;

        _bodyInput = bodyGO.AddComponent<InputField>();
        _bodyInput.textComponent = bodyText;
        _bodyInput.placeholder = bodyPhText;
        _bodyInput.lineType = InputField.LineType.MultiLineNewline;
        _bodyInput.characterLimit = 5000;

        // Scrollbar track — sibling of body (not child) to avoid RectMask2D clipping
        var trackGO = CreateChild("ScrollTrack", _detailView.transform);
        _scrollTrackRect = trackGO.GetComponent<RectTransform>();
        // Position to overlap the right edge of the body area
        _scrollTrackRect.anchorMin = new Vector2(1, 0);
        _scrollTrackRect.anchorMax = Vector2.one;
        _scrollTrackRect.pivot = new Vector2(1, 0.5f);
        _scrollTrackRect.offsetMin = new Vector2(-(pad + 2), bodyBottom + 2);
        _scrollTrackRect.offsetMax = new Vector2(-(pad + 2), -bodyTop - 2);
        _scrollTrackRect.sizeDelta = new Vector2(5, _scrollTrackRect.sizeDelta.y);
        var trackImg = trackGO.AddComponent<Image>();
        trackImg.color = new Color(0.2f, 0.2f, 0.25f, 0.3f);

        // Scrollbar handle
        var handleGO = CreateChild("Handle", trackGO.transform);
        _scrollHandleRect = handleGO.GetComponent<RectTransform>();
        _scrollHandleRect.anchorMin = new Vector2(0, 1);
        _scrollHandleRect.anchorMax = Vector2.one;
        _scrollHandleRect.pivot = new Vector2(0.5f, 1);
        _scrollHandleRect.sizeDelta = new Vector2(0, 20);
        _scrollHandleRect.anchoredPosition = Vector2.zero;
        _scrollHandleImg = handleGO.AddComponent<Image>();
        _scrollHandleImg.color = new Color(0.5f, 0.5f, 0.6f, 0.7f);

        // Footer: modified timestamp — anchored at bottom
        var modGO = CreateChild("Modified", _detailView.transform);
        var modRect = modGO.GetComponent<RectTransform>();
        modRect.anchorMin = Vector2.zero;
        modRect.anchorMax = new Vector2(1, 0);
        modRect.pivot = new Vector2(0.5f, 0);
        modRect.offsetMin = new Vector2(pad + 2, 1);
        modRect.offsetMax = new Vector2(-(pad + 2), footerH + 1);
        _modifiedLabel = modGO.AddComponent<Text>();
        _modifiedLabel.font = _font;
        _modifiedLabel.fontSize = 10;
        _modifiedLabel.color = SubtextColor;
        _modifiedLabel.alignment = TextAnchor.MiddleRight;
    }

    private InputField CreateInputField(Transform parent, string placeholder, bool multiline, float height)
    {
        var go = CreateChild("InputField", parent);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        le.flexibleHeight = multiline ? 1 : 0;

        var bgImg = go.AddComponent<Image>();
        bgImg.color = InputBgColor;

        // Display text — direct child with padding
        var textGO = CreateChild("Text", go.transform);
        Stretch(textGO.GetComponent<RectTransform>(), 8, 8, 4, 4);
        var text = textGO.AddComponent<Text>();
        text.font = _font;
        text.fontSize = 15;
        text.color = TextColor;
        text.supportRichText = false;
        text.alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        // Placeholder — same position as text
        var phGO = CreateChild("Placeholder", go.transform);
        Stretch(phGO.GetComponent<RectTransform>(), 8, 8, 4, 4);
        var phText = phGO.AddComponent<Text>();
        phText.font = _font;
        phText.fontSize = 15;
        phText.fontStyle = FontStyle.Italic;
        phText.color = new Color(1, 1, 1, 0.25f);
        phText.text = placeholder;
        phText.alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;

        // InputField component
        var input = go.AddComponent<InputField>();
        input.textComponent = text;
        input.placeholder = phText;
        input.lineType = multiline
            ? InputField.LineType.MultiLineNewline
            : InputField.LineType.SingleLine;
        input.characterLimit = multiline ? 5000 : 200;

        return input;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Navigation
    // ─────────────────────────────────────────────────────────────────────

    private void ShowListView()
    {
        _detailView?.SetActive(false);
        _listView?.SetActive(true);
        RefreshNoteList();
    }

    private void ShowDetailView(string noteId)
    {
        var note = NotesStore.GetNote(noteId);
        if (note == null) return;

        _currentNoteId = noteId;
        _scrollLine = 0;
        _listView?.SetActive(false);
        _detailView?.SetActive(true);

        var isReadOnly = note.IsReadOnly;
        _detailHeaderText!.text = isReadOnly ? "View Note" : "Edit Note";
        _titleInput!.text = note.Title;
        _titleInput.interactable = !isReadOnly;
        _bodyInput!.text = note.Body;
        _bodyInput.interactable = !isReadOnly;

        if (DateTime.TryParse(note.LastModified, out var dt))
            _modifiedLabel!.text = "Modified: " + dt.ToLocalTime().ToString("g");
        else
            _modifiedLabel!.text = "";

        _deleteButton?.SetActive(!isReadOnly);
    }

    public void RefreshNoteList()
    {
        if (_noteListContent == null) return;

        // Clear existing entries
        for (int i = _noteListContent.childCount - 1; i >= 0; i--)
            Destroy(_noteListContent.GetChild(i).gameObject);

        var notes = NotesStore.GetNotes()
            .OrderByDescending(n => n.LastModified)
            .ToList();

        _emptyLabel?.gameObject.SetActive(notes.Count == 0);

        foreach (var note in notes)
        {
            CreateNoteEntry(note);
        }

        UpdateNoteBadge();
    }

    private void UpdateNoteBadge()
    {
        if (_notifContainer == null) return;
        int count = NotesStore.GetNotes().Count;
        if (count > 0)
        {
            _notifContainer.gameObject.SetActive(true);
            if (_notifText != null) _notifText.text = count.ToString();
        }
        else
        {
            _notifContainer.gameObject.SetActive(false);
        }
    }

    private void CreateNoteEntry(Note note)
    {
        var entry = CreateChild("Entry_" + note.Id, _noteListContent!.transform);
        var entryLE = entry.AddComponent<LayoutElement>();
        entryLE.preferredHeight = 48;

        var entryImg = entry.AddComponent<Image>();
        entryImg.color = EntryColor;

        var entryBtn = entry.AddComponent<Button>();
        entryBtn.targetGraphic = entryImg;
        var colors = entryBtn.colors;
        colors.highlightedColor = EntryHoverColor;
        colors.pressedColor = EntryHoverColor;
        entryBtn.colors = colors;

        var noteId = note.Id; // capture for closure
        entryBtn.onClick.AddListener((UnityEngine.Events.UnityAction)(() => OnOpenNote(noteId)));

        // Title text
        var titleGO = CreateChild("Title", entry.transform);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.45f);
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(12, 0);
        titleRect.offsetMax = new Vector2(-12, -4);
        var titleText = titleGO.AddComponent<Text>();
        var displayTitle = string.IsNullOrEmpty(note.Title) ? "(Untitled)" : note.Title;
        if (note.IsReadOnly) displayTitle = "[Read Only] " + displayTitle;
        titleText.text = displayTitle;
        titleText.font = _font;
        titleText.fontSize = 15;
        titleText.color = TextColor;
        titleText.alignment = TextAnchor.MiddleLeft;

        // Subtitle (date)
        var subGO = CreateChild("Sub", entry.transform);
        var subRect = subGO.GetComponent<RectTransform>();
        subRect.anchorMin = Vector2.zero;
        subRect.anchorMax = new Vector2(1, 0.42f);
        subRect.offsetMin = new Vector2(12, 3);
        subRect.offsetMax = new Vector2(-12, 0);
        var subText = subGO.AddComponent<Text>();
        if (DateTime.TryParse(note.LastModified, out var dt))
            subText.text = dt.ToLocalTime().ToString("g");
        else
            subText.text = "";
        subText.font = _font;
        subText.fontSize = 12;
        subText.color = SubtextColor;
        subText.alignment = TextAnchor.MiddleLeft;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Actions
    // ─────────────────────────────────────────────────────────────────────

    private void OnNewNote()
    {
        var note = NotesStore.CreateNote("", "");
        ShowDetailView(note.Id);
    }

    private void OnOpenNote(string noteId) => ShowDetailView(noteId);

    private void OnBackToList()
    {
        SaveCurrentNote();
        ShowListView();
    }

    private void OnDeleteNote()
    {
        if (_currentNoteId == null) return;
        NotesStore.DeleteNote(_currentNoteId);
        _currentNoteId = null;
        ShowListView();
    }

    private void SaveCurrentNote()
    {
        if (_currentNoteId == null) return;
        var note = NotesStore.GetNote(_currentNoteId);
        if (note == null || note.IsReadOnly) return;

        var titleChanged = note.Title != _titleInput!.text;
        var bodyChanged = note.Body != _bodyInput!.text;
        if (!titleChanged && !bodyChanged) return;

        NotesStore.UpdateNote(_currentNoteId,
            titleChanged ? _titleInput.text : null,
            bodyChanged ? _bodyInput.text : null);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  UI helpers
    // ─────────────────────────────────────────────────────────────────────

    private static GameObject CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect, float left = 0, float right = 0,
        float top = 0, float bottom = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
