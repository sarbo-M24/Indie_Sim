using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RB / R1 glyph next to a button with a right-shoulder shortcut (the shop's
/// Continue). Shows the current controller family's sprite; the Image is
/// disabled with no controller or no sprite assigned. Event-driven via
/// ControllerPresence.OnChanged.
/// </summary>
[RequireComponent(typeof(Image))]
public class ShoulderGlyphImage : MonoBehaviour
{
    [SerializeField] private ControllerGlyphSet glyphs;

    private Image _image;
    private bool _warnedNoGlyphs;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.raycastTarget = false; // never steal the Continue button's clicks
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
        if (ControllerPresence.IsConnected)
        {
            if (glyphs != null) sprite = glyphs.GetRightShoulder(ControllerPresence.Family);
            else if (!_warnedNoGlyphs)
            {
                _warnedNoGlyphs = true;
                Debug.LogWarning($"[ShoulderGlyphImage] '{name}' has no Controller Glyph Set — glyph hidden.", this);
            }
        }

        _image.sprite = sprite;
        _image.enabled = sprite != null;
    }
}
