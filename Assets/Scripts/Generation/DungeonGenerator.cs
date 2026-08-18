using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Main controller for procedural dungeon generation.
///
/// This class controls the overall generation process and exposes
/// generation parameters in the Unity Inspector.
///
/// Individual generation algorithms are kept in separate classes.
/// For Iteration 1, DungeonGenerator coordinates BSPNode objects
/// rather than performing the partition calculations itself.
/// </summary>
public class DungeonGenerator : MonoBehaviour
{
    [Header("Dungeon Size")]
    [SerializeField]
    [Min(20)]
    private int dungeonWidth = 80;

    [SerializeField]
    [Min(20)]
    private int dungeonHeight = 50;

    [Header("BSP Settings")]
    [SerializeField]
    [Min(5)]
    private int minimumPartitionSize = 10;

    [SerializeField]
    [Range(1, 10)]
    private int maximumDepth = 4;

    [Header("Room Settings")]
    [SerializeField]
    [Min(3)]
    private int minimumRoomSize = 5;

    [SerializeField]
    [Min(1)]
    private int roomPadding = 1;

    [Header("Random Generation")]
    [Tooltip("Using the same seed will reproduce the same dungeon.")]
    [SerializeField]
    private int seed = 12345;

    [Tooltip("Generate a new random seed each time generation starts.")]
    [SerializeField]
    private bool useRandomSeed = false;    

    [Header("Debug Display")]
    [Tooltip("Draw BSP partition boundaries in the Scene view.")]
    [SerializeField]
    private bool showPartitions = true;

    [Tooltip("Draw generated room boundaries in the Scene view.")]
    [SerializeField]
    private bool showRooms = true;

    [Tooltip("Draw logical room connections in the Scene view.")]
    [SerializeField]
    private bool showConnections = true;

    [Tooltip("Draw the generated physical corridor cells.")]
    [SerializeField]
    private bool showCorridors = true;

    [Tooltip("Draw the final combined walkable dungeon grid.")]
    [SerializeField]
    private bool showDungeonGrid = false;

    [Header("Rendering")]

    [Tooltip("Renderer used to display the generated dungeon.")]
    [SerializeField]
    private DungeonRenderer dungeonRenderer;

    [Header("Gameplay")]

    [Tooltip("Player that will be placed into the generated dungeon.")]
    [SerializeField]
    private PlayerController playerController;

    [Tooltip("Visual object used to show the generated dungeon exit.")]
    [SerializeField]
    private GameObject exitObject;

    // Gameplay state for the currently generated dungeon.
    private bool dungeonCompleted;
    private int completionMovementCount;

    public bool DungeonCompleted => dungeonCompleted;
    public int CompletionMovementCount => completionMovementCount;

    /// <summary>
    /// Provides read access to the final generated dungeon grid.
    ///
    /// Other systems such as player movement can query the dungeon,
    /// while DungeonGenerator remains responsible for creating it.
    /// </summary>
    public DungeonGrid Grid => dungeonGrid;

    // Unified grid representation of all walkable dungeon space.
    // This combines room and corridor cells after generation.
    private DungeonGrid dungeonGrid =
        new DungeonGrid();

    // Converts logical graph connections into physical grid-based paths.
    private CorridorGenerator corridorGenerator =
        new CorridorGenerator();

    // Physical corridors generated for the current dungeon.
    private List<CorridorGenerator.Corridor> corridors =
        new List<CorridorGenerator.Corridor>();

    // Logical representation of which generated rooms should be connected.
    private DungeonGraph dungeonGraph = new DungeonGraph();

    // Root of the BSP tree.
    private BSPNode rootNode;

    // Cached final partitions.
    private List<BSPNode> leafNodes = new List<BSPNode>();

    // Rooms generated inside the final BSP leaf partitions.
    private List<Room> rooms = new List<Room>();

    // Gameplay rooms selected after the dungeon graph has been created.
    private Room startRoom;
    public Room StartRoom => startRoom;

    private Room exitRoom;
    public Room ExitRoom => exitRoom;

    // Logical number of graph connections between start and exit.
    private int startToExitDistance;
    public int StartToExitDistance => startToExitDistance;

    // System.Random is used rather than UnityEngine.Random so each
    // generator can have its own reproducible random sequence.
    private System.Random random;

    private void Start()
    {
        GenerateDungeon();
    }

