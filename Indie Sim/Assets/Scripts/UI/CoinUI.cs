using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CoinUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI coinText;
    [Tooltip("{0} = current coins, {1} = max coins.")]
    [SerializeField] private string textFormat = "COINS:{0} / {1}";

    [Tooltip("Image set to Image Type: Filled, Fill Method: Horizontal")]
    [SerializeField] private Image fillImage;

    [Header("Full Cap Flash Settings")]
    [Tooltip("Color the bar flashes when coins hit the max")]
    [SerializeField] private Color flashColor = Color.red;

    [Tooltip("How many times it flashes")]
    [SerializeField] private int flashCount = 3;

    [Tooltip("Seconds for one flash on/off cycle")]
    [SerializeField] private float flashInterval = 0.15f;

    // The original tint color of the fill image (set from its starting color in Inspector)
    private Color _normalColor;
    private Coroutine _flashCoroutine;

    // ─────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────
    private void Start()
    {
        if (fillImage != null)
            _normalColor = fillImage.color;

        if (CoinManager.Instance == null) return;

        CoinManager.Instance.OnCoinsChanged += OnCoinsChanged;
        CoinManager.Instance.OnMaxCoinsChanged += OnMaxCoinsChanged;

        RefreshUI(CoinManager.Instance.GetCurrentCoins(),
                  CoinManager.Instance.GetMaxCoins());
    }

    private void OnDestroy()
    {
        if (CoinManager.Instance == null) return;

        CoinManager.Instance.OnCoinsChanged -= OnCoinsChanged;
        CoinManager.Instance.OnMaxCoinsChanged -= OnMaxCoinsChanged;
    }

    // ─────────────────────────────────────────────────────────────────
    //  EVENT HANDLERS
    // ─────────────────────────────────────────────────────────────────
    private void OnCoinsChanged(int current)
    {
        RefreshUI(current, CoinManager.Instance.GetMaxCoins());
    }

    private void OnMaxCoinsChanged(int max)
    {
        RefreshUI(CoinManager.Instance.GetCurrentCoins(), max);
    }

    // ─────────────────────────────────────────────────────────────────
    //  DISPLAY UPDATE
    // ─────────────────────────────────────────────────────────────────
    private void RefreshUI(int current, int max)
    {
        // ── Text ─────────────────────────────────────────────────────
        if (coinText != null)
            coinText.text = string.Format(textFormat, current, max);

        // ── Fill bar ─────────────────────────────────────────────────
        if (fillImage != null)
        {
            float fill = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            fillImage.fillAmount = fill;

            // Flash red if at the cap
            if (current >= max && max > 0)
                TriggerFlash();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  FLASH LOGIC
    // ─────────────────────────────────────────────────────────────────
    private void TriggerFlash()
    {
        // Cancel any in-progress flash before starting a new one
        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);

        _flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        for (int i = 0; i < flashCount; i++)
        {
            fillImage.color = flashColor;
            yield return new WaitForSeconds(flashInterval);

            fillImage.color = _normalColor;
            yield return new WaitForSeconds(flashInterval);
        }

        // Make absolutely sure we end on the normal color
        fillImage.color = _normalColor;
        _flashCoroutine = null;
    }
}