using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using static Unity.Cinemachine.CinemachineSplineRoll;

public enum RoomType
{
    START_ROOM,           // First room in the main artery
    END_ROOM,             // Last room in the main artery
    MAIN_ARTERY_ROOM,
    DISTRIBUTIVE_NODE_ROOM,
    LEAF_NODE_ROOM,
    ARTERY_CORNER_ROOM
}

public enum ConnectionType
{
    ARTERY_PATH,
    VEIN_PATH
}

// Room ID Categories for easy identification
public static class RoomIDCategories
{
    public const int MAIN_ROOM_START = 0;
    public const int MAIN_ROOM_END = 100;

    public const int LEAF_ROOM_START = 100;
    public const int LEAF_ROOM_END = 200;

    public const int DISTRIBUTIVE_ROOM_START = 200;
    public const int DISTRIBUTIVE_ROOM_END = 300;

    public const int CORNER_ROOM_START = 300;
    public const int CORNER_ROOM_END = 400;

    // Helper methods to determine room type from ID
    public static bool IsMainRoom(int id) => id >= MAIN_ROOM_START && id < MAIN_ROOM_END;
    public static bool IsLeafRoom(int id) => id >= LEAF_ROOM_START && id < LEAF_ROOM_END;
    public static bool IsDistributiveRoom(int id) => id >= DISTRIBUTIVE_ROOM_START && id < DISTRIBUTIVE_ROOM_END;
    public static bool IsCornerRoom(int id) => id >= CORNER_ROOM_START && id < CORNER_ROOM_END;

    // Get room type from ID
    public static RoomType GetRoomTypeFromID(int id)
    {
        if (IsMainRoom(id)) return RoomType.MAIN_ARTERY_ROOM;
        if (IsLeafRoom(id)) return RoomType.LEAF_NODE_ROOM;
        if (IsDistributiveRoom(id)) return RoomType.DISTRIBUTIVE_NODE_ROOM;
        if (IsCornerRoom(id)) return RoomType.ARTERY_CORNER_ROOM;
        return RoomType.MAIN_ARTERY_ROOM; // Default
    }
}

[System.Serializable]
public struct RoomConnection
{
    public int connectedRoomId;
    public ConnectionType type;

    public RoomConnection(int roomId, ConnectionType connectionType)
    {
        connectedRoomId = roomId;
        type = connectionType;
    }
}

[System.Serializable]
public class Room
{
    public int uniqueId;
    public Vector2 worldPosition;
    public RoomType type;
    public Vector2Int size;
    public List<RoomConnection> connections;

    public Room()
    {
        connections = new List<RoomConnection>();
    }

    public Bounds GetBounds()
    {
        return new Bounds(worldPosition, new Vector3(size.x, size.y, 0));
    }
}

// Tracks spawner respawn exclusion zones
[System.Serializable]
public class SpawnerExclusionZone
{
    public Vector3 position;
    public float radius;
    public float expirationTime;

    public SpawnerExclusionZone(Vector3 pos, float rad, float expTime)
    {
        position = pos;
        radius = rad;
        expirationTime = expTime;
    }
}

[System.Serializable]
public class MapParameters
{
    [Header("Main Artery Control")]

    public int nodeCount = 5;

    public float mainRoomSpacing = 8f;
    [Range(0f, 1f)] public float chanceForLTurn = 0.3f;
    public float mainArteryPositionJitter = 2f;

    [Header("Distributive Node & Leaf Control")]
    [Range(0f, 1f)] public float distributiveNodeChancePerSegment = 0.4f;
    public int minLeafNodesPerDistributive = 1;
    public int maxLeafNodesPerDistributive = 3;
    public float leafBranchLength = 6f;
    public float leafNodePositionJitter = 1.5f;

    [Header("Room Size Control")]
    public Vector2Int baseStartRoomSize = new Vector2Int(8, 8);      // Start room can be larger
    public Vector2Int baseEndRoomSize = new Vector2Int(8, 8);        // End room can be larger
    public Vector2Int baseMainArteryRoomSize = new Vector2Int(6, 6);
    public Vector2Int baseDistributiveRoomSize = new Vector2Int(4, 4);
    public Vector2Int baseLeafRoomSize = new Vector2Int(5, 5);
    public Vector2Int baseCornerRoomSize = new Vector2Int(3, 3);
    [Range(0f, 1f)] public float roomSizeVariationPercentage = 0.2f;

    [Header("Collision & Placement Control")]
    public float minRoomDistance = 2f;
    public int maxRepositionAttempts = 10;
    public float repositionSearchRadius = 3f;

    [Header("Start Room Isolation")]
    public float startRoomToArteryDistance = 12f; // Distance before main artery begins
    public float minStartToEndDistance = 40f; // Minimum distance between start and teleporter

    [Header("Room Rotation")]
    [Range(0f, 1f)] public float roomRotationChance = 0.5f; // Chance for rectangular rooms to rotate

    [Header("Corridor Settings")]
    public int corridorWidth = 2;
}

public class MapData
{
    public Dictionary<int, Room> rooms;
    public HashSet<Vector2Int> floorTiles;
    public HashSet<Vector2Int> wallTiles;
    public int startRoomId = -1;  // ID of the start room
    public int endRoomId = -1;    // ID of the end room
    public int lastMainRoomId = -1; // For teleporter spawning

    public MapData()
    {
        rooms = new Dictionary<int, Room>();
        floorTiles = new HashSet<Vector2Int>();
        wallTiles = new HashSet<Vector2Int>();
    }

    // Helper methods to get start and end rooms
    public Room GetStartRoom() => startRoomId >= 0 && rooms.ContainsKey(startRoomId) ? rooms[startRoomId] : null;
    public Room GetEndRoom() => endRoomId >= 0 && rooms.ContainsKey(endRoomId) ? rooms[endRoomId] : null;
    public Room GetLastMainRoom() => lastMainRoomId >= 0 && rooms.ContainsKey(lastMainRoomId) ? rooms[lastMainRoomId] : null;

    // Get all rooms of a specific category
    public List<Room> GetMainRooms() => rooms.Values.Where(r => RoomIDCategories.IsMainRoom(r.uniqueId)).ToList();
    public List<Room> GetLeafRooms() => rooms.Values.Where(r => RoomIDCategories.IsLeafRoom(r.uniqueId)).ToList();
    public List<Room> GetDistributiveRooms() => rooms.Values.Where(r => RoomIDCategories.IsDistributiveRoom(r.uniqueId)).ToList();
    public List<Room> GetCornerRooms() => rooms.Values.Where(r => RoomIDCategories.IsCornerRoom(r.uniqueId)).ToList();
}

public class DungeonMapGenerator : MonoBehaviour
{
    [Header("Map Configuration")]
    [SerializeField] private MapParametersSO mapParametersSO;
    [SerializeField] public bool showGizmos = true;
    [SerializeField] public bool showRoomIds = true;

    private MapData currentMapData;
    private System.Random rng;

