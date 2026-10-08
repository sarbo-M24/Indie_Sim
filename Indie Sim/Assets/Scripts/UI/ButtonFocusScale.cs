using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Grows a button slightly while it's focused — hovered by the mouse, or
/// selected by gamepad navigation — so pad users can see which button A
/// will press. Mouse clicks also select a button, so selection only counts
/// while the gamepad is in use; otherwise a clicked button would stay big
/// after the cursor leaves. Non-interactable buttons never grow.
///
/// EnableGlow (from ButtonFocusStyle) upgrades the look: a bigger grow and
/// a reddish tint on the button's Image. Same focus rule for both, so mouse
/// and pad look identical.
///
/// Scales localScale only (layout groups size by rect, not scale, so this
/// never fights the layout), in unscaled time so it works while paused.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class ButtonFocusScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private float focusedScale = 1.08f;
    [Tooltip("Seconds to go from normal to full focus scale.")]
    [SerializeField] private float tweenDuration = 0.08f;

    private Selectable _selectable;
    private Vector3 _baseScale;
    private bool _hovered;
    private bool _selected;

    // Glow mode (EnableGlow) — null/unused otherwise.
    private ButtonFocusGlowSettings _glow;
    private Graphic _tintGraphic;
    private Color _tintBase;
    private float _tintAmount;

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        _baseScale = transform.localScale;
    }

    /// <summary>Switches this button to the bigger grow + red tint look. Safe to call more than once.</summary>
    public void EnableGlow(ButtonFocusGlowSettings settings)
    {
        if (settings == null) return;
        if (_selectable == null) _selectable = GetComponent<Selectable>();

        if (_glow == null)
        {
            _tintGraphic = _selectable.targetGraphic;
            if (_tintGraphic != null) _tintBase = _tintGraphic.color;
        }

        _glow = settings;
        focusedScale = settings.focusedScale;
    }

    private void OnDisable()
    {
        // Exit/deselect events don't fire on a button hidden mid-focus.
        _hovered = false;
        _selected = false;
        transform.localScale = _baseScale;

        _tintAmount = 0f;
        ApplyTint();
    }

    private void Update()
    {
        // Mouse hover counts only on mouse/keyboard: on the pad the cursor is
        // hidden but still parked, and would highlight a second button.
        bool focused = (InputManager.UsingGamepad ? _selected : _hovered) && _selectable.IsInteractable();

        if (_glow != null)
        {
            float tintStep = Time.unscaledDeltaTime / Mathf.Max(tweenDuration, 0.0001f);
            float tintTarget = focused ? 1f : 0f;
            if (_tintAmount != tintTarget)
            {
                _tintAmount = Mathf.MoveTowards(_tintAmount, tintTarget, tintStep);
                ApplyTint();
            }
        }

        Vector3 target = focused ? _baseScale * focusedScale : _baseScale;
        if (transform.localScale == target) return;

        float step = Mathf.Abs(focusedScale - 1f) * _baseScale.magnitude / Mathf.Max(tweenDuration, 0.0001f) * Time.unscaledDeltaTime;
        transform.localScale = Vector3.MoveTowards(transform.localScale, target, step);
    }

    private void ApplyTint()
    {
        if (_glow == null || _tintGraphic == null) return;
        _tintGraphic.color = Color.Lerp(_tintBase, _tintBase * _glow.focusTint, _tintAmount);
    }

    public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
    public void OnPointerExit(PointerEventData eventData) => _hovered = false;
    public void OnSelect(BaseEventData eventData) => _selected = true;
    public void OnDeselect(BaseEventData eventData) => _selected = false;
}
