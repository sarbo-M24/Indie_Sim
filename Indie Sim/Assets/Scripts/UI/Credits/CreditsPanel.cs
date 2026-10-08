using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Main-menu Credits screen: the sections of a CreditsData asset as one
/// scrolling text, rolling up on its own. Scrolling by hand (mouse wheel,
/// stick / D-pad) holds the roll for a moment. Built by
/// Tools/UI/Build Credits Panel.
///
/// Opened by MainMenu with Open(onClosed); Back or B/Esc closes it.
/// </summary>
public class CreditsPanel : MonoBehaviour
{
    private const float ManualHoldSeconds = 3f;
    private const float StartDelaySeconds = 1.5f;

    [SerializeField] private CreditsData data;
    [SerializeField] private TMP_Text body;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Button backButton;

    [Tooltip("Auto-roll speed, in canvas pixels per second.")]
    [SerializeField, Min(0f)] private float rollSpeed = 60f;
    [Tooltip("Manual scroll speed (stick / D-pad), in canvas pixels per second.")]
    [SerializeField, Min(0f)] private float manualSpeed = 900f;
    [SerializeField] private Color headingColor = new Color(0.95f, 0.8f, 0.3f, 1f);

    private Action _onClosed;
    private float _rollResumesAt;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        backButton.onClick.AddListener(Close);
        if (!backButton.TryGetComponent(out ButtonFocusScale _))
            backButton.gameObject.AddComponent<ButtonFocusScale>();
    }

    private void OnEnable() => InputManager.Controls.UI.Cancel.performed += OnCancelPressed;
    private void OnDisable() => InputManager.Controls.UI.Cancel.performed -= OnCancelPressed;

    public void Open(Action onClosed)
    {
        _onClosed = onClosed;
        gameObject.SetActive(true);

        body.text = BuildText();
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
        _rollResumesAt = Time.unscaledTime + StartDelaySeconds;
    }

    public void Close()
    {
        gameObject.SetActive(false);
        Action onClosed = _onClosed;
        _onClosed = null;
        onClosed?.Invoke();
    }

    private void Update()
    {
        UIFocus.EnsureSelection(transform, backButton);

        float dt = Time.unscaledDeltaTime;
        float manual = InputManager.Controls.UI.Navigate.ReadValue<Vector2>().y;
        Mouse mouse = Mouse.current;
        bool wheel = mouse != null && mouse.scroll.ReadValue().y != 0f; // ScrollRect handles the wheel itself

        if (Mathf.Abs(manual) > 0.2f)
            ScrollBy(-manual * manualSpeed * dt);
        bool dragging = mouse != null && mouse.leftButton.isPressed;

        if (Mathf.Abs(manual) > 0.2f || wheel || dragging)
            _rollResumesAt = Time.unscaledTime + ManualHoldSeconds;

        if (Time.unscaledTime >= _rollResumesAt)
            ScrollBy(rollSpeed * dt);
    }

    // Moves the content up by `pixels` (negative = back down), clamped to its ends.
    private void ScrollBy(float pixels)
    {
        RectTransform content = scroll.content;
        RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        float max = Mathf.Max(0f, content.rect.height - viewport.rect.height);

        Vector2 position = content.anchoredPosition;
        position.y = Mathf.Clamp(position.y + pixels, 0f, max);
        content.anchoredPosition = position;
    }

    private string BuildText()
    {
        if (data == null || data.sections == null) return "";

        string hex = ColorUtility.ToHtmlStringRGB(headingColor);
        StringBuilder text = new StringBuilder();
        foreach (CreditsData.Section section in data.sections)
        {
            if (text.Length > 0) text.Append("\n\n");
            bool first = true;
            if (!string.IsNullOrEmpty(section.heading))
            {
                text.Append("<size=125%><color=#").Append(hex).Append('>').Append(section.heading).Append("</color></size>");
                first = false;
            }
            if (section.lines == null) continue;
            foreach (string line in section.lines)
            {
                if (!first) text.Append('\n');
                text.Append(line);
                first = false;
            }
        }
        return text.ToString();
    }

    private void OnCancelPressed(InputAction.CallbackContext ctx) => Close();
}