    [Header("Tilemap Settings")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap foliageTilemap;
    [SerializeField] private TileBase[] floorTiles;
    [SerializeField] private TileBase[] wallTiles;
    [SerializeField] private TileBase[] foliageTiles;

    [Header("Foliage Settings")]
    [SerializeField][Range(0f,1f)] private float foliageSpawnChance = 0.2f;
    [SerializeField] private bool spawnFoliageInRooms = true;
    [SerializeField] private bool spawnFoliageInCorridors = true;

    [Header("Teleporter Settings")]
    [SerializeField] private GameObject teleporterPrefab;

    [Header("Enemy Spawning")]
    [SerializeField] private GameObject enemySpawnerPrefab; // Assign your EnemySpawner prefab in inspector
    [SerializeField] private bool spawnEnemySpawnersInMainRooms = false;
    [SerializeField] private float spawnerOffsetFromCenter = 0f; // Optional offset from exact center
    [SerializeField] private float minDistanceBetweenSpawners = 5f;

    [Header("Spawner Respawn")]
    [SerializeField] private bool enableSpawnerRespawn = true;
    private List<SpawnerExclusionZone> spawnerExclusionZones = new List<SpawnerExclusionZone>();

    [Header("Key Spawning")]
    [SerializeField] private GameObject keyPrefab;
    [SerializeField] private bool hasKeyBeenSpawned = false; // Ensures only one key spawns
    [SerializeField] private bool shouldKeyBeSpawned = true;

    [Header("Relic Spawning")]
    [Tooltip("Array of relic prefabs (Relic_1, Relic_2, etc.)")]
    public GameObject[] relicPrefabs; // Assign your relic prefab variants here

    [Tooltip("How many rooms before moving to the next relic type")]
    public int roomsPerRelicType = 3; // First 3 rooms = relic_1, next 3 = relic_2, etc.

    public float relicSpawnChance = 0.25f;

    [Tooltip("Height offset for relic spawn position (above floor)")]
    public float relicSpawnHeight = 1f;


    // Add this field at the top of your dungeon generator class
    [Header("Game Mode Integration")]
    [Tooltip("If false, will wait for external call instead of generating on Start")]
    public bool generateOnStart = false;

    [SerializeField] private TilemapShadowCaster2D shadowCaster; // for shadows
    [SerializeField] private ChunkedGorePainter gorePainter; // for blood splatter effects

    // Room ID counters for each category
    private int mainRoomIdCounter = RoomIDCategories.MAIN_ROOM_START;
    private int leafRoomIdCounter = RoomIDCategories.LEAF_ROOM_START;
    private int distributiveRoomIdCounter = RoomIDCategories.DISTRIBUTIVE_ROOM_START;
    private int cornerRoomIdCounter = RoomIDCategories.CORNER_ROOM_START;
    private int keyRoomId = -1;

    void Start()
    {
        // Only generate if not being controlled by CasualGameModeManager
        if (generateOnStart)
        {
            GenerateNewMap();
        }
    }

    [ContextMenu("Generate New Map")]
    public void GenerateNewMap()
    {
        if (mapParametersSO == null)
        {
            Debug.LogError("MapParametersSO is not assigned!");
            return;
        }
        GenerateNewMap(mapParametersSO.nodeCount);
    }

    // Called by RoguelikeManager with single int
    public void GenerateNewMap(int dungeonSize)
    {
        rng = new System.Random();

        EnemySpawner.ResetCthulhuEyeTracking();

        // Reset ID counters
        mainRoomIdCounter = RoomIDCategories.MAIN_ROOM_START;
        leafRoomIdCounter = RoomIDCategories.LEAF_ROOM_START;
        distributiveRoomIdCounter = RoomIDCategories.DISTRIBUTIVE_ROOM_START;
        cornerRoomIdCounter = RoomIDCategories.CORNER_ROOM_START;

        ResetKeySpawnStatus();

        // Set the nodeCount in parameters based on the single int value
        MapParameters runtimeParams = mapParametersSO.ToMapParameters();
        runtimeParams.nodeCount = dungeonSize;

        currentMapData = GenerateDungeon(runtimeParams);
        Debug.Log($"Generated dungeon with {currentMapData.rooms.Count} rooms");
        Debug.Log($"Start Room ID: {currentMapData.startRoomId}, End Room ID: {currentMapData.endRoomId}");
        Debug.Log($"Last Main Room ID: {currentMapData.lastMainRoomId} (Teleporter spawn location)");
        Debug.Log($"Floor tiles: {currentMapData.floorTiles.Count}, Wall tiles: {currentMapData.wallTiles.Count}");

        PrintRoomDebugInfo();
        SpawnAllEnemySpawners();
        PaintTiles(currentMapData);
        SpawnLevelObjects();
    }

    // Data-only generation (used by the showcase scene): seeded, no spawning, no painting.
    public MapData GenerateMapDataOnly(int seed, int nodeCount)
    {
        mainRoomIdCounter = RoomIDCategories.MAIN_ROOM_START;
        leafRoomIdCounter = RoomIDCategories.LEAF_ROOM_START;
        distributiveRoomIdCounter = RoomIDCategories.DISTRIBUTIVE_ROOM_START;
        cornerRoomIdCounter = RoomIDCategories.CORNER_ROOM_START;

        rng = new System.Random(seed);

        MapParameters runtimeParams = mapParametersSO.ToMapParameters();
        runtimeParams.nodeCount = nodeCount;
        return GenerateDungeon(runtimeParams);
    }

    private void PrintRoomDebugInfo()
    {
        Debug.Log("=== ROOM DEBUG INFO ===");
        foreach (var room in currentMapData.rooms.Values.OrderBy(r => r.uniqueId))
        {
            string connectionInfo = "";
            foreach (var connection in room.connections)
            {
                connectionInfo += $"→{connection.connectedRoomId}({connection.type}) ";
            }

            string categoryInfo = "";
            if (RoomIDCategories.IsMainRoom(room.uniqueId)) categoryInfo = "[MAIN]";
            else if (RoomIDCategories.IsLeafRoom(room.uniqueId)) categoryInfo = "[LEAF]";
            else if (RoomIDCategories.IsDistributiveRoom(room.uniqueId)) categoryInfo = "[DISTRIBUTIVE]";
            else if (RoomIDCategories.IsCornerRoom(room.uniqueId)) categoryInfo = "[CORNER]";

            Debug.Log($"Room ID: {room.uniqueId} {categoryInfo} | Type: {room.type} | Position: {room.worldPosition} | Size: {room.size} | Connections: {connectionInfo}");
        }
        Debug.Log("=== END ROOM DEBUG INFO ===");
    }
    
    private int GetNextRoomId(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.START_ROOM => mainRoomIdCounter++,
            RoomType.END_ROOM => mainRoomIdCounter++,
            RoomType.MAIN_ARTERY_ROOM => mainRoomIdCounter++,
            RoomType.LEAF_NODE_ROOM => leafRoomIdCounter++,
            RoomType.DISTRIBUTIVE_NODE_ROOM => distributiveRoomIdCounter++,
            RoomType.ARTERY_CORNER_ROOM => cornerRoomIdCounter++,
            _ => mainRoomIdCounter++
        };
    }

    public MapData GenerateDungeon(MapParameters param)
    {
        var mapData = new MapData();
        var mainPathIds = new List<int>();

        // NEW: Generate isolated start room FIRST
        var startRoom = CreateIsolatedStartRoom(param, mapData.rooms);
        mapData.startRoomId = startRoom.uniqueId;

        // Generate the main artery (now starts AFTER the start room)
        GenerateMainArtery(param, mapData.rooms, mainPathIds, startRoom);

        // Connect start room to first main artery room
        if (mainPathIds.Count > 0)
        {
            ConnectRooms(startRoom.uniqueId, mainPathIds[0], ConnectionType.ARTERY_PATH, mapData.rooms);
        }

        // Set end room ID
        if (mainPathIds.Count > 0)
        {
            mapData.endRoomId = mainPathIds[mainPathIds.Count - 1];
            mapData.lastMainRoomId = mapData.endRoomId;
            mapData.rooms[mapData.endRoomId].type = RoomType.END_ROOM;
        }

        // NEW: Validate minimum distance between start and end
        ValidateStartToEndDistance(param, mapData, startRoom);

        InsertDistributiveNodesAndSproutLeaves(param, mapData.rooms, mainPathIds);

        // Generate floor tiles (rooms + corridors)
        GenerateFloorTiles(param, mapData.rooms, mapData.floorTiles);

        // Generate walls around all floor areas
        GenerateWallTiles(mapData.floorTiles, mapData.wallTiles);

        return mapData;
    }

