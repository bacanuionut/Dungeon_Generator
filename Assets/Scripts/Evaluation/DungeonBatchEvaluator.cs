using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

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

    [Header("CSV Export")]

    [Tooltip("File name used when exporting individual dungeon metrics.")]
    [SerializeField]
    private string csvFileName = "DungeonBatchResults.csv";

    [Tooltip("Automatically export the individual results after a batch completes.")]
    [SerializeField]
    private bool exportToCsv = true;


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

        // Keep the individual results as well as the aggregate summary.
        // This allows the evaluation data to be analysed later using
        // spreadsheets, graphs or statistical tools.
        if (exportToCsv)
        {
            ExportResultsToCsv(
                results,
                failedTests
            );
        }
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

    /// <summary>
    /// Exports the individual result from every successful generated
    /// dungeon to a CSV file.
    ///
    /// The batch summary is useful for quick inspection in Unity, while
    /// the CSV preserves the underlying per-seed data for later analysis.
    /// </summary>
    private void ExportResultsToCsv(
        List<DungeonMetrics.Result> results,
        int failedTests)
    {
        if (results == null || results.Count == 0)
        {
            UnityEngine.Debug.LogWarning(
                "CSV export skipped because there are no successful results."
            );

            return;
        }

        StringBuilder csv =
            new StringBuilder();

        // Column headings.
        csv.AppendLine(
            "Seed," +
            "Rooms," +
            "Connections," +
            "Corridors," +
            "FloorCells," +
            "RoomCells," +
            "CorridorCells," +
            "AverageRoomArea," +
            "SmallestRoomArea," +
            "LargestRoomArea," +
            "TotalCorridorLength," +
            "AverageCorridorLength," +
            "GraphDistance," +
            "ShortestPlayablePath"
        );

        // One row represents one generated dungeon.
        foreach (DungeonMetrics.Result result in results)
        {
            csv.Append(result.Seed).Append(",");
            csv.Append(result.RoomCount).Append(",");
            csv.Append(result.ConnectionCount).Append(",");
            csv.Append(result.CorridorCount).Append(",");
            csv.Append(result.FloorCellCount).Append(",");
            csv.Append(result.RoomCellCount).Append(",");
            csv.Append(result.CorridorCellCount).Append(",");

            csv.Append(
                result.AverageRoomArea.ToString(
                    "F2",
                    CultureInfo.InvariantCulture
                )
            ).Append(",");

            csv.Append(result.SmallestRoomArea).Append(",");
            csv.Append(result.LargestRoomArea).Append(",");
            csv.Append(result.TotalCorridorLength).Append(",");

            csv.Append(
                result.AverageCorridorLength.ToString(
                    "F2",
                    CultureInfo.InvariantCulture
                )
            ).Append(",");

            csv.Append(result.StartToExitGraphDistance).Append(",");
            csv.Append(result.ShortestPlayablePathLength);

            csv.AppendLine();
        }

        // Application.dataPath points to the project's Assets folder.
        // We place evaluation output in a dedicated subfolder.
        string evaluationFolder =
            Path.Combine(
                UnityEngine.Application.dataPath,
                "EvaluationResults"
            );

        if (!Directory.Exists(evaluationFolder))
        {
            Directory.CreateDirectory(
                evaluationFolder
            );
        }

        string filePath =
            Path.Combine(
                evaluationFolder,
                csvFileName
            );

        File.WriteAllText(
            filePath,
            csv.ToString()
        );

        UnityEngine.Debug.Log(
            $"CSV EVALUATION DATA EXPORTED\n" +
            $"Successful rows: {results.Count}\n" +
            $"Failed generations: {failedTests}\n" +
            $"File: {filePath}"
        );
    }
}