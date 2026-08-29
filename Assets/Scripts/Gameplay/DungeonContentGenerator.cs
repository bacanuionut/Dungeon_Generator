using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Places gameplay content inside an already generated dungeon.
///
/// Placement is influenced by logical graph distance from the start
/// room so that later rooms can contain more enemies.
/// </summary>
public class DungeonContentGenerator : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private PlayerController playerController;

    [Header("Rendering")]

    [Tooltip("Shared unlit material used for generated dungeon content.")]
    [SerializeField]
    private Material unlitMaterial;

    [Header("Threat Budget")]

    [Tooltip("Base threat assigned to a normal Combat room.")]
    [SerializeField]
    private int baseCombatThreat = 2;

    [Tooltip("Extra threat added for every graph step from the start.")]
    [SerializeField]
    private int threatPerGraphStep = 1;

    [Tooltip("Additional threat assigned to Elite rooms.")]
    [SerializeField]
    private int eliteThreatBonus = 4;

    [Tooltip("Threat cost of the current basic enemy type.")]
    [SerializeField]
    private int basicEnemyThreatCost = 2;

    [Tooltip("Maximum number of enemies placed in one normal room.")]
    [SerializeField]
    private int maximumEnemiesPerRoom = 3;


    [Header("Item Placement")]

    [Tooltip("Chance that an eligible room contains an item.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float itemChance = 0.30f;


    [Header("Display")]

    [SerializeField]
    private Color enemyColour = Color.red;

    [SerializeField]
    private Color itemColour = Color.yellow;

    [SerializeField]
    private float enemyScale = 0.55f;

    [SerializeField]
    private float itemScale = 0.35f;


    private int totalThreatBudget;
    private int combatEnemiesGenerated;
    private int eliteEnemiesGenerated;
    private int rewardItemsGenerated;
    private int randomItemsGenerated;

    private int enemySequence;

    private GameObject contentParent;

    // Allows gameplay systems to find an item directly from a
    // dungeon-grid position without using physics collision.
    private readonly Dictionary<Vector2Int, GameObject> itemsByCell =
        new Dictionary<Vector2Int, GameObject>();


    private readonly List<GameObject> enemyObjects =
        new List<GameObject>();

    private readonly List<GameObject> itemObjects =
        new List<GameObject>();


    public IReadOnlyList<GameObject> EnemyObjects => enemyObjects;

    public IReadOnlyList<GameObject> ItemObjects => itemObjects;

    public int EnemyCount => enemyObjects.Count;

    public int ItemCount => itemObjects.Count;


    /// <summary>
    /// Removes content belonging to the previous dungeon.
    /// </summary>
    public void ClearContent()
    {
        enemyObjects.Clear();
        itemObjects.Clear();
        itemsByCell.Clear();

        if (contentParent != null)
        {
            Destroy(contentParent);
            contentParent = null;
        }
    }


    /// <summary>
    /// Generates enemies and items after the dungeon itself has
    /// passed validation.
    /// </summary>
    public void GenerateContent(DungeonGenerator generator)
    {
        ClearContent();

        enemySequence = 0;

        totalThreatBudget = 0;
        combatEnemiesGenerated = 0;
        eliteEnemiesGenerated = 0;
        rewardItemsGenerated = 0;
        randomItemsGenerated = 0;

        if (generator == null)
        {
            UnityEngine.Debug.LogError(
                "Content generation failed because DungeonGenerator was null."
            );

            return;
        }

        if (generator.StartRoom == null ||
            generator.ExitRoom == null ||
            generator.Graph == null ||
            generator.Rooms == null ||
            generator.Rooms.Count == 0)
        {
            UnityEngine.Debug.LogError(
                "Content generation failed because the dungeon data was incomplete."
            );

            return;
        }

        contentParent =
            new GameObject("Generated Content");


        // Use a different deterministic sequence from the main dungeon
        // generator while still deriving it from the same dungeon seed.
        int contentSeed =
            unchecked(generator.CurrentSeed * 397 + 7919);

        System.Random random =
            new System.Random(contentSeed);


        Dictionary<Room, int> roomDistances =
            DungeonValidator.CalculateRoomDistances(
                generator.StartRoom,
                generator.Graph
            );


        HashSet<Vector2Int> occupiedCells =
            new HashSet<Vector2Int>();

        occupiedCells.Add(
            generator.GetPlayerSpawnPosition()
        );

        occupiedCells.Add(
            generator.GetExitPosition()
        );

        // Objective cells are reserved before normal procedural content
        // is placed so enemies/items cannot initially spawn on a Sigil.
        if (generator.ObjectiveManager != null)
        {
            foreach (Vector2Int objectiveCell in
                     generator.ObjectiveManager.ObjectiveCells)
            {
                occupiedCells.Add(
                    objectiveCell
                );
            }
        }

        foreach (Room room in generator.Rooms)
        {
            if (room == null)
                continue;


            switch (room.Role)
            {
                case RoomRole.Start:

                    // The player's arrival room is always safe.
                    break;


                case RoomRole.Exit:

                    // Keep the descent area clear so reaching an unlocked
                    // shaft cannot be obstructed by normal content.
                    break;


                case RoomRole.Rest:

                    // Rest rooms deliberately contain no normal enemies.
                    // A healing/resource mechanic will be added later.
                    break;


                case RoomRole.Puzzle:

                    // Reserved for the procedural environmental puzzle
                    // system. Avoid normal content for now.
                    break;


                case RoomRole.Reward:

                    GenerateRewardRoomContent(
                        generator,
                        room,
                        random,
                        occupiedCells
                    );

                    break;


                case RoomRole.Elite:

                    GenerateThreatRoomContent(
                        generator,
                        room,
                        random,
                        occupiedCells,
                        true
                    );

                    break;


                case RoomRole.Combat:
                default:

                    GenerateThreatRoomContent(
                        generator,
                        room,
                        random,
                        occupiedCells,
                        false
                    );

                    break;
            }
        }


        UnityEngine.Debug.Log(
            "========== SEMANTIC CONTENT ==========\n" +
            $"Seed: {generator.CurrentSeed}\n" +
            $"Total threat budget: {totalThreatBudget}\n" +
            $"Combat enemies: {combatEnemiesGenerated}\n" +
            $"Elite enemies: {eliteEnemiesGenerated}\n" +
            $"Total enemies: {EnemyCount}\n" +
            $"Reward-room items: {rewardItemsGenerated}\n" +
            $"Other items: {randomItemsGenerated}\n" +
            $"Total items: {ItemCount}\n" +
            "Start room enemy-free: YES\n" +
            "Rest rooms enemy-free: YES\n" +
            "Puzzle rooms reserved: YES\n" +
            "======================================"
        );
    }


    /// <summary>
    /// Creates a temporary enemy marker.
    ///
    /// Enemy behaviour will be added in the next stage.
    /// </summary>
    private void SpawnEnemy(
        DungeonGenerator generator,
        Room room,
        System.Random random,
        HashSet<Vector2Int> occupiedCells)
    {
        Vector2Int cell;

        if (!TryFindFreeRoomCell(
                room,
                random,
                occupiedCells,
                out cell))
        {
            return;
        }

        GameObject enemy =
            CreateMarker(
                "Enemy",
                cell,
                enemyScale,
                enemyColour
            );


                EnemyController enemyController =
                    enemy.AddComponent<EnemyController>();


                // Each enemy gets its own deterministic AI random sequence.
                int enemySeed =
                    unchecked(
                        random.Next() +
                        enemySequence * 997
                    );

                enemySequence++;


        enemyController.Initialise(
            generator,
            playerController,
            room,
            cell,
            enemySeed
            );


        enemyObjects.Add(enemy);
                occupiedCells.Add(cell);
    }


    /// <summary>
    /// Creates an item marker on a free room cell.
    /// </summary>
    private void SpawnItem(
        Room room,
        System.Random random,
        HashSet<Vector2Int> occupiedCells)
    {
        Vector2Int cell;

        if (!TryFindFreeRoomCell(
                room,
                random,
                occupiedCells,
                out cell))
        {
            return;
        }

        GameObject item =
            CreateMarker(
                "Item",
                cell,
                itemScale,
                itemColour
            );

        itemObjects.Add(item);
        occupiedCells.Add(cell);
        itemsByCell[cell] = item;
    }


    /// <summary>
    /// Attempts several random cells inside the supplied room and
    /// returns the first cell that has not already been occupied.
    /// </summary>
    private bool TryFindFreeRoomCell(
        Room room,
        System.Random random,
        HashSet<Vector2Int> occupiedCells,
        out Vector2Int selectedCell)
    {
        const int maximumAttempts = 30;

        for (int attempt = 0;
             attempt < maximumAttempts;
             attempt++)
        {
            int x =
                random.Next(
                    room.Bounds.xMin,
                    room.Bounds.xMax
                );

            int y =
                random.Next(
                    room.Bounds.yMin,
                    room.Bounds.yMax
                );

            Vector2Int candidate =
                new Vector2Int(x, y);

            if (!occupiedCells.Contains(candidate))
            {
                selectedCell = candidate;
                return true;
            }
        }

        selectedCell = Vector2Int.zero;
        return false;
    }


    /// <summary>
    /// Creates a simple coloured marker at a dungeon grid position.
    ///
    /// Simple geometry is intentional because the project is focused
    /// on procedural generation and behaviour rather than artwork.
    /// </summary>
    private GameObject CreateMarker(
        string markerName,
        Vector2Int gridPosition,
        float scale,
        Color colour)
    {
        GameObject marker =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );

        marker.name =
            $"{markerName} ({gridPosition.x}, {gridPosition.y})";

        marker.transform.SetParent(
            contentParent.transform
        );

        marker.transform.position =
            new Vector3(
                gridPosition.x + 0.5f,
                gridPosition.y + 0.5f,
                -2f
            );

        marker.transform.localScale =
            new Vector3(
                scale,
                scale,
                1f
            );


        Collider markerCollider =
            marker.GetComponent<Collider>();

        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }


        Renderer markerRenderer =
    marker.GetComponent<Renderer>();

        ApplyColour(
            markerRenderer,
            colour
        );


        return marker;
    }

    private void ApplyColour(Renderer renderer, Color colour)
    {
        if (renderer == null)
            return;

        if (unlitMaterial != null)
        {
            renderer.sharedMaterial = unlitMaterial;
        }

        MaterialPropertyBlock properties = new MaterialPropertyBlock();

        renderer.GetPropertyBlock(properties);
        properties.SetColor("_Color", colour);
        renderer.SetPropertyBlock(properties);
    }

    /// <summary>
    /// Attempts to collect an item from the supplied dungeon-grid cell.
    ///
    /// Returns true only when an item was actually present.
    /// </summary>
    public bool TryCollectItem(Vector2Int gridPosition)
    {
        GameObject item;

        if (!itemsByCell.TryGetValue(
                gridPosition,
                out item))
        {
            return false;
        }


        itemsByCell.Remove(
            gridPosition
        );

        itemObjects.Remove(
            item
        );


        if (item != null)
        {
            Destroy(item);
        }


        UnityEngine.Debug.Log(
            $"ITEM COLLECTED at " +
            $"({gridPosition.x}, {gridPosition.y})"
        );

        return true;
    }

    /// <summary>
    /// Calculates how much enemy threat a generated room should contain.
    ///
    /// Graph distance provides basic progression through the floor while
    /// semantic room roles modify that structural difficulty.
    /// </summary>
    private int CalculateThreatBudget(
        Room room,
        bool isEliteRoom)
    {
        if (room == null)
            return 0;


        int graphDistance =
            Mathf.Max(
                0,
                room.GraphDistanceFromStart
            );


        int budget =
            baseCombatThreat +
            graphDistance *
            threatPerGraphStep;


        if (isEliteRoom)
        {
            budget +=
                eliteThreatBonus;
        }


        return Mathf.Max(
            0,
            budget
        );
    }

    /// <summary>
    /// Converts a room's threat budget into procedural enemy placement.
    ///
    /// The same budget system can later choose between enemy archetypes
    /// with different threat costs.
    /// </summary>
    private void GenerateThreatRoomContent(
        DungeonGenerator generator,
        Room room,
        System.Random random,
        HashSet<Vector2Int> occupiedCells,
        bool isEliteRoom)
    {
        int threatBudget =
            CalculateThreatBudget(
                room,
                isEliteRoom
            );


        totalThreatBudget +=
            threatBudget;


        int enemiesToGenerate =
            threatBudget /
            Mathf.Max(
                1,
                basicEnemyThreatCost
            );


        enemiesToGenerate =
            Mathf.Clamp(
                enemiesToGenerate,
                1,
                maximumEnemiesPerRoom
            );


        int enemiesActuallyGenerated =
            0;


        for (int i = 0;
             i < enemiesToGenerate;
             i++)
        {
            int beforeCount =
                EnemyCount;


            SpawnEnemy(
                generator,
                room,
                random,
                occupiedCells
            );


            if (EnemyCount > beforeCount)
            {
                enemiesActuallyGenerated++;
            }
        }


        if (isEliteRoom)
        {
            eliteEnemiesGenerated +=
                enemiesActuallyGenerated;
        }
        else
        {
            combatEnemiesGenerated +=
                enemiesActuallyGenerated;
        }

        // Combat encounters can occasionally contain ordinary treasure,
        // but dedicated Reward rooms remain much more valuable.
        if (!isEliteRoom &&
            random.NextDouble() < itemChance)
        {
            int beforeItemCount =
                ItemCount;


            SpawnItem(
                room,
                random,
                occupiedCells
            );


            if (ItemCount > beforeItemCount)
            {
                randomItemsGenerated++;
            }
        }

        UnityEngine.Debug.Log(
            $"ROOM THREAT - " +
            $"{room.Role} at {room.Centre}. " +
            $"Depth: {room.GraphDistanceFromStart}. " +
            $"Budget: {threatBudget}. " +
            $"Enemies: {enemiesActuallyGenerated}"
        );
    }

    /// <summary>
    /// Places guaranteed treasure in optional Reward rooms.
    ///
    /// Reward rooms are intentionally safe at this stage so choosing to
    /// explore a side branch has a clear benefit.
    /// </summary>
    private void GenerateRewardRoomContent(
        DungeonGenerator generator,
        Room room,
        System.Random random,
        HashSet<Vector2Int> occupiedCells)
    {
        const int guaranteedItems = 2;


        for (int i = 0;
             i < guaranteedItems;
             i++)
        {
            int beforeCount =
                ItemCount;


            SpawnItem(
                room,
                random,
                occupiedCells
            );


            if (ItemCount > beforeCount)
            {
                rewardItemsGenerated++;
            }
        }


        UnityEngine.Debug.Log(
            $"REWARD ROOM CONTENT - " +
            $"Room {room.Centre}. " +
            $"Items generated: {guaranteedItems}"
        );
    }
}