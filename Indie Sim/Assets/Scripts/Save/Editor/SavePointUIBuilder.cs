using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static UIBuildKit;

/// <summary>
/// One-click builder for the 4D UI, edited straight into the gameplay canvas
/// prefab (used by RoguelikeMode and BossArena), so no scene needs touching:
///   - the store's Save &amp; Exit button (a clone of its Continue button)
///   - the pause menu's quit warning (a ConfirmDialog under the pause panel)
/// Each is only built if its ShopUIController / OptionsMenu field is still
/// empty, so re-running is safe. Restyle/move freely afterwards.
/// </summary>
public static class SavePointUIBuilder
{
    private const string CanvasPrefabPath = "Assets/Prefabs/UI/Player Canvas RoguelikeMode.prefab";

    [MenuItem("Tools/Save/Build Save & Exit + Quit Warning", priority = 101)]
    private static void Build()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Build Save & Exit + Quit Warning", "Exit Play mode first.", "OK");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            bool builtSaveExit = BuildSaveAndExit(root);
            bool builtWarning = BuildQuitWarning(root);

            if (builtSaveExit || builtWarning)
            {
                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
                Debug.Log($"[SavePointUIBuilder] Updated {CanvasPrefabPath}: " +
                          $"Save & Exit {(builtSaveExit ? "built" : "already set")}, quit warning {(builtWarning ? "built" : "already set")}.");
            }
            else
            {
                Debug.Log("[SavePointUIBuilder] Both are already set up — nothing to do.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Clones the shop's Continue button just below itself.
    private static bool BuildSaveAndExit(GameObject root)
    {
        ShopUIController shop = root.GetComponentInChildren<ShopUIController>(true);
        if (shop == null)
        {
            Debug.LogError("[SavePointUIBuilder] No ShopUIController in the canvas prefab.");
            return false;
        }

        SerializedObject so = new SerializedObject(shop);
        SerializedProperty saveExitProp = so.FindProperty("saveAndExitButton");
        if (saveExitProp.objectReferenceValue != null) return false;

        Button continueButton = so.FindProperty("continueButton").objectReferenceValue as Button;
        if (continueButton == null)
        {
            Debug.LogError("[SavePointUIBuilder] ShopUIController has no Continue button to clone.");
            return false;
        }

        GameObject clone = Object.Instantiate(continueButton.gameObject, continueButton.transform.parent);
        clone.name = "Save & Exit Button";
        clone.transform.SetSiblingIndex(continueButton.transform.GetSiblingIndex() + 1);

        RectTransform rect = (RectTransform)clone.transform;
        rect.anchoredPosition -= new Vector2(0f, rect.rect.height + 16f);

        foreach (TMP_Text label in clone.GetComponentsInChildren<TMP_Text>(true))
            label.text = "SAVE & EXIT";

        Button button = clone.GetComponent<Button>();
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);

        saveExitProp.objectReferenceValue = button;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // A ConfirmDialog under the pause panel (so its gamepad focus stays in
    // the pause menu's GamepadMenuPanel), with an oversized blocker.
    private static bool BuildQuitWarning(GameObject root)
    {
        OptionsMenu menu = root.GetComponentInChildren<OptionsMenu>(true);
        if (menu == null)
        {
            Debug.LogError("[SavePointUIBuilder] No OptionsMenu in the canvas prefab.");
            return false;
        }

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
