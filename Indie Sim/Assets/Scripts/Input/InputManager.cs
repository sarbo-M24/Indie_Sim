using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the single PlayerControls instance every script reads from, so a
/// rebind (SettingsAudioInputPlan.md, Controls tab) applies everywhere at once
/// — separate `new PlayerControls()` instances would each keep their own
/// bindings. Static and created before the first scene loads, so it needs no
/// object in Boot.unity.
///
/// Maps: UI is always on (pointer, navigation, Pause — Pause lives here so it
/// can also resume while the Player map is off). Player is on unless blocked;
/// the shop and pause menu block it so a menu press can't also fire, dash or
/// stomp. Blocks are per-source, so pausing inside the shop and resuming
/// doesn't hand control back while the shop is still open.
///
/// Also points every scene's InputSystemUIInputModule at this instance's UI
/// map (it ships pointing at Unity's DefaultInputActions), so UI bindings are
/// rebindable too.
/// </summary>
public static class InputManager
{
    private static PlayerControls controls;
    private static readonly HashSet<object> playerBlockers = new HashSet<object>();
    private static Vector2 lastStickAim = Vector2.up;

    public static PlayerControls Controls
    {
        get
        {
            if (controls == null) Create();
            return controls;
        }
    }

    /// <summary>True when the last meaningful input came from a gamepad; flips back on any mouse/keyboard input.</summary>
    public static bool UsingGamepad { get; private set; }

    /// <summary>Screen-space pointer position, via the UI map's Point action.</summary>
    public static Vector2 PointerPosition => Controls.UI.Point.ReadValue<Vector2>();

    // Statics survive play-mode restarts when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        controls = null;
        playerBlockers.Clear();
        lastStickAim = Vector2.up;
        UsingGamepad = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (controls != null) return;

        controls = new PlayerControls();
        controls.UI.Enable();
        controls.Player.Enable();

        InputSystem.onActionChange += OnActionChange;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting += Shutdown;
    }

    private static void Shutdown()
    {
        InputSystem.onActionChange -= OnActionChange;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Application.quitting -= Shutdown;

        controls?.Disable();
        controls?.Dispose();
        controls = null;
    }

    /// <summary>Turns the Player map off while any source holds a block (shop, pause menu).</summary>
    public static void SetPlayerBlocked(object source, bool blocked)
    {
        if (blocked) playerBlockers.Add(source);
        else playerBlockers.Remove(source);

        if (playerBlockers.Count > 0) Controls.Player.Disable();
        else Controls.Player.Enable();
    }

    /// <summary>
    /// Returns true when the gamepad owns aiming. direction is the right
    /// stick's last non-zero direction, so aim holds after the stick is released.
    /// </summary>
    public static bool TryGetGamepadAim(out Vector2 direction)
    {
        Vector2 stick = Controls.Player.Aim.ReadValue<Vector2>();
        if (stick.sqrMagnitude > 0f) lastStickAim = stick.normalized;

        direction = lastStickAim;
        return UsingGamepad;
    }

    private static void OnActionChange(object obj, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed) return;
        if (obj is not InputAction action || action.activeControl == null) return;

        UsingGamepad = action.activeControl.device is Gamepad;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (InputSystemUIInputModule module in Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None))
        {
            // The setter re-resolves each UI action by map/name ("UI/Point", ...) in the new asset.
            if (module.actionsAsset != Controls.asset)
                module.actionsAsset = Controls.asset;
        }
    }
}
