using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A button glyph that also has a keyboard version, e.g. the Settings tab
/// switch: Q / E on mouse/keyboard input, LB / RB on Xbox (and generic pads),
/// L1 / R1 on PlayStation — by the last input used (ControllerPresence.IsActive),
/// not by what's plugged in. Follows ControllerPresence.OnChanged. A missing
/// sprite disables the Image (one warning per missing sprite).
/// </summary>
[RequireComponent(typeof(Image))]
public class InputDeviceGlyph : MonoBehaviour
{
    [SerializeField] private Sprite keyboard;
    [SerializeField] private Sprite xbox;
    [SerializeField] private Sprite playStation;

    private Image _image;
    private int _warnedMask;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.raycastTarget = false;
    }

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
        ControllerFamily family = ControllerPresence.IsActive ? ControllerPresence.Family : ControllerFamily.None;
        Sprite sprite = family == ControllerFamily.PlayStation ? playStation
            : family == ControllerFamily.Xbox ? xbox
            : keyboard;

        int bit = 1 << (int)family;
        if (sprite == null && (_warnedMask & bit) == 0)
        {
            _warnedMask |= bit;
            Debug.LogWarning($"[InputDeviceGlyph] '{name}' has no sprite for {(family == ControllerFamily.None ? "keyboard" : family.ToString())} — hidden.", this);
        }

        _image.sprite = sprite;
        _image.enabled = sprite != null;
    }
}
