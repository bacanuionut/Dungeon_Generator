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

    [Header("Organic Room Post-Processing")]

    [Tooltip("Adds cellular-automata-inspired floor growth around BSP room edges.")]
    [SerializeField]
    private bool useOrganicRoomShapes = false;

    [Tooltip("Maximum number of cells the room shape can grow beyond its rectangular BSP core.")]
    [Range(1, 4)]
    [SerializeField]
    private int organicGrowthRadius = 2;

    [Tooltip("Initial probability that a candidate edge cell begins as floor.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float organicInitialGrowthChance = 0.50f;

    [Tooltip("Number of cellular smoothing passes.")]
    [Range(0, 5)]
    [SerializeField]
    private int organicSmoothingIterations = 2;

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

    [Header("Run Progression")]

    [SerializeField]
    private DungeonRunManager dungeonRunManager;

    [Header("Floor Objective")]

    [SerializeField]
    private FloorObjectiveManager floorObjectiveManager;

    [Header("Procedural Content")]

    [SerializeField]
    private DungeonContentGenerator dungeonContentGenerator;

    [Header("Procedural Environment")]

    [SerializeField]
    private DungeonEnvironmentGenerator dungeonEnvironmentGenerator;

    // When true, generation runs without rendering or gameplay setup.
    // This is used when evaluating many dungeon seeds automatically.
    private bool batchEvaluationMode;

    // Measurements calculated from the current generated dungeon.
    private DungeonMetrics.Result currentMetrics;

    public DungeonMetrics.Result CurrentMetrics =>
        currentMetrics;

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

    /// <summary>
    /// Read-only access to the generated rooms.
    /// Used by systems such as procedural content placement.
    /// </summary>
    public IReadOnlyList<Room> Rooms => rooms;

    public IReadOnlyList<CorridorGenerator.Corridor> Corridors =>
    corridors;

    /// <summary>
    /// Gives other systems access to the logical room graph
    /// without moving graph generation outside this class.
    /// </summary>
    public DungeonGraph Graph => dungeonGraph;

    /// <summary>
    /// The seed used for the current dungeon.
    /// Content generation can use this to remain reproducible.
    /// </summary>
    public int CurrentSeed => seed;


    /*
     * Logical depth of the current run floor.
     *
     * This is kept separately from GenerationVersion because regenerating
     * a floor for testing should not necessarily mean descending deeper.
     */
    private int currentFloorDepth = 1;

    public int CurrentFloorDepth =>
        currentFloorDepth;


    public int GenerationVersion { get; private set; }

    // Logical number of graph connections between start and exit.
    private int startToExitDistance;
    public int StartToExitDistance => startToExitDistance;

    // System.Random is used rather than UnityEngine.Random so each
    // generator can have its own reproducible random sequence.
    private System.Random random;

    public DungeonContentGenerator ContentGenerator => dungeonContentGenerator;

    public DungeonEnvironmentGenerator EnvironmentGenerator =>
        dungeonEnvironmentGenerator;


    /// <summary>
    /// Generates a run floor using both its deterministic seed and its
    /// logical depth.
    ///
    /// Keeping seed and depth separate is important because:
    ///
    /// - seed controls reproducible procedural generation;
    /// - depth can influence visual deterioration and future difficulty;
    /// - Survival mode can continue beyond a fixed number of floors.
    /// </summary>
    public void GenerateRunFloor(
        int floorSeed,
        int floorDepth)
    {
        seed =
            floorSeed;


        currentFloorDepth =
            Mathf.Max(
                1,
                floorDepth
            );


        // RunManager controls the seed sequence, so random Inspector
        // seed generation should not replace it.
        useRandomSeed =
            false;


        GenerateDungeon();
    }


    /// <summary>
    /// Temporary backwards-compatible overload.
    ///
    /// Existing systems which currently provide only a seed can continue
    /// working until DungeonRunManager is updated to also provide depth.
    /// </summary>
    public void GenerateRunFloor(
        int floorSeed)
    {
        GenerateRunFloor(
            floorSeed,
            currentFloorDepth
        );
    }

    public FloorObjectiveManager ObjectiveManager =>
    floorObjectiveManager;

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

        GenerationVersion++;

        dungeonCompleted = false;
        completionMovementCount = 0;

        // Remove procedural decoration from the previous generation.
        if (!batchEvaluationMode &&
            dungeonEnvironmentGenerator != null)
        {
            dungeonEnvironmentGenerator.ClearEnvironment();
        }

        // Remove gameplay content from the previous generation.
        if (!batchEvaluationMode &&
            dungeonContentGenerator != null)
        {
            dungeonContentGenerator.ClearContent();
        }

        if (!batchEvaluationMode &&
            floorObjectiveManager != null)
        {
            floorObjectiveManager.ClearObjectives();
        }

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

        if (graphConnected && startRoom != null && exitRoom != null)
        {
            RoomRoleAssigner.AssignRoles(
                rooms,
                dungeonGraph,
                startRoom,
                exitRoom
            );
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

        // Optional room-shape post-processing.
        //
        // This happens after the BSP rooms and corridors have been converted
        // to the common grid, but before final grid/playability validation.
        if (corridorsValid &&
            useOrganicRoomShapes)
        {
            // Keep the CA random sequence independent from the BSP and
            // gameplay-content random sequences.
            int organicSeed =
                unchecked(
                    seed * 613 +
                    104729
                );

            System.Random organicRandom =
                new System.Random(
                    organicSeed
                );


            HashSet<Vector2Int> organicCells =
                RoomShapePostProcessor.GenerateOrganicRoomCells(
                    rooms,
                    dungeonGrid,
                    dungeonWidth,
                    dungeonHeight,
                    organicRandom,
                    organicGrowthRadius,
                    organicInitialGrowthChance,
                    organicSmoothingIterations
                );


            dungeonGrid.AddOrganicRoomCells(
                organicCells
            );


            UnityEngine.Debug.Log(
                "ROOM SHAPE POST-PROCESSING\n" +
                $"Seed: {seed}\n" +
                $"Organic cells added: {dungeonGrid.OrganicRoomCellCount}\n" +
                $"Growth radius: {organicGrowthRadius}\n" +
                $"Smoothing iterations: {organicSmoothingIterations}"
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

        // Calculate quantitative measurements only when the final
        // gameplay representation has passed validation.
        currentMetrics = null;

        if (playablePathValid)
        {
            currentMetrics =
                DungeonMetrics.Calculate(
                    seed,
                    rooms,
                    dungeonGraph,
                    corridors,
                    dungeonGrid,
                    GetPlayerSpawnPosition(),
                    GetExitPosition(),
                    startToExitDistance
                );

            DungeonMetrics.LogResult(
                currentMetrics
            );
        }

        // Render only a completely validated dungeon.
        if (!batchEvaluationMode &&
            gridValid &&
            dungeonRenderer != null)
        {
            /*
             * Generate ONE deterministic visual theme for this newly generated
             * floor before constructing the visual meshes.
             *
             * This happens here rather than inside DungeonRenderer.Render()
             * because Render() is also called when the Shaper or Warden modifies
             * terrain during gameplay.
             *
             * Runtime terrain refreshes must preserve the existing theme.
             */
            dungeonRenderer.GenerateVisualTheme(
                seed,
                currentFloorDepth
            );


            dungeonRenderer.Render(
                dungeonGrid
            );
        }

        if (!batchEvaluationMode && gridValid)
        {
            PositionExit();
        }

        // Gameplay is initialised only after a completely valid dungeon
        // has been generated.
        if (!batchEvaluationMode && gridValid && playerController != null)
        {
            playerController.InitialisePlayer();
        }


        if (!batchEvaluationMode && playablePathValid && floorObjectiveManager != null)
        {
            floorObjectiveManager.GenerateObjectives(this);
        }

        // Environmental decoration is generated after objectives but before
        // normal gameplay content. This makes the environment establish the
        // navigation blockers first, so all subsequently generated enemies and
        // collectibles can reject prop-occupied cells through DungeonGrid.
        if (!batchEvaluationMode &&
            playablePathValid &&
            dungeonEnvironmentGenerator != null)
        {
            dungeonEnvironmentGenerator.GenerateEnvironment(this);
        }

        // Gameplay content is generated after environmental blockers exist.
        // DungeonContentGenerator therefore cannot place enemies or items on
        // solid decoration. Collectibles also keep a visual clearance from it.
        if (!batchEvaluationMode && playablePathValid && dungeonContentGenerator != null)
        {
            dungeonContentGenerator.GenerateContent(this);
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

    // Recursively splits nodes until maximumDepth is reached
    // or the partition becomes too small to divide.
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

        ExitHatchController hatch =
            exitObject.GetComponent<ExitHatchController>();

        if (hatch != null)
        {
            hatch.ResetForNewFloor();
        }

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

        // In normal gameplay, reaching the exit represents descending
        // to the next procedural floor rather than ending immediately.
        if (dungeonRunManager != null)
        {
            dungeonRunManager.CompleteCurrentFloor();
        }
    }

    /// <summary>
    /// Generates and evaluates one seed without rendering the dungeon
    /// or initialising gameplay.
    ///
    /// Used by automated batch testing.
    /// </summary>
    public DungeonMetrics.Result EvaluateSeed(int testSeed)
    {
        int previousSeed = seed;
        bool previousBatchMode = batchEvaluationMode;

        batchEvaluationMode = true;
        seed = testSeed;

        GenerateDungeon();

        DungeonMetrics.Result result =
            currentMetrics;

        // Restore the generator settings after evaluation.
        seed = previousSeed;
        batchEvaluationMode = previousBatchMode;

        return result;
    }

    /// <summary>
    /// Rebuilds only the dungeon visuals after runtime terrain has changed.
    ///
    /// Generation data, player state, enemies and objectives are left
    /// untouched.
    /// </summary>
    public void RefreshDungeonVisuals()
    {
        if (batchEvaluationMode ||
            dungeonRenderer == null ||
            dungeonGrid == null)
        {
            return;
        }

        dungeonRenderer.Render(dungeonGrid);
    }
}