    /// <summary>
    /// Creates an isolated start room at origin with standard generation rules
    /// </summary>
    private Room CreateIsolatedStartRoom(MapParameters param, Dictionary<int, Room> rooms)
    {
        Vector2 startPosition = Vector2.zero;
        
        // Get size with variation applied (follows normal rules)
        Vector2Int size = GetRoomSizeForType(RoomType.START_ROOM, param);

        var startRoom = new Room
        {
            uniqueId = GetNextRoomId(RoomType.START_ROOM),
            worldPosition = startPosition,
            type = RoomType.START_ROOM,
            size = size
        };
        
        rooms[startRoom.uniqueId] = startRoom;
        
        Debug.Log($"Isolated Start Room created at {startPosition} with size {size}");
        
        return startRoom;
    }

    /// <summary>
    /// Validates that end room is far enough from start room, repositions if needed
    /// </summary>
    private void ValidateStartToEndDistance(MapParameters param, MapData mapData, Room startRoom)
    {
        Room endRoom = mapData.GetEndRoom();
        if (endRoom == null || startRoom == null) return;

        float currentDistance = Vector2.Distance(startRoom.worldPosition, endRoom.worldPosition);
        
        if (currentDistance < param.minStartToEndDistance)
        {
            Debug.LogWarning($"End room too close to start ({currentDistance} < {param.minStartToEndDistance}). Repositioning...");
            
            // Calculate direction away from start
            Vector2 directionFromStart = (endRoom.worldPosition - startRoom.worldPosition).normalized;
            
            // Move end room to minimum distance
            Vector2 newEndPosition = startRoom.worldPosition + directionFromStart * param.minStartToEndDistance;
            
            // Find valid position near the target
            endRoom.worldPosition = FindValidRoomPosition(newEndPosition, endRoom.size, param, mapData.rooms);
            
            float newDistance = Vector2.Distance(startRoom.worldPosition, endRoom.worldPosition);
            Debug.Log($"End room repositioned. New distance: {newDistance}");
        }
    }

    private void PaintTiles(MapData mapData)
    {
        if (floorTilemap == null || wallTilemap == null)
        {
            Debug.LogError("Tilemaps not assigned!");
            return;
        }

        if (floorTiles == null || floorTiles.Length == 0 || wallTiles == null || wallTiles.Length < 18)
        {
            Debug.LogError("Tiles not assigned! Wall tiles array must have 18 entries.");
            return;
        }

        // Clear gore chunks before painting new tiles to prevent leftover gore from old map
        gorePainter.ClearAllChunks();
        // clear all shadows before painting new tiles to prevent leftover shadows from old map
        shadowCaster.ClearShadows();
        // Clear all tilemaps
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        foliageTilemap.ClearAllTiles();

        Debug.Log($"Painting {mapData.floorTiles.Count} floor tiles and {mapData.wallTiles.Count} wall tiles");

        // Create a set to track all positions where floor should be painted
        HashSet<Vector2Int> allFloorPositions = new HashSet<Vector2Int>(mapData.floorTiles);

        // Paint floor tiles under walls
        foreach (var wallPos in mapData.wallTiles)
        {
            allFloorPositions.Add(wallPos);
        }

        // Add randomized spill tiles around walls (minimum 1, up to 2-3 units outside)
        HashSet<Vector2Int> spillPositions = new HashSet<Vector2Int>();
        foreach (var wallPos in mapData.wallTiles)
        {
            // Random spill distance (2-3 units total, with minimum 1 guaranteed)
            int maxSpillDistance = UnityEngine.Random.Range(2, 4);
            
            // Try all four cardinal directions
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // North
                new Vector2Int(0, -1),  // South
                new Vector2Int(1, 0),   // East
                new Vector2Int(-1, 0)   // West
            };

            foreach (var direction in directions)
            {
                // Check if this direction leads away from the dungeon (no wall AND no floor)
                Vector2Int checkPos = wallPos + direction;
                if (!mapData.wallTiles.Contains(checkPos) && !mapData.floorTiles.Contains(checkPos))
                {
                    // This direction is outside, create spill
                    // First tile is ALWAYS placed (minimum 1 unit)
                    spillPositions.Add(checkPos);
                    
                    // Then randomly continue for 1-2 more tiles
                    for (int i = 2; i <= maxSpillDistance; i++)
                    {
                        Vector2Int spillPos = wallPos + (direction * i);
                        
                        // Random chance to stop early for irregular edges (70% chance to continue)
                        if (UnityEngine.Random.value < 0.3f)
                            break;
                        
                        spillPositions.Add(spillPos);
                    }
                }
            }
        }

        // Add spill positions to all floor positions
        foreach (var spillPos in spillPositions)
        {
            allFloorPositions.Add(spillPos);
        }

        // Paint all floor tiles (original floors + under walls + spill)
        foreach (var floorPos in allFloorPositions)
        {
            floorTilemap.SetTile((Vector3Int)floorPos, GetRandomFloorTile());

            if (foliageTilemap != null && ShouldSpawnFoliage(floorPos, mapData))
            {
                foliageTilemap.SetTile((Vector3Int)floorPos, GetRandomFoliageTile());
            }
        }

        // Paint wall tiles with autotiling logic (painted AFTER floors so they appear on top)
        foreach (var wallPos in mapData.wallTiles)
        {
            TileBase selectedWallTile = GetWallTileForPosition(wallPos, mapData.floorTiles, mapData.wallTiles);
            wallTilemap.SetTile((Vector3Int)wallPos, selectedWallTile);
        }

        shadowCaster.RegenerateShadows();

