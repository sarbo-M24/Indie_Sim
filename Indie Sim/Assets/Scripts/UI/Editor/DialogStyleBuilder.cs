using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIBuildKit;

/// <summary>
/// Gives every confirm popup the main menu's button look and a bordered box:
///   - each ConfirmDialog in Main menu (slot delete) and the gameplay canvas
///     prefab (pause-menu quit / give-up warning)
///   - the shop's replace confirm (its window already has a border)
/// and lays out BossArena's Demo Complete screen: title, a thanks + wishlist
/// message, the run stats and a main-menu-style MAIN MENU button.
/// Restyles in place (no references change), so re-running is safe.
/// </summary>
public static class DialogStyleBuilder
{
    private const string MainMenuScenePath = "Assets/Scenes/Main menu.unity";
    private const string BossScenePath = "Assets/Scenes/BossArena.unity";
    private const string CanvasPrefabPath = "Assets/Prefabs/UI/Player Canvas RoguelikeMode.prefab";

    private const string DemoMessage =
        "Thanks for playing the demo!\n" +
        "The full game is still in the works, with more guns,\n" +
        "enemies, bosses and dungeons on the way.\n" +
        "If you had fun, please wishlist One Bit Kill on Steam!";

    [MenuItem("Tools/UI/Restyle Dialogs + Demo Complete", priority = 101)]
    private static void Build()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Restyle Dialogs", "Exit Play mode first.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        RestylePrefab();
        RestyleScene(MainMenuScenePath, scene =>
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (ConfirmDialog dialog in root.GetComponentsInChildren<ConfirmDialog>(true))
                    StyleDialog(dialog);
        });
        RestyleScene(BossScenePath, scene =>
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (DemoCompleteScreen screen in root.GetComponentsInChildren<DemoCompleteScreen>(true))
                    LayOutDemoComplete(screen);
        });
        Debug.Log("[DialogStyleBuilder] Dialogs and Demo Complete restyled.");
    }

    private static void RestylePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            foreach (ConfirmDialog dialog in root.GetComponentsInChildren<ConfirmDialog>(true))
                StyleDialog(dialog);

            foreach (ReplaceConfirmPanel panel in root.GetComponentsInChildren<ReplaceConfirmPanel>(true))
            {
                SerializedObject so = new SerializedObject(panel);
                foreach (string field in new[] { "replaceButton", "cancelButton" })
                    if (so.FindProperty(field).objectReferenceValue is Button button)
                        StyleMenuButton(button, 28);
                if (so.FindProperty("statsHover").objectReferenceValue is Component stats && stats.TryGetComponent(out Button statsButton))
                    StyleMenuButton(statsButton, 22);
            }

            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RestyleScene(string path, System.Action<Scene> edit)
    {
        bool alreadyOpen = SceneManager.GetSceneByPath(path).isLoaded;
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        edit(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
    }

    private static void StyleDialog(ConfirmDialog dialog)
    {
        SerializedObject so = new SerializedObject(dialog);
        Button yes = so.FindProperty("yesButton").objectReferenceValue as Button;
        Button no = so.FindProperty("noButton").objectReferenceValue as Button;

        foreach (Button button in new[] { yes, no })
        {
            if (button == null) continue;
            ((RectTransform)button.transform).sizeDelta = MenuButtonSize;
            StyleMenuButton(button, 28);

            // The box is the buttons' parent.
            if (button.transform.parent.TryGetComponent(out Image box)) AddBorder(box);
        }
        if (yes != null) ((RectTransform)yes.transform).anchoredPosition = new Vector2(-170f, 70f);
        if (no != null) ((RectTransform)no.transform).anchoredPosition = new Vector2(170f, 70f);
        EditorUtility.SetDirty(dialog.gameObject);
    }

    private static void LayOutDemoComplete(DemoCompleteScreen screen)
    {
        SerializedObject so = new SerializedObject(screen);
        GameObject panel = so.FindProperty("panel").objectReferenceValue as GameObject;
        TMP_Text summary = so.FindProperty("summaryText").objectReferenceValue as TMP_Text;
        Button mainMenu = so.FindProperty("mainMenuButton").objectReferenceValue as Button;
        if (panel == null || summary == null || mainMenu == null)
        {
            Debug.LogError("[DialogStyleBuilder] DemoCompleteScreen is missing a reference.");
            return;
        }

        TMP_FontAsset menuFont = MenuFont;

        TMP_Text title = FindOrMakeLabel(panel.transform, "Title");
        Place((RectTransform)title.transform, Center, new Vector2(0f, 300f), new Vector2(1400f, 120f));
        title.text = "DEMO COMPLETE";
        title.fontSize = 84;
        title.color = Color.white;
        if (menuFont != null) { title.font = menuFont; title.fontSharedMaterial = menuFont.material; }

        TMP_Text message = FindOrMakeLabel(panel.transform, "Message");
        Place((RectTransform)message.transform, Center, new Vector2(0f, 120f), new Vector2(1300f, 220f));
        message.text = DemoMessage;
        message.fontSize = 36;
        message.lineSpacing = 10f;
        message.color = Color.white;

        Place((RectTransform)summary.transform, Center, new Vector2(0f, -70f), new Vector2(700f, 150f));
        summary.fontSize = 30;
        summary.color = Muted;
        summary.alignment = TextAlignmentOptions.Center;

        Place((RectTransform)mainMenu.transform, Center, new Vector2(0f, -240f), new Vector2(400f, 80f));
        TMP_Text label = mainMenu.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "MAIN MENU";
        StyleMenuButton(mainMenu, 35);

        EditorUtility.SetDirty(screen.gameObject);
    }

    private static TMP_Text FindOrMakeLabel(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out TMP_Text found)) return found;
        TextMeshProUGUI label = Label(parent, name, "", 32, Center, Vector2.zero, new Vector2(100f, 100f), Color.white);
        label.transform.SetSiblingIndex(0);
        return label;
    }
}
