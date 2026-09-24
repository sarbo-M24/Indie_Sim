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
    [SerializeField] private CigCardUI[] buyCardSlots;
    [SerializeField] private CigCardUI[] burnCardSlots;

    [Header("Rarity icons, indexed by Rarity enum (Common, Uncommon, Rare, Epic)")]
    [SerializeField] private Sprite[] rarityIcons;

    private CigInstance _selectedOffer;
    private CigInstance _selectedHeld;
    private CigInstance _pendingReplaceOffer;
    private CigInstance _pendingReplaceConflict;

    /// <summary>Fired when a Buy attempt hits a same-slot Mild/Regular brand conflict — hook for a confirmation panel. Not fired for a normal, unconflicted buy. Params: (offer, conflictingHeld).</summary>
    public static event System.Action<CigInstance, CigInstance> OnBrandConflictDetected;

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

        if (buyCardSlots != null)
            foreach (CigCardUI card in buyCardSlots)
                if (card != null) card.OnClicked += OnBuyCardClicked;

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
        if (shopPanel != null) shopPanel.SetActive(false);
        CursorController.Instance?.SetCursorOverride(false);
        BurningCigsHUD.Instance?.SetVisible(true);
    }

    private void PopulateBuyCards()
    {
        List<CigInstance> offers = CigPool.Instance != null
            ? CigPool.Instance.RollOffers()
            : new List<CigInstance>();

        int buySlotCount = buyCardSlots != null ? buyCardSlots.Length : 0;
        for (int i = 0; i < buySlotCount; i++)
        {
            CigCardUI slot = buyCardSlots[i];
            if (slot == null) continue;

            if (i < offers.Count)
                slot.Populate(offers[i], RarityIconFor(offers[i]));
            else
                slot.SetEmpty();
        }
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

    private void OnBuyCardClicked(CigCardUI card)
    {
        if (card.BoundInstance == null) return;

        // Selecting a different offer abandons any pending replace-confirmation tied to the old one.
        _pendingReplaceOffer = null;
        _pendingReplaceConflict = null;

        _selectedOffer = card.BoundInstance;
        HighlightOnly(buyCardSlots, card);
        ShowDetail(_selectedOffer);
        if (buyButton != null) buyButton.interactable = true;
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
        HighlightOnly(buyCardSlots, null);
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

        FinishBuyAttempt(Pack.Instance.Buy(_selectedOffer));
    }

    /// <summary>Call from a confirmation panel's Confirm button to finalize a buy flagged by OnBrandConflictDetected.</summary>
    public void ConfirmReplacePurchase()
    {
        if (_pendingReplaceOffer == null || Pack.Instance == null) return;

        bool bought = Pack.Instance.BuyWithReplace(_pendingReplaceOffer, _pendingReplaceConflict);
        _pendingReplaceOffer = null;
        _pendingReplaceConflict = null;
        FinishBuyAttempt(bought);
    }

    /// <summary>Call from a confirmation panel's Cancel button to abandon a pending replace-buy.</summary>
    public void CancelReplacePurchase()
    {
        _pendingReplaceOffer = null;
        _pendingReplaceConflict = null;
    }

    private void FinishBuyAttempt(bool bought)
    {
        if (bought)
        {
            RefreshCoinsText();
            ClearBuySelection();
            PopulateBuyCards();
            PopulateBurnCards(); // held list changed — the bought cig now occupies a pack slot
        }
        else if (detailText != null)
        {
            detailText.text = "Can't buy — pack full or not enough coins.";
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
