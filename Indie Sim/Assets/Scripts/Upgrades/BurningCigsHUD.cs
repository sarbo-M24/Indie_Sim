using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One fill bar per active pack slot, live only during actual gameplay —
/// ShopUIController hides this whenever the shop is open. Fixed pool sized
/// to Pack.MaxSlots, positional rather than bound to a specific CigInstance
/// (same convention as ShopUIController's card slots): slot i shows
/// Pack.Instance.HeldCigs[i] if that instance is burning, else it's hidden.
/// Value follows CigInstance.BurnFraction (1 -> 0); a slot goes back to
/// hidden on its own once BurnResolver removes the instance at 0.
/// </summary>
public class BurningCigsHUD : MonoBehaviour
{
    public static BurningCigsHUD Instance { get; private set; }

    [SerializeField] private Slider[] burnSliders;

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

        var held = Pack.Instance.HeldCigs;
        for (int i = 0; i < burnSliders.Length; i++)
        {
            Slider slider = burnSliders[i];
            if (slider == null) continue;

            bool burning = i < held.Count && held[i].IsBurning;
            if (slider.gameObject.activeSelf != burning)
                slider.gameObject.SetActive(burning);

            if (burning)
                slider.value = held[i].BurnFraction;
        }
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
