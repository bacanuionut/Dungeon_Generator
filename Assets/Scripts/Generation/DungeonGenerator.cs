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

    // Root of the BSP tree.
    private BSPNode rootNode;

    // Cached final partitions.
    private List<BSPNode> leafNodes = new List<BSPNode>();

    // Rooms generated inside the final BSP leaf partitions.
    private List<Room> rooms = new List<Room>();

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

        UnityEngine.Debug.Log(
            $"Dungeon generated with seed {seed}. " +
            $"Created {leafNodes.Count} leaf partitions and " +
            $"{rooms.Count} rooms. " +
            $"Room validation: {(roomsValid ? "PASSED" : "FAILED")}."
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
}