using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authoring helpers for Tutorial.unity: drop hint zones at the Scene
/// view's centre (under a "Tutorial Zones" parent), and clear or set the
/// profile's tutorial-completed flag (replay it / skip it on new runs).
/// </summary>
public static class TutorialMenu
{
    private const string Root = "Tools/Tutorial/";
    private const string ZonesParent = "Tutorial Zones";

    [MenuItem(Root + "Add Hint Zone", priority = 0)]
    private static void AddHintZone()
    {
        GameObject parent = GameObject.Find(ZonesParent);
        int number = parent != null ? parent.GetComponentsInChildren<TutorialHintZone>(true).Length + 1 : 1;
        CreateZone<TutorialHintZone>($"Room {number} Hint", new Vector2(10f, 8f));
    }

    private static void CreateZone<T>(string name, Vector2 size) where T : Component
    {
        GameObject parent = GameObject.Find(ZonesParent);
        if (parent == null)
        {
            parent = new GameObject(ZonesParent);
            Undo.RegisterCreatedObjectUndo(parent, "Create Tutorial Zones");
        }

        GameObject zone = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(zone, "Add " + name);
        zone.transform.SetParent(parent.transform, false);

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 pivot = view != null ? view.pivot : Vector3.zero;
        zone.transform.position = new Vector3(Mathf.Round(pivot.x), Mathf.Round(pivot.y), 0f);

        BoxCollider2D box = zone.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;
        zone.AddComponent<T>();

        Selection.activeGameObject = zone;
        EditorGUIUtility.PingObject(zone);
    }

    [MenuItem(Root + "Replay Tutorial On Next New Run", priority = 20)]
    private static void ResetCompletedFlag()
    {
        if (Application.isPlaying && GameSession.Instance != null)
        {
            GameSession.Instance.Persistent.TutorialCompleted = false;
            GameSession.Instance.Save();
            Debug.Log("[TutorialMenu] Tutorial flag cleared (live session + profile.json).");
            return;
        }

        string path = Path.Combine(SaveService.DefaultFolder, "profile.json");
        if (!File.Exists(path))
        {
            Debug.Log("[TutorialMenu] No profile.json yet — the tutorial will play on the next new run.");
            return;
        }

        JObject profile = JObject.Parse(File.ReadAllText(path));
        if (profile["Sections"] is JObject sections && sections.Remove("global.tutorial"))
        {
            File.WriteAllText(path, profile.ToString());
            Debug.Log("[TutorialMenu] Tutorial flag cleared in profile.json.");
        }
        else
        {
            Debug.Log("[TutorialMenu] profile.json has no tutorial flag — the tutorial will play on the next new run.");
        }
    }

    /// <summary>
    /// Testing shortcut: marks the tutorial done so new runs (Retry, New Game)
    /// start in the first dungeon, as if it had been played through once.
    /// </summary>
    [MenuItem(Root + "Mark Tutorial Completed", priority = 21)]
    private static void SetCompletedFlag()
    {
        if (Application.isPlaying && GameSession.Instance != null)
        {
            GameSession.Instance.Persistent.TutorialCompleted = true;
            GameSession.Instance.Save();
            Debug.Log("[TutorialMenu] Tutorial marked completed (live session + profile.json).");
            return;
        }

        string path = Path.Combine(SaveService.DefaultFolder, "profile.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning("[TutorialMenu] No profile.json yet — enter Play mode once (or run this during Play mode) so there's a profile to mark.");
            return;
        }

        JObject profile = JObject.Parse(File.ReadAllText(path));
        if (!(profile["Sections"] is JObject sections))
        {
            Debug.LogWarning("[TutorialMenu] profile.json has no Sections — not touching it.");
            return;
        }

        sections["global.tutorial"] = new JObject
        {
            ["Version"] = 1,
            ["Scope"] = "Global",
            ["Payload"] = new JObject { ["Completed"] = true }
        };
        File.WriteAllText(path, profile.ToString());
        Debug.Log("[TutorialMenu] Tutorial marked completed in profile.json.");
    }
}
