#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Right mouse button saves a 1920x1080 PNG of the game (UI included) for store-page
/// screenshots. Editor and development builds only.
///
/// The game is re-rendered at a whole-number multiple of the window
/// (ScreenCapture superSize) until it's at least 1080 tall, so a small Game
/// view still gives a sharp shot; the result is then centre-cropped to 16:9
/// and scaled down to exactly 1920x1080. For the cleanest pixels set the
/// Game view to 1920x1080 (or 960x540) so no scaling is needed.
///
/// Saved to <project>/Screenshots in the Editor (outside Assets, so Unity
/// doesn't import them), or persistentDataPath/Screenshots in a build.
/// Created before the first scene loads, so it needs no scene object.
/// </summary>
public class ScreenshotCapture : MonoBehaviour
{
    private const int Width = 1920;
    private const int Height = 1080;

    private bool _capturing;

    public static string Folder =>
        Application.isEditor
            ? Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots")
            : Path.Combine(Application.persistentDataPath, "Screenshots");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        GameObject go = new GameObject(nameof(ScreenshotCapture));
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenshotCapture>();
    }

    private void Update()
    {
        // Right mouse isn't bound to anything in gameplay (only the UI map's unused RightClick).
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame && !_capturing)
            StartCoroutine(Capture());
    }

    private IEnumerator Capture()
    {
        _capturing = true;
        yield return new WaitForEndOfFrame();

        int superSize = Mathf.Max(1, Mathf.CeilToInt((float)Height / Screen.height));
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture(superSize);
        Texture2D output = ToFullHd(shot);

        try
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png");
            File.WriteAllBytes(path, output.EncodeToPNG());
            Debug.Log($"[ScreenshotCapture] Saved {Width}x{Height} screenshot (captured at {shot.width}x{shot.height}): {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ScreenshotCapture] Couldn't save screenshot: {e.Message}");
        }
        finally
        {
            if (output != shot) Destroy(output);
            Destroy(shot);
            _capturing = false;
        }
    }

    // Centre-crops to 16:9, then scales to 1920x1080 on the GPU.
    private static Texture2D ToFullHd(Texture2D source)
    {
        if (source.width == Width && source.height == Height) return source;

        float targetAspect = (float)Width / Height;
        float sourceAspect = (float)source.width / source.height;
        Vector2 scale = Vector2.one;
        Vector2 offset = Vector2.zero;
        if (sourceAspect > targetAspect)
        {
            scale.x = targetAspect / sourceAspect;
            offset.x = (1f - scale.x) / 2f;
        }
        else if (sourceAspect < targetAspect)
        {
            scale.y = sourceAspect / targetAspect;
            offset.y = (1f - scale.y) / 2f;
        }

        if (source.width < Width || source.height < Height)
            Debug.LogWarning($"[ScreenshotCapture] Captured {source.width}x{source.height}, upscaling to {Width}x{Height}. Use a 1920x1080 Game view for a sharper shot.");

        source.filterMode = FilterMode.Bilinear;
        RenderTexture rt = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, rt, scale, offset);

        RenderTexture.active = rt;
        Texture2D result = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        result.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }
}
#endif
