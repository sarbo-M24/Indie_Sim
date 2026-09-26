using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One cig currently held in the pack, shown in the shop. Deliberately a
/// different prefab from the Buy offers (UpgradeCardView): no framed box,
/// no name/cost — just the cig image as a button. Clicking selects it for
/// burning, which lifts it up out of the pack; ShopUIController owns the one
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

    [Header("Lift on select")]
    [Tooltip("What moves up when selected. Must be a CHILD (not this root, which the pack's layout group positions). Defaults to the icon.")]
    [SerializeField] private RectTransform liftTarget;
    [SerializeField] private float liftDistance = 25f;
    [SerializeField] private float liftDuration = 0.1f;

    private Vector2 _restPosition;
    private Coroutine _liftRoutine;

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
        bool changed = IsSelected != selected;
        IsSelected = selected;
        if (selectedHighlight != null) selectedHighlight.SetActive(selected);

        if (liftTarget == null) return;
        if (!isActiveAndEnabled)
        {
            liftTarget.anchoredPosition = TargetPosition();
            return;
        }
        if (!changed) return;

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

    private Vector2 TargetPosition() => _restPosition + (IsSelected ? Vector2.up * liftDistance : Vector2.zero);

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
