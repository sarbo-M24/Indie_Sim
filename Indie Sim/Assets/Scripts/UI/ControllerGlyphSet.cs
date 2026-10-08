using System.Collections.Generic;
using UnityEngine;

/// <summary>Single gamepad buttons with their own glyph (outside the hint rows).</summary>
public enum GamepadGlyph { RightShoulder, West, North, Start }

/// <summary>
/// Gamepad button sprites per controller family: four hint slots (shown by
/// ControllerHintPanel) and single buttons (RB / R1, X, Y, Start, shown by
/// GamepadGlyphImage). A missing sprite returns null — the caller hides that
/// slot — and logs one warning per missing entry, not one per refresh.
/// </summary>
[CreateAssetMenu(fileName = "ControllerGlyphSet", menuName = "UI/Controller Glyph Set")]
public class ControllerGlyphSet : ScriptableObject
{
    public const int HintSlotCount = 4;

    [Tooltip("Xbox / XInput, also used for generic pads. Four hint slots, left to right.")]
    [SerializeField] private Sprite[] xboxHints = new Sprite[HintSlotCount];
    [Tooltip("DualShock 4 / DualSense. Four hint slots, left to right.")]
    [SerializeField] private Sprite[] playStationHints = new Sprite[HintSlotCount];

    [Header("Right shoulder (shop Continue)")]
    [SerializeField] private Sprite xboxRightShoulder;
    [SerializeField] private Sprite playStationRightShoulder;

    [Header("Virtual keyboard shortcuts")]
    [Tooltip("X — Backspace")]
    [SerializeField] private Sprite xboxWest;
    [Tooltip("Square — Backspace")]
    [SerializeField] private Sprite playStationWest;
    [Tooltip("Y — Space")]
    [SerializeField] private Sprite xboxNorth;
    [Tooltip("Triangle — Space")]
    [SerializeField] private Sprite playStationNorth;
    [Tooltip("Menu — Done")]
    [SerializeField] private Sprite xboxStart;
    [Tooltip("Options — Done")]
    [SerializeField] private Sprite playStationStart;

    [System.NonSerialized] private readonly HashSet<string> _warned = new HashSet<string>();

    public Sprite GetHint(ControllerFamily family, int slot)
    {
        Sprite[] hints = family == ControllerFamily.PlayStation ? playStationHints : xboxHints;
        Sprite sprite = hints != null && slot >= 0 && slot < hints.Length ? hints[slot] : null;
        return Checked(sprite, $"{Name(family)} hint slot {slot}");
    }

    public Sprite GetButton(ControllerFamily family, GamepadGlyph button)
    {
        bool ps = family == ControllerFamily.PlayStation;
        Sprite sprite;
        switch (button)
        {
            case GamepadGlyph.West: sprite = ps ? playStationWest : xboxWest; break;
            case GamepadGlyph.North: sprite = ps ? playStationNorth : xboxNorth; break;
            case GamepadGlyph.Start: sprite = ps ? playStationStart : xboxStart; break;
            default: sprite = ps ? playStationRightShoulder : xboxRightShoulder; break;
        }
        return Checked(sprite, $"{Name(family)} {button}");
    }

    private Sprite Checked(Sprite sprite, string entry)
    {
        if (sprite == null && _warned.Add(entry))
            Debug.LogWarning($"[ControllerGlyphSet] '{name}' has no sprite for {entry} — hidden.", this);
        return sprite;
    }

    private static string Name(ControllerFamily family) =>
        family == ControllerFamily.PlayStation ? "PlayStation" : "Xbox";
}
