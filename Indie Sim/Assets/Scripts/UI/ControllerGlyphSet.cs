using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gamepad button sprites per controller family: four hint slots (shown by
/// ControllerHintPanel) and the right shoulder (RB / R1, shown by
/// ShoulderGlyphImage). A missing sprite returns null — the caller hides that
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

    [System.NonSerialized] private readonly HashSet<string> _warned = new HashSet<string>();

    public Sprite GetHint(ControllerFamily family, int slot)
    {
        Sprite[] hints = family == ControllerFamily.PlayStation ? playStationHints : xboxHints;
        Sprite sprite = hints != null && slot >= 0 && slot < hints.Length ? hints[slot] : null;
        return Checked(sprite, $"{Name(family)} hint slot {slot}");
    }

    public Sprite GetRightShoulder(ControllerFamily family)
    {
        Sprite sprite = family == ControllerFamily.PlayStation ? playStationRightShoulder : xboxRightShoulder;
        return Checked(sprite, $"{Name(family)} right shoulder");
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
