using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pure UI glue over Pack/CigPool/BurnResolver, per UpgradeSystemPlan.md
/// Step 3. Scene-local (no DontDestroyOnLoad), same pattern as Pack/CigPool/
/// BurnResolver. Buy and Burn slots are both visible at once (no tabs);
/// each has its own selection and its own confirm button, so a buy
/// selection and a burn selection can be held independently. Functional
/// first pass for testing, not final UI.
///
/// Buy offers use UpgradeCardView (framed name + cost face, hover focus,
/// cursor tooltip with a stat preview). Held cigs use PackCigView — a
/// separate prefab: just the cig image, click to select it for burning (it
/// lifts up), then the pack's one shared Burn button burns it. The burn
/// selection only clears on a click anywhere other than the selected cig or
/// the Burn button (another pack cig, an offer card, empty space). Hover
/// shows the same cursor tooltip, with the held cig's exact stat
/// contribution. The Burn button lives here, not on the PackCig prefab, so
/// no prefab needs a scene reference in any scene the shop is placed in. This class
/// owns which offer card is hover-focused and raises local (instance, not
/// static) events the shop mascot reacts to (offer selected, purchased,
/// purchase failed with a reason, pack cig selected, reshuffle failed) —
/// shop-local chatter, kept off the global event bus.
/// </summary>
public class ShopUIController : MonoBehaviour
{
    public static ShopUIController Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject shopPanel;

    [Header("Shared UI")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private Button buyButton;
    [Tooltip("The pack's one shared Burn button. Only interactable while a pack cig is selected.")]
    [SerializeField] private Button burnButton;
    [SerializeField] private Button continueButton;

    [Header("Card slots — fixed pool, hidden when unused")]
    [SerializeField] private UpgradeCardView[] offerCards;
    [FormerlySerializedAs("burnCardSlots")]
    [SerializeField] private PackCigView[] packCigSlots;

    [Header("Offers")]
    [Tooltip("How many Buy offers to show per shop visit. Capped by offerCards.Length and the pool size.")]
    [SerializeField, Min(0)] private int offerCount = 3;

    [Header("Reshuffle")]
    [Tooltip("Re-rolls every card still showing an offer. Bought (hidden) cards stay hidden until the next visit.")]
    [SerializeField] private Button reshuffleButton;
    [Tooltip("Optional — shows the current reshuffle cost.")]
    [SerializeField] private TMP_Text reshuffleCostText;
    [SerializeField, Min(0)] private int reshuffleBaseCost = 5;
    [Tooltip("Each reshuffle this visit raises the cost by this %, compounding. Resets every shop visit.")]
    [SerializeField, Min(0f)] private float reshuffleCostIncreasePercent = 50f;

    [Header("Tooltip")]
    [SerializeField] private CursorTooltip tooltip;
    [Tooltip("Same shared RarityConfig asset the CigData assets use — source of the tooltip's rarity tint.")]
    [SerializeField] private RarityConfig rarityConfig;

    private CigInstance _selectedOffer;
    private CigInstance _selectedHeld;
    private CigInstance _pendingReplaceOffer;
    private CigInstance _pendingReplaceConflict;
    private UpgradeCardView _focusedCard;
    // Clicked card: keeps the Focused look after the pointer leaves, until a click elsewhere (see Update).
    private UpgradeCardView _selectedCard;
    private int _reshufflesThisVisit;
    private readonly List<RaycastResult> _clickHits = new List<RaycastResult>();

    private int ReshuffleCost =>
        Mathf.RoundToInt(reshuffleBaseCost * Mathf.Pow(1f + reshuffleCostIncreasePercent / 100f, _reshufflesThisVisit));

    /// <summary>Fired when a Buy attempt hits a same-slot Mild/Regular brand conflict — hook for a confirmation panel. Not fired for a normal, unconflicted buy. Params: (offer, conflictingHeld).</summary>
    public static event System.Action<CigInstance, CigInstance> OnBrandConflictDetected;

    /// <summary>An offer card was clicked (selected, pending Buy).</summary>
    public event System.Action<CigInstance> UpgradeSelected;
    /// <summary>A Buy (or confirmed replace-buy) went through.</summary>
    public event System.Action<CigInstance> UpgradePurchased;
    /// <summary>A Buy attempt failed, and why.</summary>
    public event System.Action<CigInstance, PurchaseFailReason> PurchaseFailed;
    /// <summary>A held pack cig was clicked (selected, pending Burn).</summary>
    public event System.Action<CigInstance> PackCigSelected;
    /// <summary>A reshuffle was clicked but couldn't be paid for.</summary>
    public event System.Action ReshuffleFailed;

    public enum PurchaseFailReason { NotEnoughCoins, PackFull }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[ShopUIController] Duplicate on '{name}' destroyed — '{Instance.name}' is the live one. Only one ShopUIController per scene.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (buyButton != null) buyButton.onClick.AddListener(OnBuyButtonClicked);
        if (burnButton != null) burnButton.onClick.AddListener(OnBurnButtonClicked);
        if (reshuffleButton != null) reshuffleButton.onClick.AddListener(OnReshuffleClicked);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);

