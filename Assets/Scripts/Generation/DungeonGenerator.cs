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

    // Root of the BSP tree.
    private BSPNode rootNode;

    // Cached final partitions.
    private List<BSPNode> leafNodes = new List<BSPNode>();

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

        UnityEngine.Debug.Log(
            $"Dungeon generated with seed {seed}. " +
            $"Created {leafNodes.Count} leaf partitions."
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
    /// Draw the generated BSP partitions in Unity's Scene view.
    /// Gizmos are used so this debug visualisation does not become
    /// part of the actual game world.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showPartitions || leafNodes == null)
            return;

        Gizmos.color = Color.green;

        foreach (BSPNode node in leafNodes)
        {
            RectInt bounds = node.Bounds;

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
    }
}