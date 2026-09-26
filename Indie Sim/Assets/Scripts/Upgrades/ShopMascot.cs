using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The shop cat. Hover shows a "Pet" cursor tooltip; click pets it — cycles
/// the pet sprites while it hops: each hop is a Y squish toward its feet,
/// then a lift on its Y position — and a speech bubble
/// reacts to ShopUIController's local events (shop-local chatter —
/// deliberately not the global GameEvents bus). Subscribes on enable,
/// unsubscribes on disable, so re-opening the shop or reloading the scene
/// never doubles up.
///
/// The bubble is always up while the shop is open: every visit starts with
/// a greeting, and each line stays until the next one replaces it. Lines
/// type out one character at a time; any new line (from a click on a card,
/// cig, Buy, the cat...) cancels the one in progress and starts typing
/// itself from scratch.
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
    [Tooltip("The cat's Image — the hit area, and what squishes / bounces / swaps sprites.")]
    [SerializeField] private Image catImage;

    [Header("Pet")]
    [SerializeField] private string petLabel = "Pet";
    [Tooltip("Frames cycled in order while petting. The cat's resting sprite comes back afterwards.")]
    [SerializeField] private Sprite[] petFrames;
    [SerializeField, Min(0.01f)] private float petFrameDuration = 0.1f;
    [Tooltip("Total length of one pet (sprite cycling + squish + bouncing).")]
    [SerializeField, Min(0.01f)] private float petDuration = 0.6f;
    [Tooltip("How high each bounce lifts the cat, in its parent's local units.")]
    [SerializeField] private float bounceHeight = 12f;
    [Tooltip("Hops per pet.")]
    [SerializeField, Min(1)] private int bounceCount = 2;
    [Tooltip("How much each hop's squish flattens the cat's Y scale. 0.2 = down to 80% height.")]
    [SerializeField, Range(0f, 0.9f)] private float squishAmount = 0.2f;
    [Tooltip("Share of each hop spent squishing on the ground before the lift.")]
    [SerializeField, Range(0.05f, 0.95f)] private float squishPortion = 0.35f;

    [Header("Dialogue")]
    [SerializeField] private ShopDialogueSet dialogue;
    [SerializeField] private GameObject bubbleRoot;
    [SerializeField] private TMP_Text bubbleText;
    [SerializeField, Min(1f)] private float charactersPerSecond = 40f;

    private RectTransform _catRect;
    private Vector3 _restPosition;
    private Vector3 _restScale = Vector3.one;
    private Sprite _restSprite;
    private Coroutine _petRoutine;
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
            // Keep the feet planted when pet frames differ in height. Done at
            // runtime and position-compensated, so the cat doesn't visibly
            // move; a no-op if already bottom-center in the editor.
            SetPivotKeepingPosition(_catRect, new Vector2(0.5f, 0f));
            _restPosition = _catRect.localPosition;
            _restScale = _catRect.localScale;
            _restSprite = catImage.sprite;
        }
    }

    private void OnEnable()
    {
        // The mascot lives under the shop panel, so enabling = the shop just opened.
        Say(dialogue != null ? dialogue.onShopOpened : null);

        _subscribedShop = shop != null ? shop : ShopUIController.Instance;
        if (_subscribedShop == null) return;

        _subscribedShop.UpgradeSelected += HandleSelected;
        _subscribedShop.UpgradePurchased += HandlePurchased;
        _subscribedShop.PurchaseFailed += HandlePurchaseFailed;
        _subscribedShop.PackCigSelected += HandlePackCigSelected;
        _subscribedShop.ReshuffleFailed += HandleReshuffleFailed;
    }

    private void OnDisable()
    {
        if (_subscribedShop != null)
        {
            _subscribedShop.UpgradeSelected -= HandleSelected;
            _subscribedShop.UpgradePurchased -= HandlePurchased;
            _subscribedShop.PurchaseFailed -= HandlePurchaseFailed;
            _subscribedShop.PackCigSelected -= HandlePackCigSelected;
            _subscribedShop.ReshuffleFailed -= HandleReshuffleFailed;
            _subscribedShop = null;
        }

        // Coroutines die with the object — reset so nothing is left mid-pet
        // or half-typed; the next open types a fresh greeting.
        _petRoutine = null;
        _bubbleRoutine = null;
        ResetCat();
        if (bubbleText != null) bubbleText.maxVisibleCharacters = int.MaxValue;
        if (tooltip != null) tooltip.Hide(this);
    }

    private void HandleSelected(CigInstance offer) => Say(dialogue != null ? dialogue.onSelected : null);
    private void HandlePurchased(CigInstance offer) => Say(dialogue != null ? dialogue.onPurchased : null);
    private void HandlePackCigSelected(CigInstance held) => Say(dialogue != null ? dialogue.onPackCigSelected : null);
    private void HandleReshuffleFailed() => Say(dialogue != null ? dialogue.onNotEnoughCoins : null);

    private void HandlePurchaseFailed(CigInstance offer, ShopUIController.PurchaseFailReason reason)
    {
        if (dialogue == null) return;
        Say(reason == ShopUIController.PurchaseFailReason.NotEnoughCoins ? dialogue.onNotEnoughCoins : dialogue.onPackFull);
    }

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

        // Restart from the top — never stacks.
        if (_petRoutine != null) StopCoroutine(_petRoutine);
        _petRoutine = StartCoroutine(Pet());

        Say(dialogue != null ? dialogue.onPet : null);
    }

    private IEnumerator Pet()
    {
        bool hasFrames = petFrames != null && petFrames.Length > 0;

        float elapsed = 0f;
        while (elapsed < petDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / petDuration);

            // Each hop: squish down and back up on the ground (Y scale,
            // toward the bottom-centre pivot = its feet), then a lift that
            // lands back on the rest Y. Both return to rest at every hop's
            // edges, so restarting mid-pet never pops.
            float hop = Mathf.Min(t * bounceCount, bounceCount - 0.0001f);
            float phase = hop - Mathf.Floor(hop);

            float squish = 0f, lift = 0f;
            if (phase < squishPortion)
                squish = Mathf.Sin(phase / squishPortion * Mathf.PI) * squishAmount;
            else
                lift = Mathf.Sin((phase - squishPortion) / (1f - squishPortion) * Mathf.PI) * bounceHeight;

            _catRect.localScale = new Vector3(_restScale.x, _restScale.y * (1f - squish), _restScale.z);
            _catRect.localPosition = _restPosition + Vector3.up * lift;

            if (hasFrames)
            {
                int frame = Mathf.FloorToInt(elapsed / petFrameDuration) % petFrames.Length;
                if (petFrames[frame] != null) catImage.sprite = petFrames[frame];
            }

            yield return null;
        }

        ResetCat();
        _petRoutine = null;
    }

    private void ResetCat()
    {
        if (_catRect == null) return;
        _catRect.localPosition = _restPosition;
        _catRect.localScale = _restScale;
        catImage.sprite = _restSprite;
    }

    private void Say(string[] pool)
    {
        string line = PickLine(pool);
        if (line == null || !isActiveAndEnabled) return;

        bubbleRoot.SetActive(true);

        // A new line cancels the current one mid-type and starts over.
        if (_bubbleRoutine != null) StopCoroutine(_bubbleRoutine);
        _bubbleRoutine = StartCoroutine(TypeLine(line));
    }

    // The finished line stays up until the next Say replaces it.
    private IEnumerator TypeLine(string line)
    {
        if (bubbleText != null)
        {
            // Full text set up front and revealed via maxVisibleCharacters, so
            // the bubble's layout/wrapping doesn't shift as letters appear
            // and rich-text tags are never shown half-typed.
            bubbleText.text = line;
            bubbleText.maxVisibleCharacters = 0;
            bubbleText.ForceMeshUpdate();
            int total = bubbleText.textInfo.characterCount;

            float elapsed = 0f;
            while (bubbleText.maxVisibleCharacters < total)
            {
                elapsed += Time.unscaledDeltaTime;
                bubbleText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(elapsed * charactersPerSecond));
                yield return null;
            }
        }

        _bubbleRoutine = null;
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
