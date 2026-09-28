using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only convenience: pressing Play directly on a non-Boot scene still
/// needs the [Persistent] root (GameSession, GameManager, the render camera...).
/// Real builds always start at Boot.unity (build index 0) and never need this,
/// hence UNITY_EDITOR — so a real launch can never double-load Boot.
///
/// IMPORTANT — this cannot make the persistent objects exist before the entered
/// scene's Awake()/Start(). SceneManager.LoadScene never completes synchronously;
/// an additive load queued here still finishes at the end of the frame. Scripts
/// that depend on persistent objects (e.g. anything reading Camera.main) must
/// therefore resolve them lazily at point of use rather than caching once in
/// Awake/Start. Do not "fix" ordering problems by moving this earlier — there is
/// nothing earlier; fix the consumer instead.
/// </summary>
public static class SceneBootstrapGuard
{
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += OnFirstSceneLoaded;
    }

    private static void OnFirstSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnFirstSceneLoaded;

        if (scene.name == "Boot") return;          // Boot bootstraps itself
        if (scene.name == "DungeonRevealShowcase") return; // standalone marketing scene (Assets/_Showcase)
        if (GameSession.Instance != null) return;  // already bootstrapped

        Debug.Log($"[SceneBootstrapGuard] '{scene.name}' entered without Boot having run first — additively loading Boot now. If you didn't press Play directly on a gameplay scene, this means Play started from whatever scene was open in the editor instead of Boot.unity.");
        SceneManager.sceneLoaded += UnloadBootShell;
        SceneManager.LoadScene("Boot", LoadSceneMode.Additive);
    }

    // The [Persistent] root DontDestroyOnLoad's itself as Boot loads, so the
    // leftover Boot scene shell can be dropped once that has actually happened.
    private static void UnloadBootShell(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Boot") return;

        SceneManager.sceneLoaded -= UnloadBootShell;
        SceneManager.UnloadSceneAsync(scene);
    }
#endif
}
