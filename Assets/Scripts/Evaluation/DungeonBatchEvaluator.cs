using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Runs the dungeon generator across multiple deterministic seeds
/// and summarises the resulting validation and generation metrics.
/// </summary>
public class DungeonBatchEvaluator : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;


    [Header("Batch Settings")]

    [Tooltip("Number of dungeon seeds to evaluate.")]
    [SerializeField]
    private int numberOfTests = 100;

    [Tooltip("First seed used by the batch.")]
    [SerializeField]
    private int startingSeed = 1;

    [Tooltip("Press this key during Play mode to run the batch.")]
    [SerializeField]
    private KeyCode runKey = KeyCode.B;


    private void Update()
    {
        if (Input.GetKeyDown(runKey))
        {
            RunBatch();
        }
    }


    /// <summary>
    /// Generates a sequence of dungeon seeds and calculates
    /// aggregate statistics from all successful results.
    /// </summary>
    private void RunBatch()
    {
        if (dungeonGenerator == null)
        {
            UnityEngine.Debug.LogError(
                "Batch evaluation cannot run because DungeonGenerator " +
                "has not been assigned."
            );

            return;
        }

        if (numberOfTests <= 0)
        {
            UnityEngine.Debug.LogError(
                "Batch evaluation requires at least one test."
            );

            return;
        }

        List<DungeonMetrics.Result> results =
            new List<DungeonMetrics.Result>();

        int failedTests = 0;

        for (int i = 0; i < numberOfTests; i++)
        {
            int testSeed =
                startingSeed + i;

            DungeonMetrics.Result result =
                dungeonGenerator.EvaluateSeed(testSeed);

            if (result == null ||
                result.ShortestPlayablePathLength < 0)
            {
                failedTests++;
                continue;
            }

            results.Add(result);
        }

        LogSummary(
            results,
            failedTests
        );
    }


    /// <summary>
    /// Calculates aggregate values across the complete test batch.
    /// </summary>
    private void LogSummary(
        List<DungeonMetrics.Result> results,
        int failedTests)
    {
        if (results.Count == 0)
        {
            UnityEngine.Debug.LogError(
                "BATCH EVALUATION FAILED - No valid dungeons were produced."
            );

            return;
        }

        int totalRooms = 0;
        int minRooms = int.MaxValue;
        int maxRooms = int.MinValue;

        int totalFloorCells = 0;

        int totalPathLength = 0;
        int minPathLength = int.MaxValue;
        int maxPathLength = int.MinValue;

        int totalGraphDistance = 0;
        int minGraphDistance = int.MaxValue;
        int maxGraphDistance = int.MinValue;

        float totalAverageRoomArea = 0f;
        float totalAverageCorridorLength = 0f;

        foreach (DungeonMetrics.Result result in results)
        {
            totalRooms += result.RoomCount;

            minRooms =
                Mathf.Min(minRooms, result.RoomCount);

            maxRooms =
                Mathf.Max(maxRooms, result.RoomCount);


            totalFloorCells +=
                result.FloorCellCount;


            totalPathLength +=
                result.ShortestPlayablePathLength;

            minPathLength =
                Mathf.Min(
                    minPathLength,
                    result.ShortestPlayablePathLength
                );

            maxPathLength =
                Mathf.Max(
                    maxPathLength,
                    result.ShortestPlayablePathLength
                );


            totalGraphDistance +=
                result.StartToExitGraphDistance;

            minGraphDistance =
                Mathf.Min(
                    minGraphDistance,
                    result.StartToExitGraphDistance
                );

            maxGraphDistance =
                Mathf.Max(
                    maxGraphDistance,
                    result.StartToExitGraphDistance
                );


            totalAverageRoomArea +=
                result.AverageRoomArea;

            totalAverageCorridorLength +=
                result.AverageCorridorLength;
        }

        float count =
            results.Count;

        UnityEngine.Debug.Log(
            "========== DUNGEON BATCH EVALUATION ==========\n" +
            $"Seeds tested: {numberOfTests}\n" +
            $"Successful: {results.Count}\n" +
            $"Failed: {failedTests}\n\n" +

            $"Rooms - Average: {totalRooms / count:F2}, " +
            $"Min: {minRooms}, Max: {maxRooms}\n" +

            $"Floor cells - Average: {totalFloorCells / count:F2}\n" +

            $"Average room area: " +
            $"{totalAverageRoomArea / count:F2}\n" +

            $"Average corridor length: " +
            $"{totalAverageCorridorLength / count:F2}\n\n" +

            $"Shortest playable path - Average: " +
            $"{totalPathLength / count:F2}, " +
            $"Min: {minPathLength}, Max: {maxPathLength}\n" +

            $"Start-to-exit graph distance - Average: " +
            $"{totalGraphDistance / count:F2}, " +
            $"Min: {minGraphDistance}, Max: {maxGraphDistance}\n" +

            "=============================================="
        );
    }
}