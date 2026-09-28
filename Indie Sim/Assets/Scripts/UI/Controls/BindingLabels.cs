using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

/// <summary>
/// Human-readable text for a binding, as shown on the Controls screen.
/// Keyboard and mouse use the Input System's own short names ("W", "LMB",
/// "Space"), except the wheel, which reads "Mouse Wheel". Gamepad controls
/// use the face-button names players expect, Xbox style by default and
/// PlayStation style when a DualShock/DualSense is the current pad.
/// </summary>
public static class BindingLabels
{
    public static string For(InputAction action, int bindingIndex)
    {
        string path = action.bindings[bindingIndex].effectivePath;
        if (string.IsNullOrEmpty(path)) return "—";

        if (path.StartsWith("<Mouse>/scroll")) return "Mouse Wheel";

        int slash = path.IndexOf('/');
        bool isGamepad = slash > 0 && path.StartsWith("<Gamepad>");
        if (!isGamepad) return action.GetBindingDisplayString(bindingIndex);

        bool playStation = Gamepad.current is DualShockGamepad;
        string control = path.Substring(slash + 1);
        switch (control)
        {
            case "buttonSouth": return playStation ? "Cross" : "A";
            case "buttonEast": return playStation ? "Circle" : "B";
            case "buttonWest": return playStation ? "Square" : "X";
            case "buttonNorth": return playStation ? "Triangle" : "Y";
            case "leftShoulder": return playStation ? "L1" : "LB";
            case "rightShoulder": return playStation ? "R1" : "RB";
            case "leftTrigger": return playStation ? "L2" : "LT";
            case "rightTrigger": return playStation ? "R2" : "RT";
            case "leftStickPress": return playStation ? "L3" : "LS";
            case "rightStickPress": return playStation ? "R3" : "RS";
            case "start": return playStation ? "Options" : "Menu";
            case "select": return playStation ? "Share" : "View";
            case "dpad": return "D-Pad";
            case "dpad/up": return "D-Pad Up";
            case "dpad/down": return "D-Pad Down";
            case "dpad/left": return "D-Pad Left";
            case "dpad/right": return "D-Pad Right";
            default: return action.GetBindingDisplayString(bindingIndex);
        }
    }
}
