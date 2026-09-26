using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One Buy-offer card in the shop. Shows the offer's name, cost, cig art and
/// a rarity-coloured border (colours from the shared RarityConfig),
/// reports pointer enter/exit/click up to ShopUIController, and animates its
/// own scale + border when the controller tells it its focus state. Cards
/// never reference each other — the controller owns which card is focused.
/// A fixed pool sits in the scene (never instantiated at runtime), same
/// convention as PackCigView, which drives the held-cig slots.
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
    [Tooltip("Child Image showing the offer's cig art (CigData.icon).")]
    [SerializeField] private Image cigIcon;

    [Header("Rarity border")]
    [Tooltip("Child Border Image. Left at 0 alpha in the scene; shown in the rolled rarity's colour on Populate.")]
    [SerializeField] private Image rarityBorder;
    [Tooltip("Same shared RarityConfig asset the CigData assets use — source of the rarity colours.")]
    [SerializeField] private RarityConfig rarityConfig;

    [Header("Hover border (Outline on the background child Image only)")]
    [Tooltip("The separate Background child Image — not the root, which has no Image.")]
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

        if (cigIcon != null)
        {
            cigIcon.sprite = data != null ? data.icon : null;
            cigIcon.enabled = cigIcon.sprite != null;
        }

        ApplyRarityBorder(instance);
        SetSelected(false);
    }

    /// <summary>
    /// Border shows at full alpha in the rolled rarity's colour. Rarity-less
    /// upgrades (hasRarity false) keep it at 0 alpha — they have no rarity to show.
    /// </summary>
    private void ApplyRarityBorder(CigInstance instance)
    {
        if (rarityBorder == null) return;

        bool show = instance.Data != null && instance.Data.hasRarity && rarityConfig != null;
        Color c = show ? rarityConfig.GetColor(instance.RolledRarity) : rarityBorder.color;
        c.a = show ? 1f : 0f;
        rarityBorder.color = c;
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
