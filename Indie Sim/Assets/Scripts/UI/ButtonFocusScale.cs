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

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        _baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        // Exit/deselect events don't fire on a button hidden mid-focus.
        _hovered = false;
        _selected = false;
        transform.localScale = _baseScale;
    }

    private void Update()
    {
        bool focused = (_hovered || (_selected && InputManager.UsingGamepad)) && _selectable.IsInteractable();
        Vector3 target = focused ? _baseScale * focusedScale : _baseScale;
        if (transform.localScale == target) return;

        float step = Mathf.Abs(focusedScale - 1f) * _baseScale.magnitude / Mathf.Max(tweenDuration, 0.0001f) * Time.unscaledDeltaTime;
        transform.localScale = Vector3.MoveTowards(transform.localScale, target, step);
    }

    public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
    public void OnPointerExit(PointerEventData eventData) => _hovered = false;
    public void OnSelect(BaseEventData eventData) => _selected = true;
    public void OnDeselect(BaseEventData eventData) => _selected = false;
}