        if (offerCards != null)
            foreach (UpgradeCardView card in offerCards)
            {
                if (card == null) continue;
                card.OnClicked += OnBuyCardClicked;
                card.OnHoverEnter += OnOfferCardHoverEnter;
                card.OnHoverExit += OnOfferCardHoverExit;
            }

        if (packCigSlots != null)
            foreach (PackCigView cig in packCigSlots)
            {
                if (cig == null) continue;
                cig.OnClicked += OnPackCigClicked;
                cig.OnHoverEnter += OnPackCigHoverEnter;
                cig.OnHoverExit += OnPackCigHoverExit;
            }
    }

    private void Update()
    {
        if (shopPanel != null && !shopPanel.activeInHierarchy) return;
        if (!Input.GetMouseButtonDown(0)) return;

        // Runs on mouse-down, before any Button's onClick (mouse-up), so
        // clicking another card/cig clears the old selection first and then
        // selects the new one.
        Transform hit = PointerHit();

        // Burn selection survives only a click on the selected cig or the Burn button.
        PackCigView selectedCig = SelectedPackCig();
        if (selectedCig != null && !IsUnder(hit, selectedCig.transform) && !IsUnder(hit, burnButton))
            ClearBurnSelection();

        // Buy selection (and its stuck focus) survives only a click on the
        // selected card or the Buy button. Left alone while a replace-confirm
        // is pending — that popup's own buttons would otherwise clear it
        // before ConfirmReplacePurchase reads it.
        if (_selectedCard != null && _pendingReplaceOffer == null
            && !IsUnder(hit, _selectedCard.transform) && !IsUnder(hit, buyButton))
            ClearBuySelection();
    }

    /// <summary>Topmost UI object under the cursor, or null.</summary>
    private Transform PointerHit()
    {
        if (EventSystem.current == null) return null;

        PointerEventData pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        _clickHits.Clear();
        EventSystem.current.RaycastAll(pointer, _clickHits);
        return _clickHits.Count > 0 ? _clickHits[0].gameObject.transform : null;
    }

    private static bool IsUnder(Transform hit, Component root)
    {
        return hit != null && root != null && hit.IsChildOf(root.transform);
    }

    private PackCigView SelectedPackCig()
    {
        if (packCigSlots == null) return null;
        foreach (PackCigView slot in packCigSlots)
            if (slot != null && slot.IsSelected) return slot;
        return null;
    }

    public void Open()
    {
        if (shopPanel != null) shopPanel.SetActive(true);

        RoguelikeManager.Instance?.SetGameplayInputEnabled(false);
        CursorController.Instance?.SetCursorOverride(true);
        BurningCigsHUD.Instance?.SetVisible(false);

        RefreshCoinsText();
        ClearSelection();
        _reshufflesThisVisit = 0;
        PopulateBuyCards();
        PopulatePackCigs();
        RefreshReshuffleUI();
    }

    private void Close()
    {
        ClearFocus();
        if (shopPanel != null) shopPanel.SetActive(false);
        CursorController.Instance?.SetCursorOverride(false);
        BurningCigsHUD.Instance?.SetVisible(true);
    }

    /// <summary>Fresh visit: the first offerCount cards get offers, the rest are hidden.</summary>
    private void PopulateBuyCards()
    {
        List<UpgradeCardView> slots = new List<UpgradeCardView>();
        if (offerCards != null)
            foreach (UpgradeCardView card in offerCards)
            {
                if (card == null) continue;
                if (slots.Count < offerCount) slots.Add(card);
                else card.SetEmpty();
            }

        FillCards(slots);
    }

    /// <summary>
    /// Rolls fresh offers into `slots` only. RollOffers' selection (every
    /// unpurchased lineage, shuffled) is untouched — the first slots.Count
    /// are shown; slots left over when the pool runs dry are hidden.
    /// </summary>
    private void FillCards(List<UpgradeCardView> slots)
    {
        List<CigInstance> offers = CigPool.Instance != null
            ? CigPool.Instance.RollOffers()
            : new List<CigInstance>();

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < offers.Count)
                slots[i].Populate(offers[i]);
            else
                slots[i].SetEmpty();
        }

        // A card still under the cursor now holds a different offer (or is
        // hidden) — refresh its focus and tooltip instead of leaving stale ones.
        if (_focusedCard != null && _focusedCard.isActiveAndEnabled && _focusedCard.BoundInstance != null)
            OnOfferCardHoverEnter(_focusedCard);
        else
            ClearFocus();
    }

    private void PopulatePackCigs()
    {
        List<CigInstance> burnable = new List<CigInstance>();
        if (Pack.Instance != null)
        {
            // Already-burning cigs drop out of the Burn list immediately —
            // they can't be burned again, and their countdown shows on the
            // BurningCigsHUD once the player leaves the shop instead.
            foreach (CigInstance instance in Pack.Instance.HeldCigs)
                if (!instance.IsBurning)
                    burnable.Add(instance);
        }

        int slotCount = packCigSlots != null ? packCigSlots.Length : 0;
        for (int i = 0; i < slotCount; i++)
        {
            PackCigView slot = packCigSlots[i];
            if (slot == null) continue;

            if (i < burnable.Count)
                slot.Populate(burnable[i]);
            else
                slot.SetEmpty();
        }
    }

    private void OnBuyCardClicked(UpgradeCardView card)
    {
        if (card.BoundInstance == null) return;

        // Selecting a different offer abandons any pending replace-confirmation tied to the old one.
        _pendingReplaceOffer = null;
        _pendingReplaceConflict = null;

        _selectedOffer = card.BoundInstance;
        _selectedCard = card;
        HighlightOnly(offerCards, card);
        ApplyFocus();
        ShowDetail(_selectedOffer);
        if (buyButton != null) buyButton.interactable = true;

        UpgradeSelected?.Invoke(_selectedOffer);
    }

    private void OnOfferCardHoverEnter(UpgradeCardView card)
    {
        if (card.BoundInstance == null) return;

        _focusedCard = card;
        ApplyFocus();
        ShowOfferTooltip(card);
    }

    private void OnOfferCardHoverExit(UpgradeCardView card)
    {
        if (tooltip != null) tooltip.Hide(card);
        if (card != _focusedCard) return;

        _focusedCard = null;
        ApplyFocus();
    }

    private void ClearFocus()
    {
        if (_focusedCard != null && tooltip != null) tooltip.Hide(_focusedCard);
        _focusedCard = null;
        ApplyFocus();
    }

    private void ApplyFocus()
    {
        if (offerCards == null) return;

        // A clicked card stays focused over whatever the pointer is hovering.
        UpgradeCardView focus = _selectedCard != null ? _selectedCard : _focusedCard;

        foreach (UpgradeCardView card in offerCards)
        {
            if (card == null || card.BoundInstance == null) continue; // hidden cards sit out of the focus group

            UpgradeCardView.FocusState state = focus == null ? UpgradeCardView.FocusState.Neutral
                : card == focus ? UpgradeCardView.FocusState.Focused
                : UpgradeCardView.FocusState.Unfocused;
            card.SetFocusState(state);
        }
    }

    private void ShowOfferTooltip(UpgradeCardView card)
    {
        CigData data = card.BoundInstance?.Data;
        if (tooltip == null || data == null) return;

        string stats = null;
        if (Pack.Instance != null)
        {
            // Current = the live PackStats; upgraded = Pack's side-effect-free
            // preview, which resolves through the same path as a real Buy.
            List<string> lines = PackStatsPreview.BuildLines(
                Pack.Instance.Stats,
                Pack.Instance.PreviewBuy(card.BoundInstance),
                tooltip.PreviewArrow());
            if (lines.Count > 0) stats = string.Join("\n", lines);
        }

        tooltip.ShowLarge(card, data.displayName, data.description, stats, RarityTint(card.BoundInstance));
    }

    private void OnPackCigClicked(PackCigView cig)
    {
        if (cig.BoundInstance == null) return;

        // Clicking the already-selected cig keeps it selected — only a click elsewhere clears it (see Update).
        if (cig.IsSelected) return;

        _selectedHeld = cig.BoundInstance;
        HighlightOnly(packCigSlots, cig);
        if (burnButton != null) burnButton.interactable = true;
        ShowDetail(_selectedHeld);

        PackCigSelected?.Invoke(_selectedHeld);
    }

    private void OnBurnButtonClicked()
    {
        PackCigView cig = SelectedPackCig();
        if (_selectedHeld == null || BurnResolver.Instance == null) return;

        // That slot is about to hold a different cig (or be hidden) — drop
        // the tooltip rather than leave it describing the burned one.
        if (tooltip != null && cig != null) tooltip.Hide(cig);

        BurnResolver.Instance.Burn(_selectedHeld);
        ClearBurnSelection();
        PopulatePackCigs();
    }

    private void OnPackCigHoverEnter(PackCigView cig)
    {
        CigInstance instance = cig.BoundInstance;
        if (tooltip == null || instance?.Data == null) return;

        // Exact numbers this cig adds: the live stats vs. the same pack without it.
        string stats = TierRarityLine(instance);
        if (Pack.Instance != null)
        {
            List<string> lines = PackStatsPreview.BuildContributionLines(
                Pack.Instance.PreviewWithout(instance),
                Pack.Instance.Stats);
            if (lines.Count > 0) stats += "\n" + string.Join("\n", lines);
        }

        tooltip.ShowLarge(cig, instance.Data.displayName, instance.Data.description, stats, RarityTint(instance));
    }

    private void OnPackCigHoverExit(PackCigView cig)
    {
        if (tooltip != null) tooltip.Hide(cig);
    }

    private static void HighlightOnly(PackCigView[] slots, PackCigView selected)
    {
        if (slots == null) return;
        foreach (PackCigView slot in slots)
            if (slot != null) slot.SetSelected(slot == selected);
    }

    private static void HighlightOnly(UpgradeCardView[] cards, UpgradeCardView selected)
    {
        if (cards == null) return;
        foreach (UpgradeCardView card in cards)
            if (card != null) card.SetSelected(card == selected);
    }

    // Single shared field for now, per UpgradeSystemPlan.md Step 3 — segregate
    // into separate name/desc/cost/stat fields later once this is validated.
    private void ShowDetail(CigInstance instance)
    {
        if (detailText == null || instance?.Data == null) return;

        CigData data = instance.Data;
        string burning = instance.IsBurning
            ? "\nBurning — resolves at max Tier this level, then gone"
            : "";

        detailText.text =
            $"{data.displayName}\n" +
            $"{data.description}\n\n" +
            $"Cost: {data.cost}\n" +
            $"Slot: {data.targetSlot} | Brand: {data.brand}\n" +
            $"{TierRarityLine(instance)}" +
            $"{burning}";
    }

    /// <summary>Rarity colour for the tooltip tint; null (untinted) for rarity-less upgrades, same rule as the card border.</summary>
    private Color? RarityTint(CigInstance instance)
    {
        if (rarityConfig == null || instance?.Data == null || !instance.Data.hasRarity) return null;
        return rarityConfig.GetColor(instance.RolledRarity);
    }

    private static string TierRarityLine(CigInstance instance)
    {
        return instance.Data.hasRarity
            ? $"Tier {instance.RolledTier} | {instance.RolledRarity}"
            : $"Tier {instance.RolledTier}";
    }

    private void ClearSelection()
    {
        ClearBuySelection();
        ClearBurnSelection();
        if (detailText != null) detailText.text = "Select a cig to see its details.";
    }

    private void ClearBuySelection()
    {
        _selectedOffer = null;
        _selectedCard = null;
        _pendingReplaceOffer = null;
        _pendingReplaceConflict = null;
        HighlightOnly(offerCards, null);
        if (buyButton != null) buyButton.interactable = false;
        ApplyFocus();
    }

    private void ClearBurnSelection()
    {
        _selectedHeld = null;
        HighlightOnly(packCigSlots, null);
        if (burnButton != null) burnButton.interactable = false;
    }

    private void RefreshCoinsText()
    {
        if (coinsText == null) return;
        int amount = CoinManager.Instance != null ? CoinManager.Instance.GetCurrentCoins() : 0;
        coinsText.text = $"Coins: {amount}";
    }

    private void OnBuyButtonClicked()
    {
        if (_selectedOffer == null || Pack.Instance == null) return;

        CigInstance conflict = Pack.Instance.GetBrandConflict(_selectedOffer.Data);
        if (conflict != null)
        {
            _pendingReplaceOffer = _selectedOffer;
            _pendingReplaceConflict = conflict;
            OnBrandConflictDetected?.Invoke(_selectedOffer, conflict);
            if (detailText != null)
                detailText.text = $"Buying {_selectedOffer.Data.displayName} will replace {conflict.Data.displayName} in your pack — confirm to proceed.";
            return;
        }

        FinishBuyAttempt(Pack.Instance.Buy(_selectedOffer), _selectedOffer);
    }

    /// <summary>Call from a confirmation panel's Confirm button to finalize a buy flagged by OnBrandConflictDetected.</summary>
    public void ConfirmReplacePurchase()
    {
        if (_pendingReplaceOffer == null || Pack.Instance == null) return;

        CigInstance offer = _pendingReplaceOffer;
        bool bought = Pack.Instance.BuyWithReplace(offer, _pendingReplaceConflict);
        _pendingReplaceOffer = null;
        _pendingReplaceConflict = null;
        FinishBuyAttempt(bought, offer);
    }

    /// <summary>Call from a confirmation panel's Cancel button to abandon a pending replace-buy.</summary>
    public void CancelReplacePurchase()
    {
        _pendingReplaceOffer = null;
        _pendingReplaceConflict = null;
    }

    private void FinishBuyAttempt(bool bought, CigInstance offer)
    {
        if (bought)
        {
            RefreshCoinsText();
            ClearBuySelection();

            // A bought card's slot stays empty until the next visit — the
            // other cards keep their offers, nothing re-rolls.
            UpgradeCardView boughtCard = FindCard(offer);
            if (boughtCard != null) boughtCard.SetEmpty();

            PopulatePackCigs(); // held list changed — the bought cig now occupies a pack slot
            RefreshReshuffleUI();
            UpgradePurchased?.Invoke(offer);
        }
        else
        {
            // Buy/BuyWithReplace only fail on coins or space — coins are
            // checked first, so still being short means that was the cause.
            PurchaseFailReason reason =
                CoinManager.Instance != null && !CoinManager.Instance.HasEnoughCoins(offer.Data.cost)
                    ? PurchaseFailReason.NotEnoughCoins
                    : PurchaseFailReason.PackFull;

            if (detailText != null)
                detailText.text = reason == PurchaseFailReason.NotEnoughCoins
                    ? "Can't buy — not enough coins."
                    : "Can't buy — pack full.";
            PurchaseFailed?.Invoke(offer, reason);
        }
    }

    private UpgradeCardView FindCard(CigInstance offer)
    {
        if (offerCards == null || offer == null) return null;
        foreach (UpgradeCardView card in offerCards)
            if (card != null && card.BoundInstance == offer) return card;
        return null;
    }

    /// <summary>Cards still showing an offer — the only ones a reshuffle refills.</summary>
    private List<UpgradeCardView> VisibleOfferCards()
    {
        List<UpgradeCardView> visible = new List<UpgradeCardView>();
        if (offerCards != null)
            foreach (UpgradeCardView card in offerCards)
                if (card != null && card.BoundInstance != null) visible.Add(card);
        return visible;
    }

    private void OnReshuffleClicked()
    {
        List<UpgradeCardView> visible = VisibleOfferCards();
        if (visible.Count == 0) return;

        if (CoinManager.Instance != null && !CoinManager.Instance.SpendCoins(ReshuffleCost))
        {
            if (detailText != null) detailText.text = $"Not enough coins to reshuffle ({ReshuffleCost}).";
            ReshuffleFailed?.Invoke();
            return;
        }

        _reshufflesThisVisit++;
        ClearSelection();
        FillCards(visible);
        RefreshCoinsText();
        RefreshReshuffleUI();
    }

    private void RefreshReshuffleUI()
    {
        if (reshuffleCostText != null) reshuffleCostText.text = $"Reshuffle: ({ReshuffleCost})";
        if (reshuffleButton != null) reshuffleButton.interactable = VisibleOfferCards().Count > 0;
    }

    private void OnContinueClicked()
    {
        RoguelikeManager.Instance?.SetGameplayInputEnabled(true);
        Close();
        RoguelikeManager.Instance?.ContinueDungeon();
    }
}
