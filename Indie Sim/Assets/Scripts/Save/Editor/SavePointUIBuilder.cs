using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static UIBuildKit;

/// <summary>
/// One-click builder for the pause-menu run UI, edited straight into the
/// gameplay canvas prefab (used by RoguelikeMode and BossArena), so no scene
/// needs touching:
///   - the quit warning (a ConfirmDialog under the pause panel, also used by
///     Give Up's confirm)
///   - Save &amp; Exit (in the Exit button's spot; OptionsMenu shows it instead
///     of Exit only while the store is open) and Give Up, both clones of Exit
///   - removes the store panel's old Save &amp; Exit button, which moved here
/// Each piece is only built if its OptionsMenu field is still empty, so
/// re-running is safe. Restyle/move freely afterwards.
/// </summary>
public static class SavePointUIBuilder
{
    private const string CanvasPrefabPath = "Assets/Prefabs/UI/Player Canvas RoguelikeMode.prefab";
    private const string OldShopSaveExitName = "Save & Exit Button";

    [MenuItem("Tools/Save/Build Pause Menu Buttons", priority = 101)]
    private static void Build()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Build Pause Menu Buttons", "Exit Play mode first.", "OK");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            OptionsMenu menu = root.GetComponentInChildren<OptionsMenu>(true);
            if (menu == null)
            {
                Debug.LogError("[SavePointUIBuilder] No OptionsMenu in the canvas prefab.");
                return;
            }

            bool builtWarning = BuildQuitWarning(menu);
            bool builtButtons = BuildRunButtons(menu);
            bool removedOld = RemoveOldShopSaveExit(root);

            if (builtWarning || builtButtons || removedOld)
            {
                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
                Debug.Log($"[SavePointUIBuilder] Updated {CanvasPrefabPath}: quit warning {(builtWarning ? "built" : "already set")}, " +
                          $"run buttons {(builtButtons ? "built" : "already set")}, old store Save & Exit {(removedOld ? "removed" : "not found")}.");
            }
            else
            {
                Debug.Log("[SavePointUIBuilder] Everything is already set up — nothing to do.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Exit is the button wired to OptionsMenu.ExitToMainMenu. Save & Exit
    // takes its exact spot (only one of the two shows at a time); Give Up
    // goes one row below.
    private static bool BuildRunButtons(OptionsMenu menu)
    {
        SerializedObject so = new SerializedObject(menu);
        SerializedProperty exitProp = so.FindProperty("exitButton");
        SerializedProperty saveExitProp = so.FindProperty("saveAndExitButton");
        SerializedProperty giveUpProp = so.FindProperty("giveUpButton");

        if (exitProp.objectReferenceValue == null)
            exitProp.objectReferenceValue = FindExitButton(so.FindProperty("optionsPanel").objectReferenceValue as GameObject);

        Button exit = exitProp.objectReferenceValue as Button;
        if (exit == null)
        {
            Debug.LogError("[SavePointUIBuilder] No pause-menu button wired to OptionsMenu.ExitToMainMenu — assign OptionsMenu's Exit Button by hand and re-run.");
            so.ApplyModifiedPropertiesWithoutUndo();
            return false;
        }

        bool built = false;
        RectTransform exitRect = (RectTransform)exit.transform;

        if (saveExitProp.objectReferenceValue == null)
        {
            Button saveExit = CloneButton(exit, "Save & Exit", "SAVE & EXIT", exitRect.anchoredPosition);
            saveExit.gameObject.SetActive(false);
            saveExitProp.objectReferenceValue = saveExit;
            built = true;
        }

        if (giveUpProp.objectReferenceValue == null)
        {
            Vector2 below = exitRect.anchoredPosition - new Vector2(0f, exitRect.rect.height + 25f);
            giveUpProp.objectReferenceValue = CloneButton(exit, "Give Up", "GIVE UP", below);
            built = true;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return built;
    }

    private static Button FindExitButton(GameObject pausePanel)
    {
        if (pausePanel == null) return null;
        foreach (Button button in pausePanel.GetComponentsInChildren<Button>(true))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentMethodName(i) == nameof(OptionsMenu.ExitToMainMenu))
                    return button;
        return null;
    }

    // A copy of source beside it with its persistent listeners stripped
    // (OptionsMenu wires the clones in code), widened to fit longer labels.
    private static Button CloneButton(Button source, string name, string label, Vector2 position)
    {
        GameObject clone = Object.Instantiate(source.gameObject, source.transform.parent);
        clone.name = name;
        clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

        RectTransform rect = (RectTransform)clone.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 200f), rect.sizeDelta.y);

        foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(true))
            text.text = label;

        Button button = clone.GetComponent<Button>();
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        return button;
    }

    // The store's own Save & Exit (built by the earlier version of this tool).
    private static bool RemoveOldShopSaveExit(GameObject root)
    {
        ShopUIController shop = root.GetComponentInChildren<ShopUIController>(true);
        Transform old = shop != null ? FindDeep(shop.transform, OldShopSaveExitName) : null;
        if (old == null) return false;

        Object.DestroyImmediate(old.gameObject);
        return true;
    }

    // A ConfirmDialog under the pause panel (so its gamepad focus stays in
    // the pause menu's GamepadMenuPanel), with an oversized blocker.
    private static bool BuildQuitWarning(OptionsMenu menu)
    {
        SerializedObject so = new SerializedObject(menu);
        SerializedProperty warningProp = so.FindProperty("quitWarning");
        if (warningProp.objectReferenceValue != null) return false;

        GameObject pausePanel = so.FindProperty("optionsPanel").objectReferenceValue as GameObject;
        Transform parent = pausePanel != null ? pausePanel.transform : menu.transform;

        ConfirmDialog dialog = BuildConfirmDialog(parent, "Quit Warning",
            "Quit to the main menu?\nProgress since your last checkpoint will be lost.", "QUIT", "CANCEL", oversized: true);
        dialog.transform.SetAsLastSibling();

        warningProp.objectReferenceValue = dialog;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
