using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared uGUI primitives for the editor UI builders (SlotSelectUIBuilder,
/// SavePointUIBuilder): plain placeholder styling in the menus' existing
/// greys, meant to be restyled by hand afterwards.
/// </summary>
public static class UIBuildKit
{
    public static readonly Color Backdrop = new Color(0.04f, 0.04f, 0.04f, 0.96f);
    public static readonly Color Modal = new Color(0f, 0f, 0f, 0.75f);
    public static readonly Color Card = new Color(0.16f, 0.16f, 0.16f, 1f);
    public static readonly Color Box = new Color(0.1f, 0.1f, 0.1f, 1f);
    public static readonly Color Danger = new Color(0.45f, 0.12f, 0.12f, 1f);
    public static readonly Color Muted = new Color(0.7f, 0.7f, 0.7f, 1f);
    public static readonly Color Accent = new Color(0.95f, 0.8f, 0.3f, 1f);

    public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    public static readonly Vector2 Top = new Vector2(0.5f, 1f);
    public static readonly Vector2 Bottom = new Vector2(0.5f, 0f);

    private static Sprite Sprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    public static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = Center;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static Image AddImage(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = Sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Vector2 anchor, Vector2 position, Vector2 box, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        Place(rect, anchor, position, box);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    public static Button MakeButton(Transform parent, string name, string text, Vector2 anchor, Vector2 position, Vector2 size, Color color, float fontSize)
    {
        RectTransform rect = NewRect(name, parent);
        Place(rect, anchor, position, size);
        Image background = AddImage(rect, color);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        TextMeshProUGUI label = Label(rect, "Text (TMP)", text, fontSize, Center, Vector2.zero, size, Color.white);
        Stretch((RectTransform)label.transform);
        return button;
    }

    /// <summary>
    /// Click blocker holding a centred box; returns the box (its parent is the
    /// blocker). oversized: the blocker is a huge centred rect instead of
    /// stretched, so it covers the screen even under a smaller parent panel.
    /// </summary>
    public static RectTransform ModalBox(Transform parent, string name, Vector2 size, bool oversized = false)
    {
        RectTransform blocker = NewRect(name, parent);
        if (oversized) Place(blocker, Center, Vector2.zero, new Vector2(8000f, 8000f));
        else Stretch(blocker);
        AddImage(blocker, Modal);

        RectTransform box = NewRect("Box", blocker);
        Place(box, Center, Vector2.zero, size);
        AddImage(box, Box);
        return box;
    }

    /// <summary>A wired, hidden ConfirmDialog. Yes is styled as the dangerous option.</summary>
    public static ConfirmDialog BuildConfirmDialog(Transform parent, string name, string message, string yesText, string noText, bool oversized = false)
    {
        RectTransform box = ModalBox(parent, name, new Vector2(760f, 340f), oversized);
        RectTransform dialogRoot = (RectTransform)box.parent;

        TMP_Text messageText = Label(box, "Message", message, 32, Center, new Vector2(0f, 50f), new Vector2(680f, 150f), Color.white);
        Button yes = MakeButton(box, "Yes Button", yesText, Bottom, new Vector2(-150f, 70f), new Vector2(260f, 80f), Danger, 30);
        Button no = MakeButton(box, "No Button", noText, Bottom, new Vector2(150f, 70f), new Vector2(260f, 80f), Card, 30);

        ConfirmDialog dialog = dialogRoot.gameObject.AddComponent<ConfirmDialog>();
        SerializedObject so = new SerializedObject(dialog);
        so.FindProperty("messageText").objectReferenceValue = messageText;
        so.FindProperty("yesButton").objectReferenceValue = yes;
        so.FindProperty("noButton").objectReferenceValue = no;
        so.ApplyModifiedPropertiesWithoutUndo();

        dialogRoot.gameObject.SetActive(false);
        return dialog;
    }

    public static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
