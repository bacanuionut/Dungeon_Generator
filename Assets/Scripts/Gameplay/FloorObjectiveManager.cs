using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Controls the procedural objective for one generated floor.
///
/// The player must collect all Anchor Sigils before the descent
/// point becomes active.
/// </summary>
public class FloorObjectiveManager : MonoBehaviour
{
    [Header("Objective Settings")]

    [Tooltip("Number of Anchor Sigils required to unlock the descent.")]
    [SerializeField]
    private int requiredSigils = 3;


    [Header("Display")]

    [SerializeField]
    private float sigilScale = 0.45f;

    [SerializeField]
    private Color sigilColour = Color.cyan;

    [SerializeField]
    private Color lockedExitColour = Color.red;

    [SerializeField]
    private Color unlockedExitColour = Color.green;


    [Header("References")]

    [SerializeField]
    private GameObject exitObject;


    private GameObject objectiveParent;

    private readonly Dictionary<Vector2Int, GameObject> sigilsByCell =
        new Dictionary<Vector2Int, GameObject>();

    private readonly HashSet<Vector2Int> objectiveCells =
        new HashSet<Vector2Int>();


    private int collectedSigils;


    public int CollectedSigils => collectedSigils;

    public int RequiredSigils => requiredSigils;

    public bool ExitUnlocked =>
        collectedSigils >= requiredSigils;

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


        if (objectiveParent != null)
        {
            Destroy(objectiveParent);
            objectiveParent = null;
        }
    }


    /// <summary>
    /// Creates the floor's Anchor Sigils after dungeon generation
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


        SetExitUnlocked(false);


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
                continue;

            if (room == generator.StartRoom ||
                room == generator.ExitRoom ||
                room.Role == RoomRole.Puzzle)
            {
                continue;
            }

            if (!distances.ContainsKey(room))
                continue;


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


            CreateSigil(
                cell
            );
        }


        // Use the number that was actually successfully placed.
        requiredSigils =
            sigilsByCell.Count;


        UnityEngine.Debug.Log(
            "========== FLOOR OBJECTIVE ==========\n" +
            $"Seed: {generator.CurrentSeed}\n" +
            $"Anchor Sigils placed: {requiredSigils}\n" +
            $"Anchor Sigils collected: {collectedSigils}/{requiredSigils}\n" +
            $"Descent shaft unlocked: {ExitUnlocked}\n" +
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
                requiredSigils,
                candidates.Count
            );


        while (selected.Count < targetCount)
        {
            Room bestRoom = null;
            float bestScore = float.MinValue;


            foreach (Room candidate in candidates)
            {
                if (selected.Contains(candidate))
                    continue;


                float score =
                    distances[candidate] * 20f;


                // After the first choice, favour rooms that are
                // spatially separated from the Sigils already chosen.
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
                break;


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
                continue;


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
                continue;


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
        GameObject sigil =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );


        sigil.name =
            $"Anchor Sigil ({gridPosition.x}, {gridPosition.y})";


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


        sigilsByCell[gridPosition] =
            sigil;

        objectiveCells.Add(
            gridPosition
        );
    }


    /// <summary>
    /// Attempts to collect an Anchor Sigil from the player's cell.
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
            "ANCHOR SIGIL COLLECTED - " +
            $"{collectedSigils}/{requiredSigils}"
        );


        if (ExitUnlocked)
        {
            SetExitUnlocked(true);


            UnityEngine.Debug.Log(
                "====================================\n" +
                "       DESCENT SHAFT ACTIVATED\n" +
                "All Anchor Sigils have been collected.\n" +
                "===================================="
            );
        }


        return true;
    }


    /// <summary>
    /// Updates the exit marker so its state is visually obvious.
    /// </summary>
    private void SetExitUnlocked(
        bool unlocked)
    {
        if (exitObject == null)
            return;


        Renderer renderer =
            exitObject.GetComponent<Renderer>();


        if (renderer != null)
        {
            renderer.material.color =
                unlocked
                    ? unlockedExitColour
                    : lockedExitColour;
        }
    }
}