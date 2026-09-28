using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Marketing-only showcase: generates a dungeon instantly (data only), builds the full
/// paint plan up front, then reveals it room by room for recording a reel. Enemy spawners and
/// relics pop in as visual-only props (their scripts are stripped before they can run).
/// Not a gameplay feature — delete Assets/_Showcase/ to remove it.
///
/// Controls: Space = start take, R = restart same seed, N = new seed, H = toggle seed label.
/// </summary>
public class DungeonRevealShowcase : MonoBehaviour
{
    public enum AspectMode { Landscape, Portrait }

    private enum Layer { Floor = 0, Wall = 1, Foliage = 2 }

    private struct Entry
    {
        public Layer layer;
        public Vector2Int pos;
        public float time;
    }

    private class RevealGroup
    {
        public string label;
        public bool isRoom;
        public int roomId = -1;
        public readonly List<(Layer layer, Vector2Int pos)> entries = new List<(Layer, Vector2Int)>();
        public float start;
        public float duration;
    }

    [Header("References")]
    [SerializeField] private DungeonMapGenerator generator;
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap foliageTilemap;
    [Tooltip("Optional. Its wall colliders stay disabled during the reveal and are enabled once for the finale.")]
    [SerializeField] private TilemapShadowCaster2D shadowCaster;
    [SerializeField] private Camera cam;

    [Header("Seed")]
    [SerializeField] private int seed = 12345;
    [Tooltip("0 = use the MapParametersSO node count")]
    [SerializeField] private int nodeCountOverride = 0;

    [Header("Timing (seconds)")]
    [SerializeField] private float revealDuration = 8f;
    [SerializeField] private float minRoomGroupTime = 0.08f;
    [SerializeField] private float dressingDuration = 1.5f;
    [SerializeField] private float finalHold = 1f;
    [SerializeField] private float preRoll = 0.5f;

    [Header("Foliage (visual duplicate of generator logic)")]
    [SerializeField][Range(0f, 1f)] private float foliageSpawnChance = 0.2f;
    [SerializeField] private bool spawnFoliageInRooms = true;
    [SerializeField] private bool spawnFoliageInCorridors = true;

    [Header("Audio (optional)")]
    [SerializeField] private AudioClip roomRevealClip;
    [SerializeField] private AudioClip finaleClip;
    [SerializeField] private float roomPitchStep = 0.04f;
    [SerializeField] private float maxRoomPitch = 1.6f;

    [Header("Camera")]
    [SerializeField] private AspectMode aspectMode = AspectMode.Landscape;
    [SerializeField] private float framingPadding = 3f;
    [SerializeField] private bool pushInDuringHold = true;
    [Tooltip("Fraction of the orthographic size removed by the end of the hold")]
    [SerializeField][Range(0f, 0.3f)] private float pushInAmount = 0.05f;

    [Header("Props (visual only: game scripts stripped, colliders disabled)")]
    [SerializeField] private bool spawnProps = true;
    [SerializeField] private GameObject enemySpawnerPrefab;
    [SerializeField] private GameObject[] relicPrefabs;
    [Tooltip("Mirrors DungeonMapGenerator.minDistanceBetweenSpawners")]
    [SerializeField] private float minDistanceBetweenSpawners = 5f;
    [SerializeField] private float propPopDuration = 0.15f;

    [Header("Overlay")]
    [SerializeField] private bool showSeedLabel = false;

    private readonly Dictionary<Vector2Int, TileBase>[] plan =
    {
        new Dictionary<Vector2Int, TileBase>(),
        new Dictionary<Vector2Int, TileBase>(),
        new Dictionary<Vector2Int, TileBase>()
    };
    private readonly HashSet<Vector2Int> spillPositions = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int>[] claimed =
    {
        new HashSet<Vector2Int>(), new HashSet<Vector2Int>(), new HashSet<Vector2Int>()
    };

    private MapData mapData;
    private System.Random showRng;
    private Coroutine take;
    private AudioSource audioSource;
    private Behaviour[] wallColliders = new Behaviour[0];
    private Transform propRoot;
    private Transform propStaging; // inactive, so prefab scripts never Awake before they're stripped
    private readonly List<(GameObject go, float time)> propEvents = new List<(GameObject, float)>();

