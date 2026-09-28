using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Keeps gamepad focus inside an open panel: while a gamepad is driving the
/// UI and nothing under `root` is selected, selects `preferred` (or the
/// panel's first usable Selectable). Mouse users are left alone, so no button
/// sits highlighted under a cursor that isn't on it. Call every frame the
/// panel is open.
/// </summary>
public static class UIFocus
{
    public static void EnsureSelection(Transform root, Selectable preferred = null)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || root == null || !InputManager.UsingGamepad) return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(root)) return;

        Selectable target = IsUsable(preferred) ? preferred : FirstUsable(root);
        if (target != null) eventSystem.SetSelectedGameObject(target.gameObject);
    }

    /// <summary>
    /// Links every usable Selectable under `root` into one top-to-bottom list
    /// by on-screen height — explicit Up/Down, wrapping at the ends, so
    /// hierarchy order or rotated art can't send the D-pad the wrong way.
    /// Returns the top one (the natural default focus), or null.
    /// </summary>
    public static Selectable LinkVertical(Transform root)
    {
        List<Selectable> list = new List<Selectable>();
        foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>())
            if (IsUsable(selectable)) list.Add(selectable);

        list.Sort((a, b) => ScreenY(b).CompareTo(ScreenY(a)));

        int count = list.Count;
        for (int i = 0; i < count; i++)
        {
            Navigation nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = list[(i - 1 + count) % count],
                selectOnDown = list[(i + 1) % count]
            };
            list[i].navigation = nav;
        }

        return count > 0 ? list[0] : null;
    }

    private static float ScreenY(Selectable selectable)
    {
        RectTransform rect = selectable.transform as RectTransform;
        return rect != null ? rect.TransformPoint(rect.rect.center).y : selectable.transform.position.y;
    }

    private static Selectable FirstUsable(Transform root)
    {
        foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>())
            if (IsUsable(selectable)) return selectable;
        return null;
    }

    private static bool IsUsable(Selectable selectable)
    {
        return selectable != null && selectable.IsActive() && selectable.IsInteractable();
    }
}
