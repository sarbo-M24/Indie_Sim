using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The stomp bar: a filled Image for the recharge, plus charge pips on top
/// that only show once Chain Stomp raises the cap above one. Fixed pip pool
/// in the scene — one pip per possible charge, extras hidden. Reads the
/// player's PlayerStompController, found lazily since the player can spawn
/// after the HUD.
/// </summary>
public class StompHUD : MonoBehaviour
{
    [Tooltip("Image Type: Filled. 1 = ready.")]
    [SerializeField] private Image fill;
    [SerializeField] private Image[] chargePips;
    [SerializeField] private Color pipFullColor = Color.white;
    [SerializeField] private Color pipEmptyColor = new Color(1f, 1f, 1f, 0.25f);

    private PlayerStompController _stomp;
    private int _shownCharges = -1;
    private int _shownMax = -1;

    private void Update()
    {
        if (_stomp == null)
        {
            _stomp = FindFirstObjectByType<PlayerStompController>();
            if (_stomp == null) return;
        }

        if (fill != null && !Mathf.Approximately(fill.fillAmount, _stomp.StompFill))
            fill.fillAmount = _stomp.StompFill;

        int max = _stomp.StompMaxCharges;
        int charges = Mathf.Min(_stomp.StompCharges, max);
        if (charges == _shownCharges && max == _shownMax) return;
        _shownCharges = charges;
        _shownMax = max;
        RefreshPips(charges, max);
    }

    private void RefreshPips(int charges, int max)
    {
        if (chargePips == null) return;
        bool showPips = max > 1;

        for (int i = 0; i < chargePips.Length; i++)
        {
            Image pip = chargePips[i];
            if (pip == null) continue;

            bool active = showPips && i < max;
            if (pip.gameObject.activeSelf != active) pip.gameObject.SetActive(active);
            if (active) pip.color = i < charges ? pipFullColor : pipEmptyColor;
        }
    }
}
