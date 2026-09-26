using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Line pools for the shop mascot (ShopMascot). Placeholder lines only —
/// final dialogue comes later.
/// </summary>
[CreateAssetMenu(menuName = "Shop/Dialogue Set")]
public class ShopDialogueSet : ScriptableObject
{
    [Tooltip("Greeting typed out every time the shop opens. Stays up until the next line.")]
    public string[] onShopOpened = { "Hello! Look at my wares.", "Welcome, welcome. Take a look.", "Fresh stock today." };

    [Tooltip("An offer card was clicked (selected, before Buy).")]
    public string[] onSelected = { "Nice choice.", "Ooh, that one?", "Heh. Bold." };

    [Tooltip("A purchase went through.")]
    public string[] onPurchased = { "Excellent choice.", "Pleasure doing business.", "Smoke 'em well." };

    [Tooltip("Buy or Reshuffle failed for lack of coins.")]
    [FormerlySerializedAs("onPurchaseFailed")]
    public string[] onNotEnoughCoins = { "Get more coins.", "Can't afford that, pal.", "Come back with more coins." };

    [Tooltip("Buy failed because the pack is full.")]
    public string[] onPackFull = { "Your pack's full.", "No room in that pack.", "Burn something first." };

    [Tooltip("A held cig in the pack was clicked (selected for burning).")]
    public string[] onPackCigSelected =
    {
        "Burn it and it hits its full potential... for this level only.",
        "Light that one up — max power, but it won't last.",
        "Burning it maxes it out. Then it's gone.",
    };

    [Tooltip("The cat was petted.")]
    public string[] onPet = { "Mrrp.", "...Fine. Keep going.", "Purrr." };
}
