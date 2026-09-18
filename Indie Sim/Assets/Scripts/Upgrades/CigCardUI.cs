using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One selectable card slot in the shop's Buy/Burn grid. A fixed pool of
/// these sits in the scene per tab (never instantiated/destroyed at
/// runtime) — ShopUIController populates or hides each slot every time a
/// tab is (re)shown, per UpgradeSystemPlan.md Step 3's "modular" card
/// requirement: only CigData.icon needs authoring, the card itself stays a
/// dumb, reusable prefab.
/// </summary>
public class CigCardUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Image rarityIcon;
    [SerializeField] private Button button;
    [SerializeField] private GameObject selectedHighlight;

    public CigInstance BoundInstance { get; private set; }
    public event Action<CigCardUI> OnClicked;

    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(() => OnClicked?.Invoke(this));

        SetEmpty();
    }

    public void Populate(CigInstance instance, Sprite rarityIconSprite)
    {
        BoundInstance = instance;
        gameObject.SetActive(true);

        if (icon != null)
        {
            icon.sprite = instance.Data != null ? instance.Data.icon : null;
            icon.enabled = icon.sprite != null;
        }

        if (rarityIcon != null)
        {
            rarityIcon.sprite = rarityIconSprite;
            rarityIcon.enabled = rarityIconSprite != null;
        }

        SetSelected(false);
    }

    /// <summary>Hides the slot — used when there are fewer offers/held cigs than slots.</summary>
    public void SetEmpty()
    {
        BoundInstance = null;
        SetSelected(false);
        gameObject.SetActive(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null)
            selectedHighlight.SetActive(selected);
    }
}
