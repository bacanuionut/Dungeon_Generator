using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Controls the procedural objective for one generated floor.
///
/// Keys are placed in selected generated rooms. Collecting all of
/// them satisfies the descent requirement, but the exit hatch remains closed
/// until the player later approaches it within its configured proximity range.
/// </summary>
public class FloorObjectiveManager : MonoBehaviour
{
    [Header("Objective Settings")]

    [Tooltip("Target number of keys to place on each generated floor.")]
    [SerializeField]
    private int requiredSigils = 3;


    [Header("Display")]

    [FormerlySerializedAs("sigilSprite")]
    [FormerlySerializedAs("objectiveSprite")]
    [Tooltip("Sprite used for keys placed on the dungeon floor.")]
    [SerializeField]
    private Sprite keySprite;

    [SerializeField]
    private float sigilScale = 0.45f;

    [SerializeField]
    private Color sigilColour = Color.cyan;


    [Header("References")]

    [Tooltip("The same Exit Hatch object referenced by DungeonGenerator.")]
    [SerializeField]
    private GameObject exitObject;


    private GameObject objectiveParent;

    private readonly Dictionary<Vector2Int, GameObject> sigilsByCell =
        new Dictionary<Vector2Int, GameObject>();

    private readonly HashSet<Vector2Int> objectiveCells =
        new HashSet<Vector2Int>();


    private int collectedSigils;

    // Kept separate from the Inspector target. This prevents one unusual
    // floor that can only place two keys from permanently reducing later
    // floors to two keys as well.
    private int activeRequiredSigils;


    public int CollectedSigils => collectedSigils;

    public int RequiredSigils =>
        activeRequiredSigils > 0
            ? activeRequiredSigils
            : Mathf.Max(0, requiredSigils);

    /// <summary>
    /// Kept for compatibility with existing gameplay code.
    /// This now means the objective requirement has been met.
    /// It does NOT mean the hatch has already physically opened.
    /// </summary>
    public bool ExitUnlocked =>
        activeRequiredSigils > 0 &&
        collectedSigils >= activeRequiredSigils;

    public IReadOnlyCollection<Vector2Int> ObjectiveCells =>
        objectiveCells;


    /// <summary>
    /// Removes objective objects belonging to the previous floor.
    /// </summary>
    public void ClearObjectives()
    {
        sigilsByCell.Clear();
        objectiveCells.Clear();

        collectedSigils = 0;
        activeRequiredSigils = 0;

        if (objectiveParent != null)
        {
            Destroy(objectiveParent);
            objectiveParent = null;
        }

        ResetExitHatchForNewFloor();
    }


    /// <summary>
    /// Creates the floor's keys after dungeon generation
    /// and validation have completed.
    /// </summary>
    public void GenerateObjectives(
        DungeonGenerator generator)
    {
        ClearObjectives();

        if (generator == null ||
            generator.Rooms == null ||
            generator.Rooms.Count == 0 ||
            generator.StartRoom == null ||
            generator.ExitRoom == null)
        {
            UnityEngine.Debug.LogError(
                "Floor objectives could not be generated because " +
                "the dungeon data was incomplete."
            );

            return;
        }

        objectiveParent =
            new GameObject("Generated Objectives");

        Dictionary<Room, int> distances =
            DungeonValidator.CalculateRoomDistances(
                generator.StartRoom,
                generator.Graph
            );

        List<Room> candidates =
            new List<Room>();

        foreach (Room room in generator.Rooms)
        {
            if (room == null)
            {
                continue;
            }

            if (room == generator.StartRoom ||
                room == generator.ExitRoom ||
                room.Role == RoomRole.Puzzle)
            {
                continue;
            }

            if (!distances.ContainsKey(room))
            {
                continue;
            }

            candidates.Add(room);
        }

        List<Room> selectedRooms =
            SelectSigilRooms(
                candidates,
                distances
            );

        System.Random random =
            new System.Random(
                unchecked(
                    generator.CurrentSeed * 761 +
                    15485863
                )
            );

        foreach (Room room in selectedRooms)
        {
            Vector2Int cell;

            if (!TryChooseSigilCell(
                    generator,
                    room,
                    random,
                    out cell))
            {
                continue;
            }

            CreateSigil(cell);
        }

        // Use the number actually placed on THIS floor without modifying
        // the Inspector target used by later procedural floors.
        activeRequiredSigils =
            sigilsByCell.Count;

        UpdateExitHatchProgress();

        UnityEngine.Debug.Log(
            "========== FLOOR OBJECTIVE ==========\n" +
            $"Seed: {generator.CurrentSeed}\n" +
            $"Keys placed: {activeRequiredSigils}\n" +
            $"Keys collected: {collectedSigils}/{activeRequiredSigils}\n" +
            $"Descent requirement complete: {ExitUnlocked}\n" +
            "Hatch opening requires player proximity: YES\n" +
            "====================================="
        );
    }


