using UnityEngine;

/// <summary>
/// Central controller that drives a smooth, looping "psychedelic" colour shift
/// across both blood systems:
///
///   • BloodSplatterEffect  — the particle burst + chunks enemies emit on death.
///     Only affects splatters spawned AFTER the colour changes (each lives ~2s).
///
///   • ChunkedGorePainter   — the blood painted on the ground.
///     - Future splats     : recoloured via SetBloodColor().
///     - Already-painted    : recoloured live via SetDisplayTint() (the
///       BloodDisplay shader's _Tint), so the whole floor pulses through the
///       palette, not just new pools.
///
/// Put this on a single manager GameObject. Leave the references empty to
/// auto-find the scene instances.
/// </summary>
public class PsychedelicBloodController : MonoBehaviour
{
    [Header("Enable")]
    [Tooltip("Follows the Psychedelic mode setting (GameSettings.psychedelicMode); the debug key or SetActive() override it until the setting next changes.")]
    [SerializeField] private bool active = false;
    [SerializeField] private bool affectKillSplatter = true;
    [SerializeField] private bool affectGroundGore = true;

    [Tooltip("Also hue-shift blood that's already on the ground (not just new pools). " +
             "Uses the BloodDisplay shader _Tint. When on, new ground splats are " +
             "painted near-white so the tint fully controls the colour.")]
    [SerializeField] private bool tintExistingGroundGore = true;

    [Tooltip("Render the ground blood through the water material (ChunkedGorePainter.waterMaterialTemplate) " +
             "instead of the flat blood look. Needs the water material assigned on the painter.")]
    [SerializeField] private bool waterGroundGore = false;

    [Header("Palette")]
    [Tooltip("Colours to cycle through, in order. Loops back to the first.")]
    [SerializeField]
    private Color[] palette =
    {
        new Color(1.00f, 0.00f, 0.60f), // hot pink
        new Color(0.60f, 0.00f, 1.00f), // violet
        new Color(0.00f, 0.55f, 1.00f), // electric blue
        new Color(0.00f, 1.00f, 0.80f), // aqua
        new Color(0.40f, 1.00f, 0.00f), // lime
        new Color(1.00f, 0.85f, 0.00f), // yellow
        new Color(1.00f, 0.35f, 0.00f), // orange
    };

    [Header("Motion")]
    [Tooltip("Seconds spent blending from one palette colour to the next.")]
    [SerializeField] private float secondsPerColor = 1.5f;

    [Tooltip("Interpolate through HSV (rainbow sweep) instead of straight RGB (can pass through grey).")]
    [SerializeField] private bool hsvInterpolation = true;

    [Tooltip("Overall brightness / intensity multiplier applied to the final colour.")]
    [Range(0f, 3f)][SerializeField] private float intensity = 1f;

    [Tooltip("Alpha handed to the ground display tint. Lower = more see-through floor gore.")]
    [Range(0f, 1f)][SerializeField] private float groundTintAlpha = 1f;

    [Header("Debug Toggle")]
    [Tooltip("Press this key to toggle the effect on/off. Editor and development builds only — players use the Psychedelic mode setting.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.P;

    [Header("References (auto-found if empty)")]
    [SerializeField] private BloodSplatterEffect killSplatter;
    [SerializeField] private ChunkedGorePainter groundGore;

    private float phase;
    private bool wasActive;

    private void Awake()
    {
        if (killSplatter == null) killSplatter = FindFirstObjectByType<BloodSplatterEffect>();
        if (groundGore == null) groundGore = FindFirstObjectByType<ChunkedGorePainter>();
    }

    private void OnEnable()
    {
        SettingsService.OnChanged += ApplySetting;
        ApplySetting(SettingsService.Current);
    }

    private void OnDisable()
    {
        SettingsService.OnChanged -= ApplySetting;

        // Leave the blood systems in a sane state if this controller is turned off.
        RestoreDefaults();
    }

    private void ApplySetting(GameSettings settings) => active = settings.psychedelicMode;

    private void Update()
    {
        HandleToggleKey();

        if (!active)
        {
            if (wasActive) RestoreDefaults();
            wasActive = false;
            return;
        }
        wasActive = true;

        Color color = EvaluatePalette() * intensity;
        color.a = 1f;

        if (affectKillSplatter && killSplatter != null)
            killSplatter.bloodTintColor = color;

        if (affectGroundGore && groundGore != null)
        {
            groundGore.SetWaterMode(waterGroundGore);

            if (tintExistingGroundGore)
            {
                groundGore.SetBloodColor(Color.white * Mathf.Max(1f, intensity)); // paint neutral so _Tint drives hue
                Color t = color;
                t.a = groundTintAlpha;
                groundGore.SetDisplayTint(t);
            }
            else
            {
                groundGore.SetBloodColor(color); // future pools only
            }
        }
    }

    private void HandleToggleKey()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (toggleKey == KeyCode.None) return;

        // Legacy Input — debug-only key (Active Input Handling is still Both).
        if (Input.GetKeyDown(toggleKey))
        {
            active = !active;
            Debug.Log($"[PsychedelicBloodController] Toggled {(active ? "ON" : "OFF")} via '{toggleKey}'.");
        }
#endif
    }

    /// <summary>Current palette colour for this instant (before intensity).</summary>
    private Color EvaluatePalette()
    {
        if (palette == null || palette.Length == 0) return Color.red;
        if (palette.Length == 1) return palette[0];

        float step = Mathf.Max(0.01f, secondsPerColor);
        phase += Time.deltaTime / step;
        phase %= palette.Length;

        int i = Mathf.FloorToInt(phase);
        int next = (i + 1) % palette.Length;
        float frac = phase - i;

        return hsvInterpolation
            ? LerpHSV(palette[i], palette[next], frac)
            : Color.Lerp(palette[i], palette[next], frac);
    }

    private static Color LerpHSV(Color a, Color b, float t)
    {
        Color.RGBToHSV(a, out float ha, out float sa, out float va);
        Color.RGBToHSV(b, out float hb, out float sb, out float vb);

        // shortest way around the hue wheel
        float dh = Mathf.Repeat(hb - ha + 0.5f, 1f) - 0.5f;
        float h = Mathf.Repeat(ha + dh * t, 1f);

        Color result = Color.HSVToRGB(h, Mathf.Lerp(sa, sb, t), Mathf.Lerp(va, vb, t));
        result.a = Mathf.Lerp(a.a, b.a, t);
        return result;
    }

    private void RestoreDefaults()
    {
        if (killSplatter != null)
            killSplatter.bloodTintColor = Color.white;

        if (groundGore != null)
        {
            groundGore.SetWaterMode(false);
            groundGore.SetDisplayTint(Color.white);
            groundGore.SetBloodColor(new Color(0.6f, 0.05f, 0.05f, 1f)); // original dark red
        }
    }

    // Runtime API ---------------------------------------------------------

    public void SetActive(bool on) => active = on;
    public void SetWaterGroundGore(bool on) => waterGroundGore = on;
    public void SetSpeed(float secondsPerColour) => secondsPerColor = secondsPerColour;
    public void SetIntensity(float value) => intensity = value;
    public void SetPalette(Color[] colors) { palette = colors; phase = 0f; }
}
