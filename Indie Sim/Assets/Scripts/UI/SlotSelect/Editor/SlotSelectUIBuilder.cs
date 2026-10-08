using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static UIBuildKit;

/// <summary>
/// One-click builder for the 4C main-menu UI: a Continue button next to Start,
/// and the Slot Select panel (3 tiles, Back, name prompt, delete confirm),
/// all wired to MainMenu. Plain placeholder styling (UIBuildKit) — restyle
/// freely afterwards; only the serialized references matter.
/// Open Main menu.unity, run Tools/Save/Build Slot Select UI, save the scene.
/// Re-running replaces the panel (Continue is kept if already assigned).
/// </summary>
public static class SlotSelectUIBuilder
{
    [MenuItem("Tools/Save/Build Slot Select UI", priority = 100)]
    private static void Build()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Build Slot Select UI", "Exit Play mode first.", "OK");
            return;
        }

        MainMenu menu = Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            EditorUtility.DisplayDialog("Build Slot Select UI", "Open Main menu.unity first (no MainMenu found in the open scene).", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Build Slot Select UI");
        int undoGroup = Undo.GetCurrentGroup();

        SerializedObject menuSO = new SerializedObject(menu);
        SerializedProperty panelProp = menuSO.FindProperty("slotSelectPanel");
        SerializedProperty continueProp = menuSO.FindProperty("continueButton");

        if (panelProp.objectReferenceValue is SlotSelectPanel oldPanel)
        {
            if (!EditorUtility.DisplayDialog("Build Slot Select UI", "A Slot Select panel already exists. Replace it?", "Replace", "Cancel"))
                return;
            Undo.DestroyObjectImmediate(oldPanel.gameObject);
        }

        if (continueProp.objectReferenceValue == null)
            continueProp.objectReferenceValue = BuildContinueButton(menu, menuSO.FindProperty("menuButtons").objectReferenceValue as GameObject);

        SlotSelectPanel panel = BuildPanel(menu.transform);
        panelProp.objectReferenceValue = panel;
        menuSO.ApplyModifiedProperties();

        panel.gameObject.SetActive(false);
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log("[SlotSelectUIBuilder] Built the Slot Select panel and wired MainMenu. Save the scene.");
    }

    // ─────────────────────────────────────────────────────────────────
    //  CONTINUE
    // ─────────────────────────────────────────────────────────────────

    // Clones the existing Start ("Casual Button") so it matches, relabels
    // Start, and points the clone at MainMenu.ContinueRun.
    private static GameObject BuildContinueButton(MainMenu menu, GameObject menuButtons)
    {
        Transform start = menuButtons != null ? FindDeep(menuButtons.transform, "Casual Button") : null;
        if (start == null)
        {
            Debug.LogWarning("[SlotSelectUIBuilder] Couldn't find 'Casual Button' under Menu Buttons — making a plain Continue button instead.");
            Transform parent = menuButtons != null ? menuButtons.transform : menu.transform;
            Button plain = MakeButton(parent, "Continue Button", "CONTINUE", Center, new Vector2(0f, -120f), new Vector2(320f, 90f), Card, 34);
            UnityEventTools.AddVoidPersistentListener(plain.onClick, new UnityAction(menu.ContinueRun));
            Undo.RegisterCreatedObjectUndo(plain.gameObject, "Continue Button");
            return plain.gameObject;
        }

        TMP_Text startLabel = start.GetComponentInChildren<TMP_Text>(true);
        if (startLabel != null)
        {
            Undo.RecordObject(startLabel, "Relabel Start");
            startLabel.text = "Start";
        }

        GameObject clone = Object.Instantiate(start.gameObject, start.parent);
        clone.name = "Continue Button";
        Undo.RegisterCreatedObjectUndo(clone, "Continue Button");

        RectTransform rect = (RectTransform)clone.transform;
        rect.anchoredPosition += new Vector2(0f, rect.rect.height + 30f);

        TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Continue";

        Button button = clone.GetComponent<Button>();
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddVoidPersistentListener(button.onClick, new UnityAction(menu.ContinueRun));

        return clone;
    }

    // ─────────────────────────────────────────────────────────────────
    //  PANEL
    // ─────────────────────────────────────────────────────────────────

    private static SlotSelectPanel BuildPanel(Transform canvas)
    {
        RectTransform root = NewRect("Slot Select Panel", canvas);
        Stretch(root);
        AddImage(root, Backdrop);
        root.SetAsLastSibling();

        Label(root, "Title", "SELECT SLOT", 64, Top, new Vector2(0f, -120f), new Vector2(1200f, 100f), Color.white);

        SlotTileView[] tiles = new SlotTileView[SaveService.SlotCount];
        for (int i = 0; i < tiles.Length; i++)
            tiles[i] = BuildTile(root, i, new Vector2((i - (tiles.Length - 1) / 2f) * 520f, 20f));

        Button back = MakeButton(root, "Back Button", "BACK", Bottom, new Vector2(0f, 110f), new Vector2(300f, 90f), Card, 34);

        NamePromptDialog prompt = BuildNamePrompt(root);
        ConfirmDialog confirm = BuildConfirmDialog(root, "Confirm Dialog", "Delete this slot?", "DELETE", "CANCEL");

        SlotSelectPanel panel = root.gameObject.AddComponent<SlotSelectPanel>();
        SerializedObject so = new SerializedObject(panel);
        SerializedProperty tilesProp = so.FindProperty("tiles");
        tilesProp.arraySize = tiles.Length;
        for (int i = 0; i < tiles.Length; i++)
            tilesProp.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
        so.FindProperty("backButton").objectReferenceValue = back;
        so.FindProperty("namePrompt").objectReferenceValue = prompt;
        so.FindProperty("confirmDialog").objectReferenceValue = confirm;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(root.gameObject, "Slot Select Panel");
        return panel;
    }

    private static SlotTileView BuildTile(Transform parent, int index, Vector2 position)
    {
        RectTransform tile = NewRect($"Slot Tile {index + 1}", parent);
        Place(tile, Center, position, new Vector2(460f, 560f));
        Image background = AddImage(tile, Card);
        Button select = tile.gameObject.AddComponent<Button>();
        select.targetGraphic = background;

        TMP_Text title = Label(tile, "Title", $"Slot {index + 1}", 40, Top, new Vector2(0f, -60f), new Vector2(420f, 70f), Color.white);
        title.fontStyle = FontStyles.Bold;
        TMP_Text detail = Label(tile, "Detail", "Empty", 30, Top, new Vector2(0f, -190f), new Vector2(420f, 140f), Color.white);
        TMP_Text lastPlayed = Label(tile, "Last Played", "", 22, Top, new Vector2(0f, -300f), new Vector2(420f, 50f), Muted);
        TMP_Text action = Label(tile, "Action", "New Game", 34, Bottom, new Vector2(0f, 150f), new Vector2(420f, 60f), Accent);
        Button delete = MakeButton(tile, "Delete Button", "DELETE", Bottom, new Vector2(0f, 55f), new Vector2(220f, 60f), Danger, 24);

        SlotTileView view = tile.gameObject.AddComponent<SlotTileView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("selectButton").objectReferenceValue = select;
        so.FindProperty("deleteButton").objectReferenceValue = delete;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("detailText").objectReferenceValue = detail;
        so.FindProperty("lastPlayedText").objectReferenceValue = lastPlayed;
        so.FindProperty("actionText").objectReferenceValue = action;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static NamePromptDialog BuildNamePrompt(Transform parent)
    {
        RectTransform box = ModalBox(parent, "Name Prompt", new Vector2(760f, 380f));
        RectTransform dialogRoot = (RectTransform)box.parent;

        Label(box, "Label", "NAME THIS SLOT", 36, Top, new Vector2(0f, -60f), new Vector2(680f, 60f), Color.white);

        GameObject inputGO = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources
        {
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd")
        });
        inputGO.name = "Name Input";
        inputGO.transform.SetParent(box, false);
        Place((RectTransform)inputGO.transform, Center, new Vector2(0f, 10f), new Vector2(600f, 80f));
        TMP_InputField input = inputGO.GetComponent<TMP_InputField>();
        input.characterLimit = 20;
        input.pointSize = 36;
        if (input.placeholder is TMP_Text placeholder) placeholder.text = "Enter a name...";

        Button confirm = MakeButton(box, "Confirm Button", "CONFIRM", Bottom, new Vector2(-150f, 70f), new Vector2(260f, 80f), Card, 30);
        Button cancel = MakeButton(box, "Cancel Button", "CANCEL", Bottom, new Vector2(150f, 70f), new Vector2(260f, 80f), Card, 30);

        NamePromptDialog dialog = dialogRoot.gameObject.AddComponent<NamePromptDialog>();
        SerializedObject so = new SerializedObject(dialog);
        so.FindProperty("nameInput").objectReferenceValue = input;
        so.FindProperty("confirmButton").objectReferenceValue = confirm;
        so.FindProperty("cancelButton").objectReferenceValue = cancel;
        so.FindProperty("keyboard").objectReferenceValue = BuildKeyboard(dialogRoot, input);
        so.ApplyModifiedPropertiesWithoutUndo();

        dialogRoot.gameObject.SetActive(false);
        return dialog;
    }

    // ─────────────────────────────────────────────────────────────────
    //  VIRTUAL KEYBOARD (gamepad only)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds the on-screen keyboard to an existing name prompt (one built
    /// before the keyboard existed). Open Main menu.unity, run, save the scene.
    /// </summary>
    [MenuItem("Tools/Save/Build Virtual Keyboard", priority = 102)]
    private static void BuildKeyboardForExistingPrompt()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Build Virtual Keyboard", "Exit Play mode first.", "OK");
            return;
        }

        NamePromptDialog prompt = Object.FindFirstObjectByType<NamePromptDialog>(FindObjectsInactive.Include);
        if (prompt == null)
        {
            EditorUtility.DisplayDialog("Build Virtual Keyboard", "Open Main menu.unity first (no NamePromptDialog in the open scene).", "OK");
            return;
        }

        SerializedObject so = new SerializedObject(prompt);
        SerializedProperty keyboardProp = so.FindProperty("keyboard");
        if (keyboardProp.objectReferenceValue != null)
        {
            Debug.Log("[SlotSelectUIBuilder] The name prompt already has a virtual keyboard — nothing to do.");
            return;
        }

        TMP_InputField input = so.FindProperty("nameInput").objectReferenceValue as TMP_InputField;
        VirtualKeyboard keyboard = BuildKeyboard((RectTransform)prompt.transform, input);
        Undo.RegisterCreatedObjectUndo(keyboard.gameObject, "Build Virtual Keyboard");
        Undo.RecordObject(prompt, "Build Virtual Keyboard");
        keyboardProp.objectReferenceValue = keyboard;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(prompt.gameObject.scene);
        Debug.Log("[SlotSelectUIBuilder] Built the virtual keyboard under the name prompt. Save the scene.");
    }

    // A panel along the bottom of the prompt's full-screen blocker, below the
    // centred box. Keys come from the hidden template at runtime.
    private static VirtualKeyboard BuildKeyboard(RectTransform dialogRoot, TMP_InputField input)
    {
        RectTransform panel = NewRect("Virtual Keyboard", dialogRoot);
        Place(panel, Bottom, new Vector2(0f, 170f), new Vector2(840f, 320f));
        AddImage(panel, Box);

        Button template = MakeButton(panel, "Key Template", "A", Center, Vector2.zero, new Vector2(74f, 52f), Card, 28);
        template.gameObject.SetActive(false);

        VirtualKeyboard keyboard = panel.gameObject.AddComponent<VirtualKeyboard>();
        SerializedObject so = new SerializedObject(keyboard);
        so.FindProperty("target").objectReferenceValue = input;
        so.FindProperty("keyTemplate").objectReferenceValue = template;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.gameObject.SetActive(false);
        return keyboard;
    }
}
