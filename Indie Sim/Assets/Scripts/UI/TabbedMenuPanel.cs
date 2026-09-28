using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Shared behaviour of the tabbed sub-menus opened from the pause / main
/// menu (SettingsPanel, ControlsPanel): tabs, a Reset-to-defaults button for
/// the current tab, and a Back button.
///
/// Opened by the owning menu with Open(onClosed). The owner hides its own
/// panel while this is open and re-shows it in onClosed. Back, B/Esc or
/// Start/Esc close it and save settings.json.
///
/// Gamepad: LB/RB (or Q/E) switch tabs; Up/Down walk the current tab's rows
/// then Reset and Back. A tab whose rows sit in a ScrollRect scrolls to keep
/// the selected row in view. While open it blocks the Player map and shows
/// the cursor, like GamepadMenuPanel.
/// </summary>
public abstract class TabbedMenuPanel : MonoBehaviour
{
    [Serializable]
    private class Tab
    {
        [Tooltip("Header button that opens this tab (mouse). LB/RB also switch tabs.")]
        public Button button;
        [Tooltip("The rows for this tab. Only the current tab's content is active.")]
        public GameObject content;
        [Tooltip("Optional. Shown only while this tab is current (underline, highlight...).")]
        public GameObject activeMarker;
    }

    [SerializeField] private Tab[] tabs;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button backButton;

    private static int openCount;
    private static int lastClosedFrame = -1;

    private int _current;
    private bool _relink;
    private bool _selectTopAfterRelink;
    private Selectable _top;
    private Action _onClosed;
    private readonly List<Selectable> _navList = new List<Selectable>();
    private readonly List<Selectable> _rows = new List<Selectable>();

    /// <summary>
    /// True while any of these panels is open, and on the frame one closes.
    /// The pause menu checks this so the Esc/Start that closes a sub-menu
    /// doesn't also resume the game.
    /// </summary>
    public static bool BlocksPauseInput => openCount > 0 || lastClosedFrame == Time.frameCount;

    public bool IsOpen => gameObject.activeSelf;

    /// <summary>The current tab's content (its rows live under it).</summary>
    protected GameObject CurrentContent => tabs.Length > 0 ? tabs[_current].content : null;

    /// <summary>While true (e.g. waiting for a rebind key) tabs, Back and focus repair pause.</summary>
    protected virtual bool IsBusy => false;

    /// <summary>Restores the defaults of everything shown in `content`.</summary>
    protected abstract void ResetTab(GameObject content);

