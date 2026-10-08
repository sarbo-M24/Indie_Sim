using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Expands {Action} tokens in tutorial hints into the player's current binding
/// for the device they're using: "{Dash} to dash" → "[Space] to dash" on
/// keyboard, "[A] to dash" on an Xbox pad. Follows rebinds from the Controls tab.
/// </summary>
public static class TutorialText
{
    private const string KeyboardGroup = "Keyboard&Mouse";
    private const string GamepadGroup = "Gamepad";

    public static string Format(string text, Color keyColor)
    {
        if (string.IsNullOrEmpty(text)) return "";

        string hex = ColorUtility.ToHtmlStringRGB(keyColor);
        PlayerControls controls = InputManager.Controls;

        text = Replace(text, "{Move}", controls.Player.Move, hex);
        text = Replace(text, "{Fire}", controls.Player.Fire, hex);
        text = Replace(text, "{Dash}", controls.Player.Dash, hex);
        text = Replace(text, "{Stomp}", controls.Player.Stomp, hex);
        text = Replace(text, "{Reload}", controls.Player.Reload, hex);
        text = Replace(text, "{SwitchWeapon}", controls.Player.SwitchWeapon, hex);
        text = Replace(text, "{Pause}", controls.UI.Pause, hex);
        return text;
    }

    private static string Replace(string text, string token, InputAction action, string hex)
    {
        if (!text.Contains(token)) return text;
        return text.Replace(token, $"<color=#{hex}>[{Label(action)}]</color>");
    }

    /// <summary>The first binding of action in the current device's group, e.g. "W/A/S/D", "Space", "A".</summary>
    public static string Label(InputAction action)
    {
        string group = InputManager.UsingGamepad ? GamepadGroup : KeyboardGroup;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isPartOfComposite) continue;

            if (binding.isComposite)
            {
                // A composite carries no group itself — its parts do.
                if (i + 1 < action.bindings.Count && InGroup(action.bindings[i + 1], group))
                    return action.GetBindingDisplayString(i);
                continue;
            }

            if (InGroup(binding, group)) return BindingLabels.For(action, i);
        }

        return action.GetBindingDisplayString();
    }

    private static bool InGroup(InputBinding binding, string group) =>
        binding.groups != null && binding.groups.Contains(group);
}
