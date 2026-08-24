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

    [Header("Enemy Placement")]

    [Tooltip("Enemy probability for rooms close to the start.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float baseEnemyChance = 0.15f;

    [Tooltip("Extra enemy probability added for each graph step from the start.")]
    [Range(0f, 0.5f)]
    [SerializeField]
    private float enemyChancePerGraphStep = 0.20f;

    [Tooltip("Distant rooms can occasionally contain a second enemy.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float secondEnemyChance = 0.35f;


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


        int maximumGraphDistance = 0;

        foreach (Room room in generator.Rooms)
        {
            if (room == null)
                continue;

            if (!roomDistances.ContainsKey(room))
                continue;

            int graphDistance =
                roomDistances[room];

            maximumGraphDistance =
                Mathf.Max(
                    maximumGraphDistance,
                    graphDistance
                );


            // The starting room is deliberately kept safe.
            if (room == generator.StartRoom)
                continue;


            // Keep the exit room clear for now so that the objective
            // cannot immediately be blocked by an enemy.
            bool canContainEnemy =
                room != generator.ExitRoom;


            if (canContainEnemy)
            {
                float enemyChance =
                    baseEnemyChance +
                    graphDistance * enemyChancePerGraphStep;

                enemyChance =
                    Mathf.Clamp01(enemyChance);


                if (random.NextDouble() < enemyChance)
                {
                    SpawnEnemy(
                        generator,
                        room,
                        random,
                        occupiedCells
                    );


                    // Rooms further from the start can sometimes
                    // contain an additional enemy.
                    if (graphDistance >= 3 &&
                        random.NextDouble() < secondEnemyChance)
                    {
                        SpawnEnemy(
                            generator,
                            room,
                            random,
                            occupiedCells
                        );
                    }
                }
            }


            if (random.NextDouble() < itemChance)
            {
                SpawnItem(
                    room,
                    random,
                    occupiedCells
                );
            }
        }


        UnityEngine.Debug.Log(
            "========== PROCEDURAL CONTENT ==========\n" +
            $"Seed: {generator.CurrentSeed}\n" +
            $"Enemies generated: {EnemyCount}\n" +
            $"Items generated: {ItemCount}\n" +
            $"Maximum room graph distance: {maximumGraphDistance}\n" +
            "Start room enemy-free: YES\n" +
            "========================================"
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
}