    /// <summary>
    /// Selects rooms for objective placement.
    ///
    /// Rooms deeper in the logical graph are preferred, while later
    /// selections also favour physical separation from previously
    /// selected rooms.
    /// </summary>
    private List<Room> SelectSigilRooms(
        List<Room> candidates,
        Dictionary<Room, int> distances)
    {
        List<Room> selected =
            new List<Room>();

        int targetCount =
            Mathf.Min(
                Mathf.Max(0, requiredSigils),
                candidates.Count
            );

        while (selected.Count < targetCount)
        {
            Room bestRoom = null;
            float bestScore = float.MinValue;

            foreach (Room candidate in candidates)
            {
                if (selected.Contains(candidate))
                {
                    continue;
                }

                float score =
                    distances[candidate] * 20f;

                // After the first choice, favour rooms that are
                // spatially separated from the keys already chosen.
                if (selected.Count > 0)
                {
                    int minimumSeparation =
                        int.MaxValue;

                    foreach (Room existing in selected)
                    {
                        int separation =
                            Mathf.Abs(
                                candidate.Centre.x -
                                existing.Centre.x
                            ) +
                            Mathf.Abs(
                                candidate.Centre.y -
                                existing.Centre.y
                            );

                        minimumSeparation =
                            Mathf.Min(
                                minimumSeparation,
                                separation
                            );
                    }

                    score +=
                        minimumSeparation;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRoom = candidate;
                }
            }

            if (bestRoom == null)
            {
                break;
            }

            selected.Add(bestRoom);
        }

        return selected;
    }


    /// <summary>
    /// Chooses a walkable position inside a selected objective room.
    /// </summary>
    private bool TryChooseSigilCell(
        DungeonGenerator generator,
        Room room,
        System.Random random,
        out Vector2Int selectedCell)
    {
        const int maximumAttempts = 40;

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

            if (!generator.Grid.IsWalkable(candidate))
            {
                continue;
            }

            if (candidate ==
                generator.GetPlayerSpawnPosition())
            {
                continue;
            }

            if (candidate ==
                generator.GetExitPosition())
            {
                continue;
            }

            if (objectiveCells.Contains(candidate))
            {
                continue;
            }

            selectedCell = candidate;
            return true;
        }

        selectedCell =
            Vector2Int.zero;

        return false;
    }


    private void CreateSigil(
        Vector2Int gridPosition)
    {
        GameObject sigil;

        if (keySprite != null)
        {
            sigil =
                new GameObject(
                    $"Key ({gridPosition.x}, {gridPosition.y})"
                );

            sigil.transform.SetParent(
                objectiveParent.transform
            );

            sigil.transform.position =
                new Vector3(
                    gridPosition.x + 0.5f,
                    gridPosition.y + 0.5f,
                    -2.1f
                );

            sigil.transform.localScale =
                new Vector3(
                    sigilScale,
                    sigilScale,
                    1f
                );

            SpriteRenderer renderer =
                sigil.AddComponent<SpriteRenderer>();

            renderer.sprite =
                keySprite;

            renderer.color =
                Color.white;

            CollectibleVisualAnimator animator =
                sigil.AddComponent<CollectibleVisualAnimator>();

            animator.SetVisualTarget(
                sigil.transform
            );

            animator.ConfigureAsKey();
        }
        else
        {
            sigil =
                GameObject.CreatePrimitive(
                    PrimitiveType.Quad
                );

            sigil.name =
                $"Key ({gridPosition.x}, {gridPosition.y})";

            sigil.transform.SetParent(
                objectiveParent.transform
            );

            sigil.transform.position =
                new Vector3(
                    gridPosition.x + 0.5f,
                    gridPosition.y + 0.5f,
                    -2.1f
                );

            sigil.transform.localScale =
                new Vector3(
                    sigilScale,
                    sigilScale,
                    1f
                );

            Collider sigilCollider =
                sigil.GetComponent<Collider>();

            if (sigilCollider != null)
            {
                Destroy(sigilCollider);
            }

            Renderer renderer =
                sigil.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.material.color =
                    sigilColour;
            }

            UnityEngine.Debug.LogWarning(
                "KEY VISUAL - Key Sprite is not assigned on FloorObjectiveManager. " +
                "Using the fallback coloured square."
            );
        }

        sigilsByCell[gridPosition] =
            sigil;

        objectiveCells.Add(
            gridPosition
        );
    }

    /// <summary>
    /// Attempts to collect a key from the player's cell.
    /// </summary>
    public bool TryCollectSigil(
        Vector2Int gridPosition)
    {
        GameObject sigil;

        if (!sigilsByCell.TryGetValue(
                gridPosition,
                out sigil))
        {
            return false;
        }

        sigilsByCell.Remove(
            gridPosition
        );

        objectiveCells.Remove(
            gridPosition
        );

        if (sigil != null)
        {
            Destroy(sigil);
        }

        collectedSigils++;

        UnityEngine.Debug.Log(
            "KEY COLLECTED - " +
            $"{collectedSigils}/{activeRequiredSigils}"
        );

        UpdateExitHatchProgress();

        if (ExitUnlocked)
        {
            UnityEngine.Debug.Log(
                "====================================\n" +
                "     DESCENT REQUIREMENT COMPLETE\n" +
                "All keys have been collected.\n" +
                "The hatch remains closed until the player approaches it.\n" +
                "===================================="
            );
        }

        return true;
    }


    /// <summary>
    /// Sends objective progress to the visual hatch controller.
    /// The hatch controller decides when proximity should actually open it.
    /// </summary>
    private void UpdateExitHatchProgress()
    {
        if (exitObject == null)
        {
            return;
        }

        ExitHatchController hatch =
            exitObject.GetComponent<ExitHatchController>();

        if (hatch == null)
        {
            return;
        }

        hatch.SetObjectiveProgress(
            collectedSigils,
            activeRequiredSigils
        );
    }


    private void ResetExitHatchForNewFloor()
    {
        if (exitObject == null)
        {
            return;
        }

        ExitHatchController hatch =
            exitObject.GetComponent<ExitHatchController>();

        if (hatch != null)
        {
            hatch.ResetForNewFloor();
        }
    }
}
