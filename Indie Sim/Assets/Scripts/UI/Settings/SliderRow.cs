using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One settings row: a label, a Slider and a "80%" readout, bound to one
/// 0–1 setting. Applies live as the slider moves; SettingsPanel saves on close.
///
/// The slider is driven in whole steps (0..steps), so each D-pad / stick
/// press moves exactly one step (5% at the default 20), and holding repeats
/// at the UI input module's rate. Mouse drags snap to the same steps.
/// Min/Max/Whole Numbers on the Slider are overwritten at runtime.
/// </summary>
public class SliderRow : MonoBehaviour
{
    [SerializeField] private FloatSetting setting;
    [SerializeField] private Slider slider;
    [Tooltip("Optional. Shows the value as a percentage.")]
    [SerializeField] private TMP_Text valueText;
    [Tooltip("Number of steps from 0% to 100%. 20 = 5% per press.")]
    [SerializeField] private int steps = 20;

    private void Awake()
    {
        if (slider == null) slider = GetComponentInChildren<Slider>(true);

        slider.minValue = 0;
        slider.maxValue = Mathf.Max(1, steps);
        slider.wholeNumbers = true;
    }

    private void OnEnable()
    {
        slider.onValueChanged.AddListener(OnSliderMoved);
        SettingsService.OnChanged += OnSettingsChanged;
        Refresh();
    }

    private void OnDisable()
    {
        slider.onValueChanged.RemoveListener(OnSliderMoved);
        SettingsService.OnChanged -= OnSettingsChanged;
    }

    private void OnSliderMoved(float stepValue)
    {
        float value = stepValue / slider.maxValue;
        SettingBindings.Set(setting, value);
        UpdateText(value);
    }

    // Picks up Reset to defaults and changes made elsewhere (e.g. the pause
    // menu's Sound button) without re-firing onValueChanged.
    private void OnSettingsChanged(GameSettings settings) => Refresh();

    private void Refresh()
    {
        float value = SettingBindings.Get(setting);
        slider.SetValueWithoutNotify(Mathf.Round(value * slider.maxValue));
        UpdateText(value);
    }

    private void UpdateText(float value)
    {
        if (valueText != null) valueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
