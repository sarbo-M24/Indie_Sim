using TMPro;
using UnityEngine;

/// <summary>
/// Shows RoguelikeManager's dungeon countdown as m:ss. Hidden in scenes with
/// no RoguelikeManager (boss arena, tutorial). Text is only rewritten when the
/// shown second changes, so the timer's nested canvas rebuilds once a second.
/// </summary>
public class HUDTimer : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("Optional drop-shadow copy of the label.")]
    [SerializeField] private TMP_Text shadowLabel;
    [SerializeField] private Color warningColor = Color.red;
    [SerializeField] private float warningThreshold = 15f;

    private Color _normalColor = Color.white;
    private int _shownSeconds = -1;
    private bool _shown = true;

    private void Awake()
    {
        if (label != null) _normalColor = label.color;
    }

    private void Update()
    {
        RoguelikeManager manager = RoguelikeManager.Instance;
        SetShown(manager != null);
        if (manager == null) return;

        float remaining = Mathf.Max(0f, manager.GetDungeonTimeRemaining());
        int seconds = Mathf.FloorToInt(remaining);
        if (seconds == _shownSeconds) return;
        _shownSeconds = seconds;

        string text = $"{seconds / 60}:{seconds % 60:00}";
        if (label != null)
        {
            label.text = text;
            label.color = remaining <= warningThreshold ? warningColor : _normalColor;
        }
        if (shadowLabel != null) shadowLabel.text = text;
    }

    private void SetShown(bool shown)
    {
        if (_shown == shown) return;
        _shown = shown;
        if (label != null) label.enabled = shown;
        if (shadowLabel != null) shadowLabel.enabled = shown;
    }
}
