using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One Buy-offer card in the shop. Shows only the offer's name and cost,
/// reports pointer enter/exit/click up to ShopUIController, and animates its
/// own scale + border when the controller tells it its focus state. Cards
/// never reference each other — the controller owns which card is focused.
/// A fixed pool sits in the scene (never instantiated at runtime), same
/// convention as CigCardUI, which still drives the Burn slots.
///
/// Scale is tweened on localScale only (layout groups size by rect, not
/// scale, so this never fights the layout), always from the current scale,
/// in unscaled time so it keeps working if the shop ever pauses the game.
/// </summary>
public class UpgradeCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum FocusState { Neutral, Focused, Unfocused }

    [Header("Face")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Button button;
    [Tooltip("Existing click-to-select highlight (select-then-Buy flow). Independent of the hover border.")]
    [SerializeField] private GameObject selectedHighlight;

    [Header("Border (Outline on the background Image only)")]
    [SerializeField] private Image background;
    [SerializeField] private Color borderColor = Color.white;
    [SerializeField] private Vector2 borderDistance = new Vector2(3f, -3f);

    [Header("Focus scale")]
    [SerializeField] private float neutralScale = 1f;
    [SerializeField] private float focusedScale = 1.1f;
    [SerializeField] private float unfocusedScale = 0.9f;
    [SerializeField] private float tweenDuration = 0.12f;

    private Outline _outline;
    private Coroutine _scaleRoutine;

    public CigInstance BoundInstance { get; private set; }

    public event Action<UpgradeCardView> OnClicked;
    public event Action<UpgradeCardView> OnHoverEnter;
    public event Action<UpgradeCardView> OnHoverExit;

    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(() => OnClicked?.Invoke(this));

        if (background != null && !background.TryGetComponent(out _outline))
            _outline = background.gameObject.AddComponent<Outline>();
        if (_outline != null)
        {
            _outline.effectColor = borderColor;
            _outline.effectDistance = borderDistance;
            _outline.enabled = false;
        }

        // No SetEmpty() here: Awake can first run *inside* Populate's
        // SetActive(true) (the shop panel is hidden at load), and hiding here
        // would undo that Populate. The controller sets every card's
        // visibility on each Open anyway.
        SetSelected(false);
    }

    private void OnDisable()
    {
        // Pointer-exit never fires on a card that gets hidden mid-hover — let
        // the controller drop focus/tooltip, and snap back so a half-finished
        // tween isn't frozen until the card is shown again.
        OnHoverExit?.Invoke(this);
        StopScaleTween();
        transform.localScale = Vector3.one * neutralScale;
        if (_outline != null) _outline.enabled = false;
    }

    public void Populate(CigInstance instance)
    {
        BoundInstance = instance;
        gameObject.SetActive(true);

        CigData data = instance.Data;
        if (nameText != null) nameText.text = data != null ? data.displayName : string.Empty;
        if (costText != null) costText.text = data != null ? data.cost.ToString() : string.Empty;

        SetSelected(false);
    }

    /// <summary>Hides the card — used when there are fewer offers than cards.</summary>
    public void SetEmpty()
    {
        BoundInstance = null;
        SetSelected(false);
        gameObject.SetActive(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null)
            selectedHighlight.SetActive(selected);
    }

    public void SetFocusState(FocusState state)
    {
        if (_outline != null) _outline.enabled = state == FocusState.Focused;

        float target = state == FocusState.Focused ? focusedScale
            : state == FocusState.Unfocused ? unfocusedScale
            : neutralScale;

        if (!isActiveAndEnabled)
        {
            transform.localScale = Vector3.one * target;
            return;
        }

        StopScaleTween();
        _scaleRoutine = StartCoroutine(TweenScale(target));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (BoundInstance != null) OnHoverEnter?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnHoverExit?.Invoke(this);
    }

    private void StopScaleTween()
    {
        if (_scaleRoutine == null) return;
        StopCoroutine(_scaleRoutine);
        _scaleRoutine = null;
    }

    private IEnumerator TweenScale(float target)
    {
        Vector3 from = transform.localScale;
        Vector3 to = Vector3.one * target;
        float elapsed = 0f;

        while (elapsed < tweenDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / tweenDuration);
            float eased = 1f - (1f - t) * (1f - t); // ease-out quad
            transform.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }

        transform.localScale = to;
        _scaleRoutine = null;
    }
}
