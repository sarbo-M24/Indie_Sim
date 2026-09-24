using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Confirmation popup for a Buy blocked by Mild/Regular brand exclusivity
/// (see Pack.GetBrandConflict). Listens to ShopUIController's
/// OnBrandConflictDetected hook and routes Replace/Cancel to
/// ConfirmReplacePurchase/CancelReplacePurchase — the buy logic itself is
/// untouched. Hovering the stats button shows the shared CursorTooltip with
/// the same "current → after" preview the offer cards use; Pack.PreviewBuy
/// already removes the conflicting cig, so the preview shows what's lost
/// and gained by the swap.
///
/// Put this on an object that stays active while the shop is open (it has
/// to be enabled to hear the event); panelRoot is the part that toggles.
/// panelRoot should include a full-screen raycast-target blocker so the
/// shop behind can't be clicked while the question is open.
/// </summary>
public class ReplaceConfirmPanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Falls back to ShopUIController.Instance when left empty.")]
    [SerializeField] private ShopUIController shop;
    [SerializeField] private CursorTooltip tooltip;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button replaceButton;
    [SerializeField] private Button cancelButton;
    [Tooltip("Button/image that shows the stat preview tooltip on hover.")]
    [SerializeField] private PointerHoverRelay statsHover;

    private CigInstance _offer;
    private CigInstance _conflict;

    private ShopUIController Shop => shop != null ? shop : ShopUIController.Instance;

    private void Awake()
    {
        if (replaceButton != null) replaceButton.onClick.AddListener(OnReplaceClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        ShopUIController.OnBrandConflictDetected += Show;
        if (statsHover != null)
        {
            statsHover.Entered += ShowStats;
            statsHover.Exited += HideStats;
        }
    }

    private void OnDisable()
    {
        ShopUIController.OnBrandConflictDetected -= Show;
        if (statsHover != null)
        {
            statsHover.Entered -= ShowStats;
            statsHover.Exited -= HideStats;
        }

        // Shop closed with the question still open — treat it as a cancel so
        // no pending replace-buy lingers into the next visit.
        if (_offer != null) OnCancelClicked();
    }

    private void Show(CigInstance offer, CigInstance conflict)
    {
        if (offer?.Data == null || conflict?.Data == null) return;

        _offer = offer;
        _conflict = conflict;

        if (messageText != null)
            messageText.text =
                $"You already have {conflict.Data.displayName} ({conflict.Data.brand}).\n" +
                $"Replace it with {offer.Data.displayName} ({offer.Data.brand}) for {offer.Data.cost} coins?";

        if (panelRoot != null) panelRoot.SetActive(true);
    }

    private void OnReplaceClicked()
    {
        Close();
        Shop?.ConfirmReplacePurchase();
    }

    private void OnCancelClicked()
    {
        Close();
        Shop?.CancelReplacePurchase();
    }

    private void Close()
    {
        HideStats();
        _offer = null;
        _conflict = null;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void ShowStats()
    {
        if (tooltip == null || _offer == null || _conflict == null) return;

        string stats = null;
        if (Pack.Instance != null)
        {
            var lines = PackStatsPreview.BuildLines(
                Pack.Instance.Stats,
                Pack.Instance.PreviewBuy(_offer),
                tooltip.PreviewArrow());
            if (lines.Count > 0) stats = string.Join("\n", lines);
        }

        tooltip.ShowLarge(
            this,
            $"{_conflict.Data.displayName} {tooltip.PreviewArrow()} {_offer.Data.displayName}",
            _offer.Data.description,
            stats);
    }

    private void HideStats()
    {
        if (tooltip != null) tooltip.Hide(this);
    }
}
