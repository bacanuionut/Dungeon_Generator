using System.Collections;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Controls progression across a complete multi-floor dungeon run.
///
/// DungeonGenerator remains responsible for generating one dungeon.
/// DungeonRunManager decides which floor is being played and which
/// deterministic seed that floor should use.
/// </summary>
public class DungeonRunManager : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;


    [Header("Run Settings")]

    [Tooltip("Number of generated floors required to complete a run.")]
    [SerializeField]
    private int totalFloors = 5;

    [Tooltip("Starting seed from which deterministic floor seeds are derived.")]
    [SerializeField]
    private int baseRunSeed = 12345;

    [Tooltip("Small delay before descending to the next generated floor.")]
    [SerializeField]
    private float floorTransitionDelay = 0.75f;


    private int currentFloor = 1;

    private bool transitioning;


    public int CurrentFloor => currentFloor;

    public int TotalFloors => totalFloors;

    public int BaseRunSeed => baseRunSeed;

    public bool RunComplete { get; private set; }


    /// <summary>
    /// Called by DungeonGenerator when the player reaches the
    /// currently active descent point.
    /// </summary>
    public void CompleteCurrentFloor()
    {
        if (transitioning || RunComplete)
            return;


        UnityEngine.Debug.Log(
            "========== FLOOR COMPLETE ==========\n" +
            $"Floor: {currentFloor}/{totalFloors}\n" +
            $"Seed: {dungeonGenerator.CurrentSeed}\n" +
            "===================================="
        );


        if (currentFloor >= totalFloors)
        {
            CompleteRun();
            return;
        }


        StartCoroutine(
            DescendToNextFloor()
        );
    }


    /// <summary>
    /// Advances the player one floor deeper and generates the
    /// next deterministic dungeon.
    /// </summary>
    private IEnumerator DescendToNextFloor()
    {
        transitioning = true;


        UnityEngine.Debug.Log(
            $"DESCENDING FROM FLOOR {currentFloor}..."
        );


        yield return new WaitForSeconds(
            floorTransitionDelay
        );


        currentFloor++;


        int nextFloorSeed =
            CalculateFloorSeed(
                currentFloor
            );


        UnityEngine.Debug.Log(
            "========== NEW FLOOR ==========\n" +
            $"Floor: {currentFloor}/{totalFloors}\n" +
            $"Seed: {nextFloorSeed}\n" +
            "Generating new dungeon...\n" +
            "==============================="
        );


        dungeonGenerator.GenerateRunFloor(
            nextFloorSeed
        );


        transitioning = false;
    }


    /// <summary>
    /// Produces a stable but different seed for every floor.
    ///
    /// Using a prime multiplier separates the floor seeds while
    /// keeping the entire run reproducible from one base seed.
    /// </summary>
    private int CalculateFloorSeed(
        int floorNumber)
    {
        return unchecked(
            baseRunSeed +
            (floorNumber - 1) * 1009
        );
    }


    private void CompleteRun()
    {
        RunComplete = true;
        transitioning = false;


        UnityEngine.Debug.Log(
            "=====================================\n" +
            "              RUN COMPLETE\n" +
            "=====================================\n" +
            $"Floors completed: {totalFloors}\n" +
            $"Base run seed: {baseRunSeed}\n" +
            "The player escaped the dungeon.\n" +
            "====================================="
        );
    }
}