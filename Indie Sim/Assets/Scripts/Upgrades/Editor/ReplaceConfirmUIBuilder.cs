using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static UIBuildKit;

/// <summary>
/// One-click builder for the shop's brand-conflict replace confirm, edited
/// straight into the gameplay canvas prefab:
///   - a ReplaceConfirmPanel on the shop panel (active while the shop is open)
///   - "Replace Panel" under the shop: oversized click blocker + a window in
///     the Settings menu's look, message, Stats button (hover / pad-select
///     shows the swap preview tooltip), Replace and Cancel
/// The buttons are clones of the Settings menu's Back button and the window
/// copies its sprite/colour/outline — placeholders to swap art on.
/// Replaces the empty "Replace Panel" stub if it's still there. Skipped if a
/// ReplaceConfirmPanel already exists, so re-running is safe.
/// </summary>
public static class ReplaceConfirmUIBuilder
{
    private const string CanvasPrefabPath = "Assets/Prefabs/UI/Player Canvas RoguelikeMode.prefab";
    private const string PanelName = "Replace Panel";
    private const string SettingsWindowPath = "Settings Panel/Window";
    private const string SettingsButtonPath = "Settings Panel/Window/Footer/Back Button";

    [MenuItem("Tools/Shop/Build Replace Confirm Panel", priority = 110)]
    private static void Build()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Build Replace Confirm Panel", "Exit Play mode first.", "OK");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            ShopUIController shop = root.GetComponentInChildren<ShopUIController>(true);
            if (shop == null)
            {
                Debug.LogError("[ReplaceConfirmUIBuilder] No ShopUIController in the canvas prefab.");
                return;
            }

            if (root.GetComponentInChildren<ReplaceConfirmPanel>(true) != null)
            {
                Debug.Log("[ReplaceConfirmUIBuilder] A ReplaceConfirmPanel already exists — nothing to do.");
                return;
            }

            Transform window = root.transform.Find(SettingsWindowPath);
            Button templateButton = root.transform.Find(SettingsButtonPath)?.GetComponent<Button>();
            if (window == null || templateButton == null)
            {
                Debug.LogError($"[ReplaceConfirmUIBuilder] Settings menu not found at '{SettingsButtonPath}' — it's the style template.");
                return;
            }

            BuildPanel(root, shop, window, templateButton);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            Debug.Log($"[ReplaceConfirmUIBuilder] Built the replace confirm into {CanvasPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildPanel(GameObject root, ShopUIController shop, Transform settingsWindow, Button templateButton)
    {
        Transform shopRoot = shop.transform;
        SerializedObject shopSo = new SerializedObject(shop);
        CursorTooltip tooltip = shopSo.FindProperty("tooltip").objectReferenceValue as CursorTooltip;

        // Take the stub's slot (just under the tooltip, so the tooltip draws on top).
        int siblingIndex = tooltip != null ? tooltip.transform.GetSiblingIndex() : shopRoot.childCount;
        Transform stub = shopRoot.Find(PanelName);
        if (stub != null && stub.childCount == 0)
        {
            siblingIndex = stub.GetSiblingIndex();
            Object.DestroyImmediate(stub.gameObject);
        }

        // Blocker + window. Oversized blocker: the shop panel is smaller than the screen.
        RectTransform box = ModalBox(shopRoot, PanelName, new Vector2(760f, 380f), oversized: true);
        RectTransform panelRoot = (RectTransform)box.parent;
        panelRoot.SetSiblingIndex(siblingIndex);
        box.name = "Window";
        CopyWindowLook(settingsWindow, box);

        TMP_Text title = Label(box, "Title", "REPLACE CIG?", 36, Top, new Vector2(0f, -45f), new Vector2(680f, 50f), Color.white);
        TMP_Text message = Label(box, "Message", "You already have X.\nReplace it with Y?", 28, Center, new Vector2(0f, 25f), new Vector2(680f, 130f), Color.white);
        TMP_Text templateLabel = templateButton.GetComponentInChildren<TMP_Text>(true);
        if (templateLabel != null)
        {
            title.font = templateLabel.font;
            message.font = templateLabel.font;
        }

        Button stats = CloneButton(templateButton, box, "Stats Button", "STATS", Center, new Vector2(0f, -55f), new Vector2(200f, 50f), 24f);
        stats.gameObject.AddComponent<PointerHoverRelay>();
        Button replace = CloneButton(templateButton, box, "Replace Button", "REPLACE", Bottom, new Vector2(-140f, 60f), new Vector2(240f, 72f), -1f);
        Button cancel = CloneButton(templateButton, box, "Cancel Button", "CANCEL", Bottom, new Vector2(140f, 60f), new Vector2(240f, 72f), -1f);

        ReplaceConfirmPanel panel = shop.gameObject.AddComponent<ReplaceConfirmPanel>();
        SerializedObject so = new SerializedObject(panel);
        so.FindProperty("shop").objectReferenceValue = shop;
        so.FindProperty("tooltip").objectReferenceValue = tooltip;
        so.FindProperty("focusStyle").objectReferenceValue = root.GetComponentInChildren<ButtonFocusStyle>(true);
        so.FindProperty("panelRoot").objectReferenceValue = panelRoot.gameObject;
        so.FindProperty("messageText").objectReferenceValue = message;
        so.FindProperty("replaceButton").objectReferenceValue = replace;
        so.FindProperty("cancelButton").objectReferenceValue = cancel;
        so.FindProperty("statsHover").objectReferenceValue = stats.GetComponent<PointerHoverRelay>();
        so.ApplyModifiedPropertiesWithoutUndo();

        panelRoot.gameObject.SetActive(false);
    }

    private static void CopyWindowLook(Transform settingsWindow, RectTransform box)
    {
        Image source = settingsWindow.GetComponent<Image>();
        Image target = box.GetComponent<Image>();
        if (source != null)
        {
            target.sprite = source.sprite;
            target.type = source.type;
            target.color = source.color;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        }

        Outline sourceOutline = settingsWindow.GetComponent<Outline>();
        if (sourceOutline != null)
        {
            Outline outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = sourceOutline.effectColor;
            outline.effectDistance = sourceOutline.effectDistance;
            outline.useGraphicAlpha = sourceOutline.useGraphicAlpha;
        }
    }

    // A copy of the Settings button with its persistent listeners stripped
    // (ReplaceConfirmPanel wires its buttons in code). fontSize < 0 keeps the template's.
    private static Button CloneButton(Button source, Transform parent, string name, string label, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject clone = Object.Instantiate(source.gameObject, parent);
        clone.name = name;
        Place((RectTransform)clone.transform, anchor, position, size);

        foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = label;
            if (fontSize > 0f) text.fontSize = fontSize;
        }

        Button button = clone.GetComponent<Button>();
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        return button;
    }
}
