using UnityEngine;

/// <summary>
/// Gameplay HUD row of the cigs currently in the pack. Fixed pool sized to
/// Pack.MaxSlots, positional like BurningCigsHUD: slot i shows
/// Pack.HeldCigs[i], extras are hidden. Refreshes on Pack.Changed rather than
/// polling.
/// </summary>
public class HeldCigsHUD : MonoBehaviour
{
    [SerializeField] private RarityConfig rarityConfig;
    [SerializeField] private HeldCigIconView[] slots;

    private Pack _pack;

    private void Start()
    {
        // Start, not OnEnable: Pack.Instance is set in its Awake.
        _pack = Pack.Instance;
        if (_pack != null) _pack.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_pack != null) _pack.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (slots == null) return;
        var held = _pack != null ? _pack.HeldCigs : null;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (held != null && i < held.Count && held[i]?.Data != null)
                slots[i].Populate(held[i], rarityConfig);
            else
                slots[i].SetEmpty();
        }
    }
}
