using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single shared tooltip that follows the mouse cursor. Two presentation
/// modes on one panel: Small (one short label, e.g. the shop cat's "Pet")
/// and Large (title, description, optional stat block, e.g. an upgrade
/// card). Last caller wins: Show() takes ownership, and Hide() is ignored
/// unless the caller is the current owner, so one element's pointer-exit
/// can't close a tooltip another element just opened.
///
/// Level-scoped UI — lives on the scene's canvas as its last sibling. Every
/// Graphic under it has raycastTarget forced off at init: a tooltip that
/// catches the pointer steals hover from the element that opened it, which
/// hides the tooltip, which re-fires pointer-enter — endless flicker.
///
/// Reads Input.mousePosition, the same source CustomCrosshair uses; the
/// shop shows the hardware cursor via CursorController, so no custom cursor
/// position needs tracking. The panel's own VerticalLayoutGroup +
/// ContentSizeFitter (Preferred/Preferred) size it; this script only caps
/// each text's preferred width at maxTextWidth so long text wraps.
///
/// ShowLarge takes an optional tint (e.g. a cig's rarity colour): the
/// background blends only tintStrength of the way toward it, so it stays
/// light and the text readable. No tint = the background's authored colour.
/// </summary>
public class CursorTooltip : MonoBehaviour
{
    public enum Mode { Small, Large }

    [Header("Panel (defaults to this object's RectTransform)")]
    [SerializeField] private RectTransform panel;
    [Tooltip("Canvas-unit offset from the cursor. Mirrored when the panel flips to stay on screen.")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(20f, 20f);
    [Tooltip("Widest any text line may get before it wraps, in canvas units.")]
    [SerializeField] private float maxTextWidth = 420f;

    [Header("Background tint")]
    [Tooltip("The Image to tint. Defaults to the Panel's own Image.")]
    [SerializeField] private Image background;
    [Tooltip("How far the background moves toward the tint colour. 0 = no tint, 1 = full colour.")]
    [SerializeField, Range(0f, 1f)] private float tintStrength = 0.3f;

    [Header("Small mode")]
    [SerializeField] private GameObject smallRoot;
    [SerializeField] private TMP_Text smallLabel;

    [Header("Large mode")]
    [SerializeField] private GameObject largeRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statsText;

    private Canvas _rootCanvas;
    private RectTransform _canvasRect;
    private object _owner;
    private bool _initialized;
    private Color _baseBackgroundColor = Color.white;

    public bool IsShowing => _owner != null;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDisable()
    {
        _owner = null;
        if (panel != null && panel != transform) panel.gameObject.SetActive(false);
    }

    // Lazy so Show() still works if this object starts inactive and Awake hasn't run yet.
    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        if (panel == null) panel = (RectTransform)transform;
        if (background == null) background = panel.GetComponent<Image>();
        if (background != null) _baseBackgroundColor = background.color;

        Canvas canvas = GetComponentInParent<Canvas>(true);
        _rootCanvas = canvas != null ? canvas.rootCanvas : null;
        _canvasRect = _rootCanvas != null ? (RectTransform)_rootCanvas.transform : null;

        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        panel.gameObject.SetActive(false);
    }

    /// <summary>Small mode: a single short label.</summary>
    public void ShowSmall(object owner, string label)
    {
        BeginShow();
        SetMode(Mode.Small);
        SetTint(null);
        SetText(smallLabel, label);
        Open(owner);
    }

    /// <summary>Large mode: header, description, an optional stat block (null/empty hides it), and an optional background tint (null = untinted).</summary>
    public void ShowLarge(object owner, string title, string description, string stats, Color? tint = null)
    {
        BeginShow();
        SetMode(Mode.Large);
        SetTint(tint);
        SetText(titleText, title);
        SetText(descriptionText, description);
        SetText(statsText, stats);
        Open(owner);
    }

    /// <summary>Hides the tooltip only if `owner` is the one currently showing it.</summary>
    public void Hide(object owner)
    {
        if (owner == null || owner != _owner) return;

        _owner = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns "→" if the stats text's font (or its fallbacks) can render it,
    /// else ">". LiberationSans SDF (the shop's font) lacks the glyph itself
    /// but its dynamic fallback can add it at runtime.
    /// </summary>
    public string PreviewArrow()
    {
        const char arrow = '→';
        TMP_FontAsset font = statsText != null ? statsText.font : null;
        return font != null && font.HasCharacter(arrow, true, true) ? arrow.ToString() : ">";
    }

    // Active before any text is set, so TMP measures preferred sizes on a live object.
    private void BeginShow()
    {
        EnsureInitialized();
        panel.gameObject.SetActive(true);
    }

    private void SetMode(Mode mode)
    {
        if (smallRoot != null) smallRoot.SetActive(mode == Mode.Small);
        if (largeRoot != null) largeRoot.SetActive(mode == Mode.Large);
    }

    private void SetTint(Color? tint)
    {
        if (background == null) return;

        Color color = _baseBackgroundColor;
        if (tint.HasValue)
        {
            color = Color.Lerp(_baseBackgroundColor, tint.Value, tintStrength);
            color.a = _baseBackgroundColor.a; // tint the hue only — keep the authored transparency
        }
        background.color = color;
    }

    private void SetText(TMP_Text text, string value)
    {
        if (text == null) return;

        bool hasValue = !string.IsNullOrEmpty(value);
        text.gameObject.SetActive(hasValue);
        text.text = hasValue ? value : string.Empty;
        if (!hasValue) return;

        // Natural single-line width, capped — short text hugs, long text wraps.
        float natural = text.GetPreferredValues(value, float.PositiveInfinity, float.PositiveInfinity).x;
        if (!text.TryGetComponent(out LayoutElement layout))
            layout = text.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = Mathf.Min(natural, maxTextWidth);
    }

    private void Open(object owner)
    {
        _owner = owner;
        transform.SetAsLastSibling();

        // Size now so the first positioned frame already uses the real
        // bounds — otherwise the flip check runs against last show's size.
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        UpdatePosition();
    }

    private void LateUpdate()
    {
        if (_owner != null) UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (_canvasRect == null) return;

        Camera cam = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, Input.mousePosition, cam, out Vector2 cursor))
            return;

        Rect bounds = _canvasRect.rect;
        float scale = _canvasRect.lossyScale.x != 0f ? panel.lossyScale.x / _canvasRect.lossyScale.x : 1f;
        Vector2 size = panel.rect.size * scale;

        // Default: below-right of the cursor. Flip to the other side of the
        // cursor on whichever axis would run off the right/bottom edge.
        bool flipX = cursor.x + cursorOffset.x + size.x > bounds.xMax;
        bool flipY = cursor.y - cursorOffset.y - size.y < bounds.yMin;

        Vector2 pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
        if (panel.pivot != pivot) panel.pivot = pivot;

        Vector2 target = cursor + new Vector2(
            flipX ? -cursorOffset.x : cursorOffset.x,
            flipY ? cursorOffset.y : -cursorOffset.y);

        // Final clamp for a panel too big to fit on either side of the cursor.
        target.x = ClampAxis(target.x, pivot.x, size.x, bounds.xMin, bounds.xMax);
        target.y = ClampAxis(target.y, pivot.y, size.y, bounds.yMin, bounds.yMax);

        panel.position = _canvasRect.TransformPoint(target);
    }

    private static float ClampAxis(float position, float pivot, float size, float min, float max)
    {
        float lowEdge = position - pivot * size;
        lowEdge = Mathf.Clamp(lowEdge, min, Mathf.Max(min, max - size));
        return lowEdge + pivot * size;
    }
}
