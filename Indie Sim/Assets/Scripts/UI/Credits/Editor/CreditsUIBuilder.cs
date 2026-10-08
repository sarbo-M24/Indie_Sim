using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static UIBuildKit;

/// <summary>
/// One-click builder for the main menu's Credits: a Credits button (a copy
/// of the Controls button, placed above Quit) and the Credits panel, wired
/// to MainMenu and to Assets/Data/Credits.asset (created with starter
/// entries if missing). Plain placeholder styling (UIBuildKit) — restyle
/// freely; only the serialized references matter.
/// Open Main menu.unity, run Tools/UI/Build Credits Panel, save the scene.
/// Re-running replaces the panel and keeps the button.
/// </summary>
public static class CreditsUIBuilder
{
    private const string DataPath = "Assets/Data/Credits.asset";

    [MenuItem("Tools/UI/Build Credits Panel", priority = 100)]
    private static void Build()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Build Credits Panel", "Exit Play mode first.", "OK");
            return;
        }

        MainMenu menu = Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            EditorUtility.DisplayDialog("Build Credits Panel", "Open Main menu.unity first (no MainMenu found in the open scene).", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Build Credits Panel");
        int undoGroup = Undo.GetCurrentGroup();

        SerializedObject menuSO = new SerializedObject(menu);
        SerializedProperty panelProp = menuSO.FindProperty("creditsPanel");

        if (panelProp.objectReferenceValue is CreditsPanel oldPanel)
        {
            if (!EditorUtility.DisplayDialog("Build Credits Panel", "A Credits panel already exists. Replace it?", "Replace", "Cancel"))
                return;
            Undo.DestroyObjectImmediate(oldPanel.gameObject);
        }

        GameObject menuButtons = menuSO.FindProperty("menuButtons").objectReferenceValue as GameObject;
        TMP_FontAsset font = EnsureCreditsButton(menu, menuButtons);

        CreditsPanel panel = BuildPanel(menu.transform, LoadOrCreateData(), font);
        panelProp.objectReferenceValue = panel;
        menuSO.ApplyModifiedProperties();

        panel.gameObject.SetActive(false);
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log("[CreditsUIBuilder] Built the Credits panel and wired MainMenu. Save the scene; edit the text in " + DataPath + ".");
    }

    // ─────────────────────────────────────────────────────────────────
    //  BUTTON
    // ─────────────────────────────────────────────────────────────────

    // Copies "Controls btn" (so it matches) into the button column just above
    // Quit, pointed at MainMenu.OpenCredits, and grows the column by one slot.
    // Returns the menu's button font for the panel to reuse.
    private static TMP_FontAsset EnsureCreditsButton(MainMenu menu, GameObject menuButtons)
    {
        Transform template = menuButtons != null ? FindDeep(menuButtons.transform, "Controls btn") : null;
        TMP_Text templateLabel = template != null ? template.GetComponentInChildren<TMP_Text>(true) : null;
        TMP_FontAsset font = templateLabel != null ? templateLabel.font : null;

        if (menuButtons != null && FindDeep(menuButtons.transform, "Credits btn") != null) return font;

        if (template == null)
        {
            Debug.LogWarning("[CreditsUIBuilder] Couldn't find 'Controls btn' under Menu Buttons — add a button calling MainMenu.OpenCredits by hand.");
            return font;
        }

        GameObject clone = Object.Instantiate(template.gameObject, template.parent);
        clone.name = "Credits btn";
        Undo.RegisterCreatedObjectUndo(clone, "Credits Button");

        Transform quit = FindDeep(template.parent, "Quit btn");
        clone.transform.SetSiblingIndex(quit != null ? quit.GetSiblingIndex() : template.GetSiblingIndex() + 1);

        TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "CREDITS";

        Button button = clone.GetComponent<Button>();
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddVoidPersistentListener(button.onClick, new UnityAction(menu.OpenCredits));

        // The column is a fixed-height VerticalLayoutGroup that already fills
        // the screen: make room for one more button, then scale the column
        // down so it keeps its old footprint.
        RectTransform column = (RectTransform)template.parent;
        VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();
        float step = ((RectTransform)template).rect.height + (layout != null ? layout.spacing : 0f);
        float oldHeight = column.rect.height;
        Undo.RecordObject(column, "Grow Button Column");
        column.sizeDelta += new Vector2(0f, step);
        column.localScale *= oldHeight / (oldHeight + step);

        return font;
    }

    // ─────────────────────────────────────────────────────────────────
    //  PANEL
    // ─────────────────────────────────────────────────────────────────

    private static CreditsPanel BuildPanel(Transform canvas, CreditsData data, TMP_FontAsset font)
    {
        RectTransform root = NewRect("Credits Panel", canvas);
        Stretch(root);
        AddImage(root, Backdrop);
        root.SetAsLastSibling();

        TextMeshProUGUI title = Label(root, "Title", "CREDITS", 64, Top, new Vector2(0f, -110f), new Vector2(1200f, 100f), Color.white);
        if (font != null) title.font = font;

        // Scroll view: viewport (masked) → body text, which sizes itself to its text.
        RectTransform scrollRect = NewRect("Scroll", root);
        scrollRect.anchorMin = new Vector2(0.5f, 0f);
        scrollRect.anchorMax = new Vector2(0.5f, 1f);
        scrollRect.pivot = Center;
        scrollRect.sizeDelta = new Vector2(1100f, -380f);
        scrollRect.anchoredPosition = new Vector2(0f, 10f);

        RectTransform viewport = NewRect("Viewport", scrollRect);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image hitArea = viewport.gameObject.AddComponent<Image>(); // lets the wheel / drag reach the ScrollRect
        hitArea.color = Color.clear;

        RectTransform content = NewRect("Body", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        TextMeshProUGUI body = content.gameObject.AddComponent<TextMeshProUGUI>();
        body.fontSize = 34;
        body.alignment = TextAlignmentOptions.Top;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.raycastTarget = false;
        body.margin = new Vector4(0f, 40f, 0f, 200f); // breathing room before the first line and after the last
        if (font != null) body.font = font;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        Button back = MakeButton(root, "Back Button", "BACK", Bottom, new Vector2(0f, 90f), new Vector2(300f, 90f), Card, 34);
        if (font != null) back.GetComponentInChildren<TMP_Text>().font = font;

        CreditsPanel panel = root.gameObject.AddComponent<CreditsPanel>();
        SerializedObject so = new SerializedObject(panel);
        so.FindProperty("data").objectReferenceValue = data;
        so.FindProperty("body").objectReferenceValue = body;
        so.FindProperty("scroll").objectReferenceValue = scroll;
        so.FindProperty("backButton").objectReferenceValue = back;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(root.gameObject, "Credits Panel");
        return panel;
    }

    // ─────────────────────────────────────────────────────────────────
    //  DATA
    // ─────────────────────────────────────────────────────────────────

    private static CreditsData LoadOrCreateData()
    {
        CreditsData data = AssetDatabase.LoadAssetAtPath<CreditsData>(DataPath);
        if (data != null) return data;

        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");

        data = ScriptableObject.CreateInstance<CreditsData>();
        data.sections = new[]
        {
            new CreditsData.Section { heading = "A GAME BY", lines = new[] { "Your Name" } },
            new CreditsData.Section { heading = "MUSIC", lines = new[] { "\"Track Title\" by Artist (link)", "Licensed under ... (link)" } },
            new CreditsData.Section { heading = "SOUND EFFECTS & ART", lines = new[] { "Kenney (kenney.nl) — CC0" } },
            new CreditsData.Section { heading = "MADE WITH", lines = new[] { "Unity" } },
            new CreditsData.Section { heading = "", lines = new[] { "Thank you for playing!" } },
        };
        AssetDatabase.CreateAsset(data, DataPath);
        AssetDatabase.SaveAssets();
        return data;
    }
}
