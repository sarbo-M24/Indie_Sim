using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One save slot on the slot-select screen. Renders a SlotInfo (header only —
/// nothing is restored to draw a tile) in one of the four states of
/// save-system-spec.md §3. The whole card is the select button; Delete is a
/// separate button, hidden on empty slots.
/// </summary>
public class SlotTileView : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text lastPlayedText;
    [SerializeField] private TMP_Text actionText;

    public SlotState State { get; private set; }
    public string DisplayName { get; private set; }
    public Button SelectButton => selectButton;
    public Button DeleteButton => deleteButton;

    public void Bind(Action onSelect, Action onDelete)
    {
        selectButton.onClick.AddListener(() => onSelect());
        deleteButton.onClick.AddListener(() => onDelete());
    }

    public void Show(SlotInfo info)
    {
        State = info.State;
        string fallbackName = $"Slot {info.Index + 1}";
        SlotSummary summary = info.Header?.Summary;
        DisplayName = summary != null && !string.IsNullOrWhiteSpace(summary.SlotName) ? summary.SlotName : fallbackName;
        string lastPlayed = info.Header != null ? $"Last played {info.Header.LastWrittenUtc.ToLocalTime():dd MMM yyyy, HH:mm}" : "";

        switch (info.State)
        {
            case SlotState.Empty:
                Set(fallbackName, "Empty", "", "New Game");
                break;
            case SlotState.NoRun:
                Set(DisplayName, "No active run", lastPlayed, "New Run");
                break;
            case SlotState.ActiveRun:
                Set(DisplayName, $"Dungeon {summary.DungeonNumber}\n{summary.Coins} coins", lastPlayed, "Continue");
                break;
            case SlotState.Corrupted:
                Set(fallbackName, "Save data can't be read", "", "Delete to reuse");
                break;
        }

        selectButton.interactable = info.State != SlotState.Corrupted;
        deleteButton.gameObject.SetActive(info.State != SlotState.Empty);
    }

    private void Set(string title, string detail, string lastPlayed, string action)
    {
        titleText.text = title;
        detailText.text = detail;
        lastPlayedText.text = lastPlayed;
        actionText.text = action;
    }
}
