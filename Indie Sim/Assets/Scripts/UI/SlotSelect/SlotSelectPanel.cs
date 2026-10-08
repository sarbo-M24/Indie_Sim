using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Main-menu slot select (save-system-spec.md §1, 4C). Three tiles drawn from
/// slot headers only; picking one:
///   Empty      → name prompt, then a new run in that slot
///   Named, no run → new run in that slot
///   Active run → resume it
///   Corrupted  → not selectable; Delete is the only option
/// Delete asks for confirmation. There's no overwrite path — to start over,
/// the player deletes the slot.
///
/// Opened by MainMenu with Open(onClosed); Back or B/Esc closes it (or the
/// open dialog first). Gamepad: Left/Right between tiles, Down to Delete and
/// Back; X / Square deletes the focused slot (with the same confirm).
/// </summary>
public class SlotSelectPanel : MonoBehaviour
{
    [SerializeField] private SlotTileView[] tiles;
    [SerializeField] private Button backButton;
    [SerializeField] private NamePromptDialog namePrompt;
    [SerializeField] private ConfirmDialog confirmDialog;

    private Action _onClosed;
    private int _focusIndex;
    private bool _starting; // a run is loading — ignore further picks

    private void Awake()
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            int index = i;
            tiles[i].Bind(() => OnTilePicked(index), () => OnDeletePicked(index));
        }
        backButton.onClick.AddListener(Close);

        foreach (Button button in GetComponentsInChildren<Button>(includeInactive: true))
            if (!button.TryGetComponent(out ButtonFocusScale _))
                button.gameObject.AddComponent<ButtonFocusScale>();
    }

    private void OnEnable() => InputManager.Controls.UI.Cancel.performed += OnCancelPressed;
    private void OnDisable() => InputManager.Controls.UI.Cancel.performed -= OnCancelPressed;

    public void Open(Action onClosed)
    {
        _onClosed = onClosed;
        _starting = false;
        _focusIndex = 0;
        gameObject.SetActive(true);
        namePrompt.Close();
        confirmDialog.Close();
        Refresh();
    }

    public void Close()
    {
        if (_starting) return;
        gameObject.SetActive(false);
        Action onClosed = _onClosed;
        _onClosed = null;
        onClosed?.Invoke();
    }

    private void Update()
    {
        if (namePrompt.IsOpen) UIFocus.EnsureSelection(namePrompt.transform, namePrompt.DefaultSelection);
        else if (confirmDialog.IsOpen) UIFocus.EnsureSelection(confirmDialog.transform, confirmDialog.DefaultSelection);
        else
        {
            UIFocus.EnsureSelection(transform, tiles[_focusIndex].SelectButton.interactable ? tiles[_focusIndex].SelectButton : null);

            // X / Square (UI/DeleteSlot): Delete on the focused tile, same confirm as its button.
            if (InputManager.Controls.UI.DeleteSlot.WasPressedThisFrame())
            {
                int focused = FocusedTile();
                if (focused >= 0 && tiles[focused].DeleteButton.gameObject.activeSelf) OnDeletePicked(focused);
            }
        }
    }

    /// <summary>The tile whose card or Delete button has gamepad focus, or -1.</summary>
    private int FocusedTile()
    {
        GameObject selected = UnityEngine.EventSystems.EventSystem.current != null
            ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
        if (selected == null) return -1;

        for (int i = 0; i < tiles.Length; i++)
            if (selected == tiles[i].SelectButton.gameObject || selected == tiles[i].DeleteButton.gameObject) return i;
        return -1;
    }

    private void Refresh()
    {
        SaveService saves = GameSession.Instance.Saves;
        for (int i = 0; i < tiles.Length; i++)
            tiles[i].Show(saves.GetSlotInfo(i));
        LinkNavigation();
    }

    private void OnTilePicked(int index)
    {
        if (_starting || namePrompt.IsOpen || confirmDialog.IsOpen) return;
        _focusIndex = index;

        switch (tiles[index].State)
        {
            case SlotState.Empty:
                namePrompt.Open($"Slot {index + 1}", name => StartNewRun(index, name));
                break;
            case SlotState.NoRun:
                StartNewRun(index, null);
                break;
            case SlotState.ActiveRun:
                _starting = true;
                GameManager.Instance.ContinueRunInSlot(index);
                break;
        }
    }

    private void StartNewRun(int index, string newSlotName)
    {
        _starting = true;
        GameManager.Instance.StartNewRunInSlot(index, newSlotName);
    }

    private void OnDeletePicked(int index)
    {
        if (_starting || namePrompt.IsOpen || confirmDialog.IsOpen) return;
        _focusIndex = index;

        confirmDialog.Open($"Delete \"{tiles[index].DisplayName}\"?\nThis can't be undone.", () =>
        {
            GameSession.Instance.Saves.DeleteSlot(index);
            Refresh();
        });
    }

    private void OnCancelPressed(InputAction.CallbackContext ctx)
    {
        if (namePrompt.IsOpen) namePrompt.Close();
        else if (confirmDialog.IsOpen) confirmDialog.Answer(false);
        else Close();
    }

    // Left/Right across the tiles, Down to that tile's Delete (if shown) then
    // Back, Up from Back to the first tile. Rebuilt after every Refresh since
    // Delete buttons come and go.
    private void LinkNavigation()
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            Button select = tiles[i].SelectButton;
            Button delete = tiles[i].DeleteButton;
            bool hasDelete = delete.gameObject.activeSelf;
            Selectable left = tiles[(i - 1 + tiles.Length) % tiles.Length].SelectButton;
            Selectable right = tiles[(i + 1) % tiles.Length].SelectButton;

            select.navigation = Explicit(up: backButton, down: hasDelete ? delete : backButton, left: left, right: right);
            if (hasDelete)
                delete.navigation = Explicit(up: select, down: backButton,
                    left: tiles[(i - 1 + tiles.Length) % tiles.Length].DeleteButton,
                    right: tiles[(i + 1) % tiles.Length].DeleteButton);
        }

        backButton.navigation = Explicit(up: tiles[0].SelectButton, down: tiles[0].SelectButton, left: null, right: null);
    }

    private static Navigation Explicit(Selectable up, Selectable down, Selectable left, Selectable right) => new Navigation
    {
        mode = Navigation.Mode.Explicit,
        selectOnUp = up,
        selectOnDown = down,
        selectOnLeft = left,
        selectOnRight = right
    };
}
