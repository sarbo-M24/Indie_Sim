using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The shop cat. Hover shows a "Pet" cursor tooltip, click plays a Y-squish
/// toward its feet, and a speech bubble reacts to ShopUIController's local
/// select/purchase/purchase-failed events (shop-local chatter — deliberately
/// not the global GameEvents bus). Subscribes on enable, unsubscribes on
/// disable, so re-opening the shop or reloading the scene never doubles up.
///
/// Sits on the cat's root (or on the cat image itself); pointer events bubble
/// up from catImage, the only raycast target — everything else under this
/// object has raycastTarget forced off at Awake. All timing is unscaled.
/// </summary>
public class ShopMascot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("References")]
    [Tooltip("Falls back to ShopUIController.Instance when left empty.")]
    [SerializeField] private ShopUIController shop;
    [SerializeField] private CursorTooltip tooltip;
    [Tooltip("The cat's Image — the hit area, and what squishes.")]
    [SerializeField] private Image catImage;

    [Header("Pet")]
    [SerializeField] private string petLabel = "Pet";
    [SerializeField] private float squishScaleY = 0.85f;
    [SerializeField] private float squishDownDuration = 0.06f;
    [SerializeField] private float squishReturnDuration = 0.12f;
    [Tooltip("Ease-out-back strength on the return; 0 = no overshoot.")]
    [SerializeField] private float squishOvershoot = 1.5f;

    [Header("Dialogue")]
    [SerializeField] private ShopDialogueSet dialogue;
    [SerializeField] private GameObject bubbleRoot;
    [SerializeField] private TMP_Text bubbleText;
    [SerializeField] private float bubbleDuration = 2f;

    private RectTransform _catRect;
    private Vector3 _restScale = Vector3.one;
    private Coroutine _squishRoutine;
    private Coroutine _bubbleRoutine;
    private string _lastLine;
    private ShopUIController _subscribedShop;

    private void Awake()
    {
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = graphic == catImage;

        if (catImage != null)
        {
            _catRect = catImage.rectTransform;
            _restScale = _catRect.localScale;
            // Squish toward the floor, not the middle. Done at runtime (not
            // authored in the scene) and position-compensated, so the cat
            // doesn't visibly move; a no-op if already bottom-center.
            SetPivotKeepingPosition(_catRect, new Vector2(0.5f, 0f));
        }

        if (bubbleRoot != null) bubbleRoot.SetActive(false);
    }

    private void OnEnable()
    {
        _subscribedShop = shop != null ? shop : ShopUIController.Instance;
        if (_subscribedShop == null) return;

        _subscribedShop.UpgradeSelected += HandleSelected;
        _subscribedShop.UpgradePurchased += HandlePurchased;
        _subscribedShop.PurchaseFailed += HandlePurchaseFailed;
    }

    private void OnDisable()
    {
        if (_subscribedShop != null)
        {
            _subscribedShop.UpgradeSelected -= HandleSelected;
            _subscribedShop.UpgradePurchased -= HandlePurchased;
            _subscribedShop.PurchaseFailed -= HandlePurchaseFailed;
            _subscribedShop = null;
        }

        // Coroutines die with the object — reset so nothing is left mid-squish
        // or with a stale bubble the next time the shop opens.
        _squishRoutine = null;
        _bubbleRoutine = null;
        if (_catRect != null) _catRect.localScale = _restScale;
        if (bubbleRoot != null) bubbleRoot.SetActive(false);
        if (tooltip != null) tooltip.Hide(this);
    }

    private void HandleSelected(CigInstance offer) => Say(dialogue != null ? dialogue.onSelected : null);
    private void HandlePurchased(CigInstance offer) => Say(dialogue != null ? dialogue.onPurchased : null);
    private void HandlePurchaseFailed(CigInstance offer) => Say(dialogue != null ? dialogue.onPurchaseFailed : null);

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.ShowSmall(this, petLabel);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.Hide(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_catRect == null || !isActiveAndEnabled) return;

        // Restart from wherever the last squish left off — never stacks.
        if (_squishRoutine != null) StopCoroutine(_squishRoutine);
        _squishRoutine = StartCoroutine(Squish());

        // Pet dialogue hook: dialogue.onPet is reserved but not wired in this pass.
    }

    private IEnumerator Squish()
    {
        float startY = _catRect.localScale.y;
        float squashedY = _restScale.y * squishScaleY;

        float elapsed = 0f;
        while (elapsed < squishDownDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / squishDownDuration);
            SetCatScaleY(Mathf.Lerp(startY, squashedY, 1f - (1f - t) * (1f - t)));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < squishReturnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / squishReturnDuration);
            SetCatScaleY(Mathf.LerpUnclamped(squashedY, _restScale.y, EaseOutBack(t, squishOvershoot)));
            yield return null;
        }

        _catRect.localScale = _restScale;
        _squishRoutine = null;
    }

    private void SetCatScaleY(float y)
    {
        _catRect.localScale = new Vector3(_restScale.x, y, _restScale.z);
    }

    private static float EaseOutBack(float t, float overshoot)
    {
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
    }

    private void Say(string[] pool)
    {
        string line = PickLine(pool);
        if (line == null || bubbleRoot == null || !isActiveAndEnabled) return;

        if (bubbleText != null) bubbleText.text = line;
        bubbleRoot.SetActive(true);

        // A new line interrupts the current one and resets the timer.
        if (_bubbleRoutine != null) StopCoroutine(_bubbleRoutine);
        _bubbleRoutine = StartCoroutine(HideBubbleAfterDelay());
    }

    private string PickLine(string[] pool)
    {
        if (pool == null || pool.Length == 0) return null;
        if (pool.Length == 1) return _lastLine = pool[0];

        // Never repeat the previous line back-to-back when there's a choice.
        int index = Random.Range(0, pool.Length);
        if (pool[index] == _lastLine)
            index = (index + Random.Range(1, pool.Length)) % pool.Length;

        return _lastLine = pool[index];
    }

    private IEnumerator HideBubbleAfterDelay()
    {
        yield return new WaitForSecondsRealtime(bubbleDuration);
        if (bubbleRoot != null) bubbleRoot.SetActive(false);
        _bubbleRoutine = null;
    }

    private static void SetPivotKeepingPosition(RectTransform rect, Vector2 pivot)
    {
        Vector2 delta = pivot - rect.pivot;
        if (delta == Vector2.zero) return;

        Vector3 shift = new Vector3(
            delta.x * rect.rect.width * rect.localScale.x,
            delta.y * rect.rect.height * rect.localScale.y,
            0f);
        rect.pivot = pivot;
        rect.localPosition += rect.localRotation * shift;
    }
}
