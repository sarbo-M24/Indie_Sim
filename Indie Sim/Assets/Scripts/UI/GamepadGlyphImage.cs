using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One gamepad button's glyph (RB/R1 next to the shop's Continue, X/Y/Start
/// on the virtual keyboard's special keys). Shows the current controller
/// family's sprite while the player is using the pad (ControllerPresence.IsActive);
/// the Image is disabled on mouse/keyboard input, with no controller, or with no
/// sprite assigned. Event-driven via ControllerPresence.OnChanged.
/// </summary>
[RequireComponent(typeof(Image))]
public class GamepadGlyphImage : MonoBehaviour
{
    [SerializeField] private ControllerGlyphSet glyphs;
    [SerializeField] private GamepadGlyph button = GamepadGlyph.RightShoulder;

    private Image _image;
    private bool _warnedNoGlyphs;

    /// <summary>For glyphs made at runtime: call before the object is first enabled.</summary>
    public void Init(ControllerGlyphSet glyphSet, GamepadGlyph glyph)
    {
        glyphs = glyphSet;
        button = glyph;
    }

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.raycastTarget = false; // never steal the owning button's clicks
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
        Sprite sprite = null;
        if (ControllerPresence.IsActive)
        {
            if (glyphs != null) sprite = glyphs.GetButton(ControllerPresence.Family, button);
            else if (!_warnedNoGlyphs)
            {
                _warnedNoGlyphs = true;
                Debug.LogWarning($"[GamepadGlyphImage] '{name}' has no Controller Glyph Set — glyph hidden.", this);
            }
        }

        _image.sprite = sprite;
        _image.enabled = sprite != null;
    }
}
