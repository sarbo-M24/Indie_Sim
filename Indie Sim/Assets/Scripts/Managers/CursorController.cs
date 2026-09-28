using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sole owner of Cursor.visible/lockState. Lives under [Persistent] in Boot.unity.
/// Reads the SceneUIMode marker present in the newly loaded scene rather than
/// comparing scene names as strings.
/// </summary>
public class CursorController : MonoBehaviour
{
    public static CursorController Instance { get; private set; }

    // In-scene UI (e.g. DemoCompleteScreen, shop, pause/death panels) that
    // needs the cursor visible without a scene change to trigger
    // HandleSceneLoaded. Per source, so the pause menu closing over the shop
    // doesn't hide the shop's cursor. Cleared automatically on the next scene load.
    private readonly HashSet<object> _overrideSources = new HashSet<object>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        Apply();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _overrideSources.Clear();
        Apply();
    }

    /// <summary>
    /// For in-scene UI (paused overlays, end screens) that need the cursor
    /// visible without a scene load. Overrides SceneUIMode until the next
    /// scene load, which resets it automatically.
    /// </summary>
    public void SetCursorOverride(bool showCursor) => SetCursorOverride(this, showCursor);

    /// <summary>Per-source override: the cursor shows while any source wants it.</summary>
    public void SetCursorOverride(object source, bool showCursor)
    {
        if (showCursor) _overrideSources.Add(source);
        else _overrideSources.Remove(source);
        Apply();
    }

    private void Apply()
    {
        SceneUIMode uiMode = FindFirstObjectByType<SceneUIMode>();
        bool showCursor = _overrideSources.Count > 0 || (uiMode != null && uiMode.ShowCursor);

        Cursor.visible = showCursor;
        Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Confined;
    }
}
