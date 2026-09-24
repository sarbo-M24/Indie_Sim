using UnityEngine;

/// <summary>
/// Line pools for the shop mascot (ShopMascot). Placeholder lines only —
/// final dialogue comes later. OnPet is intentionally unwired for now.
/// </summary>
[CreateAssetMenu(menuName = "Shop/Dialogue Set")]
public class ShopDialogueSet : ScriptableObject
{
    [Tooltip("An offer card was clicked (selected, before Buy).")]
    public string[] onSelected = { "Good choice.", "Ooh, that one?", "Heh. Bold." };

    [Tooltip("A purchase went through.")]
    public string[] onPurchased = { "Excellent choice.", "Pleasure doing business.", "Smoke 'em well." };

    [Tooltip("Buy was pressed but failed (not enough coins or pack full).")]
    public string[] onPurchaseFailed = { "Can't afford that.", "Nice try.", "Come back with more coins." };

    [Tooltip("Petting the cat. Not wired yet — reserved hook.")]
    public string[] onPet = { };
}
