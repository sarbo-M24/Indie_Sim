using UnityEngine;

/// <summary>
/// The Settings screen: Audio and Gameplay tabs of SliderRow / ToggleRow.
/// Tabs, Back, Reset and gamepad focus come from TabbedMenuPanel. One prefab,
/// placed in the pause menu's canvas and in the main menu
/// (see SettingsAudioInputPlan.md).
/// </summary>
public class SettingsPanel : TabbedMenuPanel
{
    // Reset to defaults = every row on the current tab.
    protected override void ResetTab(GameObject content)
    {
        if (content == null) return;

        foreach (SliderRow row in content.GetComponentsInChildren<SliderRow>(true)) row.ResetToDefault();
        foreach (ToggleRow row in content.GetComponentsInChildren<ToggleRow>(true)) row.ResetToDefault();
    }
}
