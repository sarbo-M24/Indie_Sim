using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The Settings screen: tabs of SliderRow / ToggleRow, a Reset-to-defaults
/// button for the current tab, and a Back button. One prefab, placed in the
/// pause menu's canvas and in the main menu (see SettingsAudioInputPlan.md).
///
/// Opened by the owning menu with Open(onClosed). The owner hides its own
/// panel while this is open and re-shows it in onClosed. Closing (Back,
/// B/Esc, or Start/Esc on Pause) saves settings.json.
///
/// Gamepad: LB/RB (or Q/E) switch tabs; Up/Down walk the current tab's rows
/// then Reset and Back; Left/Right move sliders. While open it blocks the
/// Player map and shows the cursor, like GamepadMenuPanel.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    public enum Category { Audio, Gameplay }

    [Serializable]
    private class Tab
    {
        public Category category;
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

    /// <summary>
    /// True while any settings panel is open, and on the frame one closes.
    /// The pause menu checks this so the Esc/Start that closes Settings
    /// doesn't also resume the game.
    /// </summary>
    public static bool BlocksPauseInput => openCount > 0 || lastClosedFrame == Time.frameCount;

    public bool IsOpen => gameObject.activeSelf;

    // Statics survive play-mode restarts when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        openCount = 0;
        lastClosedFrame = -1;
    }

    private void Awake()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i].button == null) continue;

            tabs[i].button.onClick.AddListener(() => ShowTab(index));

            // Tabs are LB/RB on the pad; keep D-pad navigation on the rows.
            tabs[i].button.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        if (resetButton != null) resetButton.onClick.AddListener(ResetCurrentTab);
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
        if (!IsOpen) return;

        lastClosedFrame = Time.frameCount;

        // The owner re-shows its menu first, so the Player map block and the
        // cursor never lapse between the two panels.
        Action onClosed = _onClosed;
        _onClosed = null;
        onClosed?.Invoke();

        gameObject.SetActive(false);
    }

    private void OnEnable()
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

    private void OnDisable()
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

    private void Update()
    {
        HandleTabKeys();

        // Linked a frame after a tab is shown, once layout groups have placed its rows.
        if (_relink)
        {
            Relink();
            _relink = false;
        }

        UIFocus.EnsureSelection(transform, _top);
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

        _relink = true;
        _selectTopAfterRelink = true;
    }

    private void Relink()
    {
        // Current tab's rows top-to-bottom by screen height (not hierarchy
        // order), then Reset and Back, wrapping back to the top row.
        _navList.Clear();

        GameObject content = tabs.Length > 0 ? tabs[_current].content : null;
        if (content != null)
            foreach (Selectable selectable in content.GetComponentsInChildren<Selectable>())
                if (selectable.IsInteractable()) _navList.Add(selectable);
        _navList.Sort((a, b) => ScreenY(b).CompareTo(ScreenY(a)));

        if (resetButton != null && resetButton.isActiveAndEnabled) _navList.Add(resetButton);
        if (backButton != null && backButton.isActiveAndEnabled) _navList.Add(backButton);

        _top = UIFocus.LinkInOrder(_navList);

        if (_selectTopAfterRelink && _top != null && InputManager.UsingGamepad && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_top.gameObject);
        _selectTopAfterRelink = false;
    }

    private static float ScreenY(Selectable selectable)
    {
        RectTransform rect = selectable.transform as RectTransform;
        return rect != null ? rect.TransformPoint(rect.rect.center).y : selectable.transform.position.y;
    }

    private void ResetCurrentTab()
    {
        if (tabs.Length == 0) return;

        switch (tabs[_current].category)
        {
            case Category.Audio: SettingsService.ResetAudio(); break;
            case Category.Gameplay: SettingsService.ResetGameplay(); break;
        }
    }

    private void OnBackPressed(InputAction.CallbackContext ctx) => Close();
}
