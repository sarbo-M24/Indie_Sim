using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds Assets/_Showcase/DungeonRevealShowcase.unity by copying the gameplay Grid/tilemaps,
/// generator settings and global 2D light out of RoguelikeMode.unity (read-only — the source
/// scene is closed without saving). Re-run whenever the gameplay tilemap setup changes.
/// The scene is intentionally NOT added to Build Settings.
/// </summary>
public static class DungeonRevealShowcaseSceneBuilder
{
    private const string SourceScenePath = "Assets/Scenes/RoguelikeMode.unity";
    private const string ShowcaseScenePath = "Assets/_Showcase/DungeonRevealShowcase.unity";

    [MenuItem("Tools/Showcase/Build Dungeon Reveal Scene")]
    private static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ShowcaseScenePath) &&
            !EditorUtility.DisplayDialog("Dungeon Reveal Showcase", $"Overwrite {ShowcaseScenePath}?", "Overwrite", "Cancel"))
            return;

        Scene showcase = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);

        try
        {
            var srcGen = source.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<DungeonMapGenerator>(true))
                .FirstOrDefault();
            if (srcGen == null)
            {
                Debug.LogError($"[ShowcaseBuilder] No DungeonMapGenerator found in {SourceScenePath}.");
                return;
            }

            var srcSo = new SerializedObject(srcGen);
            var srcFloor = srcSo.FindProperty("floorTilemap").objectReferenceValue as Tilemap;
            var srcWall = srcSo.FindProperty("wallTilemap").objectReferenceValue as Tilemap;
            var srcFoliage = srcSo.FindProperty("foliageTilemap").objectReferenceValue as Tilemap;
            Grid srcGrid = srcFloor != null ? srcFloor.GetComponentInParent<Grid>() : null;
            if (srcGrid == null || srcWall == null)
            {
                Debug.LogError("[ShowcaseBuilder] Source generator is missing its tilemaps / Grid.");
                return;
            }

            // --- Grid + tilemaps (exact renderer / sorting / material setup) ---
            GameObject grid = Object.Instantiate(srcGrid.gameObject);
            grid.name = srcGrid.name;
            SceneManager.MoveGameObjectToScene(grid, showcase);
            grid.transform.SetPositionAndRotation(srcGrid.transform.position, srcGrid.transform.rotation);

            Tilemap floor = FindCopy(grid, srcGrid, srcFloor);
            Tilemap wall = FindCopy(grid, srcGrid, srcWall);
            Tilemap foliage = srcFoliage != null ? FindCopy(grid, srcGrid, srcFoliage) : null;

            // Drop anything under the Grid that isn't one of the three tilemaps
            var keep = new[] { floor, wall, foliage }.Where(t => t != null).Select(t => t.gameObject).ToList();
            foreach (Transform child in grid.transform.Cast<Transform>().ToList())
            {
                if (!keep.Any(k => k.transform.IsChildOf(child))) Object.DestroyImmediate(child.gameObject);
            }

            foreach (var t in new[] { floor, wall, foliage })
            {
                if (t == null) continue;
                t.ClearAllTiles();
                foreach (Transform child in t.transform.Cast<Transform>().ToList())
                    Object.DestroyImmediate(child.gameObject); // stale shadow casters / gore chunks
            }

            StripToVisuals(floor.gameObject);
            if (foliage != null) StripToVisuals(foliage.gameObject);

            // Walls keep their shadow setup, but colliders stay disabled until the finale
            foreach (var mb in wall.GetComponents<MonoBehaviour>())
            {
                if (!(mb is TilemapShadowCaster2D) && !(mb is CompositeShadowCaster2D)) Object.DestroyImmediate(mb);
            }
            foreach (var c in wall.GetComponents<Collider2D>()) c.enabled = false;
            var shadowCaster = wall.GetComponent<TilemapShadowCaster2D>();

            // --- Generator: same MapParametersSO + tile arrays, nothing that spawns ---
            var genGo = new GameObject("DungeonMapGenerator");
            SceneManager.MoveGameObjectToScene(genGo, showcase);
            var gen = genGo.AddComponent<DungeonMapGenerator>();
            EditorUtility.CopySerialized(srcGen, gen);
            var so = new SerializedObject(gen);
            so.FindProperty("floorTilemap").objectReferenceValue = floor;
            so.FindProperty("wallTilemap").objectReferenceValue = wall;
            so.FindProperty("foliageTilemap").objectReferenceValue = foliage;
            so.FindProperty("shadowCaster").objectReferenceValue = shadowCaster;
            so.FindProperty("gorePainter").objectReferenceValue = null;
            so.FindProperty("teleporterPrefab").objectReferenceValue = null;
            so.FindProperty("enemySpawnerPrefab").objectReferenceValue = null;
            so.FindProperty("spawnerPrefabs").arraySize = 0;
            so.FindProperty("keyPrefab").objectReferenceValue = null;
            so.FindProperty("relicPrefabs").arraySize = 0;
            so.FindProperty("shouldKeyBeSpawned").boolValue = false;
            so.FindProperty("generateOnStart").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            // --- Global 2D light(s) so lit tile materials look like gameplay ---
            foreach (var light in source.GetRootGameObjects()
                         .SelectMany(r => r.GetComponentsInChildren<Light2D>(true))
                         .Where(l => l.lightType == Light2D.LightType.Global))
            {
                var lightGo = new GameObject(light.gameObject.name);
                SceneManager.MoveGameObjectToScene(lightGo, showcase);
                EditorUtility.CopySerialized(light, lightGo.AddComponent<Light2D>());
            }

            // --- Camera ---
            var camGo = new GameObject("Showcase Camera") { tag = "MainCamera" };
            SceneManager.MoveGameObjectToScene(camGo, showcase);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();

            // --- Controller ---
            var ctrlGo = new GameObject("DungeonRevealShowcase");
            SceneManager.MoveGameObjectToScene(ctrlGo, showcase);
            var ctrl = ctrlGo.AddComponent<DungeonRevealShowcase>();
            var cso = new SerializedObject(ctrl);
            cso.FindProperty("generator").objectReferenceValue = gen;
            cso.FindProperty("floorTilemap").objectReferenceValue = floor;
            cso.FindProperty("wallTilemap").objectReferenceValue = wall;
            cso.FindProperty("foliageTilemap").objectReferenceValue = foliage;
            cso.FindProperty("shadowCaster").objectReferenceValue = shadowCaster;
            cso.FindProperty("cam").objectReferenceValue = cam;
            cso.FindProperty("enemySpawnerPrefab").objectReferenceValue = srcSo.FindProperty("enemySpawnerPrefab").objectReferenceValue;
            cso.FindProperty("minDistanceBetweenSpawners").floatValue = srcSo.FindProperty("minDistanceBetweenSpawners").floatValue;
            var srcRelics = srcSo.FindProperty("relicPrefabs");
            var relics = cso.FindProperty("relicPrefabs");
            relics.arraySize = srcRelics.arraySize;
            for (int i = 0; i < srcRelics.arraySize; i++)
                relics.GetArrayElementAtIndex(i).objectReferenceValue = srcRelics.GetArrayElementAtIndex(i).objectReferenceValue;
            cso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(showcase, ShowcaseScenePath);
            Debug.Log($"[ShowcaseBuilder] Built {ShowcaseScenePath}. Press Play, then Space.");
        }
        finally
        {
            EditorSceneManager.CloseScene(source, true); // never saved
        }
    }

    private static Tilemap FindCopy(GameObject gridCopy, Grid srcGrid, Tilemap srcTilemap)
    {
        string path = AnimationUtility.CalculateTransformPath(srcTilemap.transform, srcGrid.transform);
        Transform t = gridCopy.transform.Find(path);
        return t != null ? t.GetComponent<Tilemap>() : null;
    }

    // Removes colliders, rigidbodies and gameplay scripts (e.g. gore painter), keeping Tilemap + renderer.
    private static void StripToVisuals(GameObject go)
    {
        foreach (var mb in go.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(mb);
        foreach (var c in go.GetComponents<CompositeCollider2D>()) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponents<Collider2D>()) Object.DestroyImmediate(c);
        foreach (var rb in go.GetComponents<Rigidbody2D>()) Object.DestroyImmediate(rb);
    }
}
