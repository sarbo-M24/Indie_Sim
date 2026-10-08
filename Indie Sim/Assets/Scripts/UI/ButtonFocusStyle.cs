using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One focus look for every button under this object (put it on a canvas
/// root): hovered by the mouse or selected by the gamepad, a button grows
/// and takes a reddish tint — see ButtonFocusScale.EnableGlow.
///
/// The Button's own ColorTint transition is flattened (highlighted and
/// selected = normal) — its Selected colour was darkening the grey button
/// sprites on gamepad only, which is what made mouse and pad look different.
/// Only ColorTint buttons are touched, and nothing under an Excluded root —
/// the upgrade shop keeps its own look (cards, pack cigs, its buttons).
///
/// Runs once at Awake over inactive children too. Other scripts that add a
/// ButtonFocusScale (shop, pause menu...) find the one added here, or this
/// upgrades theirs — either Awake order works.
/// </summary>
public class ButtonFocusStyle : MonoBehaviour
{
    [SerializeField] private ButtonFocusGlowSettings glow = new ButtonFocusGlowSettings();
    [Tooltip("Buttons under these roots are left exactly as authored (e.g. the upgrade shop).")]
    [SerializeField] private Transform[] excluded;
    [Tooltip("Pressed colour multiplier — a light red flash instead of darkening.")]
    [SerializeField] private Color pressedColor = new Color(1f, 0.82f, 0.8f, 1f);

    private void Awake()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
            if (!IsExcluded(button.transform)) Apply(button);
    }

    /// <summary>
    /// Gives one button this menu look, excluded or not — for a popup inside
    /// an excluded root (the shop's replace confirm) that should still match
    /// the menus. ColorTint buttons only.
    /// </summary>
    public void Apply(Button button)
    {
        if (button == null || button.transition != Selectable.Transition.ColorTint) return;

        ColorBlock colors = button.colors;
        colors.highlightedColor = colors.normalColor;
        colors.selectedColor = colors.normalColor;
        colors.pressedColor = pressedColor;
        button.colors = colors;

        if (!button.TryGetComponent(out ButtonFocusScale focus))
            focus = button.gameObject.AddComponent<ButtonFocusScale>();
        focus.EnableGlow(glow);
    }

    private bool IsExcluded(Transform t)
    {
        if (excluded == null) return false;
        foreach (Transform root in excluded)
            if (root != null && t.IsChildOf(root)) return true;
        return false;
    }
}

[System.Serializable]
public class ButtonFocusGlowSettings
{
    [Header("Grow + tint")]
    [Min(1f)] public float focusedScale = 1.15f;
    [Tooltip("Multiplied into the button's own Image colour while focused.")]
    public Color focusTint = new Color(1f, 0.72f, 0.68f, 1f);
}
