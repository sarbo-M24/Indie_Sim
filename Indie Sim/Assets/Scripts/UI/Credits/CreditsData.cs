using System;
using UnityEngine;

/// <summary>
/// What the Credits screen lists, in order. Edit Assets/Data/Credits.asset —
/// no scene changes needed. For third-party music and sound, write the
/// credit the way its licence asks for it (CC BY: title, author, link,
/// licence name); keep the licences themselves on record outside the game.
/// </summary>
[CreateAssetMenu(menuName = "UI/Credits", fileName = "Credits")]
public class CreditsData : ScriptableObject
{
    [Serializable]
    public class Section
    {
        public string heading;
        [TextArea(1, 4)] public string[] lines;
    }

    public Section[] sections;
}