        Debug.Log("Tile painting complete");
    }

    public TileBase GetWallTileFor(Vector2Int pos, HashSet<Vector2Int> floorTiles, HashSet<Vector2Int> wallTiles)
        => GetWallTileForPosition(pos, floorTiles, wallTiles);

    private TileBase GetWallTileForPosition(Vector2Int pos, HashSet<Vector2Int> floorTiles, HashSet<Vector2Int> wallTiles)
    {
        if (this.wallTiles == null || this.wallTiles.Length < 18)
            return null;
        
        // Check wall neighbors (cardinal directions)
        bool wallN = wallTiles.Contains(new Vector2Int(pos.x, pos.y + 1));
        bool wallS = wallTiles.Contains(new Vector2Int(pos.x, pos.y - 1));
        bool wallE = wallTiles.Contains(new Vector2Int(pos.x + 1, pos.y));
        bool wallW = wallTiles.Contains(new Vector2Int(pos.x - 1, pos.y));
        
        // Check floor neighbors (cardinal directions)
        bool floorN = floorTiles.Contains(new Vector2Int(pos.x, pos.y + 1));
        bool floorS = floorTiles.Contains(new Vector2Int(pos.x, pos.y - 1));
        bool floorE = floorTiles.Contains(new Vector2Int(pos.x + 1, pos.y));
        bool floorW = floorTiles.Contains(new Vector2Int(pos.x - 1, pos.y));
        
        int wallCount = (wallN ? 1 : 0) + (wallS ? 1 : 0) + (wallE ? 1 : 0) + (wallW ? 1 : 0);
        
        // --- PRIORITY 1: Four walls (cross junction) ---
        if (wallCount == 4)
        {
            return this.wallTiles[14]; // Cross junction
        }
        
        // --- PRIORITY 2: Three walls (T-junctions) ---
        if (wallCount == 3)
        {
            if (!wallN) return this.wallTiles[6]; // T-junction up (missing north wall)
            if (!wallS) return this.wallTiles[7]; // T-junction down (missing south wall)
            if (!wallE) return this.wallTiles[8]; // T-junction left (missing east wall)
            if (!wallW) return this.wallTiles[9]; // T-junction right (missing west wall)
        }
        
        // --- PRIORITY 3: Two walls (corners and straight corridors) ---
        if (wallCount == 2)
        {
            // Adjacent walls (corners)
            if (wallN && wallE) return this.wallTiles[10]; // Bottom-left corner
            if (wallN && wallW) return this.wallTiles[11]; // Bottom-right corner
            if (wallS && wallE) return this.wallTiles[12]; // Top-left corner
            if (wallS && wallW) return this.wallTiles[13]; // Top-right corner
            
            // Opposite walls (straight corridors) - check floor to determine facing
            if (wallN && wallS)
            {
                if (floorE) return this.wallTiles[0]; // Vertical wall facing right (floor on east)
                if (floorW) return this.wallTiles[1]; // Vertical wall facing left (floor on west)
                return this.wallTiles[0]; // Default vertical
            }
            if (wallE && wallW)
            {
                if (floorN) return this.wallTiles[2]; // Horizontal wall facing up (floor on north)
                if (floorS) return this.wallTiles[3]; // Horizontal wall facing down (floor on south)
                return this.wallTiles[2]; // Default horizontal
            }
        }
        
        // --- PRIORITY 4: One wall (end caps) ---
        if (wallCount == 1)
        {
            if (wallN) return this.wallTiles[15]; // Cap facing down (wall above)
            if (wallS) return this.wallTiles[16]; // Cap facing up (wall below)
            if (wallE) return this.wallTiles[4]; // Cap facing left (wall to right)
            if (wallW) return this.wallTiles[5]; // Cap facing right (wall to left)
        }
        
        // --- FALLBACK: Isolated tile ---
        return this.wallTiles[17];
    }

    private bool ShouldSpawnFoliage(Vector2Int position, MapData mapData)
    {
        // Check if this position is in a room or corridor
        bool isInRoom = false;
        foreach (var room in mapData.rooms.Values)
        {
            if (IsPositionInRoom(position, room))
            {
                isInRoom = true;
                break;
            }
        }

        // Apply different spawn rules for rooms vs corridors
        if (isInRoom && !spawnFoliageInRooms) return false;
        if (!isInRoom && !spawnFoliageInCorridors) return false;

        // Random chance
        return UnityEngine.Random.value < foliageSpawnChance;
    }

    private bool IsPositionInRoom(Vector2Int position, Room room)
    {
        int minX = Mathf.RoundToInt(room.worldPosition.x - room.size.x / 2f);
        int maxX = Mathf.RoundToInt(room.worldPosition.x + room.size.x / 2f);
        int minY = Mathf.RoundToInt(room.worldPosition.y - room.size.y / 2f);
        int maxY = Mathf.RoundToInt(room.worldPosition.y + room.size.y / 2f);

        return position.x >= minX && position.x < maxX && 
            position.y >= minY && position.y < maxY;
    }

    private TileBase GetRandomFoliageTile()
    {
        if (foliageTiles == null || foliageTiles.Length == 0)
            return null;

        var validTiles = foliageTiles.Where(t => t != null).ToArray();
        if (validTiles.Length == 0) return null;

        return validTiles[UnityEngine.Random.Range(0, validTiles.Length)];
    }

    private Vector2Int GetRoomSizeForType(RoomType roomType, MapParameters param)
    {
        Vector2Int baseSize = roomType switch
        {
            RoomType.START_ROOM => param.baseStartRoomSize,
            RoomType.END_ROOM => param.baseEndRoomSize,
            RoomType.MAIN_ARTERY_ROOM => param.baseMainArteryRoomSize,
            RoomType.DISTRIBUTIVE_NODE_ROOM => param.baseDistributiveRoomSize,
            RoomType.LEAF_NODE_ROOM => param.baseLeafRoomSize,
            RoomType.ARTERY_CORNER_ROOM => param.baseCornerRoomSize,
            _ => new Vector2Int(3, 3)
        };

        int widthVariation = Mathf.RoundToInt(baseSize.x * param.roomSizeVariationPercentage * RandomRange(-1f, 1f));
        int heightVariation = Mathf.RoundToInt(baseSize.y * param.roomSizeVariationPercentage * RandomRange(-1f, 1f));

        Vector2Int finalSize = new Vector2Int(
            Mathf.Max(2, baseSize.x + widthVariation),
            Mathf.Max(2, baseSize.y + heightVariation)
        );

        // NEW: Random rotation for rectangular rooms (swap width/height)
        if (finalSize.x != finalSize.y && RandomValue() < param.roomRotationChance)
        {
            finalSize = new Vector2Int(finalSize.y, finalSize.x); // Swap dimensions
            Debug.Log($"Room rotated: {roomType} - New size: {finalSize}");
        }

        return finalSize;
    }

    private Room CreateRoom(Vector2 position, RoomType type, MapParameters param, Dictionary<int, Room> rooms)
    {
        var size = GetRoomSizeForType(type, param);
        var finalPosition = FindValidRoomPosition(position, size, param, rooms);
        var newRoom = new Room
        {
            uniqueId = GetNextRoomId(type),
            worldPosition = finalPosition,
            type = type,
            size = size
        };
        rooms[newRoom.uniqueId] = newRoom;

        if (shouldKeyBeSpawned)
        {
            // NEW: Spawn key in LeafNodeRoom (only once)
            if (type == RoomType.LEAF_NODE_ROOM)
            {
                SpawnKeyInRoom(newRoom);
            }
        }

        return newRoom;
    }

    private Vector2 FindValidRoomPosition(Vector2 desiredPosition, Vector2Int roomSize, MapParameters param, Dictionary<int, Room> rooms)
    {
        if (IsPositionValidForRoom(desiredPosition, roomSize, param.minRoomDistance, rooms))
        {
            return desiredPosition;
        }

        for (int attempt = 1; attempt <= param.maxRepositionAttempts; attempt++)
        {
            float radius = param.repositionSearchRadius * attempt;
            for (int angleStep = 0; angleStep < 8; angleStep++)
            {
                float angle = angleStep * Mathf.PI * 2 / 8;
                Vector2 testPosition = desiredPosition + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);

                if (IsPositionValidForRoom(testPosition, roomSize, param.minRoomDistance, rooms))
                {
                    return testPosition;
                }
            }
        }

        return desiredPosition;
    }

    private bool IsPositionValidForRoom(Vector2 testPosition, Vector2Int testSize, float minDistance, Dictionary<int, Room> rooms)
    {
        var newRoomBounds = new Bounds(testPosition, new Vector3(testSize.x + minDistance * 2, testSize.y + minDistance * 2, 0));

        foreach (var existingRoom in rooms.Values)
        {
            var existingBounds = new Bounds(existingRoom.worldPosition,
                new Vector3(existingRoom.size.x + minDistance * 2, existingRoom.size.y + minDistance * 2, 0));

            if (newRoomBounds.Intersects(existingBounds))
            {
                return false;
            }
        }

        return true;
    }

    private void SpawnKeyInRoom(Room room)
    {
        // Only spawn key if we haven't spawned one yet and we have a key prefab
        if (hasKeyBeenSpawned || keyPrefab == null || room == null)
        {
            return;
        }

        // Calculate spawn position within the room bounds
        Vector3 keySpawnPosition = new Vector3(
            room.worldPosition.x + UnityEngine.Random.Range(-room.size.x * 0.3f, room.size.x * 0.3f),
            room.worldPosition.y + UnityEngine.Random.Range(-room.size.y * 0.3f, room.size.y * 0.3f),
            0f // Assuming 2D game
        );

        // Instantiate the key
        GameObject spawnedKey = Instantiate(keyPrefab, keySpawnPosition, Quaternion.identity);

        // Optional: Set parent for organization
        spawnedKey.transform.SetParent(transform);
        spawnedKey.name = $"Key_Room_{room.uniqueId}";

        // Mark that key has been spawned
        hasKeyBeenSpawned = true;

        // ADDED: Store which room the key spawned in
        keyRoomId = room.uniqueId;

        Debug.Log($"Key spawned in room {room.uniqueId} at position {keySpawnPosition}");
    }

    public void ResetKeySpawnStatus()
    {
        hasKeyBeenSpawned = false;
        Debug.Log("Key spawn status reset - ready to spawn new key");
    }

    /// <summary>
    /// Spawns an EnemySpawner in the center of the specified room
    /// </summary>
    /// <param name="room">The room to spawn the enemy spawner in</param>
    private void SpawnEnemySpawnerInRoom(Room room)
    {
        // Don't spawn if no prefab assigned
        if (enemySpawnerPrefab == null)
        {
            Debug.LogWarning("EnemySpawner prefab not assigned to DungeonMapGenerator!");
            return;
        }

        // Get all valid edge positions for this room (must have exactly 2 adjacent walls)
        List<Vector3> validSpawnPositions = GetValidEdgeSpawnPositions(room);

        if (validSpawnPositions.Count == 0)
        {
            Debug.LogWarning($"No valid edge spawn positions found in room {room.uniqueId}");
            return;
        }

        // Spawn multiple spawners at different valid edge positions
        int spawnersToCreate = Mathf.Min(UnityEngine.Random.Range(1, 4), validSpawnPositions.Count); // 1-3 spawners
        
        for (int i = 0; i < spawnersToCreate; i++)
        {
            // Find a valid position that's far enough from existing spawners
            Vector3? spawnerPosition = FindValidSpawnerPosition(validSpawnPositions);
            
            if (!spawnerPosition.HasValue)
            {
                Debug.Log($"Could not find valid spawner position in room {room.uniqueId} (too close to other spawners)");
                break; // No more valid positions
            }

            // Instantiate the enemy spawner at edge position
            GameObject spawnedSpawner = Instantiate(enemySpawnerPrefab, spawnerPosition.Value, Quaternion.identity);

            // Optional: Set up the spawner with room-specific settings
            EnemySpawner spawnerScript = spawnedSpawner.GetComponent<EnemySpawner>();
            if (spawnerScript != null)
            {
                ConfigureSpawnerForRoom(spawnerScript, room);
            }

            // Optional: Parent the spawner to a room container for organization
            OrganizeSpawnerInHierarchy(spawnedSpawner, room);

            Debug.Log($"Enemy spawner created in room {room.uniqueId} at edge position {spawnerPosition.Value}");
        }
    }

    /// <summary>
    /// Finds a valid spawner position from the list that respects minimum distance from other spawners
    /// Removes the chosen position from the list
    /// </summary>
    private Vector3? FindValidSpawnerPosition(List<Vector3> candidatePositions)
    {
        // Find all existing spawners in the scene
        EnemySpawner[] existingSpawners = FindObjectsOfType<EnemySpawner>();

        // Try each candidate position
        for (int i = candidatePositions.Count - 1; i >= 0; i--)
        {
            Vector3 candidatePos = candidatePositions[i];
            bool tooClose = false;

            // Check distance to all existing spawners
            foreach (var spawner in existingSpawners)
            {
                float distance = Vector3.Distance(candidatePos, spawner.transform.position);
                if (distance < minDistanceBetweenSpawners)
                {
                    tooClose = true;
                    break;
                }
            }

            // If this position is valid, use it and remove from list
            if (!tooClose)
            {
                candidatePositions.RemoveAt(i);
                return candidatePos;
            }
        }

        return null; // No valid position found
    }

    /// <summary>
    /// Finds valid spawn positions along room edges that have EXACTLY 2 adjacent walls
    /// </summary>
    private List<Vector3> GetValidEdgeSpawnPositions(Room room)
    {
        List<Vector3> validPositions = new List<Vector3>();

        int minX = Mathf.RoundToInt(room.worldPosition.x - room.size.x / 2f);
        int maxX = Mathf.RoundToInt(room.worldPosition.x + room.size.x / 2f);
        int minY = Mathf.RoundToInt(room.worldPosition.y - room.size.y / 2f);
        int maxY = Mathf.RoundToInt(room.worldPosition.y + room.size.y / 2f);

        // Check all positions along the room edges
        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);

                // Only consider edge tiles
                bool isEdge = (x == minX || x == maxX - 1 || y == minY || y == maxY - 1);
                if (!isEdge) continue;

                // Count adjacent walls
                int wallCount = CountAdjacentWalls(pos);

                // Valid ONLY if has EXACTLY 2 adjacent walls
                if (wallCount == 2)
                {
                    validPositions.Add(new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0f));
                }
            }
        }

        return validPositions;
    }

    /// <summary>
    /// Counts how many of the 4 cardinal directions (up, down, left, right) have walls
    /// </summary>
    private int CountAdjacentWalls(Vector2Int position)
    {
        if (currentMapData == null || currentMapData.wallTiles == null)
            return 0;

        int wallCount = 0;

        // Check 4 cardinal directions only
        Vector2Int[] cardinalDirections = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // Up
            new Vector2Int(0, -1),  // Down
            new Vector2Int(-1, 0),  // Left
            new Vector2Int(1, 0)    // Right
        };

        foreach (var direction in cardinalDirections)
        {
            Vector2Int checkPos = position + direction;
            if (currentMapData.wallTiles.Contains(checkPos))
            {
                wallCount++;
            }
        }

        return wallCount;
    }

    /// <summary>
    /// Configure spawner settings based on room properties
    /// </summary>
    private void SpawnAllEnemySpawners()
    {
        if (currentMapData == null) return;

        // Spawn in ALL rooms except start and end
        foreach (var room in currentMapData.rooms.Values)
        {
            // Skip start and end rooms
            if (room.uniqueId == currentMapData.startRoomId || room.uniqueId == currentMapData.endRoomId)
                continue;
                
            SpawnEnemySpawnerInRoom(room);
        }
    }

    /// <param name="spawner">The spawner component to configure</param>
    /// <param name="room">The room containing the spawner</param>
    private void ConfigureSpawnerForRoom(EnemySpawner spawner, Room room)
    {
        // Example configurations - adjust based on your game design

        // Adjust spawn radius based on room size
        float roomSizeMultiplier = Mathf.Max(room.size.x, room.size.y) / 10f; // Adjust divisor as needed
        spawner.spawnRadius = Mathf.Clamp(roomSizeMultiplier * 3f, 2f, 8f); // Min 2, Max 8

        // Adjust max enemies based on room size
        int baseEnemies = 8;
        int roomSizeBonus = Mathf.RoundToInt(roomSizeMultiplier * 2f);
        spawner.maxEnemies = baseEnemies + roomSizeBonus;

        // Adjust spawn rate (optional - make larger rooms spawn faster/slower)
        spawner.spawnInterval = UnityEngine.Random.Range(1f, 3f); // Random spawn rate per room

        spawner.SetDungeonLevel(mapParametersSO.nodeCount);

        Debug.Log($"Configured spawner in room {room.uniqueId}: radius={spawner.spawnRadius}, maxEnemies={spawner.maxEnemies}, dungeonLevel={mapParametersSO.nodeCount}");
    }

    /// <summary>
    /// Organize spawner in hierarchy for better scene management
    /// </summary>
    /// <param name="spawner">The spawner GameObject</param>
    /// <param name="room">The room containing the spawner</param>
    private void OrganizeSpawnerInHierarchy(GameObject spawner, Room room)
    {
        // Find or create a container for spawners
        GameObject spawnersContainer = GameObject.Find("EnemySpawners");
        if (spawnersContainer == null)
        {
            spawnersContainer = new GameObject("EnemySpawners");
        }

        // Set spawner as child of container
        spawner.transform.SetParent(spawnersContainer.transform);

        // Rename for easier identification
        spawner.name = $"EnemySpawner_Room_{room.uniqueId}_{room.type}";

        // NEW: Set dungeon generator reference
        EnemySpawner spawnerScript = spawner.GetComponent<EnemySpawner>();
        if (spawnerScript != null)
        {
            spawnerScript.SetDungeonGenerator(this);
        }
    }

    /// <summary>
    /// Called by EnemySpawner when it dies and wants to respawn
    /// </summary>
    public void RequestSpawnerRespawn(Vector3 deadSpawnerPosition, float exclusionRadius, float respawnDelay)
    {
        if (!enableSpawnerRespawn) return;

        // Add exclusion zone (temporary, expires after respawn delay * 2)
        float expirationTime = Time.time + (respawnDelay * 2f);
        spawnerExclusionZones.Add(new SpawnerExclusionZone(deadSpawnerPosition, exclusionRadius, expirationTime));

        // Start respawn coroutine
        StartCoroutine(RespawnSpawnerAfterDelay(respawnDelay));
    }

    private IEnumerator RespawnSpawnerAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Clean up expired exclusion zones
        spawnerExclusionZones.RemoveAll(zone => Time.time > zone.expirationTime);

        // Try to find a valid respawn position
        Vector3? respawnPosition = FindValidRespawnPosition();

        if (respawnPosition.HasValue)
        {
            // Find which room this position is in
            Room targetRoom = null;
            foreach (var room in currentMapData.rooms.Values)
            {
                if (IsPositionInRoom(new Vector2Int(Mathf.RoundToInt(respawnPosition.Value.x), 
                                                    Mathf.RoundToInt(respawnPosition.Value.y)), room))
                {
                    targetRoom = room;
                    break;
                }
            }

            if (targetRoom != null)
            {
                // Spawn new spawner
                GameObject spawnedSpawner = Instantiate(enemySpawnerPrefab, respawnPosition.Value, Quaternion.identity);

                EnemySpawner spawnerScript = spawnedSpawner.GetComponent<EnemySpawner>();
                if (spawnerScript != null)
                {
                    ConfigureSpawnerForRoom(spawnerScript, targetRoom);
                }

                OrganizeSpawnerInHierarchy(spawnedSpawner, targetRoom);

                Debug.Log($"Spawner respawned at position {respawnPosition.Value}");
            }
        }
        else
        {
            Debug.LogWarning("Could not find valid respawn position for spawner!");
        }
    }

    /// <summary>
    /// Finds a valid position to respawn a spawner, avoiding exclusion zones and other spawners
    /// </summary>
    private Vector3? FindValidRespawnPosition()
    {
        List<Vector3> allValidPositions = new List<Vector3>();

        // Gather all valid edge positions from all rooms
        foreach (var room in currentMapData.rooms.Values)
        {
            // Skip start/end rooms
            if (room.uniqueId == currentMapData.startRoomId || room.uniqueId == currentMapData.endRoomId)
                continue;

            List<Vector3> roomPositions = GetValidEdgeSpawnPositions(room);
            allValidPositions.AddRange(roomPositions);
        }

        // Filter out positions that are too close to exclusion zones or existing spawners
        List<Vector3> validRespawnPositions = new List<Vector3>();
        EnemySpawner[] existingSpawners = FindObjectsOfType<EnemySpawner>();

        foreach (var position in allValidPositions)
        {
            bool isValid = true;

            // Check against exclusion zones
            foreach (var zone in spawnerExclusionZones)
            {
                if (Vector3.Distance(position, zone.position) < zone.radius)
                {
                    isValid = false;
                    break;
                }
            }

            if (!isValid) continue;

            // Check against existing spawners
            foreach (var spawner in existingSpawners)
            {
                if (Vector3.Distance(position, spawner.transform.position) < minDistanceBetweenSpawners)
                {
                    isValid = false;
                    break;
                }
            }

            if (isValid)
            {
                validRespawnPositions.Add(position);
            }
        }

        // Return random valid position, or null if none found
        if (validRespawnPositions.Count > 0)
        {
            return validRespawnPositions[UnityEngine.Random.Range(0, validRespawnPositions.Count)];
        }

        return null;
    }

    private void ConnectRooms(int room1Id, int room2Id, ConnectionType connectionType, Dictionary<int, Room> rooms)
    {
        if (rooms.ContainsKey(room1Id) && rooms.ContainsKey(room2Id))
        {
            var room1 = rooms[room1Id];
            var room2 = rooms[room2Id];

            var connection1 = new RoomConnection(room2Id, connectionType);
            var connection2 = new RoomConnection(room1Id, connectionType);

            if (!room1.connections.Any(c => c.connectedRoomId == room2Id))
                room1.connections.Add(connection1);

            if (!room2.connections.Any(c => c.connectedRoomId == room1Id))
                room2.connections.Add(connection2);
        }
    }

    private void GenerateMainArtery(MapParameters param, Dictionary<int, Room> rooms, List<int> mainPathIds, Room startRoom)
    {
        // Calculate starting position for main artery (away from start room)
        Vector2 currentPos = startRoom.worldPosition + Vector2.right * (startRoom.size.x / 2f + param.startRoomToArteryDistance);
        Vector2 currentDirection = Vector2.right;

        // Create the FIRST main artery room (no longer the start room)
        var firstMainRoom = CreateRoom(currentPos, RoomType.MAIN_ARTERY_ROOM, param, rooms);
        mainPathIds.Add(firstMainRoom.uniqueId);

        int roomCount = param.nodeCount; // This now controls ONLY main artery rooms

        // Generate remaining main artery rooms
        for (int i = 1; i < roomCount; i++)
        {
            bool isLTurn = RandomValue() < param.chanceForLTurn;
            if (isLTurn && i < roomCount - 1)
            {
                currentPos += currentDirection * param.mainRoomSpacing;
                currentPos += RandomJitterVector(param.mainArteryPositionJitter);
                var cornerRoom = CreateRoom(currentPos, RoomType.ARTERY_CORNER_ROOM, param, rooms);
                ConnectRooms(mainPathIds.Last(), cornerRoom.uniqueId, ConnectionType.ARTERY_PATH, rooms);
                mainPathIds.Add(cornerRoom.uniqueId);
                currentDirection = GetRandomPerpendicularDirection(currentDirection);
            }

            currentPos += currentDirection * param.mainRoomSpacing;
            currentPos += RandomJitterVector(param.mainArteryPositionJitter);
            var mainRoom = CreateRoom(currentPos, RoomType.MAIN_ARTERY_ROOM, param, rooms);
            ConnectRooms(mainPathIds.Last(), mainRoom.uniqueId, ConnectionType.ARTERY_PATH, rooms);
            mainPathIds.Add(mainRoom.uniqueId);
        }
    }

    private void InsertDistributiveNodesAndSproutLeaves(MapParameters param, Dictionary<int, Room> rooms, List<int> mainPathIds)
    {
        // Skip the last segment (don't insert distributive nodes before the end room)
        for (int i = 0; i < mainPathIds.Count - 2; i++)
        {
            if (RandomValue() < param.distributiveNodeChancePerSegment)
            {
                var room1 = rooms[mainPathIds[i]];
                var room2 = rooms[mainPathIds[i + 1]];

                Vector2 midPos = Vector2.Lerp(room1.worldPosition, room2.worldPosition, 0.5f);
                var distributiveRoom = CreateRoom(midPos, RoomType.DISTRIBUTIVE_NODE_ROOM, param, rooms);

                room1.connections.RemoveAll(c => c.connectedRoomId == room2.uniqueId);
                room2.connections.RemoveAll(c => c.connectedRoomId == room1.uniqueId);

                ConnectRooms(room1.uniqueId, distributiveRoom.uniqueId, ConnectionType.ARTERY_PATH, rooms);
                ConnectRooms(distributiveRoom.uniqueId, room2.uniqueId, ConnectionType.ARTERY_PATH, rooms);

                int leafCount = RandomRangeInt(param.minLeafNodesPerDistributive, param.maxLeafNodesPerDistributive);
                for (int j = 0; j < leafCount; j++)
                {
                    float angle = j * (2 * Mathf.PI / leafCount) + RandomRange(0, Mathf.PI / 4);
                    SproutLeafNode(distributiveRoom, angle, param, rooms);
                }
            }
        }
    }

    private void SproutLeafNode(Room fromNode, float initialAngle, MapParameters param, Dictionary<int, Room> rooms)
    {
        Vector2 direction = new Vector2(Mathf.Cos(initialAngle), Mathf.Sin(initialAngle));
        Vector2 leafPos = fromNode.worldPosition + direction * param.leafBranchLength;
        leafPos += RandomJitterVector(param.leafNodePositionJitter);

        var leafRoom = CreateRoom(leafPos, RoomType.LEAF_NODE_ROOM, param, rooms);
        ConnectRooms(fromNode.uniqueId, leafRoom.uniqueId, ConnectionType.VEIN_PATH, rooms);
    }

    private TileBase GetRandomFloorTile()
    {
        // Check if array exists and has elements
        if (floorTiles == null || floorTiles.Length == 0)
        {
            Debug.LogError("Floor tiles array is null or empty! Please assign floor tiles in the inspector.");
            return null;
        }

        // Filter out null tiles to avoid errors
        var validTiles = new List<TileBase>();
        for (int i = 0; i < floorTiles.Length; i++)
        {
            if (floorTiles[i] != null)
            {
                validTiles.Add(floorTiles[i]);
            }
        }

        // Check if we have any valid tiles
        if (validTiles.Count == 0)
        {
            Debug.LogError("No valid floor tiles found! All tiles in array are null.");
            return null;
        }

        // Return random valid tile
        return validTiles[UnityEngine.Random.Range(0, validTiles.Count)];
    }

    private void GenerateFloorTiles(MapParameters param, Dictionary<int, Room> rooms, HashSet<Vector2Int> floorTiles)
    {
        floorTiles.Clear();

        // Add all room tiles
        foreach (var room in rooms.Values)
        {
            AddRoomTiles(room, floorTiles);
        }

        // Add simple corridor tiles
        var processedConnections = new HashSet<string>();
        foreach (var room in rooms.Values)
        {
            foreach (var connection in room.connections)
            {
                string connectionKey = $"{Mathf.Min(room.uniqueId, connection.connectedRoomId)}-{Mathf.Max(room.uniqueId, connection.connectedRoomId)}";
                if (processedConnections.Contains(connectionKey)) continue;
                processedConnections.Add(connectionKey);

                var connectedRoom = rooms[connection.connectedRoomId];
                AddCorridorTiles(room.worldPosition, connectedRoom.worldPosition, param.corridorWidth, floorTiles);
            }
        }
    }

    private void AddRoomTiles(Room room, HashSet<Vector2Int> floorTiles)
    {
        int minX = Mathf.RoundToInt(room.worldPosition.x - room.size.x / 2f);
        int maxX = Mathf.RoundToInt(room.worldPosition.x + room.size.x / 2f);
        int minY = Mathf.RoundToInt(room.worldPosition.y - room.size.y / 2f);
        int maxY = Mathf.RoundToInt(room.worldPosition.y + room.size.y / 2f);

        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                floorTiles.Add(new Vector2Int(x, y));
            }
        }
    }

    private void AddCorridorTiles(Vector2 start, Vector2 end, int width, HashSet<Vector2Int> floorTiles)
    {
        var startInt = new Vector2Int(Mathf.RoundToInt(start.x), Mathf.RoundToInt(start.y));
        var endInt = new Vector2Int(Mathf.RoundToInt(end.x), Mathf.RoundToInt(end.y));

        var lineTiles = GetLineTiles(startInt, endInt);

        // Determine corridor direction
        bool isHorizontal = Mathf.Abs(end.x - start.x) >= Mathf.Abs(end.y - start.y);

        foreach (var tile in lineTiles)
        {
            if (isHorizontal)
            {
                // Horizontal corridor: expand up/down only
                for (int y = 0; y < width; y++)
                {
                    floorTiles.Add(new Vector2Int(tile.x, tile.y + y - width/2));
                }
            }
            else
            {
                // Vertical corridor: expand left/right only
                for (int x = 0; x < width; x++)
                {
                    floorTiles.Add(new Vector2Int(tile.x + x - width/2, tile.y));
                }
            }
        }
    }

    private List<Vector2Int> GetLineTiles(Vector2Int start, Vector2Int end)
    {
        var tiles = new List<Vector2Int>();
        int dx = Mathf.Abs(end.x - start.x);
        int dy = Mathf.Abs(end.y - start.y);
        int x = start.x;
        int y = start.y;

        int x_inc = (end.x > start.x) ? 1 : -1;
        int y_inc = (end.y > start.y) ? 1 : -1;
        int error = dx - dy;

        for (int n = dx + dy; n > 0; n--)
        {
            tiles.Add(new Vector2Int(x, y));

            if (error > 0)
            {
                x += x_inc;
                error -= 2 * dy;
            }
            else
            {
                y += y_inc;
                error += 2 * dx;
            }
        }

        return tiles;
    }

    private void GenerateWallTiles(HashSet<Vector2Int> floorTiles, HashSet<Vector2Int> wallTiles)
    {
        wallTiles.Clear();

        // For each floor tile, check its neighbors
        foreach (var floorTile in floorTiles)
        {
            // Check all 8 directions around each floor tile
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue; // Skip the center tile

                    Vector2Int neighborTile = new Vector2Int(floorTile.x + x, floorTile.y + y);

                    // If this neighbor is not a floor tile, it should be a wall
                    if (!floorTiles.Contains(neighborTile))
                    {
                        wallTiles.Add(neighborTile);
                    }
                }
            }
        }
    }

    private void SpawnTeleporter()
    {
        if (currentMapData == null || teleporterPrefab == null)
        {
            Debug.LogWarning("SpawnTeleporter aborted: currentMapData or teleporterPrefab is null.");
            return;
        }

        // Clear existing teleporters in the scene
        Teleporter[] existingTeleporters = FindObjectsOfType<Teleporter>(true);
        foreach (var teleporter in existingTeleporters)
        {
            if (teleporter.gameObject.scene.IsValid()) // Avoid deleting prefab from Project
            {
                if (Application.isPlaying)
                    Destroy(teleporter.gameObject);
                else
                    DestroyImmediate(teleporter.gameObject);
            }
        }

        // Wait one frame to ensure cleanup before spawning
        if (teleporterPrefab != null)
        {
            StartCoroutine(SpawnTeleporterDelayed());
        }
        else
        {
            Debug.LogError("Teleporter prefab became null before coroutine could start.");
        }
    }

    private IEnumerator SpawnTeleporterDelayed()
    {
        yield return null; // Wait one frame to ensure objects are destroyed

        Room teleporterRoom = currentMapData.GetLastMainRoom();
        if (teleporterRoom != null)
        {
            GameObject teleporterInstance = Instantiate(teleporterPrefab);
            Teleporter teleporterComponent = teleporterInstance.GetComponent<Teleporter>();

            if (teleporterComponent != null)
            {
                teleporterComponent.SpawnInRoom(teleporterRoom);
                teleporterComponent.SetMapGenerator(this);
                Debug.Log($" Teleporter spawned in last main room (ID: {teleporterRoom.uniqueId}) at position {teleporterRoom.worldPosition}");
            }
            else
            {
                Debug.LogError(" Teleporter prefab must have a Teleporter component!");
            }
        }
        else
        {
            Debug.LogError(" Could not find last main room for teleporter spawning!");
        }
    }

    private void SpawnLevelObjects()
    {
        if (currentMapData == null) return;

        // Spawn teleporter in the last main room
        SpawnTeleporter();

        SpawnRelics();
    }

    private void SpawnRelics()
    {
        // Make sure we have relics to spawn
        if (relicPrefabs == null || relicPrefabs.Length == 0)
        {
            Debug.LogWarning("No relic prefabs assigned!");
            return;
        }

        // Make sure we have map data
        if (currentMapData == null || currentMapData.rooms == null)
        {
            Debug.LogWarning("No room data available for relic spawning!");
            return;
        }

        // Counter for which relic type to spawn (increments for each valid room)
        int relicCounter = 0;

        // Loop through each room in the dictionary
        foreach (var roomEntry in currentMapData.rooms)
        {
            Room room = roomEntry.Value; // Get the Room from the dictionary

            // Only spawn in side rooms - CHANGE THIS to match your actual room type
            if (room.type == RoomType.LEAF_NODE_ROOM) 
            {
                // Try to spawn a relic in this room
                bool spawned = TrySpawnRelicInRoom(room, relicCounter);

                // Only increment counter if relic actually spawned
                if (spawned)
                {
                    relicCounter++;
                }
            }
        }
    }

    private bool TrySpawnRelicInRoom(Room room, int relicCounter)
    {
        // Make sure room is valid
        if (room == null)
        {
            return false;
        }

        // Don't spawn relics in the room where the key spawned
        if (room.uniqueId == keyRoomId)
        {
            Debug.Log($"Skipping relic spawn in room {room.uniqueId} (key room)");
            return false;
        }

       
        if (UnityEngine.Random.value > relicSpawnChance) return false;

        // Determine which relic should spawn based on the counter
        // First 3 valid rooms = relic 0, next 3 = relic 1, etc.
        int relicIndex = relicCounter / roomsPerRelicType;

        // Make sure we don't go beyond available relics
        if (relicIndex >= relicPrefabs.Length)
        {
            // If we run out of relic types, loop back to the first one
            relicIndex = relicIndex % relicPrefabs.Length;
        }

        // Calculate spawn position within the room bounds (random position)
        Vector3 relicSpawnPosition = new Vector3(
            room.worldPosition.x + UnityEngine.Random.Range(-room.size.x * 0.3f, room.size.x * 0.3f),
            room.worldPosition.y + UnityEngine.Random.Range(-room.size.y * 0.3f, room.size.y * 0.3f),
            0f // 2D game
        );

        // Instantiate the relic
        GameObject spawnedRelic = Instantiate(relicPrefabs[relicIndex], relicSpawnPosition, Quaternion.identity);

        // Optional: Set parent for organization
        spawnedRelic.transform.SetParent(transform);
        spawnedRelic.name = $"{relicPrefabs[relicIndex].name}_Room_{room.uniqueId}";

        Debug.Log($"{relicPrefabs[relicIndex].name} spawned in room {room.uniqueId} at position {relicSpawnPosition}");

        return true;
    }

    // Helper method to check if a room ID is the last main room (for teleporter spawning)
    public bool IsLastMainRoom(int roomId)
    {
        return currentMapData != null && roomId == currentMapData.lastMainRoomId;
    }

    // Helper method to get the teleporter spawn room
    public Room GetTeleporterSpawnRoom()
    {
        return currentMapData?.GetLastMainRoom();
    }

    // Utility Functions
    private float RandomValue() => (float)rng.NextDouble();
    private float RandomRange(float min, float max) => min + (float)rng.NextDouble() * (max - min);
    private int RandomRangeInt(int min, int max) => rng.Next(min, max + 1);

    private Vector2 RandomJitterVector(float maxJitter)
    {
        return new Vector2(RandomRange(-maxJitter, maxJitter), RandomRange(-maxJitter, maxJitter));
    }

    private Vector2 GetRandomPerpendicularDirection(Vector2 current)
    {
        return RandomValue() < 0.5f ? new Vector2(-current.y, current.x) : new Vector2(current.y, -current.x);
    }


    void OnDrawGizmos()
    {
        if (!showGizmos || currentMapData == null) return;

        // Draw rooms with special colors for start and end
        foreach (var room in currentMapData.rooms.Values)
        {
            Color roomColor = room.type switch
            {
                RoomType.START_ROOM => Color.cyan,           // Special color for start
                RoomType.END_ROOM => Color.magenta,          // Special color for end
                RoomType.MAIN_ARTERY_ROOM => Color.red,
                RoomType.DISTRIBUTIVE_NODE_ROOM => Color.blue,
                RoomType.LEAF_NODE_ROOM => Color.green,
                RoomType.ARTERY_CORNER_ROOM => Color.yellow,
                _ => Color.white
            };

            Gizmos.color = roomColor;
            Gizmos.DrawWireCube(new Vector3(room.worldPosition.x, room.worldPosition.y, 0),
                               new Vector3(room.size.x, room.size.y, 1));

            // Draw special markers for start and end rooms
            if (room.type == RoomType.START_ROOM)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(new Vector3(room.worldPosition.x, room.worldPosition.y, 0), 1f);
            }
            else if (room.type == RoomType.END_ROOM)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawSphere(new Vector3(room.worldPosition.x, room.worldPosition.y, 0), 1f);
            }
        }

        // Draw room IDs if enabled
        if (showRoomIds)
        {
            foreach (var room in currentMapData.rooms.Values)
            {
                Vector3 labelPos = new Vector3(room.worldPosition.x, room.worldPosition.y + room.size.y / 2f + 1f, 0);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(labelPos, $"ID: {room.uniqueId}");
#endif
            }
        }

        // Draw connections
        Gizmos.color = Color.white;
        var drawnConnections = new HashSet<string>();

        foreach (var room in currentMapData.rooms.Values)
        {
            foreach (var connection in room.connections)
            {
                string connectionKey = $"{Mathf.Min(room.uniqueId, connection.connectedRoomId)}-{Mathf.Max(room.uniqueId, connection.connectedRoomId)}";
                if (drawnConnections.Contains(connectionKey)) continue;
                drawnConnections.Add(connectionKey);

                var connectedRoom = currentMapData.rooms[connection.connectedRoomId];

                Gizmos.color = connection.type == ConnectionType.ARTERY_PATH ? Color.red : Color.green;

                Vector3 start = new Vector3(room.worldPosition.x, room.worldPosition.y, 0);
                Vector3 end = new Vector3(connectedRoom.worldPosition.x, connectedRoom.worldPosition.y, 0);

                Gizmos.DrawLine(start, end);
            }
        }
        // Draw spawner exclusion zones
        if (spawnerExclusionZones != null && spawnerExclusionZones.Count > 0)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // Semi-transparent red
            foreach (var zone in spawnerExclusionZones)
            {
                if (Time.time < zone.expirationTime)
                {
                    Gizmos.DrawWireSphere(zone.position, zone.radius);
                }
            }
        }
    }
    

    public MapData GetCurrentMapData() => currentMapData;
    public MapParameters GetParameters() => mapParametersSO?.ToMapParameters();
    public TileBase[] FloorTileAssets => floorTiles;
    public TileBase[] WallTileAssets => wallTiles;
    public TileBase[] FoliageTileAssets => foliageTiles;
}