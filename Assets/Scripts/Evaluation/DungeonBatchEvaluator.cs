using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
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

    [Header("CSV Export")]

    [Tooltip("File name used when exporting individual dungeon metrics.")]
    [SerializeField]
    private string csvFileName = "DungeonBatchResults.csv";

    [Tooltip("Automatically export the individual results after a batch completes.")]
    [SerializeField]
    private bool exportToCsv = true;


    /// <summary>
    /// Stores the normal DungeonMetrics result together with final-evaluation
    /// measurements that belong to the batch run itself.
    /// </summary>
    private class BatchResult
    {
        public DungeonMetrics.Result Metrics;
        public int CAAddedCells;
        public double GenerationTimeMs;
    }


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

        List<BatchResult> results =
            new List<BatchResult>();

        int failedTests = 0;

        for (int i = 0; i < numberOfTests; i++)
        {
            int testSeed =
                startingSeed + i;

            // Time the same batch-generation path used for evaluation.
            // EvaluateSeed enables batch mode, so rendering and gameplay
            // initialisation are excluded from this measurement.
            Stopwatch stopwatch =
                Stopwatch.StartNew();

            DungeonMetrics.Result result =
                dungeonGenerator.EvaluateSeed(testSeed);

            stopwatch.Stop();

            if (result == null ||
                result.ShortestPlayablePathLength < 0)
            {
                failedTests++;
                continue;
            }

            int caAddedCells =
                dungeonGenerator.Grid != null
                    ? dungeonGenerator.Grid.OrganicRoomCellCount
                    : 0;

            BatchResult batchResult =
                new BatchResult();

            batchResult.Metrics = result;
            batchResult.CAAddedCells = caAddedCells;
            batchResult.GenerationTimeMs =
                stopwatch.Elapsed.TotalMilliseconds;

            results.Add(batchResult);
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
        List<BatchResult> results,
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
        int minFloorCells = int.MaxValue;
        int maxFloorCells = int.MinValue;

        int totalCAAddedCells = 0;
        int minCAAddedCells = int.MaxValue;
        int maxCAAddedCells = int.MinValue;

        int totalPathLength = 0;
        int minPathLength = int.MaxValue;
        int maxPathLength = int.MinValue;

        int totalGraphDistance = 0;
        int minGraphDistance = int.MaxValue;
        int maxGraphDistance = int.MinValue;

        double totalGenerationTimeMs = 0.0;
        double minGenerationTimeMs = double.MaxValue;
        double maxGenerationTimeMs = double.MinValue;

        float totalAverageRoomArea = 0f;
        float totalAverageCorridorLength = 0f;

        foreach (BatchResult batchResult in results)
        {
            DungeonMetrics.Result result =
                batchResult.Metrics;

            totalRooms += result.RoomCount;

            minRooms =
                Mathf.Min(minRooms, result.RoomCount);

            maxRooms =
                Mathf.Max(maxRooms, result.RoomCount);


            totalFloorCells +=
                result.FloorCellCount;

            minFloorCells =
                Mathf.Min(
                    minFloorCells,
                    result.FloorCellCount
                );

            maxFloorCells =
                Mathf.Max(
                    maxFloorCells,
                    result.FloorCellCount
                );


            totalCAAddedCells +=
                batchResult.CAAddedCells;

            minCAAddedCells =
                Mathf.Min(
                    minCAAddedCells,
                    batchResult.CAAddedCells
                );

            maxCAAddedCells =
                Mathf.Max(
                    maxCAAddedCells,
                    batchResult.CAAddedCells
                );


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


            totalGenerationTimeMs +=
                batchResult.GenerationTimeMs;

            minGenerationTimeMs =
                System.Math.Min(
                    minGenerationTimeMs,
                    batchResult.GenerationTimeMs
                );

            maxGenerationTimeMs =
                System.Math.Max(
                    maxGenerationTimeMs,
                    batchResult.GenerationTimeMs
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

            $"Floor cells - Average: {totalFloorCells / count:F2}, " +
            $"Min: {minFloorCells}, Max: {maxFloorCells}\n" +

            $"CA-added cells - Average: {totalCAAddedCells / count:F2}, " +
            $"Min: {minCAAddedCells}, Max: {maxCAAddedCells}\n" +

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

            $"Generation time (ms) - Average: " +
            $"{totalGenerationTimeMs / count:F4}, " +
            $"Min: {minGenerationTimeMs:F4}, " +
            $"Max: {maxGenerationTimeMs:F4}\n" +

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
        List<BatchResult> results,
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
            "CAAddedCells," +
            "AverageRoomArea," +
            "SmallestRoomArea," +
            "LargestRoomArea," +
            "TotalCorridorLength," +
            "AverageCorridorLength," +
            "GraphDistance," +
            "ShortestPlayablePath," +
            "GenerationTimeMs"
        );

        // One row represents one generated dungeon.
        foreach (BatchResult batchResult in results)
        {
            DungeonMetrics.Result result =
                batchResult.Metrics;

            csv.Append(result.Seed).Append(",");
            csv.Append(result.RoomCount).Append(",");
            csv.Append(result.ConnectionCount).Append(",");
            csv.Append(result.CorridorCount).Append(",");
            csv.Append(result.FloorCellCount).Append(",");
            csv.Append(result.RoomCellCount).Append(",");
            csv.Append(result.CorridorCellCount).Append(",");
            csv.Append(batchResult.CAAddedCells).Append(",");

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
            csv.Append(result.ShortestPlayablePathLength).Append(",");

            csv.Append(
                batchResult.GenerationTimeMs.ToString(
                    "F4",
                    CultureInfo.InvariantCulture
                )
            );

            csv.AppendLine();
        }

        // Application.dataPath points to the project's Assets folder.
        // Evaluation output is stored in a dedicated subfolder.
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