    private void Update()
    {
        // Allows quick testing without stopping Play Mode.
        // Press R to regenerate the dungeon.
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (useRandomSeed)
            {
                seed = Environment.TickCount;
            }

            GenerateDungeon();
        }
    }

    /// <summary>
    /// Creates the BSP tree for the current generation settings.
    /// </summary>
    public void GenerateDungeon()
    {
        dungeonCompleted = false;
        completionMovementCount = 0;

        // Stop immediately if the Inspector settings cannot produce
        // structurally valid rooms.
        if (!ValidateGenerationSettings())
        {
            return;
        }

        // A fixed seed creates the same sequence of random numbers,
        // allowing a generated dungeon to be reproduced exactly.
        random = new System.Random(seed);

        // The root node initially represents the entire dungeon.
        RectInt dungeonBounds = new RectInt(
            0,
            0,
            dungeonWidth,
            dungeonHeight
        );

        rootNode = new BSPNode(dungeonBounds, 0);

        // Recursively divide the dungeon.
        SplitRecursively(rootNode);

        // Cache the final partitions for later room generation.
        leafNodes.Clear();
        rootNode.GetLeafNodes(leafNodes);

        // Generate one room inside each final BSP partition.
        rooms.Clear();

        foreach (BSPNode leaf in leafNodes)
        {
            Room room = leaf.GenerateRoom(
                random,
                minimumRoomSize,
                roomPadding
            );

            if (room != null)
            {
                rooms.Add(room);
            }
        }

        bool roomsValid = ValidateRooms();

        // Build the logical room network only after valid rooms exist.
        dungeonGraph.Clear();

        if (roomsValid)
        {
            BuildDungeonGraph(rootNode);
        }

        // Validate the logical dungeon structure using graph traversal.
        bool graphConnected =
            roomsValid &&
            DungeonValidator.IsFullyConnected(
                rooms,
                dungeonGraph
            );

        // Gameplay placement depends on the validated room graph.
        if (graphConnected)
        {
            SelectStartAndExitRooms();
        }

        // Generate physical corridors only if the logical graph is valid.
        corridors.Clear();

        if (graphConnected)
        {
            corridors = corridorGenerator.GenerateCorridors(
                dungeonGraph,
                random
            );
        }

        // Validate the actual grid paths independently from the logical graph.
        bool corridorsValid =
            graphConnected &&
            DungeonValidator.ValidateCorridors(
                corridors,
                dungeonGraph
            );

        // Build the final walkable dungeon representation only after
        // the generated rooms and corridors have passed validation.
        dungeonGrid.Clear();

        if (roomsValid && graphConnected && corridorsValid)
        {
            dungeonGrid.Build(
                rooms,
                corridors
            );
        }

        // Validate that the final unified grid contains all generated
        // room and corridor cells.
        bool gridValid =
            roomsValid &&
            graphConnected &&
            corridorsValid &&
            DungeonValidator.ValidateDungeonGrid(
                dungeonGrid,
                rooms,
                corridors
            );

        bool playablePathValid =
            gridValid &&
            DungeonValidator.HasWalkablePath(
                dungeonGrid,
                GetPlayerSpawnPosition(),
                GetExitPosition()
            );

        // Render only a completely validated dungeon.
        if (gridValid && dungeonRenderer != null)
        {
            dungeonRenderer.Render(dungeonGrid);
        }

        if (gridValid)
        {
            PositionExit();
        }

        // Gameplay is initialised only after a completely valid dungeon
        // has been generated.
        if (gridValid && playerController != null)
        {
            playerController.InitialisePlayer();
        }

        UnityEngine.Debug.Log(
            $"Dungeon generated with seed {seed}. " +
            $"Created {leafNodes.Count} leaf partitions, " +
            $"{rooms.Count} rooms, " +
            $"{dungeonGraph.Connections.Count} logical connections and " +
            $"{corridors.Count} corridors. " +
            $"Floor cells: {dungeonGrid.FloorCellCount} " +
            $"(room cells: {dungeonGrid.RoomCellCount}, " +
            $"corridor cells: {dungeonGrid.CorridorCellCount}). " +
            $"Room validation: {(roomsValid ? "PASSED" : "FAILED")}. " +
            $"Connectivity: {(graphConnected ? "PASSED" : "FAILED")}. " +
            $"Corridor validation: {(corridorsValid ? "PASSED" : "FAILED")}." +
            $"Grid validation: {(gridValid ? "PASSED" : "FAILED")}." +
            $"Playable path: {(playablePathValid ? "PASSED" : "FAILED")}."
        );
    }

    /// <summary>
    /// Recursively splits nodes until maximumDepth is reached
    /// or the partition becomes too small to divide.
    /// </summary>
    private void SplitRecursively(BSPNode node)
    {
        if (node.Depth >= maximumDepth)
            return;

        if (!node.Split(random, minimumPartitionSize))
            return;

        SplitRecursively(node.LeftChild);
        SplitRecursively(node.RightChild);
    }

    /// <summary>
    /// Draws development/debug information in the Scene view.
    ///
    /// Green rectangles represent BSP leaf partitions.
    /// Yellow rectangles represent generated rooms.
    ///
    /// Gizmos allow us to inspect the algorithm without creating
    /// permanent rendering objects purely for debugging.
    /// </summary>
    private void OnDrawGizmos()
    {
        // -------------------------
        // Draw BSP leaf partitions
        // -------------------------

        if (showPartitions && leafNodes != null)
        {
            Gizmos.color = Color.green;

            foreach (BSPNode node in leafNodes)
            {
                DrawRectangle(node.Bounds);
            }
        }

        // -------------------------
        // Draw generated rooms
        // -------------------------

        if (showRooms && rooms != null)
        {
            Gizmos.color = Color.yellow;

            foreach (Room room in rooms)
            {
                DrawRectangle(room.Bounds);
            }
        }

        // -------------------------
        // Draw logical connections
        // -------------------------

        if (showConnections && dungeonGraph != null)
        {
            Gizmos.color = Color.cyan;

            foreach (DungeonGraph.RoomConnection connection
                     in dungeonGraph.Connections)
            {
                Vector2Int centreA = connection.RoomA.Centre;
                Vector2Int centreB = connection.RoomB.Centre;

                Vector3 start = new Vector3(
                    centreA.x,
                    centreA.y,
                    0f
                );

                Vector3 end = new Vector3(
                    centreB.x,
                    centreB.y,
                    0f
                );

                Gizmos.DrawLine(start, end);
            }
        }

        // -------------------------
        // Draw physical corridors
        // -------------------------

        if (showCorridors && corridors != null)
        {
            Gizmos.color = Color.magenta;

            foreach (CorridorGenerator.Corridor corridor in corridors)
            {
                foreach (Vector2Int cell in corridor.Cells)
                {
                    Vector3 centre = new Vector3(
                        cell.x + 0.5f,
                        cell.y + 0.5f,
                        0f
                    );

                    // Slightly smaller than one grid unit so individual
                    // corridor cells remain visible during debugging.
                    Vector3 size = new Vector3(
                        0.8f,
                        0.8f,
                        0f
                    );

                    Gizmos.DrawWireCube(
                        centre,
                        size
                    );
                }
            }
        }

        // -------------------------
        // Draw final dungeon grid
        // -------------------------

        if (showDungeonGrid && dungeonGrid != null)
        {
            Gizmos.color = Color.white;

            foreach (Vector2Int cell in dungeonGrid.FloorCells)
            {
                Vector3 centre = new Vector3(
                    cell.x + 0.5f,
                    cell.y + 0.5f,
                    0f
                );

                // Filled cubes make it easy to verify that rooms and
                // corridors have become one continuous floor representation.
                Gizmos.DrawCube(
                    centre,
                    new Vector3(0.9f, 0.9f, 0.01f)
                );
            }
        }
    }

    /// <summary>
    /// Helper method used by the debug visualisation so rectangle
    /// drawing logic is not duplicated for partitions and rooms.
    /// </summary>
    private void DrawRectangle(RectInt bounds)
    {
        Vector3 centre = new Vector3(
            bounds.x + bounds.width / 2f,
            bounds.y + bounds.height / 2f,
            0f
        );

        Vector3 size = new Vector3(
            bounds.width,
            bounds.height,
            0f
        );

        Gizmos.DrawWireCube(centre, size);
    }

    /// <summary>
    /// Performs basic checks on generated rooms.
    ///
    /// More extensive validation will later be moved into DungeonValidator,
    /// but this gives Iteration 1 an immediate automated correctness check.
    /// </summary>
    private bool ValidateRooms()
    {
        // Every final BSP partition is expected to contain exactly one room.
        // A difference in these counts means room generation was incomplete.
        if (rooms.Count != leafNodes.Count)
        {
            UnityEngine.Debug.LogError(
                $"Room validation failed. Generated {rooms.Count} rooms " +
                $"for {leafNodes.Count} leaf partitions."
            );

            return false;
        }

        foreach (Room room in rooms)
        {
            if (!room.IsInsideParentPartition())
            {
                UnityEngine.Debug.LogError(
                    $"Room {room.Bounds} extends outside its BSP partition " +
                    $"{room.ParentPartition.Bounds}."
                );

                return false;
            }

            if (room.Width < minimumRoomSize ||
                room.Height < minimumRoomSize)
            {
                UnityEngine.Debug.LogError(
                    $"Room {room.Bounds} is below the minimum room size."
                );

                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether the current generation settings are compatible.
    ///
    /// A BSP partition must be large enough to contain the minimum room
    /// size plus the requested padding on both sides.
    /// 
    /// Checking this before generation prevents the algorithm from running
    /// with settings that cannot guarantee a room in every leaf partition.
    /// </summary>
    private bool ValidateGenerationSettings()
    {
        int requiredPartitionSize =
            minimumRoomSize + (roomPadding * 2);

        if (minimumPartitionSize < requiredPartitionSize)
        {
            UnityEngine.Debug.LogError(
                $"Invalid generation settings. Minimum partition size " +
                $"is {minimumPartitionSize}, but at least " +
                $"{requiredPartitionSize} is required for a minimum room " +
                $"size of {minimumRoomSize} with padding {roomPadding}."
            );

            return false;
        }

        return true;
    }

    /// <summary>
    /// Recursively creates logical room connections using the BSP tree.
    ///
    /// At each branch of the tree, one room from the left subtree is
    /// connected to one room from the right subtree. Repeating this for
    /// every branch creates a connected structure across the dungeon.
    /// </summary>
    private void BuildDungeonGraph(BSPNode node)
    {
        if (node == null || node.IsLeaf())
        {
            return;
        }

        if (node.LeftChild != null &&
            node.RightChild != null)
        {
            Room leftRoom =
                node.LeftChild.GetRoomFromSubtree();

            Room rightRoom =
                node.RightChild.GetRoomFromSubtree();

            dungeonGraph.AddConnection(
                leftRoom,
                rightRoom
            );
        }

        // Process the rest of the BSP tree.
        BuildDungeonGraph(node.LeftChild);
        BuildDungeonGraph(node.RightChild);
    }

    /// <summary>
    /// Returns the centre of the selected gameplay start room.
    /// </summary>
    public Vector2Int GetPlayerSpawnPosition()
    {
        if (startRoom == null)
        {
            UnityEngine.Debug.LogWarning(
                "Cannot find player spawn because no start room has been selected."
            );

            return Vector2Int.zero;
        }

        return startRoom.Centre;
    }

    /// <summary>
    /// Selects gameplay start and exit rooms.
    ///
    /// The first generated room is used as the deterministic start room.
    /// BFS graph distances are then calculated and one of the furthest
    /// rooms is selected as the exit.
    ///
    /// This ensures the exit is logically separated from the start
    /// instead of being placed in an arbitrary nearby room.
    /// </summary>
    private void SelectStartAndExitRooms()
    {
        startRoom = null;
        exitRoom = null;
        startToExitDistance = 0;

        if (rooms == null ||
            rooms.Count == 0 ||
            dungeonGraph == null)
        {
            return;
        }

        startRoom = rooms[0];

        Dictionary<Room, int> distances =
            DungeonValidator.CalculateRoomDistances(
                startRoom,
                dungeonGraph
            );

        int furthestDistance = -1;

        foreach (KeyValuePair<Room, int> entry in distances)
        {
            // Never select the starting room as the exit.
            if (entry.Key == startRoom)
            {
                continue;
            }

            if (entry.Value > furthestDistance)
            {
                furthestDistance = entry.Value;
                exitRoom = entry.Key;
            }
        }

        if (exitRoom != null)
        {
            startToExitDistance = furthestDistance;

            UnityEngine.Debug.Log(
                $"Start room selected at {startRoom.Centre}. " +
                $"Exit room selected at {exitRoom.Centre}. " +
                $"Logical distance: {startToExitDistance}."
            );
        }
    }

    /// <summary>
    /// Moves the exit visual to the centre of the selected exit room.
    /// </summary>
    private void PositionExit()
    {
        if (exitObject == null || exitRoom == null)
        {
            return;
        }

        Vector2Int exitPosition =
            exitRoom.Centre;

        exitObject.transform.position =
            new Vector3(
                exitPosition.x + 0.5f,
                exitPosition.y + 0.5f,
                -2f
            );

        exitObject.SetActive(true);
    }

    /// <summary>
    /// Returns the grid coordinate used by the dungeon exit.
    /// </summary>
    public Vector2Int GetExitPosition()
    {
        if (exitRoom == null)
        {
            return Vector2Int.zero;
        }

        return exitRoom.Centre;
    }

    /// <summary>
    /// Marks the current dungeon as completed.
    ///
    /// The guard prevents completion from being recorded more than once
    /// if the player remains on or returns to the exit cell.
    /// </summary>
    public void CompleteDungeon(int movementCount)
    {
        if (dungeonCompleted)
        {
            return;
        }

        dungeonCompleted = true;
        completionMovementCount = movementCount;

        UnityEngine.Debug.Log(
            $"DUNGEON COMPLETE - Seed {seed}. " +
            $"Player reached the exit in {completionMovementCount} movements. " +
            $"Start-to-exit logical distance: {startToExitDistance}."
        );
    }
}