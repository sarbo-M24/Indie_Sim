using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to the Retry button in the Canvas.
/// Automatically handles both dungeon and boss scene correctly.
/// </summary>
public class RetryButton : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "Main Menu";
    //[SerializeField] private string dungeonSceneName = "DungeonScene";

    // This script lives on the HUD canvas root (alongside StatTracker), not on
    // the actual death/retry UI. PlayerHealth needs the real panel — the
    // separate "DeathUIPanel" child that holds the Retry button and kill/coin
    // text and is hidden by default — not this GameObject. Assign it in the
    // Inspector on the prefab.
    [SerializeField] private GameObject deathPanel;
    public GameObject DeathPanel => deathPanel;

    /// <summary>
    /// Wire this to the button's OnClick in the Inspector
    /// </summary>
    public void OnRetryClicked()
    {
        Debug.Log($"[RetryButton] OnRetryClicked() on {gameObject.name} in scene '{gameObject.scene.name}'.");
        PauseController.ResetAll();

        // Restarts the run in place — works identically whether death
        // happened in RoguelikeMode or BossArena (D2).
        GameManager.Instance.RetryRun();
    }
}
