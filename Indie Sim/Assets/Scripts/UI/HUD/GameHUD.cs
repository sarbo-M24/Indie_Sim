using UnityEngine;

/// <summary>
/// Root of the gameplay HUD canvas (timer, ammo, stomp, burning + held cigs).
/// Kept on its own canvas, apart from the menus in the Player Canvas, so the
/// per-frame timer/fill updates don't rebuild the menu geometry. Each HUD
/// element reads its own data source; this only owns show/hide — the shop
/// hides the whole HUD while it's open.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    private Canvas _canvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _canvas = GetComponent<Canvas>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Toggles the Canvas, not the GameObject — re-enabling a GameObject rebuilds every child.</summary>
    public void SetVisible(bool visible)
    {
        if (_canvas != null) _canvas.enabled = visible;
    }
}
