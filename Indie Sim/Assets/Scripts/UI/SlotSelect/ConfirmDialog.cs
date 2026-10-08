using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Yes/No modal (slot delete, pause-menu quit warning). Gamepad focus starts
/// on No, so a stray A press never confirms something destructive; Left/Right
/// is the only navigation, so focus can't wander behind the dialog. The owner
/// routes B/Esc to Answer(false).
/// </summary>
public class ConfirmDialog : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private Action _onYes;
    private TMP_Text _yesLabel;
    private string _defaultYesText;

    public bool IsOpen => gameObject.activeSelf;
    public Selectable DefaultSelection => noButton;

    /// <summary>Frame the dialog last closed — lets an owner ignore the same key press twice.</summary>
    public int ClosedFrame { get; private set; } = -1;

    private void Awake()
    {
        yesButton.onClick.AddListener(() => Answer(true));
        noButton.onClick.AddListener(() => Answer(false));

        yesButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = noButton, selectOnLeft = noButton };
        noButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = yesButton, selectOnRight = yesButton };

        foreach (Button button in new[] { yesButton, noButton })
            if (!button.TryGetComponent(out ButtonFocusScale _))
                button.gameObject.AddComponent<ButtonFocusScale>();
    }

    /// <summary>yesText relabels Yes for this opening only (one dialog can serve several questions).</summary>
    public void Open(string message, Action onYes, string yesText = null)
    {
        messageText.text = message;
        // Looked up here, not in Awake: the dialog starts hidden, so Awake
        // hasn't run yet the first time it's opened.
        if (_yesLabel == null)
        {
            _yesLabel = yesButton.GetComponentInChildren<TMP_Text>(true);
            if (_yesLabel != null) _defaultYesText = _yesLabel.text;
        }
        if (_yesLabel != null) _yesLabel.text = yesText ?? _defaultYesText;
        _onYes = onYes;
        gameObject.SetActive(true);

        if (InputManager.UsingGamepad && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(noButton.gameObject);
    }

    public void Answer(bool yes)
    {
        Action onYes = _onYes;
        Close();
        if (yes) onYes?.Invoke();
    }

    public void Close()
    {
        if (gameObject.activeSelf) ClosedFrame = Time.frameCount;
        _onYes = null;
        gameObject.SetActive(false);
    }
}
