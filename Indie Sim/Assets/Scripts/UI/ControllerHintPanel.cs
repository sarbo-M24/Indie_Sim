using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Row of gamepad button hints under a menu (Settings panel, and any other
/// menu driven by a controller): an icon plus a label per hint. Place it
/// inside the menu so it only exists while the menu is open; it then shows
/// `content` only while a controller is connected, with that family's sprites
/// from `glyphs`. Labels are plain text set per menu in the Inspector. A hint
/// whose sprite isn't assigned is hidden whole (icon and label). Event-driven
/// via ControllerPresence.OnChanged — no polling. Keep this object active and
/// toggle only `content`, or it can't hear the event.
/// </summary>
public class ControllerHintPanel : MonoBehaviour
{
    [Serializable]
    private class Hint
    {
        [Tooltip("The hint's group (icon + label). Hidden when the icon has no sprite.")]
        public GameObject root;
        public Image icon;
    }

    [Tooltip("Sprites to show. Each menu can use its own set.")]
    [SerializeField] private ControllerGlyphSet glyphs;
    [Tooltip("Child holding the Horizontal Layout Group and the hints. Hidden with no controller.")]
    [SerializeField] private GameObject content;
    [Tooltip("Four hints, left to right; hint N uses the glyph set's slot N.")]
    [SerializeField] private Hint[] hints = new Hint[ControllerGlyphSet.HintSlotCount];

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
        if (!show || hints == null) return;

        if (glyphs == null && !_warnedNoGlyphs)
        {
            _warnedNoGlyphs = true;
            Debug.LogWarning($"[ControllerHintPanel] '{name}' has no Controller Glyph Set — hints hidden.", this);
        }

        for (int i = 0; i < hints.Length; i++)
        {
            Hint hint = hints[i];
            if (hint == null || hint.icon == null) continue;

            Sprite sprite = glyphs != null ? glyphs.GetHint(ControllerPresence.Family, i) : null;
            hint.icon.sprite = sprite;

            GameObject root = hint.root != null ? hint.root : hint.icon.gameObject;
            root.SetActive(sprite != null);
        }
    }
}