    private static readonly Vector2Int[] Cardinal =
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0)
    };

    private void Start()
    {
        if (cam == null) cam = Camera.main;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        propRoot = new GameObject("ShowcaseProps").transform;
        propStaging = new GameObject("Staging").transform;
        propStaging.SetParent(propRoot, false);
        propStaging.gameObject.SetActive(false);

        if (wallTilemap != null)
        {
            wallColliders = wallTilemap.GetComponents<Collider2D>().Cast<Behaviour>().ToArray();
        }
        SetWallColliders(false);
        ClearTilemaps();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && take == null) BeginTake();
        if (Input.GetKeyDown(KeyCode.R)) BeginTake();
        if (Input.GetKeyDown(KeyCode.N))
        {
            seed = Random.Range(int.MinValue, int.MaxValue);
            BeginTake();
        }
        if (Input.GetKeyDown(KeyCode.H)) showSeedLabel = !showSeedLabel;
    }

    private void OnGUI()
    {
        if (!showSeedLabel) return;
        GUI.Label(new Rect(10, 10, 600, 22), $"Seed: {seed}   [Space] start  [R] restart  [N] new seed  [H] hide");
    }

    private void BeginTake()
    {
        if (take != null) StopCoroutine(take);
        take = StartCoroutine(RunTake());
    }

    // ------------------------------------------------------------------ take

    private IEnumerator RunTake()
    {
        if (generator == null || floorTilemap == null || wallTilemap == null || generator.GetParameters() == null)
        {
            Debug.LogError("[DungeonRevealShowcase] Generator (with MapParametersSO) and floor/wall tilemaps must be assigned.");
            take = null;
            yield break;
        }

        Random.InitState(seed);
        showRng = new System.Random(seed);

        int nodeCount = nodeCountOverride > 0 ? nodeCountOverride : generator.GetParameters().nodeCount;
        mapData = generator.GenerateMapDataOnly(seed, nodeCount);

        SetWallColliders(false);
        if (shadowCaster != null) shadowCaster.ClearShadows();
        ClearTilemaps();

        BuildPlan();
        List<RevealGroup> groups = BuildGroups(out RevealGroup dressing);
        ScheduleGroups(groups, dressing);
        FrameCamera();
        BuildProps(groups, dressing);

        // Flatten into one time-ordered timeline
        var timeline = new List<Entry>();
        var roomStartTimes = new List<float>();
        foreach (var g in groups.Append(dressing))
        {
            if (g.isRoom) roomStartTimes.Add(g.start);
            int n = g.entries.Count;
            for (int i = 0; i < n; i++)
            {
                timeline.Add(new Entry
                {
                    layer = g.entries[i].layer,
                    pos = g.entries[i].pos,
                    time = g.start + g.duration * i / n
                });
            }
        }

        yield return new WaitForSeconds(preRoll);

        var positions = new[] { new List<Vector3Int>(), new List<Vector3Int>(), new List<Vector3Int>() };
        var tiles = new[] { new List<TileBase>(), new List<TileBase>(), new List<TileBase>() };
        var maps = new[] { floorTilemap, wallTilemap, foliageTilemap };

        float endTime = dressing.start + dressing.duration;
        float t0 = Time.time;
        int cursor = 0, roomCursor = 0, propCursor = 0;

        while (cursor < timeline.Count || propCursor < propEvents.Count)
        {
            float elapsed = Time.time - t0;

            while (propCursor < propEvents.Count && propEvents[propCursor].time <= elapsed)
            {
                StartCoroutine(PopIn(propEvents[propCursor].go));
                propCursor++;
            }

            while (roomCursor < roomStartTimes.Count && roomStartTimes[roomCursor] <= elapsed)
            {
                PlayRoomSound(roomCursor);
                roomCursor++;
            }

            for (int l = 0; l < 3; l++) { positions[l].Clear(); tiles[l].Clear(); }
            while (cursor < timeline.Count && timeline[cursor].time <= elapsed)
            {
                var e = timeline[cursor++];
                int l = (int)e.layer;
                positions[l].Add((Vector3Int)e.pos);
                tiles[l].Add(plan[l][e.pos]);
            }
            for (int l = 0; l < 3; l++)
            {
                if (positions[l].Count > 0 && maps[l] != null)
                    maps[l].SetTiles(positions[l].ToArray(), tiles[l].ToArray());
            }

            if (cursor < timeline.Count || propCursor < propEvents.Count) yield return null;
        }

        Debug.Log($"[DungeonRevealShowcase] Reveal finished in {Time.time - t0:F2}s (planned {endTime:F2}s)");

        // Finale: lights on
        if (shadowCaster != null)
        {
            SetWallColliders(true);
            shadowCaster.RegenerateShadows();
        }
        if (finaleClip != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(finaleClip);
        }

        // Hold, with optional subtle push-in
        float baseSize = cam != null ? cam.orthographicSize : 0f;
        float h = 0f;
        while (h < finalHold)
        {
            h += Time.deltaTime;
            if (pushInDuringHold && cam != null)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(h / finalHold));
                cam.orthographicSize = baseSize * (1f - pushInAmount * k);
            }
            yield return null;
        }

        take = null;
    }

    private void ClearTilemaps()
    {
        if (floorTilemap != null) floorTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();
        if (foliageTilemap != null) foliageTilemap.ClearAllTiles();
    }

    private void SetWallColliders(bool enabled)
    {
        foreach (var c in wallColliders)
        {
            if (c != null) c.enabled = enabled;
        }
    }

    private void PlayRoomSound(int roomIndex)
    {
        if (roomRevealClip == null) return;
        audioSource.pitch = Mathf.Min(1f + roomPitchStep * roomIndex, maxRoomPitch);
        audioSource.PlayOneShot(roomRevealClip);
    }

    // ------------------------------------------------------------------ props

    // Placement duplicated from DungeonMapGenerator.SpawnAllEnemySpawners / SpawnRelics (showcase RNG).
    // Each prop pops in when its room's floor group finishes.
    private void BuildProps(List<RevealGroup> groups, RevealGroup dressing)
    {
        foreach (Transform child in propRoot.Cast<Transform>().ToList())
        {
            if (child != propStaging) Destroy(child.gameObject);
        }
        foreach (Transform child in propStaging.Cast<Transform>().ToList()) Destroy(child.gameObject);
        propEvents.Clear();
        if (!spawnProps) return;

        var roomRevealed = new Dictionary<int, float>();
        foreach (var g in groups)
        {
            if (g.roomId >= 0) roomRevealed[g.roomId] = g.start + g.duration;
        }
        float RevealTime(Room r) => roomRevealed.TryGetValue(r.uniqueId, out float t) ? t : dressing.start;

        // Enemy spawners: every room except start/end, 1-3 on edge tiles with exactly 2 cardinal walls
        if (enemySpawnerPrefab != null)
        {
            var placed = new List<Vector3>();
            foreach (var room in mapData.rooms.Values)
            {
                if (room.uniqueId == mapData.startRoomId || room.uniqueId == mapData.endRoomId) continue;

                var candidates = GetValidEdgeSpawnPositions(room);
                int count = Mathf.Min(showRng.Next(1, 4), candidates.Count);
                for (int i = 0; i < count; i++)
                {
                    Vector3? pos = TakeSpawnerPosition(candidates, placed);
                    if (!pos.HasValue) break;
                    placed.Add(pos.Value);
                    AddProp(enemySpawnerPrefab, pos.Value, $"EnemySpawner_Room_{room.uniqueId}", RevealTime(room));
                }
            }
        }

        // Relics: leaf rooms, generator's chance and rooms-per-relic-type
        if (relicPrefabs != null && relicPrefabs.Length > 0)
        {
            int relicCounter = 0;
            int perType = Mathf.Max(1, generator.roomsPerRelicType);
            foreach (var room in mapData.rooms.Values)
            {
                if (room.type != RoomType.LEAF_NODE_ROOM) continue;
                if (showRng.NextDouble() > generator.relicSpawnChance) continue;

                GameObject prefab = relicPrefabs[(relicCounter / perType) % relicPrefabs.Length];
                if (prefab == null) continue;
                var pos = new Vector3(
                    room.worldPosition.x + RandomRange(-room.size.x * 0.3f, room.size.x * 0.3f),
                    room.worldPosition.y + RandomRange(-room.size.y * 0.3f, room.size.y * 0.3f),
                    0f);
                AddProp(prefab, pos, $"{prefab.name}_Room_{room.uniqueId}", RevealTime(room));
                relicCounter++;
            }
        }

        propEvents.Sort((a, b) => a.time.CompareTo(b.time));
    }

    private void AddProp(GameObject prefab, Vector3 pos, string name, float time)
    {
        // Instantiated under an inactive parent: no Awake/OnEnable runs on the gameplay scripts
        GameObject go = Instantiate(prefab, pos, Quaternion.identity, propStaging);
        go.name = name;

        // Strip project scripts (EnemySpawner, Relic...), keep renderers / URP lights
        var gameAssembly = typeof(DungeonRevealShowcase).Assembly;
        for (int pass = 0; pass < 4; pass++) // retry in case one script [RequireComponent]s another
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Assembly == gameAssembly) DestroyImmediate(mb);
            }
        }
        foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
        foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;

        propEvents.Add((go, time));
    }

    private IEnumerator PopIn(GameObject go)
    {
        if (go == null) yield break;
        Transform t = go.transform;
        Vector3 targetScale = t.localScale;
        t.SetParent(propRoot, true);
        // Keep props upright on screen when the camera is rotated for portrait
        if (cam != null) t.rotation = Quaternion.Euler(0f, 0f, cam.transform.eulerAngles.z);

        for (float e = 0f; e < propPopDuration; e += Time.deltaTime)
        {
            if (go == null) yield break;
            float k = Mathf.Clamp01(e / propPopDuration);
            t.localScale = targetScale * (1f + 0.25f * Mathf.Sin(k * Mathf.PI)) * k; // small overshoot
            yield return null;
        }
        if (go != null) t.localScale = targetScale;
    }

    // Same as DungeonMapGenerator.GetValidEdgeSpawnPositions
    private List<Vector3> GetValidEdgeSpawnPositions(Room room)
    {
        var valid = new List<Vector3>();
        RectInt rect = GetRoomRect(room);
        foreach (var pos in RectTiles(rect))
        {
            bool isEdge = pos.x == rect.xMin || pos.x == rect.xMax - 1 || pos.y == rect.yMin || pos.y == rect.yMax - 1;
            if (!isEdge) continue;
            int walls = Cardinal.Count(d => mapData.wallTiles.Contains(pos + d));
            if (walls == 2) valid.Add(new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0f));
        }
        return valid;
    }

    // Same as DungeonMapGenerator.FindValidSpawnerPosition (checks placed props instead of scene objects)
    private Vector3? TakeSpawnerPosition(List<Vector3> candidates, List<Vector3> placed)
    {
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            Vector3 c = candidates[i];
            if (placed.Any(p => Vector3.Distance(c, p) < minDistanceBetweenSpawners)) continue;
            candidates.RemoveAt(i);
            return c;
        }
        return null;
    }

    private float RandomRange(float min, float max) => min + (float)showRng.NextDouble() * (max - min);

    // ------------------------------------------------------------------ plan

    private void BuildPlan()
    {
        foreach (var p in plan) p.Clear();
        spillPositions.Clear();

        TileBase[] floorAssets = (generator.FloorTileAssets ?? new TileBase[0]).Where(t => t != null).ToArray();
        TileBase[] foliageAssets = (generator.FoliageTileAssets ?? new TileBase[0]).Where(t => t != null).ToArray();
        if (floorAssets.Length == 0) Debug.LogError("[DungeonRevealShowcase] Generator has no floor tiles assigned.");
        if (generator.WallTileAssets == null || generator.WallTileAssets.Length < 18)
            Debug.LogError("[DungeonRevealShowcase] Generator wall tiles array must have 18 entries.");

        // Iterate in a fixed order so the same seed always gives the same picture
        List<Vector2Int> sortedWalls = Sorted(mapData.wallTiles);

        // Spill (duplicated from DungeonMapGenerator.PaintTiles, using the showcase RNG)
        foreach (var wallPos in sortedWalls)
        {
            int maxSpillDistance = showRng.Next(2, 4);
            foreach (var direction in Cardinal)
            {
                Vector2Int checkPos = wallPos + direction;
                if (mapData.wallTiles.Contains(checkPos) || mapData.floorTiles.Contains(checkPos)) continue;

                spillPositions.Add(checkPos);
                for (int i = 2; i <= maxSpillDistance; i++)
                {
                    if (showRng.NextDouble() < 0.3) break;
                    spillPositions.Add(wallPos + direction * i);
                }
            }
        }
        // A spill tile can land on a wall/floor further along — it's then just floor, not dressing
        spillPositions.RemoveWhere(p => mapData.floorTiles.Contains(p) || mapData.wallTiles.Contains(p));

        var allFloor = new HashSet<Vector2Int>(mapData.floorTiles);
        allFloor.UnionWith(mapData.wallTiles);
        allFloor.UnionWith(spillPositions);

        foreach (var pos in Sorted(allFloor))
        {
            plan[(int)Layer.Floor][pos] = floorAssets.Length > 0 ? floorAssets[showRng.Next(floorAssets.Length)] : null;

            if (foliageTilemap != null && foliageAssets.Length > 0 && ShouldSpawnFoliage(pos))
                plan[(int)Layer.Foliage][pos] = foliageAssets[showRng.Next(foliageAssets.Length)];
        }

        // Walls: every sprite decided against the COMPLETE floor/wall sets so nothing flickers later
        foreach (var pos in sortedWalls)
        {
            plan[(int)Layer.Wall][pos] = generator.GetWallTileFor(pos, mapData.floorTiles, mapData.wallTiles);
        }
    }

    private bool ShouldSpawnFoliage(Vector2Int pos)
    {
        bool isInRoom = mapData.rooms.Values.Any(r => GetRoomRect(r).Contains(pos));
        if (isInRoom && !spawnFoliageInRooms) return false;
        if (!isInRoom && !spawnFoliageInCorridors) return false;
        return showRng.NextDouble() < foliageSpawnChance;
    }

    // ------------------------------------------------------------------ grouping

    private List<RevealGroup> BuildGroups(out RevealGroup dressing)
    {
        foreach (var c in claimed) c.Clear();
        var groups = new List<RevealGroup>();
        var parameters = generator.GetParameters();
        int corridorWidth = parameters != null ? parameters.corridorWidth : 2;

        // Generator draws each corridor from whichever room comes first in dictionary order
        var roomOrder = new Dictionary<int, int>();
        int idx = 0;
        foreach (var r in mapData.rooms.Values) roomOrder[r.uniqueId] = idx++;

        var processed = new HashSet<int>();
        var enqueued = new HashSet<int>();
        var emittedCorridors = new HashSet<(int, int)>();
        var queue = new Queue<int>();

        // BFS from the start room; any disconnected rooms become extra roots afterwards
        var roots = new List<int>();
        if (mapData.GetStartRoom() != null) roots.Add(mapData.startRoomId);
        roots.AddRange(mapData.rooms.Keys.OrderBy(k => k));

        foreach (int root in roots)
        {
            if (!enqueued.Add(root)) continue;
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Room room = mapData.rooms[queue.Dequeue()];
                processed.Add(room.uniqueId);
                Vector2 center = room.worldPosition;

                // Room floor
                var roomFloor = RectTiles(GetRoomRect(room)).Where(mapData.floorTiles.Contains).ToList();
                var floorGroup = new RevealGroup { label = $"Room {room.uniqueId}", isRoom = true, roomId = room.uniqueId };
                foreach (var p in RippleOrder(roomFloor, center))
                    Claim(floorGroup, Layer.Floor, p);
                AddIfNotEmpty(groups, floorGroup);

                // Room walls (with the floor under them)
                var wallGroup = new RevealGroup { label = $"Room {room.uniqueId} walls" };
                foreach (var p in RippleOrder(AdjacentWalls(roomFloor), center))
                    ClaimWall(wallGroup, p);
                AddIfNotEmpty(groups, wallGroup);

                // Corridors to rooms not yet processed
                foreach (var conn in room.connections)
                {
                    int other = conn.connectedRoomId;
                    if (!mapData.rooms.ContainsKey(other) || processed.Contains(other)) continue;
                    var key = (Mathf.Min(room.uniqueId, other), Mathf.Max(room.uniqueId, other));
                    if (!emittedCorridors.Add(key)) continue;

                    AddCorridorGroup(groups, room, mapData.rooms[other], roomOrder, corridorWidth);

                    if (enqueued.Add(other)) queue.Enqueue(other);
                }
            }
        }

        // Sweep: anything the grouping math missed (spill is left for dressing)
        Vector2 startCenter = mapData.GetStartRoom() != null ? mapData.GetStartRoom().worldPosition : Vector2.zero;
        var sweep = new RevealGroup { label = "Sweep" };
        var sweepPositions = plan[(int)Layer.Floor].Keys.Where(p => !spillPositions.Contains(p))
            .Concat(plan[(int)Layer.Wall].Keys).Distinct();
        foreach (var p in RippleOrder(sweepPositions, startCenter))
        {
            Claim(sweep, Layer.Floor, p);
            Claim(sweep, Layer.Wall, p);
        }
        Debug.Log($"[DungeonRevealShowcase] Seed {seed}: {mapData.rooms.Count} rooms, {groups.Count} groups, sweep group count = {sweep.entries.Count}");
        AddIfNotEmpty(groups, sweep);

        // Dressing: spill + foliage as one ripple from the start room
        dressing = new RevealGroup { label = "Dressing" };
        var dressingPositions = spillPositions.Concat(plan[(int)Layer.Foliage].Keys).Distinct();
        foreach (var p in RippleOrder(dressingPositions, startCenter))
        {
            Claim(dressing, Layer.Floor, p);
            Claim(dressing, Layer.Foliage, p);
        }

        return groups;
    }

    private void AddCorridorGroup(List<RevealGroup> groups, Room from, Room to, Dictionary<int, int> roomOrder, int width)
    {
        // Recompute the corridor exactly like DungeonMapGenerator.AddCorridorTiles (grouping only)
        bool generatorStartsAtFrom = roomOrder[from.uniqueId] <= roomOrder[to.uniqueId];
        Vector2 a = generatorStartsAtFrom ? from.worldPosition : to.worldPosition;
        Vector2 b = generatorStartsAtFrom ? to.worldPosition : from.worldPosition;

        var line = GetLineTiles(
            new Vector2Int(Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y)),
            new Vector2Int(Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.y)));
        if (!generatorStartsAtFrom) line.Reverse(); // order outward from the current room

        bool isHorizontal = Mathf.Abs(b.x - a.x) >= Mathf.Abs(b.y - a.y);

        var corridorFloor = new List<Vector2Int>();
        foreach (var tile in line)
        {
            for (int i = 0; i < width; i++)
            {
                Vector2Int p = isHorizontal
                    ? new Vector2Int(tile.x, tile.y + i - width / 2)
                    : new Vector2Int(tile.x + i - width / 2, tile.y);
                if (mapData.floorTiles.Contains(p)) corridorFloor.Add(p);
            }
        }

        var group = new RevealGroup { label = $"Corridor {from.uniqueId}->{to.uniqueId}" };
        foreach (var p in corridorFloor) Claim(group, Layer.Floor, p);

        // Walls follow in the same along-the-line order
        var seenWalls = new HashSet<Vector2Int>();
        foreach (var f in corridorFloor)
        {
            foreach (var w in Neighbours8(f))
            {
                if (mapData.wallTiles.Contains(w) && seenWalls.Add(w)) ClaimWall(group, w);
            }
        }
        AddIfNotEmpty(groups, group);
    }

    private void Claim(RevealGroup group, Layer layer, Vector2Int pos)
    {
        int l = (int)layer;
        if (!plan[l].ContainsKey(pos) || !claimed[l].Add(pos)) return;
        group.entries.Add((layer, pos));
    }

    private void ClaimWall(RevealGroup group, Vector2Int pos)
    {
        Claim(group, Layer.Floor, pos);
        Claim(group, Layer.Wall, pos);
    }

    private static void AddIfNotEmpty(List<RevealGroup> groups, RevealGroup group)
    {
        if (group.entries.Count > 0) groups.Add(group);
    }

    // ------------------------------------------------------------------ timing

    private void ScheduleGroups(List<RevealGroup> groups, RevealGroup dressing)
    {
        // Proportional to tile count, room groups floored at the minimum, total scaled to revealDuration.
        var floored = new HashSet<RevealGroup>();
        for (int iteration = 0; iteration < 32; iteration++)
        {
            float flooredTime = floored.Count * minRoomGroupTime;
            float freeTiles = groups.Where(g => !floored.Contains(g)).Sum(g => g.entries.Count);
            float budget = Mathf.Max(0f, revealDuration - flooredTime);
            bool changed = false;

            foreach (var g in groups)
            {
                if (floored.Contains(g)) { g.duration = minRoomGroupTime; continue; }
                g.duration = freeTiles > 0 ? budget * g.entries.Count / freeTiles : 0f;
                if (g.isRoom && g.duration < minRoomGroupTime) { floored.Add(g); changed = true; }
            }
            if (!changed) break;
        }

        // If the floors alone exceed the budget, squash everything uniformly
        float sum = groups.Sum(g => g.duration);
        if (sum > revealDuration && sum > 0f)
        {
            foreach (var g in groups) g.duration *= revealDuration / sum;
        }

        float t = 0f;
        foreach (var g in groups)
        {
            g.start = t;
            t += g.duration;
        }
        dressing.start = t;
        dressing.duration = dressingDuration;
    }

    // ------------------------------------------------------------------ camera

    private void FrameCamera()
    {
        if (cam == null) return;

        bool any = false;
        Vector2Int min = default, max = default;
        foreach (var layer in plan)
        {
            foreach (var p in layer.Keys)
            {
                if (!any) { min = max = p; any = true; continue; }
                min = Vector2Int.Min(min, p);
                max = Vector2Int.Max(max, p);
            }
        }
        if (!any) return;

        Vector3 worldMin = floorTilemap.CellToWorld((Vector3Int)min);
        Vector3 worldMax = floorTilemap.CellToWorld((Vector3Int)(max + Vector2Int.one));
        Vector3 center = (worldMin + worldMax) * 0.5f;
        float width = worldMax.x - worldMin.x + framingPadding * 2f;
        float height = worldMax.y - worldMin.y + framingPadding * 2f;

        cam.orthographic = true;
        cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);

        // The artery always heads right; in portrait the camera is rotated so it climbs up the screen instead.
        bool portrait = aspectMode == AspectMode.Portrait;
        cam.transform.rotation = Quaternion.Euler(0f, 0f, portrait ? -90f : 0f);
        float screenW = portrait ? height : width; // world extent across the screen's width
        float screenH = portrait ? width : height; // world extent along the screen's height
        cam.orthographicSize = Mathf.Max(screenH * 0.5f, screenW * 0.5f / cam.aspect);
    }

    // ------------------------------------------------------------------ helpers

    // Same rect math as DungeonMapGenerator.AddRoomTiles (max exclusive)
    private static RectInt GetRoomRect(Room room)
    {
        int minX = Mathf.RoundToInt(room.worldPosition.x - room.size.x / 2f);
        int maxX = Mathf.RoundToInt(room.worldPosition.x + room.size.x / 2f);
        int minY = Mathf.RoundToInt(room.worldPosition.y - room.size.y / 2f);
        int maxY = Mathf.RoundToInt(room.worldPosition.y + room.size.y / 2f);
        return new RectInt(minX, minY, maxX - minX, maxY - minY);
    }

    private static IEnumerable<Vector2Int> RectTiles(RectInt rect)
    {
        for (int x = rect.xMin; x < rect.xMax; x++)
            for (int y = rect.yMin; y < rect.yMax; y++)
                yield return new Vector2Int(x, y);
    }

    // Same Bresenham as DungeonMapGenerator.GetLineTiles
    private static List<Vector2Int> GetLineTiles(Vector2Int start, Vector2Int end)
    {
        var tiles = new List<Vector2Int>();
        int dx = Mathf.Abs(end.x - start.x);
        int dy = Mathf.Abs(end.y - start.y);
        int x = start.x;
        int y = start.y;
        int xInc = (end.x > start.x) ? 1 : -1;
        int yInc = (end.y > start.y) ? 1 : -1;
        int error = dx - dy;

        for (int n = dx + dy; n > 0; n--)
        {
            tiles.Add(new Vector2Int(x, y));
            if (error > 0) { x += xInc; error -= 2 * dy; }
            else { y += yInc; error += 2 * dx; }
        }
        return tiles;
    }

    private List<Vector2Int> AdjacentWalls(IEnumerable<Vector2Int> floor)
    {
        var walls = new HashSet<Vector2Int>();
        foreach (var f in floor)
            foreach (var n in Neighbours8(f))
                if (mapData.wallTiles.Contains(n)) walls.Add(n);
        return walls.ToList();
    }

    private static IEnumerable<Vector2Int> Neighbours8(Vector2Int p)
    {
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                if (x != 0 || y != 0) yield return new Vector2Int(p.x + x, p.y + y);
    }

    private static List<Vector2Int> RippleOrder(IEnumerable<Vector2Int> positions, Vector2 center)
    {
        return positions
            .OrderBy(p => (new Vector2(p.x + 0.5f, p.y + 0.5f) - center).sqrMagnitude)
            .ThenBy(p => p.x).ThenBy(p => p.y)
            .ToList();
    }

    private static List<Vector2Int> Sorted(IEnumerable<Vector2Int> positions)
    {
        return positions.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
    }
}
