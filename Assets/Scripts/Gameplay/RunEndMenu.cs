using System.Text;
using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

/// <summary>
/// Displays the final run statistics in three independently-centred columns:
///
/// RUN
/// PERFORMANCE
/// RESOURCES
///
/// Using three real Text objects avoids relying on spaces for alignment,
/// which is unreliable even with pixel fonts.
/// </summary>
public class RunEndMenu : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private RunStatsManager runStatsManager;

    [SerializeField]
    private DungeonRunManager runManager;

    [SerializeField]
    private RunStartMenu startMenu;


    [Header("Run End Panel")]

    [SerializeField]
    private GameObject runEndPanel;

    [SerializeField]
    private GameObject gameplayHudRoot;

    [SerializeField]
    private UnityEngine.UI.Text runEndTitle;


    [Header("Result Columns")]

    [Tooltip("Left column: mode, difficulty, seed and floor progress.")]
    [SerializeField]
    private UnityEngine.UI.Text runColumnText;

    [Tooltip("Centre column: time, hearts lost, puzzles and keys.")]
    [SerializeField]
    private UnityEngine.UI.Text performanceColumnText;

    [Tooltip("Right column: coins, Pulse use and Digger use.")]
    [SerializeField]
    private UnityEngine.UI.Text resourcesColumnText;


    private bool endScreenHandled;


    private void Start()
    {
        ResolveReferences();

        if (runEndPanel != null)
        {
            runEndPanel.SetActive(
                false
            );
        }
    }


    private void Update()
    {
        if (runStatsManager == null)
        {
            ResolveReferences();

            if (runStatsManager == null)
                return;
        }


        // A new run re-arms the results screen for the next ending.
        if (runStatsManager.RunActive &&
            !runStatsManager.RunFinished)
        {
            endScreenHandled =
                false;

            return;
        }


        // RunFinished remains true while sitting on the main menu after a run.
        if (endScreenHandled ||
            !runStatsManager.RunFinished)
        {
            return;
        }


        ShowRunEndScreen();
    }


    private void ShowRunEndScreen()
    {
        endScreenHandled =
            true;


        if (runEndTitle != null)
        {
            if (runStatsManager.RunCompletedSuccessfully)
            {
                runEndTitle.text =
                    "RUN COMPLETE";
            }
            else if (runStatsManager.CurrentMode ==
                     RunStatsManager.GameMode.Survival)
            {
                runEndTitle.text =
                    "SURVIVAL ENDED";
            }
            else
            {
                runEndTitle.text =
                    "GAME OVER";
            }
        }


        PopulateResultColumns();


        if (startMenu != null)
        {
            startMenu.ShowRunEndPanel();
        }
        else
        {
            if (runEndPanel != null)
            {
                runEndPanel.SetActive(
                    true
                );
            }

            if (gameplayHudRoot != null)
            {
                gameplayHudRoot.SetActive(
                    false
                );
            }
        }


        Time.timeScale =
            0f;
    }


    /// <summary>
    /// RESTART uses the exact actual configuration of the completed run:
    /// same mode, same seed, same difficulty and same Standard floor target.
    /// </summary>
    public void RestartRun()
    {
        ResolveReferences();

        if (runStatsManager == null ||
            runManager == null)
        {
            UnityEngine.Debug.LogError(
                "RUN END MENU - Cannot restart because the run managers are missing."
            );

            return;
        }


        RunStatsManager.GameMode mode =
            runStatsManager.CurrentMode;

        int seed =
            runStatsManager.BaseSeed;

        RunStatsManager.RunDifficulty difficulty =
            runStatsManager.CurrentDifficulty;

        int targetFloors =
            runStatsManager.TargetFloors;


        HideRunEndVisuals();


        Time.timeScale =
            1f;


        if (startMenu != null)
        {
            startMenu.RefreshControlsFromLastRun();

            startMenu.HideMenusForGameplay();
        }
        else if (gameplayHudRoot != null)
        {
            gameplayHudRoot.SetActive(
                true
            );
        }


        runManager.StartNewRun(
            mode,
            seed,
            difficulty,
            targetFloors,
            false
        );
    }


    /// <summary>
    /// MAIN MENU returns to the original Main Menu Panel while preserving
    /// the last configuration in the menu controls.
    /// </summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale =
            1f;

        ResolveReferences();


        HideRunEndVisuals();


        if (startMenu == null)
        {
            UnityEngine.Debug.LogError(
                "RUN END MENU - RunStartMenu reference is missing."
            );

            return;
        }


        endScreenHandled =
            true;


        startMenu.RefreshControlsFromLastRun();

        startMenu.ShowMainMenuPanel();
    }


    private void PopulateResultColumns()
    {
        if (runColumnText != null)
        {
            runColumnText.text =
                BuildRunColumn();
        }


        if (performanceColumnText != null)
        {
            performanceColumnText.text =
                BuildPerformanceColumn();
        }


        if (resourcesColumnText != null)
        {
            resourcesColumnText.text =
                BuildResourcesColumn();
        }
    }


    private string BuildRunColumn()
    {
        StringBuilder builder =
            new StringBuilder();


        builder.AppendLine(
            "RUN"
        );

        builder.AppendLine();

        builder.Append(
            "MODE "
        );

        builder.AppendLine(
            runStatsManager.CurrentMode ==
                RunStatsManager.GameMode.Standard
                    ? "STANDARD"
                    : "SURVIVAL"
        );


        builder.Append(
            "DIFFICULTY "
        );

        builder.AppendLine(
            runStatsManager.CurrentDifficulty
                .ToString()
                .ToUpper()
        );


        builder.Append(
            "SEED "
        );

        builder.AppendLine(
            runStatsManager.BaseSeed.ToString()
        );


        if (runStatsManager.CurrentMode ==
            RunStatsManager.GameMode.Standard)
        {
            builder.Append(
                "FLOORS "
            );

            builder.Append(
                runStatsManager.FloorsCompleted
            );

            builder.Append(
                "/"
            );

            builder.Append(
                runStatsManager.TargetFloors
            );
        }
        else
        {
            builder.Append(
                "FLOORS SURVIVED "
            );

            builder.Append(
                runStatsManager.FloorsCompleted
            );
        }


        return
            builder.ToString();
    }


    private string BuildPerformanceColumn()
    {
        StringBuilder builder =
            new StringBuilder();


        builder.AppendLine(
            "PERFORMANCE"
        );

        builder.AppendLine();

        builder.Append(
            "TIME "
        );

        builder.AppendLine(
            FormatTime(
                runStatsManager.ElapsedSeconds
            )
        );


        builder.Append(
            "HEARTS LOST "
        );

        builder.AppendLine(
            runStatsManager.HeartsLost.ToString()
        );


        builder.Append(
            "PUZZLES "
        );

        builder.AppendLine(
            runStatsManager.PuzzlesCompleted.ToString()
        );


        builder.Append(
            "KEYS FOUND "
        );

        builder.Append(
            runStatsManager.KeysCollected
        );


        return
            builder.ToString();
    }


    private string BuildResourcesColumn()
    {
        StringBuilder builder =
            new StringBuilder();


        builder.AppendLine(
            "RESOURCES"
        );

        builder.AppendLine();

        builder.Append(
            "COINS "
        );

        builder.AppendLine(
            runStatsManager.CoinsCollected.ToString()
        );


        builder.Append(
            "PULSE USED "
        );

        builder.AppendLine(
            runStatsManager.PulseChargesUsed.ToString()
        );


        builder.Append(
            "DIGGER USED "
        );

        builder.Append(
            runStatsManager.ShaperChargesUsed
        );


        return
            builder.ToString();
    }


    private void HideRunEndVisuals()
    {
        if (runEndPanel != null)
        {
            runEndPanel.SetActive(
                false
            );
        }


        if (runEndTitle != null)
        {
            runEndTitle.text =
                string.Empty;
        }


        if (runColumnText != null)
        {
            runColumnText.text =
                string.Empty;
        }


        if (performanceColumnText != null)
        {
            performanceColumnText.text =
                string.Empty;
        }


        if (resourcesColumnText != null)
        {
            resourcesColumnText.text =
                string.Empty;
        }
    }


    private string FormatTime(
        float seconds)
    {
        seconds =
            Mathf.Max(
                0f,
                seconds
            );


        int totalSeconds =
            Mathf.FloorToInt(
                seconds
            );


        int hours =
            totalSeconds /
            3600;


        int minutes =
            (totalSeconds %
             3600) /
            60;


        int remainingSeconds =
            totalSeconds %
            60;


        if (hours > 0)
        {
            return
                $"{hours:00}:{minutes:00}:{remainingSeconds:00}";
        }


        return
            $"{minutes:00}:{remainingSeconds:00}";
    }


    private void ResolveReferences()
    {
        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
        }


        if (runManager == null)
        {
            runManager =
                FindObjectOfType<DungeonRunManager>();
        }


        if (startMenu == null)
        {
            startMenu =
                FindObjectOfType<RunStartMenu>();
        }
    }
}
