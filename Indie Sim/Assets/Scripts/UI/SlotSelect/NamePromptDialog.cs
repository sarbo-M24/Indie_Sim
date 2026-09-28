using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Asks for a new slot's name. Pre-filled with a default ("Slot 2") so a
/// gamepad player with no keyboard can just confirm; an emptied field also
/// falls back to it. Enter in the field confirms.
/// </summary>
public class NamePromptDialog : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action<string> _onConfirm;
    private string _defaultName;

    public bool IsOpen => gameObject.activeSelf;
    public Selectable DefaultSelection => confirmButton;

    private void Awake()
    {
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Close);
        nameInput.onSubmit.AddListener(_ => Confirm());
    }

    public void Open(string defaultName, Action<string> onConfirm)
    {
        _defaultName = defaultName;
        _onConfirm = onConfirm;
        nameInput.text = defaultName;
        gameObject.SetActive(true);

        // Keyboard players can type straight away; pad players land on Confirm.
        if (!InputManager.UsingGamepad)
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
