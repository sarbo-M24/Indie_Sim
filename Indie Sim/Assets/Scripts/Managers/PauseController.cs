using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sole owner of Time.timeScale. Anything that stops the game (pause menu,
/// death screen, countdown, demo complete) holds a freeze under its own
/// source object; the game runs again only when every source has let go.
/// Hit-stop is layered underneath: it slows time only while nothing holds a
/// freeze, and ending it can never unpause the game (the old DamageIndicator
/// restored its saved timeScale, which un-paused a pause opened mid-hit-stop).
///
/// Static, like InputManager — no Boot.unity object. Every single-mode scene
/// load starts clean, because the sources that held freezes died with the
/// old scene.
/// </summary>
public static class PauseController
{
    private static readonly HashSet<object> freezers = new HashSet<object>();
    private static int hitStops;
    private static float hitStopScale = 1f;

    /// <summary>True while any source holds a freeze.</summary>
    public static bool IsFrozen => freezers.Count > 0;

    /// <summary>Raised when the game goes from running to frozen or back.</summary>
    public static event Action<bool> OnFrozenChanged;

    // Statics survive play-mode restarts when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        freezers.Clear();
        hitStops = 0;
        hitStopScale = 1f;
        OnFrozenChanged = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static bool IsFrozenBy(object source) => freezers.Contains(source);

    /// <summary>True if something other than source holds a freeze.</summary>
    public static bool IsFrozenByOtherThan(object source) =>
        freezers.Count > (freezers.Contains(source) ? 1 : 0);

    public static void SetFrozen(object source, bool frozen)
    {
        bool wasFrozen = IsFrozen;
        if (frozen) freezers.Add(source);
        else freezers.Remove(source);

        Apply();
        if (wasFrozen != IsFrozen) OnFrozenChanged?.Invoke(IsFrozen);
    }

    /// <summary>Slows time to scale until the matching EndHitStop. Ignored while frozen.</summary>
    public static void BeginHitStop(float scale)
    {
        hitStops++;
        hitStopScale = Mathf.Clamp01(scale);
        Apply();
    }

    public static void EndHitStop()
    {
        hitStops = Mathf.Max(0, hitStops - 1);
        Apply();
    }

    /// <summary>Drops every freeze and hit-stop. For scene changes and run restarts.</summary>
    public static void ResetAll()
    {
        bool wasFrozen = IsFrozen;
        freezers.Clear();
        hitStops = 0;

        Apply();
        if (wasFrozen) OnFrozenChanged?.Invoke(false);
    }

    private static void Apply()
    {
        if (IsFrozen) Time.timeScale = 0f;
        else if (hitStops > 0) Time.timeScale = hitStopScale;
        else Time.timeScale = 1f;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) ResetAll();
    }
}
