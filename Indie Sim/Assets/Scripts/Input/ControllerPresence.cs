using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

public enum ControllerFamily { None, Xbox, PlayStation }

/// <summary>
/// Single source of truth for "is a gamepad connected, and what kind", and
/// whether pad prompts should show (IsActive: a pad is connected AND the
/// player's last input came from it — a mouse nudge hides them even with a
/// pad plugged in). Lives under [Persistent] in Boot.unity; holds no run state.
///
/// The tracked pad is Gamepad.current: the Input System makes a pad current
/// when it's added and whenever it sends real (non-noise) input, and on
/// removal falls back to another connected pad. So with several pads it's
/// the most recently used one, else the most recently connected.
///
/// Family: DualShock 4 / DualSense (both DualShockGamepad) → PlayStation;
/// XInput and every other gamepad → Xbox (generic pads use Xbox glyphs).
/// Steam Input caveat: with Steam Input enabled, Steam can present a
/// PlayStation pad as a virtual XInput device, so it reads as Xbox here and
/// PS glyphs won't show in Steam builds. Not handled via Steamworks on purpose.
///
/// State and OnChanged are static so menus can subscribe regardless of load
/// order; before this component starts, the state reads None and the first
/// Refresh fires OnChanged. Runs in Update (unaffected by timeScale 0), where
/// a reference compare catches a switch between pads, which raises no
/// device-change event.
/// </summary>
public class ControllerPresence : MonoBehaviour
{
    private static ControllerPresence _instance;
    private static Gamepad _pad;

    public static ControllerFamily Family { get; private set; }
    public static bool IsConnected => Family != ControllerFamily.None;

    /// <summary>Show gamepad prompts: a pad is connected and the last input came from a pad (InputManager.UsingGamepad).</summary>
    public static bool IsActive => IsConnected && InputManager.UsingGamepad;

    /// <summary>Raised on connect, disconnect, family change (a different pad became current), or a switch between pad and mouse/keyboard input.</summary>
    public static event Action<ControllerFamily> OnChanged;

    // Statics survive play-mode restarts when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _pad = null;
        Family = ControllerFamily.None;
        OnChanged = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
    }

    private void OnEnable()
    {
        if (_instance != this) return;
        InputSystem.onDeviceChange += OnDeviceChange;
        InputManager.OnUsingGamepadChanged += OnInputMethodChanged;
        Refresh();
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        InputManager.OnUsingGamepadChanged -= OnInputMethodChanged;
    }

    private static void OnInputMethodChanged(bool usingGamepad) => OnChanged?.Invoke(Family);

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (Gamepad.current != _pad) Refresh();
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is Gamepad) Refresh();
    }

    private static void Refresh()
    {
        _pad = Gamepad.current;

        ControllerFamily family;
        if (_pad == null) family = ControllerFamily.None;
        else if (_pad is DualShockGamepad) family = ControllerFamily.PlayStation;
        else family = ControllerFamily.Xbox;

        if (family == Family) return;

        Family = family;
        OnChanged?.Invoke(family);
    }
}
