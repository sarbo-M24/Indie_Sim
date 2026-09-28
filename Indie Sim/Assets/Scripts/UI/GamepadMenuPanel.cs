using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Makes a simple button-list panel (pause menu, death screen) usable with a
/// gamepad and a visible cursor. While the panel is active it:
///   - blocks the Player map, so the stick stops aiming and the crosshair hides;
///   - shows the hardware cursor for mouse users (CursorController override);
///   - links its buttons top-to-bottom for the D-pad (UIFocus.LinkVertical)
///     and, on gamepad, keeps focus on them starting from the top button;
///   - gives each button the hover/focus grow (ButtonFocusScale).
/// Everything is released in OnDisable, so hiding the panel — or the scene
/// unloading with it open — hands control back cleanly. Added at runtime by
/// the panel's owner (OptionsMenu, PlayerHealth, MainMenu...), so no scene
/// edit is needed.
///
/// When panels stack (the Achievements panel over the main menu), only the
/// most recently opened one holds focus, so they don't fight over it.
/// </summary>
public class GamepadMenuPanel : MonoBehaviour
{
    private static readonly List<GamepadMenuPanel> openPanels = new List<GamepadMenuPanel>();

    private Selectable _topButton;
    private bool _linked;

    private bool IsTopmost => openPanels.Count > 0 && openPanels[openPanels.Count - 1] == this;

    // Statics survive play-mode restarts when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => openPanels.Clear();

    private void OnEnable()
    {
        _linked = false;
        openPanels.Remove(this);
        openPanels.Add(this);

        foreach (Button button in GetComponentsInChildren<Button>())
            if (!button.TryGetComponent(out ButtonFocusScale _))
                button.gameObject.AddComponent<ButtonFocusScale>();

        InputManager.SetPlayerBlocked(this, true);
        if (CursorController.Instance != null) CursorController.Instance.SetCursorOverride(this, true);
    }

    private void Update()
    {
        // Linked on the first frame shown rather than in OnEnable, so any
        // layout groups have positioned the buttons first.
        if (!_linked)
        {
            _topButton = UIFocus.LinkVertical(transform);
            _linked = true;

            // A panel shown before any pad input (e.g. the countdown at scene
            // start) wouldn't count as gamepad use yet — with a pad plugged
            // in, select the top button up front so the first A press works.
            if (IsTopmost && _topButton != null && Gamepad.current != null && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == null)
                EventSystem.current.SetSelectedGameObject(_topButton.gameObject);
        }

        if (IsTopmost) UIFocus.EnsureSelection(transform, _topButton);
    }

    private void OnDisable()
    {
        openPanels.Remove(this);
        InputManager.SetPlayerBlocked(this, false);
        if (CursorController.Instance != null) CursorController.Instance.SetCursorOverride(this, false);

        // Don't leave a hidden button selected.
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected != null && selected.transform.IsChildOf(transform))
            eventSystem.SetSelectedGameObject(null);
    }
}
