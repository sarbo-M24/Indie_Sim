using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// One row of the Controls screen: an action name and a button showing its
/// current binding for one control scheme. Pressing the button asks the
/// ControlsPanel to listen for a new key/button.
///
/// The binding is picked by action ("Player/Fire"), control scheme group
/// ("Keyboard&amp;Mouse" / "Gamepad") and, for composites, the part name
/// ("up", "negative"...). Reads the shared InputManager.Controls, so a
/// rebind here applies to every script at once.
/// </summary>
public class RebindRow : MonoBehaviour
{
    public const string KeyboardMouseGroup = "Keyboard&Mouse";
    public const string GamepadGroup = "Gamepad";

    [Tooltip("Map/Action, e.g. Player/Fire or UI/Pause.")]
    [SerializeField] private string actionName = "Player/Fire";
    [Tooltip("Control scheme group of the binding: Keyboard&Mouse or Gamepad.")]
    [SerializeField] private string bindingGroup = KeyboardMouseGroup;
    [Tooltip("For composite bindings only: the part (up/down/left/right, negative/positive). Empty otherwise.")]
    [SerializeField] private string compositePart = "";
    [SerializeField] private Button bindingButton;
    [SerializeField] private TMP_Text bindingText;

    private ControlsPanel _panel;

    public InputAction Action { get; private set; }
    public int BindingIndex { get; private set; } = -1;
    public bool Rebindable => BindingIndex >= 0;
    public bool IsGamepad => bindingGroup == GamepadGroup;
    public string EffectivePath => BindingIndex >= 0 ? Action.bindings[BindingIndex].effectivePath : null;

    private void Awake()
    {
        _panel = GetComponentInParent<ControlsPanel>(true);

        Action = InputManager.Controls.asset.FindAction(actionName);
        BindingIndex = Action != null ? FindBindingIndex(Action) : -1;
        if (BindingIndex < 0)
            Debug.LogWarning($"[RebindRow] '{name}': no {bindingGroup} binding for '{actionName}'{(compositePart != "" ? " part " + compositePart : "")}.", this);

        if (bindingButton != null)
        {
            bindingButton.interactable = Rebindable;
            bindingButton.onClick.AddListener(() => { if (_panel != null) _panel.StartRebind(this); });
        }
    }

    private void OnEnable() => Refresh();

    private int FindBindingIndex(InputAction action)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite || !InGroup(binding)) continue;

            bool wantPart = !string.IsNullOrEmpty(compositePart);
            if (wantPart && binding.isPartOfComposite && string.Equals(binding.name, compositePart, System.StringComparison.OrdinalIgnoreCase)) return i;
            if (!wantPart && !binding.isPartOfComposite) return i;
        }
        return -1;
    }

    private bool InGroup(InputBinding binding)
    {
        if (string.IsNullOrEmpty(binding.groups)) return false;
        foreach (string group in binding.groups.Split(InputBinding.Separator))
            if (group == bindingGroup) return true;
        return false;
    }

    public void Refresh()
    {
        if (bindingText != null) bindingText.text = BindingIndex >= 0 ? BindingLabels.For(Action, BindingIndex) : "—";
    }

    public void ShowWaiting(string prompt)
    {
        if (bindingText != null) bindingText.text = prompt;
    }

    public void ResetToDefault()
    {
        if (Rebindable) Action.RemoveBindingOverride(BindingIndex);
    }

    /// <summary>Points this binding at `path` (used to swap with a row whose key was just taken).</summary>
    public void ApplyPath(string path)
    {
        if (Rebindable) Action.ApplyBindingOverride(BindingIndex, path);
    }
}
