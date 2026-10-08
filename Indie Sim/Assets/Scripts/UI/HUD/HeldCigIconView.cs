using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One held cig on the gameplay HUD: just the cig art with a border tinted by
/// its rolled rarity. No name, stats or interaction — the shop's PackCigView
/// is the interactive version. Pooled by HeldCigsHUD.
/// </summary>
public class HeldCigIconView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [Tooltip("Tinted with the RarityConfig colour.")]
    [SerializeField] private Image border;

    private Sprite _placeholder;

    private void Awake()
    {
        if (icon != null) _placeholder = icon.sprite;
    }

    public void Populate(CigInstance instance, RarityConfig rarityConfig)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        // Cigs with no icon yet keep the prefab's placeholder art.
        if (icon != null)
            icon.sprite = instance.Data != null && instance.Data.icon != null ? instance.Data.icon : _placeholder;

        if (border != null && rarityConfig != null)
            border.color = rarityConfig.GetColor(instance.RolledRarity);
    }

    public void SetEmpty()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
