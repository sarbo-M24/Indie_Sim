using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Confirmation popup for a Buy blocked by Mild/Regular brand exclusivity
/// (see Pack.GetBrandConflict). Listens to ShopUIController's
/// OnBrandConflictDetected hook and routes Replace/Cancel to
/// ConfirmReplacePurchase/CancelReplacePurchase — the buy logic itself is
/// untouched. Hovering the stats button (or selecting it on the pad) shows
/// the shared CursorTooltip with two blocks split by a rule: the held cig
/// and what it adds, then the offer and what it would add (see ShowStats).
///
/// Modal: the shop stops its own input while a replace is pending (see
/// ShopUIController.Update), so this owns focus and B. Gamepad focus starts
/// on Cancel, so a stray A never swaps a cig out; B / Circle cancels. Esc is
/// left to the pause menu, same as the shop's own B handling.
///
/// The shop is excluded from ButtonFocusStyle (it keeps its own look), so
/// this applies the canvas's ButtonFocusStyle to its buttons itself — same
/// grow + tint as the Settings menu.
///
/// Put this on an object that stays active while the shop is open (it has
/// to be enabled to hear the event); panelRoot is the part that toggles.
/// panelRoot should include a full-screen raycast-target blocker so the
/// shop behind can't be clicked while the question is open.
/// Built by Tools/Shop/Build Replace Confirm Panel (ReplaceConfirmUIBuilder),
/// which saves panelRoot hidden.
/// </summary>
public class ReplaceConfirmPanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Falls back to ShopUIController.Instance when left empty.")]
    [SerializeField] private ShopUIController shop;
    [SerializeField] private CursorTooltip tooltip;
    [Tooltip("Focus look for the buttons. Falls back to the ButtonFocusStyle on a parent canvas.")]
    [SerializeField] private ButtonFocusStyle focusStyle;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button replaceButton;
    [SerializeField] private Button cancelButton;
    [Tooltip("Button/image that shows the stat preview tooltip on hover (and on pad selection, if it's a Selectable).")]
    [SerializeField] private PointerHoverRelay statsHover;

    private CigInstance _offer;
    private CigInstance _conflict;
    private Selectable _statsSelectable;
    private bool _statsShownBySelection;
    private int _lastFrozenFrame = -1;

    private ShopUIController Shop => shop != null ? shop : ShopUIController.Instance;

    /// <summary>True while the replace question is showing.</summary>
    public bool IsOpen => _offer != null;

    private void Awake()
    {
        if (replaceButton != null) replaceButton.onClick.AddListener(OnReplaceClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (statsHover != null) _statsSelectable = statsHover.GetComponent<Selectable>();

        if (focusStyle == null) focusStyle = GetComponentInParent<ButtonFocusStyle>(true);
        if (focusStyle != null)
        {
            focusStyle.Apply(replaceButton);
            focusStyle.Apply(cancelButton);
            focusStyle.Apply(_statsSelectable as Button);
        }

        // Stats sits above the two answers: Up from either reaches it, Down
        // goes back to Cancel (the safe one). Replace ↔ Cancel wrap.
        SetNavigation(replaceButton, up: _statsSelectable, down: null, left: cancelButton, right: cancelButton);
        SetNavigation(cancelButton, up: _statsSelectable, down: null, left: replaceButton, right: replaceButton);
        SetNavigation(_statsSelectable, up: null, down: cancelButton, left: null, right: null);

        // panelRoot is saved hidden; not toggled here, since Awake can run
        // mid-activation of the shop (Update hides a stray one).
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

        // Shop closed with the question still open (only scene exit can do
        // that — Continue is blocked) — treat it as a cancel so no pending
        // replace-buy lingers. panelRoot is left as is: toggling a child mid
        // deactivation errors; Update hides it if the shop ever reopens.
        if (_offer != null)
        {
            Close(hideRoot: false);
            Shop?.CancelReplacePurchase();
        }
    }

    private void Update()
    {
        if (_offer == null)
        {
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
            return;
        }

        // Paused over the shop: the pause menu owns focus and B. Remembered
        // for a frame too — the B that resumes unfreezes before this runs.
        if (PauseController.IsFrozen)
        {
            _lastFrozenFrame = Time.frameCount;
            return;
        }

        if (_lastFrozenFrame < Time.frameCount - 1
            && InputManager.Controls.UI.Cancel.WasPressedThisFrame()
            && InputManager.Controls.UI.Cancel.activeControl?.device is UnityEngine.InputSystem.Gamepad)
        {
            OnCancelClicked();
            return;
        }

        if (panelRoot != null) UIFocus.EnsureSelection(panelRoot.transform, cancelButton);
        TrackStatsSelection();
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

        if (InputManager.UsingGamepad && EventSystem.current != null && cancelButton != null)
            EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
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

    private void Close(bool hideRoot = true)
    {
        HideStats();
        _statsShownBySelection = false;
        _offer = null;
        _conflict = null;
        if (hideRoot && panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>Pad selection stands in for hover on the stats button, like the shop's cards.</summary>
    private void TrackStatsSelection()
    {
        if (_statsSelectable == null) return;

        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool onStats = InputManager.UsingGamepad && selected == _statsSelectable.gameObject;
        if (onStats == _statsShownBySelection) return;

        _statsShownBySelection = onStats;
        if (onStats) ShowStats();
        else HideStats();
    }

    private void ShowStats()
    {
        if (tooltip == null || _offer == null || _conflict == null) return;

        // Each cig's own contribution, both measured against the pack without
        // the held one: the held cig vs. the live stats, the offer vs. the
        // post-buy preview — so the two blocks compare like for like.
        List<string> currentLines = null, buyingLines = null;
        if (Pack.Instance != null)
        {
            PackStats withoutConflict = Pack.Instance.PreviewWithout(_conflict);
            currentLines = PackStatsPreview.BuildContributionLines(withoutConflict, Pack.Instance.Stats);
            buyingLines = PackStatsPreview.BuildContributionLines(withoutConflict, Pack.Instance.PreviewBuy(_offer));
        }

        string stats =
            CigBlock("Current", _conflict, currentLines) +
            $"\n<color={SeparatorColor}>{Separator}</color>\n" +
            CigBlock("Buying", _offer, buyingLines);

        // Owned by the stats button, so on the pad it anchors beside that button.
        tooltip.ShowLarge(TooltipOwner, "Replace cig?", null, stats);
    }

    // Plain hyphens: LiberationSans SDF (the shop font) has no box-drawing glyphs.
    private const string Separator = "------------------------------";
    private const string SeparatorColor = "#FFFFFF66";
    private const string HeaderColor = "#FFD24D";
    private const string DimColor = "#FFFFFFAA";

    /// <summary>"CURRENT: Name", tier/rarity, then the cig's stat lines (or its description if it adds no stats).</summary>
    private static string CigBlock(string heading, CigInstance cig, List<string> lines)
    {
        CigData data = cig.Data;
        string tier = data.hasRarity
            ? $"Tier {cig.RolledTier} | {cig.RolledRarity}"
            : $"Tier {cig.RolledTier}";

        string body = lines != null && lines.Count > 0
            ? string.Join("\n", lines)
            : $"<i>{data.description}</i>";

        return $"<color={HeaderColor}><b>{heading.ToUpperInvariant()}: {data.displayName}</b></color>\n" +
               $"<color={DimColor}>{data.brand} | {tier}</color>\n" +
               body;
    }

    private void HideStats()
    {
        if (tooltip != null) tooltip.Hide(TooltipOwner);
    }

    private Component TooltipOwner => statsHover != null ? statsHover : (Component)this;

    private static void SetNavigation(Selectable selectable, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        if (selectable == null) return;
        selectable.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right
        };
    }
}
