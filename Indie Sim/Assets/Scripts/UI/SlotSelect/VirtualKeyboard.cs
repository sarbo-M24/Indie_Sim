using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// On-screen keyboard for gamepad players (NamePromptDialog shows it only
/// while a pad is driving the UI). Types straight into `target`, respecting
/// its character limit.
///
/// Keys are made at runtime from `keyTemplate` (a hidden button): character
/// rows on the left, SHIFT / SPACE / BACK / CLEAR / DONE in a wider column on
/// the right (the panel widens to fit), with explicit wrap-around navigation
/// that can't leave the keyboard. Pad shortcuts: X/Square backspace, Y/Triangle space,
/// Start done. B/Esc is left to the owner (cancel the prompt).
///
/// Shift is one-shot, like a phone: on at the start of an empty field, off
/// after the next letter. Begin(replaceExisting) makes the first key typed
/// replace a pre-filled default instead of appending to it.
///
/// The selected key is drawn inverted (key colour ↔ text colour). SPACE,
/// BACK and DONE carry the glyph of their pad shortcut (Y, X, Start).
/// </summary>
public class VirtualKeyboard : MonoBehaviour
{
    [SerializeField] private TMP_InputField target;
    [Tooltip("Hidden button cloned for every key. Its size is a character key's size.")]
    [SerializeField] private Button keyTemplate;
    [SerializeField] private float spacing = 6f;
    [Tooltip("Width of the SHIFT / SPACE / BACK / CLEAR / DONE keys in the right-hand column.")]
    [SerializeField] private float specialKeyWidth = 210f;
    [Tooltip("Gap between the character grid and the special-key column.")]
    [SerializeField] private float columnGap = 16f;
    [Tooltip("Panel margin left and right of the keys; the panel widens to fit.")]
    [SerializeField] private float sidePadding = 23f;
    [Tooltip("Shortcut glyphs on SPACE / BACK / DONE (Y / X / Start). Missing sprites just hide the icon.")]
    [SerializeField] private ControllerGlyphSet glyphs;

    private static readonly string[] CharRows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL-", "ZXCVBNM_" };
    private const int SpecialKeyCount = 5; // SHIFT, SPACE, BACK, CLEAR, DONE
    private const float GlyphPadding = 6f;

    /// <summary>Done pressed (key or Start).</summary>
    public event Action Submitted;

    private readonly List<List<Button>> _rows = new List<List<Button>>(); // character rows
    private readonly List<Button> _specials = new List<Button>();         // right-hand column, top to bottom
    private readonly List<(TMP_Text label, char c)> _letterKeys = new List<(TMP_Text, char)>();
    private TMP_Text _shiftLabel;
    private bool _shift;
    private bool _replaceOnType;

    private Color _keyColor, _textColor;
    private Button _highlighted;

    public Selectable DefaultSelection
    {
        get
        {
            EnsureBuilt();
            return _rows.Count > 0 ? _rows[1][0] : null; // Q
        }
    }

    private void Awake() => EnsureBuilt();

    /// <summary>Call when the prompt opens.</summary>
    public void Begin(bool replaceExisting)
    {
        EnsureBuilt();
        _replaceOnType = replaceExisting;
        SetShift(target.text.Length == 0 || replaceExisting);
    }

    private void Update()
    {
        UpdateHighlight();

        Gamepad pad = Gamepad.current;
        if (pad == null) return;

        if (pad.buttonWest.wasPressedThisFrame) Backspace();
        if (pad.buttonNorth.wasPressedThisFrame) Type(' ');
        if (pad.startButton.wasPressedThisFrame) Submitted?.Invoke();
    }

    private void OnDisable()
    {
        SetInverted(_highlighted, false);
        _highlighted = null;
    }

    // ───────────── highlight ─────────────

    private void UpdateHighlight()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        Button key = selected != null && selected.transform.IsChildOf(transform) ? selected.GetComponent<Button>() : null;
        if (key == _highlighted) return;

