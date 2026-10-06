using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Row of gamepad button hints under a menu (Settings panel, and any other
/// menu driven by a controller). Place it inside the menu so it only exists
/// while the menu is open; it then shows `content` only while a controller is
/// connected, with that family's sprites from `glyphs`. Event-driven via
/// ControllerPresence.OnChanged — no polling. Keep this object active and
/// toggle only `content`, or it can't hear the event.
/// </summary>
public class ControllerHintPanel : MonoBehaviour
{
    [Tooltip("Sprites to show. Each menu can use its own set.")]
    [SerializeField] private ControllerGlyphSet glyphs;
    [Tooltip("Child holding the Horizontal Layout Group and the slots. Hidden with no controller.")]
    [SerializeField] private GameObject content;
    [Tooltip("Four Image slots, left to right. A slot with no sprite assigned is hidden.")]
    [SerializeField] private Image[] slots = new Image[ControllerGlyphSet.HintSlotCount];

    private bool _warnedNoGlyphs;

    private void OnEnable()
    {
        ControllerPresence.OnChanged += OnControllerChanged;
        Refresh();
    }

    private void OnDisable()
    {
        ControllerPresence.OnChanged -= OnControllerChanged;
    }

    private void OnControllerChanged(ControllerFamily family) => Refresh();

    private void Refresh()
    {
        bool show = ControllerPresence.IsConnected;
        if (content != null) content.SetActive(show);
        if (!show || slots == null) return;

        if (glyphs == null && !_warnedNoGlyphs)
        {
            _warnedNoGlyphs = true;
            Debug.LogWarning($"[ControllerHintPanel] '{name}' has no Controller Glyph Set — hints hidden.", this);
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            Sprite sprite = glyphs != null ? glyphs.GetHint(ControllerPresence.Family, i) : null;
            slots[i].sprite = sprite;
            slots[i].gameObject.SetActive(sprite != null);
        }
    }
}
