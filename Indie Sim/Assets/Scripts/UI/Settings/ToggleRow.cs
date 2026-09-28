using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One settings row: a label and a Toggle bound to one on/off setting, with
/// an optional "ON"/"OFF" readout. A (Submit) or a click flips it; applies
/// live, SettingsPanel saves on close.
/// </summary>
public class ToggleRow : MonoBehaviour
{
    [SerializeField] private BoolSetting setting;
    [SerializeField] private Toggle toggle;
    [Tooltip("Optional. Shows onText / offText.")]
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private string onText = "ON";
    [SerializeField] private string offText = "OFF";

    private void Awake()
    {
        if (toggle == null) toggle = GetComponentInChildren<Toggle>(true);
    }

    private void OnEnable()
    {
        toggle.onValueChanged.AddListener(OnToggled);
        SettingsService.OnChanged += OnSettingsChanged;
        Refresh();
    }

    private void OnDisable()
    {
        toggle.onValueChanged.RemoveListener(OnToggled);
        SettingsService.OnChanged -= OnSettingsChanged;
    }

    private void OnToggled(bool isOn)
    {
        SettingBindings.Set(setting, isOn);
        UpdateText(isOn);
    }

    private void OnSettingsChanged(GameSettings settings) => Refresh();

    private void Refresh()
    {
        bool isOn = SettingBindings.Get(setting);
        toggle.SetIsOnWithoutNotify(isOn);
        UpdateText(isOn);
    }

    private void UpdateText(bool isOn)
    {
        if (stateText != null) stateText.text = isOn ? onText : offText;
    }
}