    // Statics survive play-mode restarts when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        openCount = 0;
        lastClosedFrame = -1;
    }

    protected virtual void Awake()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i].button == null) continue;

            tabs[i].button.onClick.AddListener(() => ShowTab(index));

            // Tabs are LB/RB on the pad; keep D-pad navigation on the rows.
            tabs[i].button.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        if (resetButton != null) resetButton.onClick.AddListener(() => { if (!IsBusy) ResetTab(CurrentContent); });
        if (backButton != null) backButton.onClick.AddListener(Close);
    }

    /// <summary>Shows the panel. onClosed runs when it closes, before it hides.</summary>
    public void Open(Action onClosed = null)
    {
        _onClosed = onClosed;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        if (!IsOpen || IsBusy) return;

        lastClosedFrame = Time.frameCount;

        // The owner re-shows its menu first, so the Player map block and the
        // cursor never lapse between the two panels.
        Action onClosed = _onClosed;
        _onClosed = null;
        onClosed?.Invoke();

        gameObject.SetActive(false);
    }

    protected virtual void OnEnable()
    {
        openCount++;

        foreach (Button button in GetComponentsInChildren<Button>(true))
            if (!button.TryGetComponent(out ButtonFocusScale _))
                button.gameObject.AddComponent<ButtonFocusScale>();

        InputManager.SetPlayerBlocked(this, true);
        if (CursorController.Instance != null) CursorController.Instance.SetCursorOverride(this, true);

        InputManager.Controls.UI.Cancel.performed += OnBackPressed;
        InputManager.Controls.UI.Pause.performed += OnBackPressed;

        ShowTab(_current);
    }

    protected virtual void OnDisable()
    {
        openCount = Mathf.Max(0, openCount - 1);

        InputManager.Controls.UI.Cancel.performed -= OnBackPressed;
        InputManager.Controls.UI.Pause.performed -= OnBackPressed;

        InputManager.SetPlayerBlocked(this, false);
        if (CursorController.Instance != null) CursorController.Instance.SetCursorOverride(this, false);

        // Also covers the scene unloading with the panel open.
        SettingsService.Save();

        // Don't leave a hidden row selected.
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected != null && selected.transform.IsChildOf(transform))
            eventSystem.SetSelectedGameObject(null);
    }

    protected virtual void Update()
    {
        if (IsBusy) return;

        HandleTabKeys();

        // Linked a frame after a tab is shown, once layout groups have placed its rows.
        if (_relink)
        {
            Relink();
            _relink = false;
        }

        UIFocus.EnsureSelection(transform, _top);
        KeepSelectionVisible();
    }

    private void HandleTabKeys()
    {
        int step = 0;

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.leftShoulder.wasPressedThisFrame) step--;
            if (pad.rightShoulder.wasPressedThisFrame) step++;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.qKey.wasPressedThisFrame) step--;
            if (keyboard.eKey.wasPressedThisFrame) step++;
        }

        if (step != 0 && tabs.Length > 1)
            ShowTab((_current + step + tabs.Length) % tabs.Length);
    }

    private void ShowTab(int index)
    {
        if (tabs.Length == 0) return;

        _current = Mathf.Clamp(index, 0, tabs.Length - 1);
        for (int i = 0; i < tabs.Length; i++)
        {
            bool isCurrent = i == _current;
            if (tabs[i].content != null) tabs[i].content.SetActive(isCurrent);
            if (tabs[i].activeMarker != null) tabs[i].activeMarker.SetActive(isCurrent);
        }

        ScrollRect scroll = CurrentContent != null ? CurrentContent.GetComponentInChildren<ScrollRect>() : null;
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;

        _relink = true;
        _selectTopAfterRelink = true;
    }

    private void Relink()
    {
        // Current tab's rows top-to-bottom by screen height (not hierarchy
        // order), then Reset and Back, wrapping back to the top row.
        _rows.Clear();

        GameObject content = CurrentContent;
        if (content != null)
            foreach (Selectable selectable in content.GetComponentsInChildren<Selectable>())
                if (selectable.IsInteractable() && !(selectable is Scrollbar)) _rows.Add(selectable);
        _rows.Sort((a, b) => ScreenY(b).CompareTo(ScreenY(a)));

        _navList.Clear();
        _navList.AddRange(_rows);
        if (resetButton != null && resetButton.isActiveAndEnabled) _navList.Add(resetButton);
        if (backButton != null && backButton.isActiveAndEnabled) _navList.Add(backButton);

        _top = UIFocus.LinkInOrder(_navList);

        if (_selectTopAfterRelink && _top != null && InputManager.UsingGamepad && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_top.gameObject);
        _selectTopAfterRelink = false;
    }

    // Unity's ScrollRect doesn't follow the selection. The first row snaps to
    // the top and the last row to the bottom (so the list's padding shows);
    // anything else scrolls just enough to be fully visible.
    private void KeepSelectionVisible()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject content = CurrentContent;
        if (eventSystem == null || content == null || _rows.Count == 0) return;

        ScrollRect scroll = content.GetComponentInChildren<ScrollRect>();
        GameObject selected = eventSystem.currentSelectedGameObject;
        if (scroll == null || scroll.content == null || selected == null || !selected.transform.IsChildOf(scroll.content)) return;

        if (selected == _rows[0].gameObject) { scroll.verticalNormalizedPosition = 1f; return; }
        if (selected == _rows[_rows.Count - 1].gameObject) { scroll.verticalNormalizedPosition = 0f; return; }

        // The row that holds the selection (direct child of the scroll content).
        Transform row = selected.transform;
        while (row.parent != null && row.parent != scroll.content) row = row.parent;

        RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        Vector3[] corners = new Vector3[4];
        ((RectTransform)row).GetWorldCorners(corners);
        float top = viewport.InverseTransformPoint(corners[1]).y;
        float bottom = viewport.InverseTransformPoint(corners[0]).y;
        Rect view = viewport.rect;

        Vector2 position = scroll.content.anchoredPosition;
        if (top > view.yMax) position.y -= top - view.yMax;
        else if (bottom < view.yMin) position.y += view.yMin - bottom;
        else return;
        scroll.content.anchoredPosition = position;
    }

    private static float ScreenY(Selectable selectable)
    {
        RectTransform rect = selectable.transform as RectTransform;
        return rect != null ? rect.TransformPoint(rect.rect.center).y : selectable.transform.position.y;
    }

    private void OnBackPressed(InputAction.CallbackContext ctx) => Close();
}
