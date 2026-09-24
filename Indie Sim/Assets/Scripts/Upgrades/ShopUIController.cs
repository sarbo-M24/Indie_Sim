using System.Collections.Generic;
using UnityEngine;
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
/// Buy offers use UpgradeCardView (name + cost face, hover focus, cursor
/// tooltip with a stat preview); Burn slots still use CigCardUI. This class
/// owns which offer card is hover-focused and raises local (instance, not
/// static) events the shop mascot reacts to — shop-local chatter, kept off
/// the global event bus.
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
    [SerializeField] private Button burnButton;
    [SerializeField] private Button continueButton;

    [Header("Card slots — fixed pool, hidden when unused")]
    [SerializeField] private UpgradeCardView[] offerCards;
    [SerializeField] private CigCardUI[] burnCardSlots;

    [Header("Offers")]
    [Tooltip("How many Buy offers to show per shop visit. Capped by offerCards.Length and the pool size.")]
    [SerializeField, Min(0)] private int offerCount = 3;

    [Header("Tooltip")]
    [SerializeField] private CursorTooltip tooltip;

    [Header("Rarity icons, indexed by Rarity enum (Common, Uncommon, Rare, Epic)")]
    [SerializeField] private Sprite[] rarityIcons;

    private CigInstance _selectedOffer;
    private CigInstance _selectedHeld;
    private CigInstance _pendingReplaceOffer;
    private CigInstance _pendingReplaceConflict;
    private UpgradeCardView _focusedCard;

    /// <summary>Fired when a Buy attempt hits a same-slot Mild/Regular brand conflict — hook for a confirmation panel. Not fired for a normal, unconflicted buy. Params: (offer, conflictingHeld).</summary>
    public static event System.Action<CigInstance, CigInstance> OnBrandConflictDetected;

    /// <summary>An offer card was clicked (selected, pending Buy).</summary>
    public event System.Action<CigInstance> UpgradeSelected;
    /// <summary>A Buy (or confirmed replace-buy) went through.</summary>
    public event System.Action<CigInstance> UpgradePurchased;
    /// <summary>A Buy attempt failed — not enough coins or pack full.</summary>
    public event System.Action<CigInstance> PurchaseFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (buyButton != null) buyButton.onClick.AddListener(OnBuyButtonClicked);
        if (burnButton != null) burnButton.onClick.AddListener(OnBurnButtonClicked);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);

        if (offerCards != null)
            foreach (UpgradeCardView card in offerCards)
            {
                if (card == null) continue;
                card.OnClicked += OnBuyCardClicked;
                card.OnHoverEnter += OnOfferCardHoverEnter;
                card.OnHoverExit += OnOfferCardHoverExit;
            }

        if (burnCardSlots != null)
            foreach (CigCardUI card in burnCardSlots)
                if (card != null) card.OnClicked += OnHeldCardClicked;
    }

    public void Open()
    {
        if (shopPanel != null) shopPanel.SetActive(true);

        RoguelikeManager.Instance?.SetGameplayInputEnabled(false);
        CursorController.Instance?.SetCursorOverride(true);
        BurningCigsHUD.Instance?.SetVisible(false);

        RefreshCoinsText();
        ClearSelection();
        PopulateBuyCards();
        PopulateBurnCards();
    }

    private void Close()
    {
        ClearFocus();
        if (shopPanel != null) shopPanel.SetActive(false);
        CursorController.Instance?.SetCursorOverride(false);
        BurningCigsHUD.Instance?.SetVisible(true);
    }

    private void PopulateBuyCards()
    {
        List<CigInstance> offers = CigPool.Instance != null
            ? CigPool.Instance.RollOffers()
            : new List<CigInstance>();

        // RollOffers' selection (every unpurchased lineage, shuffled) is
        // untouched — only the first offerCount are shown.
        int shown = Mathf.Min(offerCount, offers.Count);
        int cardCount = offerCards != null ? offerCards.Length : 0;
        for (int i = 0; i < cardCount; i++)
        {
            UpgradeCardView card = offerCards[i];
            if (card == null) continue;

            if (i < shown)
                card.Populate(offers[i]);
            else
                card.SetEmpty();
        }

        // A card still under the cursor now holds a different offer (or is
        // hidden) — refresh its focus and tooltip instead of leaving stale ones.
        if (_focusedCard != null && _focusedCard.isActiveAndEnabled && _focusedCard.BoundInstance != null)
            OnOfferCardHoverEnter(_focusedCard);
        else
            ClearFocus();
    }

    private void PopulateBurnCards()
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

        int burnSlotCount = burnCardSlots != null ? burnCardSlots.Length : 0;
        for (int i = 0; i < burnSlotCount; i++)
        {
            CigCardUI slot = burnCardSlots[i];
            if (slot == null) continue;

            if (i < burnable.Count)
                slot.Populate(burnable[i], RarityIconFor(burnable[i]));
            else
                slot.SetEmpty();
        }
    }

    private Sprite RarityIconFor(CigInstance instance)
    {
        if (rarityIcons == null || rarityIcons.Length == 0) return null;
        int index = Mathf.Clamp((int)instance.RolledRarity, 0, rarityIcons.Length - 1);
        return rarityIcons[index];
    }

    private void OnBuyCardClicked(UpgradeCardView card)
    {
        if (card.BoundInstance == null) return;

        // Selecting a different offer abandons any pending replace-confirmation tied to the old one.
        _pendingReplaceOffer = null;
        _pendingReplaceConflict = null;

        _selectedOffer = card.BoundInstance;
        HighlightOnly(offerCards, card);
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

        foreach (UpgradeCardView card in offerCards)
        {
            if (card == null || card.BoundInstance == null) continue; // hidden cards sit out of the focus group

            UpgradeCardView.FocusState state = _focusedCard == null ? UpgradeCardView.FocusState.Neutral
                : card == _focusedCard ? UpgradeCardView.FocusState.Focused
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

        tooltip.ShowLarge(card, data.displayName, data.description, stats);
    }

    private void OnHeldCardClicked(CigCardUI card)
    {
        if (card.BoundInstance == null) return;

        _selectedHeld = card.BoundInstance;
        HighlightOnly(burnCardSlots, card);
        ShowDetail(_selectedHeld);
        if (burnButton != null) burnButton.interactable = true;
    }

    private static void HighlightOnly(CigCardUI[] slots, CigCardUI selected)
    {
        if (slots == null) return;
        foreach (CigCardUI slot in slots)
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
        string tierRarity = data.hasRarity
            ? $"Tier {instance.RolledTier} | {instance.RolledRarity}"
            : $"Tier {instance.RolledTier}";
        string burning = instance.IsBurning
            ? "\nBurning — resolves at max Tier this level, then gone"
            : "";

        detailText.text =
            $"{data.displayName}\n" +
            $"{data.description}\n\n" +
            $"Cost: {data.cost}\n" +
            $"Slot: {data.targetSlot} | Brand: {data.brand}\n" +
            $"{tierRarity}" +
            $"{burning}";
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
        _pendingReplaceOffer = null;
        _pendingReplaceConflict = null;
        HighlightOnly(offerCards, null);
        if (buyButton != null) buyButton.interactable = false;
    }

    private void ClearBurnSelection()
    {
        _selectedHeld = null;
        HighlightOnly(burnCardSlots, null);
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
            PopulateBuyCards();
            PopulateBurnCards(); // held list changed — the bought cig now occupies a pack slot
            UpgradePurchased?.Invoke(offer);
        }
        else
        {
            if (detailText != null)
                detailText.text = "Can't buy — pack full or not enough coins.";
            PurchaseFailed?.Invoke(offer);
        }
    }

    private void OnBurnButtonClicked()
    {
        if (_selectedHeld == null || BurnResolver.Instance == null) return;

        BurnResolver.Instance.Burn(_selectedHeld);
        ClearBurnSelection();
        PopulateBurnCards();
    }

    private void OnContinueClicked()
    {
        RoguelikeManager.Instance?.SetGameplayInputEnabled(true);
        Close();
        RoguelikeManager.Instance?.ContinueDungeon();
    }
}
