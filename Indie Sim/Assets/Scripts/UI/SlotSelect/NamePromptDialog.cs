using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Asks for a new slot's name. Pre-filled with a default ("Slot 2"); an
/// emptied field also falls back to it. Enter in the field confirms.
///
/// Gamepad players get the on-screen VirtualKeyboard instead of the typed
/// field (its first key replaces the default, Done / Start confirms). It
/// follows the active device while the prompt is open: a pad press shows it,
/// mouse/keyboard input hides it and hands the field back.
/// </summary>
public class NamePromptDialog : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [Tooltip("Gamepad-only on-screen keyboard. Built by Tools/Save/Build Virtual Keyboard.")]
    [SerializeField] private VirtualKeyboard keyboard;

    private Action<string> _onConfirm;
    private string _defaultName;

    public bool IsOpen => gameObject.activeSelf;
    public Selectable DefaultSelection => KeyboardShown ? keyboard.DefaultSelection : confirmButton;

    private bool KeyboardShown => keyboard != null && keyboard.gameObject.activeSelf;

    private void Awake()
    {
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Close);
        nameInput.onSubmit.AddListener(_ => Confirm());
        if (keyboard != null) keyboard.Submitted += Confirm;
    }

    public void Open(string defaultName, Action<string> onConfirm)
    {
        _defaultName = defaultName;
        _onConfirm = onConfirm;
        nameInput.text = defaultName;
        gameObject.SetActive(true);

        if (keyboard != null) keyboard.Begin(replaceExisting: true);
        ShowKeyboard(InputManager.UsingGamepad);
    }

    private void Update()
    {
        if (keyboard != null && KeyboardShown != InputManager.UsingGamepad)
            ShowKeyboard(InputManager.UsingGamepad);
    }

    // Pad: keyboard up, field not in edit mode, focus on the first key.
    // Mouse/keyboard: keyboard hidden, field focused so they can type straight away.
    private void ShowKeyboard(bool show)
    {
        if (keyboard != null) keyboard.gameObject.SetActive(show);
        EventSystem eventSystem = EventSystem.current;

        if (show && keyboard != null)
        {
            nameInput.DeactivateInputField();
            if (eventSystem != null) eventSystem.SetSelectedGameObject(keyboard.DefaultSelection.gameObject);
        }
        else if (!InputManager.UsingGamepad)
        {
            nameInput.Select();
            nameInput.ActivateInputField();
        }
    }

    public void Close()
    {
        _onConfirm = null;
        gameObject.SetActive(false);
    }

    private void Confirm()
    {
        if (!IsOpen) return;

        string name = nameInput.text.Trim();
        if (name.Length == 0) name = _defaultName;

        Action<string> onConfirm = _onConfirm;
        Close();
        onConfirm?.Invoke(name);
    }
}
