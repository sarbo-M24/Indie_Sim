using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Controls screen: Keyboard &amp; Mouse and Gamepad tabs of RebindRow.
/// Tabs, Back, Reset and gamepad focus come from TabbedMenuPanel.
///
/// Rebinding, the usual way: select a binding, press the new key/button
/// (on the keyboard tab, scrolling the mouse wheel binds the wheel).
/// Esc (keyboard) or Start (gamepad) cancels. If another action on the same
/// tab already uses that key, the two swap. While listening, the UI map is
/// off so the key being bound can't also click, navigate or close the menu.
/// Overrides are stored in GameSettings.bindingOverridesJson (saved when
/// the panel closes) and loaded by InputManager at startup.
/// </summary>
public class ControlsPanel : TabbedMenuPanel
{
    [Tooltip("Optional. Shows the tab hint normally and the rebind prompt while listening.")]
    [SerializeField] private TMP_Text hintText;

    private const string MouseWheelPath = "<Mouse>/scroll/y";

    private InputActionRebindingExtensions.RebindingOperation _operation;
    private RebindRow _listeningRow;
    private string _wheelPath; // set when the wheel was scrolled while listening
    private int _rebindEndedFrame = -1;
    private string _defaultHint;

    // Busy while listening, and for a frame after, so the key that finished
    // the rebind (LB, B, Esc...) doesn't also switch tabs or close the panel.
    protected override bool IsBusy => _operation != null || Time.frameCount <= _rebindEndedFrame + 1;

    protected override void Awake()
    {
        base.Awake();
        if (hintText != null) _defaultHint = hintText.text;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        RefreshAll(); // pad type (Xbox / PlayStation names) may have changed since last time
    }

    protected override void OnDisable()
    {
        _operation?.Cancel(); // hidden or unloaded mid-rebind: restores the UI map
        base.OnDisable();
    }

    protected override void Update()
    {
        if (_operation != null)
        {
            // The rebind listener only takes buttons; the wheel is an axis, so
            // it's caught here and bound directly (keyboard & mouse tab only).
            if (!_listeningRow.IsGamepad && Mouse.current != null && Mouse.current.scroll.ReadValue().y != 0f)
            {
                _wheelPath = MouseWheelPath;
                _operation.Cancel();
            }
            // Either device's cancel works whichever tab is listening.
            else if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                     || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame))
            {
                _operation.Cancel();
            }
        }

        base.Update();
    }

    public void StartRebind(RebindRow row)
    {
        if (_operation != null || row == null || !row.Rebindable) return;

        InputAction action = row.Action;
        string oldPath = row.EffectivePath;
        bool gamepad = row.IsGamepad;

        // Rebinding needs the action disabled (the Player map already is while
        // a menu is open); the UI map goes off so the key can't drive the menu.
        bool wasEnabled = action.enabled;
        if (wasEnabled) action.Disable();
        InputManager.Controls.UI.Disable();

        _listeningRow = row;
        _wheelPath = null;
        row.ShowWaiting(gamepad ? "Press a button..." : "Press a key...");
        SetHint(gamepad ? "Press a button to bind  ·  START to cancel" : "Press a key, mouse button or scroll the wheel  ·  ESC to cancel");

        var operation = action.PerformInteractiveRebinding(row.BindingIndex)
            .WithExpectedControlType("Button")
            .WithCancelingThrough(gamepad ? "<Gamepad>/start" : "<Keyboard>/escape")
            .WithControlsExcluding("<Keyboard>/anyKey")
            .WithControlsExcluding("<Pointer>/press")
            .WithControlsExcluding("<Mouse>/scroll/up")
            .WithControlsExcluding("<Mouse>/scroll/down")
            .WithControlsExcluding("<Mouse>/scroll/left")
            .WithControlsExcluding("<Mouse>/scroll/right")
            .WithControlsExcluding("<Gamepad>/leftStick/up")
            .WithControlsExcluding("<Gamepad>/leftStick/down")
            .WithControlsExcluding("<Gamepad>/leftStick/left")
            .WithControlsExcluding("<Gamepad>/leftStick/right")
            .WithControlsExcluding("<Gamepad>/rightStick/up")
            .WithControlsExcluding("<Gamepad>/rightStick/down")
            .WithControlsExcluding("<Gamepad>/rightStick/left")
            .WithControlsExcluding("<Gamepad>/rightStick/right")
            .OnMatchWaitForAnother(0.1f)
            .OnCancel(_ => EndRebind(row, oldPath, BindWheelIfScrolled(row), wasEnabled))
            .OnComplete(_ => EndRebind(row, oldPath, true, wasEnabled));

        if (gamepad)
        {
            operation.WithControlsHavingToMatchPath("<Gamepad>");
        }
        else
        {
            operation.WithControlsHavingToMatchPath("<Keyboard>");
            operation.WithControlsHavingToMatchPath("<Mouse>");
        }

        _operation = operation.Start();
    }

    // A cancel caused by the wheel is really a completed rebind to the wheel.
    private bool BindWheelIfScrolled(RebindRow row)
    {
        if (_wheelPath == null) return false;
        row.Action.ApplyBindingOverride(row.BindingIndex, _wheelPath);
        return true;
    }

    private void EndRebind(RebindRow row, string oldPath, bool completed, bool reenableAction)
    {
        _operation?.Dispose();
        _operation = null;
        _listeningRow = null;
        _wheelPath = null;
        _rebindEndedFrame = Time.frameCount;

        if (completed)
        {
            // Key already used by another action on this tab: swap them.
            string newPath = row.EffectivePath;
            foreach (RebindRow other in GetRows())
                if (other != row && other.Rebindable && other.IsGamepad == row.IsGamepad
                    && string.Equals(other.EffectivePath, newPath, StringComparison.OrdinalIgnoreCase))
                    other.ApplyPath(oldPath);

            SaveOverrides();
        }

        InputManager.Controls.UI.Enable();
        if (reenableAction) row.Action.Enable();

        SetHint(_defaultHint);
        RefreshAll();
    }

    // Reset to defaults = every binding (and toggle, e.g. Swap Sticks) on the current tab.
    protected override void ResetTab(GameObject content)
    {
        if (content == null) return;

        foreach (RebindRow row in content.GetComponentsInChildren<RebindRow>(true))
            row.ResetToDefault();
        foreach (ToggleRow row in content.GetComponentsInChildren<ToggleRow>(true))
            row.ResetToDefault();

        SaveOverrides();
        RefreshAll();
    }

    private RebindRow[] GetRows() => GetComponentsInChildren<RebindRow>(true);

    private void RefreshAll()
    {
        foreach (RebindRow row in GetRows()) row.Refresh();
    }

    private static void SaveOverrides()
    {
        string json = InputManager.Controls.asset.SaveBindingOverridesAsJson();
        SettingsService.Change(s => s.bindingOverridesJson = json);
    }

    private void SetHint(string text)
    {
        if (hintText != null && text != null) hintText.text = text;
    }
}
