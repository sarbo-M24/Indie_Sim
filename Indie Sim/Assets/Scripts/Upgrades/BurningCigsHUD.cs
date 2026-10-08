using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One indicator per active pack slot, live only during actual gameplay —
/// ShopUIController hides this whenever the shop is open. Fixed pool sized
/// to Pack.MaxSlots, positional rather than bound to a specific CigInstance
/// (same convention as ShopUIController's card slots): slot i is shown if
/// Pack.Instance.HeldCigs[i] is burning, else hidden. Per
/// UpgradeSystemSpec.md burning is not time-based (it resolves for exactly
/// the next level, not a countdown), so the burn-down is cosmetic: it tracks
/// the dungeon timer when one is running (a fixed duration otherwise) and
/// stops at maxBurn, leaving the filter. A slot goes back to hidden on its
/// own once BurnResolver removes the instance at the level boundary.
/// Each bar shows the burning cig's own CigData.icon on its fill, which
/// should be Image Type: Filled so the Slider crops the art instead of
/// squashing it.
/// </summary>
public class BurningCigsHUD : MonoBehaviour
{
    public static BurningCigsHUD Instance { get; private set; }

    [SerializeField] private Slider[] burnSliders;

    [Header("Burn-down")]
    [Tooltip("How much of the cig burns away by the end of the level. The rest is the filter, which never burns.")]
    [SerializeField, Range(0f, 1f)] private float maxBurn = 0.65f;
    [Tooltip("Seconds to burn down to maxBurn when there's no dungeon timer (boss arena, tutorial).")]
    [SerializeField] private float fallbackBurnDuration = 60f;

    private float[] _burnStartTimes;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (Pack.Instance == null || burnSliders == null) return;

        if (_burnStartTimes == null || _burnStartTimes.Length != burnSliders.Length)
            _burnStartTimes = new float[burnSliders.Length];

        var held = Pack.Instance.HeldCigs;
        for (int i = 0; i < burnSliders.Length; i++)
        {
            Slider slider = burnSliders[i];
            if (slider == null) continue;

            bool burning = i < held.Count && held[i].IsBurning;
            if (slider.gameObject.activeSelf != burning)
            {
                slider.gameObject.SetActive(burning);
                if (burning) _burnStartTimes[i] = Time.time;
            }

            if (burning)
            {
                SetCigArt(slider, held[i].Data != null ? held[i].Data.icon : null);
                slider.value = 1f - maxBurn * BurnProgress(_burnStartTimes[i]);
            }
        }
    }

    private static void SetCigArt(Slider slider, Sprite icon)
    {
        if (icon == null || slider.fillRect == null) return;
        if (slider.fillRect.TryGetComponent(out Image fill) && fill.sprite != icon)
            fill.sprite = icon;
    }

    /// <summary>0 → 1 over the level: the dungeon timer's elapsed share if it's running, else time since this cig was lit.</summary>
    private float BurnProgress(float startTime)
    {
        RoguelikeManager manager = RoguelikeManager.Instance;
        if (manager != null && manager.IsDungeonTimerActive() && manager.GetDungeonTimeLimit() > 0f)
            return Mathf.Clamp01(1f - manager.GetDungeonTimeRemaining() / manager.GetDungeonTimeLimit());

        return fallbackBurnDuration > 0f ? Mathf.Clamp01((Time.time - startTime) / fallbackBurnDuration) : 1f;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