        SetInverted(_highlighted, false);
        SetInverted(key, true);
        _highlighted = key;
    }

    private void SetInverted(Button key, bool inverted)
    {
        if (key == null) return;
        if (key.targetGraphic != null) key.targetGraphic.color = inverted ? _textColor : _keyColor;
        TMP_Text label = key.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.color = inverted ? _keyColor : _textColor;
    }

    // ───────────── typing ─────────────

    private void Type(char c)
    {
        if (_replaceOnType)
        {
            target.text = "";
            _replaceOnType = false;
        }

        if (target.characterLimit > 0 && target.text.Length >= target.characterLimit) return;

        bool letter = char.IsLetter(c);
        target.text += letter && !_shift ? char.ToLowerInvariant(c) : c;
        if (letter && _shift) SetShift(false);
    }

    private void Backspace()
    {
        if (_replaceOnType)
        {
            Clear();
            return;
        }

        if (target.text.Length > 0) target.text = target.text.Substring(0, target.text.Length - 1);
        if (target.text.Length == 0) SetShift(true);
    }

    private void Clear()
    {
        _replaceOnType = false;
        target.text = "";
        SetShift(true);
    }

    private void SetShift(bool on)
    {
        _shift = on;
        foreach ((TMP_Text label, char c) in _letterKeys)
            label.text = (on ? c : char.ToLowerInvariant(c)).ToString();
        if (_shiftLabel != null) _shiftLabel.text = on ? "SHIFT ON" : "SHIFT";
    }

    // ───────────── building ─────────────

    private void EnsureBuilt()
    {
        if (_rows.Count > 0 || keyTemplate == null) return;

        keyTemplate.gameObject.SetActive(false);
        Vector2 keySize = ((RectTransform)keyTemplate.transform).sizeDelta;

        _keyColor = keyTemplate.targetGraphic != null ? keyTemplate.targetGraphic.color : Color.black;
        TMP_Text templateLabel = keyTemplate.GetComponentInChildren<TMP_Text>(true);
        _textColor = templateLabel != null ? templateLabel.color : Color.white;
        int maxColumns = 0;
        foreach (string row in CharRows) maxColumns = Mathf.Max(maxColumns, row.Length);

        // Character grid on the left, special keys in a column on the right,
        // both centred vertically. The panel widens to fit them.
        float gridWidth = maxColumns * keySize.x + (maxColumns - 1) * spacing;
        float totalWidth = gridWidth + columnGap + specialKeyWidth;
        float gridCenterX = -totalWidth / 2f + gridWidth / 2f;
        float columnX = totalWidth / 2f - specialKeyWidth / 2f;

        RectTransform panel = (RectTransform)transform;
        panel.sizeDelta = new Vector2(Mathf.Max(panel.sizeDelta.x, totalWidth + 2f * sidePadding), panel.sizeDelta.y);

        float gridTop = (CharRows.Length - 1) * (keySize.y + spacing) / 2f;
        for (int r = 0; r < CharRows.Length; r++)
        {
            List<Button> row = new List<Button>();
            string chars = CharRows[r];
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                Button key = MakeKey(c.ToString(), gridCenterX + RowX(i, chars.Length, keySize.x), gridTop - r * (keySize.y + spacing), keySize, () => Type(c));
                if (char.IsLetter(c)) _letterKeys.Add((key.GetComponentInChildren<TMP_Text>(true), c));
                row.Add(key);
            }
            _rows.Add(row);
        }

        string[] specialNames = { "SHIFT", "SPACE", "BACK", "CLEAR", "DONE" };
        Action[] specialActions = { () => SetShift(!_shift), () => Type(' '), Backspace, Clear, () => Submitted?.Invoke() };
        GamepadGlyph?[] specialGlyphs = { null, GamepadGlyph.North, GamepadGlyph.West, null, GamepadGlyph.Start };
        Vector2 specialSize = new Vector2(specialKeyWidth, keySize.y);
        float columnTop = (SpecialKeyCount - 1) * (keySize.y + spacing) / 2f;
        for (int i = 0; i < SpecialKeyCount; i++)
        {
            Button key = MakeKey(specialNames[i], columnX, columnTop - i * (keySize.y + spacing), specialSize, specialActions[i]);
            if (specialGlyphs[i].HasValue) AddGlyph(key, specialGlyphs[i].Value);
            _specials.Add(key);
        }
        _shiftLabel = _specials[0].GetComponentInChildren<TMP_Text>(true);

        LinkNavigation();
    }

    private float RowX(int index, int count, float width)
    {
        float rowWidth = count * width + (count - 1) * spacing;
        return -rowWidth / 2f + width / 2f + index * (width + spacing);
    }

    private Button MakeKey(string label, float x, float y, Vector2 size, Action onClick)
    {
        GameObject go = Instantiate(keyTemplate.gameObject, keyTemplate.transform.parent);
        go.name = $"Key {label}";
        go.SetActive(true);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = size;

        TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;

        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(() => onClick());

        // The inverted highlight replaces the tint; keep only the pressed dip.
        ColorBlock colors = button.colors;
        colors.highlightedColor = colors.selectedColor = Color.white;
        button.colors = colors;

        if (!go.TryGetComponent(out ButtonFocusScale _)) go.AddComponent<ButtonFocusScale>();
        return button;
    }

    // Pad-shortcut icon at the key's left edge; the label moves right to make room.
    private void AddGlyph(Button key, GamepadGlyph glyph)
    {
        RectTransform keyRect = (RectTransform)key.transform;
        float size = keyRect.sizeDelta.y - 2f * GlyphPadding;

        GameObject icon = new GameObject("Glyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.SetActive(false); // Init before OnEnable reads the glyph set
        icon.transform.SetParent(key.transform, false);

        RectTransform rect = (RectTransform)icon.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(GlyphPadding, 0f);
        rect.sizeDelta = new Vector2(size, size);

        icon.GetComponent<Image>().preserveAspect = true;
        icon.AddComponent<GamepadGlyphImage>().Init(glyphs, glyph);
        icon.SetActive(true);

        TMP_Text label = key.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            Vector4 margin = label.margin;
            margin.x = GlyphPadding + size;
            label.margin = margin;
        }
    }

    // Character rows: Left/Right along the row; Right off the end goes to the
    // special key at that height, Left off the start wraps to it too. Up/Down
    // go to the nearest key (by x) in the next row, wrapping top ↔ bottom.
    // Special column: Up/Down wrap within it; Left goes back to the end of the
    // nearest row (by y), Right wraps to that row's start.
    private void LinkNavigation()
    {
        for (int r = 0; r < _rows.Count; r++)
        {
            List<Button> row = _rows[r];
            List<Button> up = _rows[(r - 1 + _rows.Count) % _rows.Count];
            List<Button> down = _rows[(r + 1) % _rows.Count];
            Button special = Nearest(_specials, Y(row[0]), Y);

            for (int i = 0; i < row.Count; i++)
            {
                row[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? row[i - 1] : special,
                    selectOnRight = i < row.Count - 1 ? row[i + 1] : special,
                    selectOnUp = Nearest(up, X(row[i]), X),
                    selectOnDown = Nearest(down, X(row[i]), X)
                };
            }
        }

        List<Button> rowStarts = new List<Button>(), rowEnds = new List<Button>();
        foreach (List<Button> row in _rows)
        {
            rowStarts.Add(row[0]);
            rowEnds.Add(row[row.Count - 1]);
        }

        for (int i = 0; i < _specials.Count; i++)
        {
            float y = Y(_specials[i]);
            _specials[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = Nearest(rowEnds, y, Y),
                selectOnRight = Nearest(rowStarts, y, Y),
                selectOnUp = _specials[(i - 1 + _specials.Count) % _specials.Count],
                selectOnDown = _specials[(i + 1) % _specials.Count]
            };
        }
    }

    private static float X(Button key) => ((RectTransform)key.transform).anchoredPosition.x;
    private static float Y(Button key) => ((RectTransform)key.transform).anchoredPosition.y;

    private static Button Nearest(List<Button> keys, float value, Func<Button, float> axis)
    {
        Button best = null;
        float bestDistance = float.MaxValue;
        foreach (Button key in keys)
        {
            float distance = Mathf.Abs(axis(key) - value);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = key;
            }
        }
        return best;
    }

    /// <summary>True if the EventSystem's selection is one of this keyboard's keys.</summary>
    public bool HasSelection
    {
        get
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.transform.IsChildOf(transform);
        }
    }
}
