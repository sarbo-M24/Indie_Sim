using UnityEngine;

/// <summary>
/// One value per tier (1-4), shown as four labelled fields in the Inspector.
/// Replaces the old 5-slot arrays whose index 0 was unused. Out-of-range
/// tiers clamp to 1-4.
/// </summary>
[System.Serializable]
public struct TierValues
{
    public float tier1, tier2, tier3, tier4;

    public TierValues(float t1, float t2, float t3, float t4)
    {
        tier1 = t1; tier2 = t2; tier3 = t3; tier4 = t4;
    }

    public float Get(int tier)
    {
        switch (Mathf.Clamp(tier, 1, 4))
        {
            case 1: return tier1;
            case 2: return tier2;
            case 3: return tier3;
            default: return tier4;
        }
    }
}

/// <summary>Integer counterpart of TierValues, for counts (bounces, bullets, charges, pellets).</summary>
[System.Serializable]
public struct TierValuesInt
{
    public int tier1, tier2, tier3, tier4;

    public TierValuesInt(int t1, int t2, int t3, int t4)
    {
        tier1 = t1; tier2 = t2; tier3 = t3; tier4 = t4;
    }

    public int Get(int tier)
    {
        switch (Mathf.Clamp(tier, 1, 4))
        {
            case 1: return tier1;
            case 2: return tier2;
            case 3: return tier3;
            default: return tier4;
        }
    }
}
