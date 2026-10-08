using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One cig currently held in the pack, shown in the shop. Deliberately a
/// different prefab from the Buy offers (UpgradeCardView): no framed box,
/// no name/cost — just the cig image as a button. Hover (or gamepad focus)
/// borders it and lifts it up out of the pack; clicking selects it for
/// burning, which shows the select highlight and keeps it lifted after the
/// hover ends. ShopUIController owns the one
/// shared Burn button and decides when the selection clears. Hover reports up
/// to ShopUIController, which shows the shared cursor tooltip. A fixed pool
/// sits in the scene (never instantiated at runtime); the controller
/// populates or hides each slot on every refresh.
///
/// No scene references live on this prefab — the Burn button used to be a
/// field here, but a prefab can't reference a scene object, so every slot in
/// every scene needed its own override. The controller holds it instead.
/// </summary>
public class PackCigView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [Tooltip("The cig itself — click to select for burning.")]
    [SerializeField] private Button button;
    [Tooltip("Optional select highlight, shown while this cig is selected.")]
    [SerializeField] private GameObject selectedHighlight;

    [Header("Lift on hover / select")]
    [Tooltip("What moves up when hovered or selected. Must be a CHILD (not this root, which the pack's layout group positions). Defaults to the icon.")]
    [SerializeField] private RectTransform liftTarget;
    [SerializeField] private float liftDistance = 25f;
    [SerializeField] private float liftDuration = 0.1f;

    [Header("Focus border (hover / gamepad focus)")]
    [Tooltip("Outline drawn around the cig art while it's hovered or gamepad-focused.")]
    [SerializeField] private Color focusBorderColor = Color.white;
    [SerializeField] private Vector2 focusBorderDistance = new Vector2(3f, -3f);

    private Vector2 _restPosition;
    private Coroutine _liftRoutine;
    private Outline _focusOutline;
    private bool _focused;

    public CigInstance BoundInstance { get; private set; }
    public bool IsSelected { get; private set; }

    public event Action<PackCigView> OnClicked;
    public event Action<PackCigView> OnHoverEnter;
    public event Action<PackCigView> OnHoverExit;

    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(() => OnClicked?.Invoke(this));

        if (liftTarget == null && icon != null) liftTarget = icon.rectTransform;
        if (liftTarget != null) _restPosition = liftTarget.anchoredPosition;

        if (icon != null && !icon.TryGetComponent(out _focusOutline))
            _focusOutline = icon.gameObject.AddComponent<Outline>();
        if (_focusOutline != null)
        {
            _focusOutline.effectColor = focusBorderColor;
            _focusOutline.effectDistance = focusBorderDistance;
            _focusOutline.enabled = false;
        }

        // No SetEmpty() here: Awake can first run *inside* Populate's
        // SetActive(true) (the shop panel is hidden at load), and hiding here
        // would undo that Populate. The controller sets every slot's
        // visibility on each refresh anyway.
        SetSelected(false);
    }

    private void OnDisable()
    {
        // Pointer-exit never fires on a slot that gets hidden mid-hover.
        OnHoverExit?.Invoke(this);
        SetFocused(false);

        // Snap down so a half-finished lift isn't frozen until the slot is shown again.
        StopLiftTween();
        if (liftTarget != null) liftTarget.anchoredPosition = TargetPosition();
    }

    public void Populate(CigInstance instance)
    {
        BoundInstance = instance;
        gameObject.SetActive(true);

        if (icon != null)
        {
            icon.sprite = instance.Data != null ? instance.Data.icon : null;
            icon.enabled = icon.sprite != null;
        }

        SetSelected(false);
    }

    /// <summary>Hides the slot — used when the pack holds fewer cigs than slots.</summary>
    public void SetEmpty()
    {
        BoundInstance = null;
        SetSelected(false);
        gameObject.SetActive(false);
    }

    public void SetSelected(bool selected)
    {
        bool wasLifted = IsLifted;
        IsSelected = selected;
        if (selectedHighlight != null) selectedHighlight.SetActive(selected);
        UpdateLift(wasLifted);
    }

    /// <summary>Border + lift on the cig art while hovered or gamepad-focused. A selected cig (chosen for burning) also gets the highlight and stays lifted.</summary>
    public void SetFocused(bool focused)
    {
        bool wasLifted = IsLifted;
        _focused = focused;
        if (_focusOutline != null) _focusOutline.enabled = focused;
        UpdateLift(wasLifted);
    }

    private bool IsLifted => IsSelected || _focused;

    private void UpdateLift(bool wasLifted)
    {
        if (liftTarget == null) return;
        UpdateHitArea();
        if (!isActiveAndEnabled)
        {
            liftTarget.anchoredPosition = TargetPosition();
            return;
        }
        if (wasLifted == IsLifted) return;

        StopLiftTween();
        _liftRoutine = StartCoroutine(TweenLift(TargetPosition()));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (BoundInstance != null) OnHoverEnter?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnHoverExit?.Invoke(this);
    }

    /// <summary>
    /// The icon is the hover hit area and it moves with the lift — hovering
    /// near its bottom edge would lift it out from under the pointer, drop it,
    /// and flicker. While lifted, its raycast area stretches back down over
    /// the rest spot (in the icon's own space, so the pack's rotation is fine).
    /// </summary>
    private void UpdateHitArea()
    {
        if (icon == null) return;
        if (!IsLifted || liftTarget.parent == null)
        {
            icon.raycastPadding = Vector4.zero;
            return;
        }

        Vector3 down = icon.rectTransform.InverseTransformVector(
            liftTarget.parent.TransformVector(Vector3.down * liftDistance));
        // raycastPadding is (left, bottom, right, top); negative grows the area.
        icon.raycastPadding = new Vector4(
            Mathf.Min(down.x, 0f), Mathf.Min(down.y, 0f),
            -Mathf.Max(down.x, 0f), -Mathf.Max(down.y, 0f));
    }

    private Vector2 TargetPosition() => _restPosition + (IsLifted ? Vector2.up * liftDistance : Vector2.zero);

    private void StopLiftTween()
    {
        if (_liftRoutine == null) return;
        StopCoroutine(_liftRoutine);
        _liftRoutine = null;
    }

    // Unscaled time, same as UpgradeCardView's scale tween, so it still runs if the shop pauses the game.
    private IEnumerator TweenLift(Vector2 to)
    {
        Vector2 from = liftTarget.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < liftDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / liftDuration);
            float eased = 1f - (1f - t) * (1f - t); // ease-out quad
            liftTarget.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            yield return null;
        }

        liftTarget.anchoredPosition = to;
        _liftRoutine = null;
    }
}
