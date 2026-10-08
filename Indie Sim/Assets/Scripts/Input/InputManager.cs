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
    private static bool? appliedStickSwap;

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

    /// <summary>Raised when UsingGamepad flips (the player switched between pad and mouse/keyboard).</summary>
    public static event System.Action<bool> OnUsingGamepadChanged;

    /// <summary>Screen-space pointer position, via the UI map's Point action.</summary>
    public static Vector2 PointerPosition => Controls.UI.Point.ReadValue<Vector2>();

    // Statics survive play-mode restarts when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        controls = null;
        playerBlockers.Clear();
        lastStickAim = Vector2.up;
        appliedStickSwap = null;
        UsingGamepad = false;
        OnUsingGamepadChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (controls != null) return;

        controls = new PlayerControls();

        // Rebinds from the Controls tab (saved by SettingsService).
        // An override for an action that no longer exists (e.g. the removed
        // SwitchWeaponScroll) must not stop the game from starting.
        string overrides = SettingsService.Current.bindingOverridesJson;
        if (!string.IsNullOrEmpty(overrides))
        {
            try { controls.asset.LoadBindingOverridesFromJson(overrides); }
            catch (System.Exception e) { Debug.LogWarning($"[InputManager] Couldn't load saved key bindings, using defaults: {e.Message}"); }
        }

        // After the saved overrides, so the Swap Sticks setting always wins
        // over any stick override that ended up in bindingOverridesJson.
        ApplyStickSwap(SettingsService.Current);
        SettingsService.OnChanged += ApplyStickSwap;

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
        SettingsService.OnChanged -= ApplyStickSwap;
        Application.quitting -= Shutdown;

        controls?.Disable();
        controls?.Dispose();
        controls = null;
    }

    // Swap Sticks (Controls ▸ Gamepad): Move reads the right stick and Aim the
    // left. Done as overrides on the two gamepad stick bindings, so every
    // script reading Move / Aim follows; menus keep navigating on the left stick.
    private static void ApplyStickSwap(GameSettings settings)
    {
        if (appliedStickSwap == settings.swapSticks) return;
        appliedStickSwap = settings.swapSticks;

        SetGamepadBinding(controls.Player.Move, settings.swapSticks ? "<Gamepad>/rightStick" : null);
        SetGamepadBinding(controls.Player.Aim, settings.swapSticks ? "<Gamepad>/leftStick" : null);
    }

    // path null = back to the asset's default binding.
    private static void SetGamepadBinding(InputAction action, string path)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite || binding.groups == null || !binding.groups.Contains("Gamepad")) continue;

            if (path == null) action.RemoveBindingOverride(i);
            else action.ApplyBindingOverride(i, path);
        }
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

        bool usingGamepad = action.activeControl.device is Gamepad;
        if (usingGamepad == UsingGamepad) return;

        UsingGamepad = usingGamepad;
        OnUsingGamepadChanged?.Invoke(usingGamepad);
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
