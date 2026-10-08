using UnityEngine;

/// <summary>
/// Gameplay HUD row of the cigs currently in the pack. Fixed pool sized to
/// Pack.MaxSlots: the held cigs that aren't burning, in pack order, extras
/// hidden (burning ones are on BurningCigsHUD). Refreshes on Pack.Changed
/// rather than polling.
/// </summary>
public class HeldCigsHUD : MonoBehaviour
{
    [SerializeField] private RarityConfig rarityConfig;
    [SerializeField] private HeldCigIconView[] slots;

    private Pack _pack;
    private readonly System.Collections.Generic.List<CigInstance> _shown = new System.Collections.Generic.List<CigInstance>();

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

        // Burning cigs leave this row the moment they're burned (like the
        // shop's pack list) — BurningCigsHUD shows them until the level ends.
        _shown.Clear();
        if (_pack != null)
            foreach (CigInstance instance in _pack.HeldCigs)
                if (instance?.Data != null && !instance.IsBurning)
                    _shown.Add(instance);

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (i < _shown.Count)
                slots[i].Populate(_shown[i], rarityConfig);
            else
                slots[i].SetEmpty();
        }
    }
}
