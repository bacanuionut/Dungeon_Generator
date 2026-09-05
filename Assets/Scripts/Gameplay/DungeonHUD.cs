using System.Text;
using UnityEngine;

/// <summary>
/// Displays important gameplay and run state.
///
/// The HUD deliberately reads existing authoritative systems rather than
/// storing its own gameplay state.
/// </summary>
public class DungeonHUD : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonRunManager runManager;

    [SerializeField]
    private RunStatsManager runStatsManager;

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private PlayerPulseController pulseController;

    [SerializeField]
    private PlayerShaperController shaperController;

    [SerializeField]
    private WardenManager wardenManager;

    [SerializeField]
    private UnityEngine.UI.Text hudText;


    [Header("Display")]

    [Tooltip("Use heart symbols for player health.")]
    [SerializeField]
    private bool useHeartSymbols = true;

    [Tooltip("How often HUD values are checked.")]
    [SerializeField]
    private float refreshInterval = 0.1f;


    private float nextRefreshTime;

    private string previousDisplay;


    private void Start()
    {
        ResolveReferences();

        RefreshHUD();
    }


    private void Update()
    {
        if (Time.unscaledTime <
            nextRefreshTime)
        {
            return;
        }

        nextRefreshTime =
            Time.unscaledTime +
            refreshInterval;

        RefreshHUD();
    }


    /// <summary>
    /// Builds the current HUD text from the existing gameplay systems.
    /// The UI Text component is changed only when its content changes.
    /// </summary>
    private void RefreshHUD()
    {
        if (hudText == null)
            return;

        string display =
            BuildHUDText();

        if (display ==
            previousDisplay)
        {
            return;
        }

        hudText.text =
            display;

        previousDisplay =
            display;
    }


    private string BuildHUDText()
    {
        StringBuilder builder =
            new StringBuilder();


        // --------------------------------------------------------
        // FLOOR / RUN MODE
        // --------------------------------------------------------

        if (runManager != null)
        {
            if (runManager.IsSurvival)
            {
                builder.Append(
                    "SURVIVAL FLOOR "
                );

                builder.Append(
                    runManager.CurrentFloor
                );
            }
            else
            {
                builder.Append(
                    "FLOOR "
                );

                builder.Append(
                    runManager.CurrentFloor
                );

                builder.Append(
                    "/"
                );

                builder.Append(
                    runManager.TotalFloors
                );
            }
        }
        else
        {
            builder.Append(
                "FLOOR -"
            );
        }


        builder.AppendLine();


        // --------------------------------------------------------
        // KEYS
        // --------------------------------------------------------

        FloorObjectiveManager objectiveManager =
            dungeonGenerator != null
                ? dungeonGenerator.ObjectiveManager
                : null;

        if (objectiveManager != null)
        {
            builder.Append(
                "KEYS "
            );

            builder.Append(
                objectiveManager.CollectedSigils
            );

            builder.Append(
                "/"
            );

            builder.Append(
                objectiveManager.RequiredSigils
            );
        }
        else
        {
            builder.Append(
                "KEYS -"
            );
        }


        builder.AppendLine();


        // --------------------------------------------------------
        // HEALTH
        // --------------------------------------------------------

        builder.Append(
            "HEALTH "
        );

        if (playerController != null)
        {
            if (useHeartSymbols)
            {
                builder.Append(
                    BuildHealthHearts(
                        playerController.CurrentHealth,
                        playerController.MaximumHealth
                    )
                );
            }
            else
            {
                builder.Append(
                    playerController.CurrentHealth
                );

                builder.Append(
                    "/"
                );

                builder.Append(
                    playerController.MaximumHealth
                );
            }
        }
        else
        {
            builder.Append(
                "-"
            );
        }


        builder.AppendLine();


        // --------------------------------------------------------
        // RUN COINS
        // --------------------------------------------------------

        builder.Append(
            "COINS "
        );

        if (runStatsManager != null)
        {
            builder.Append(
                runStatsManager.CoinsCollected
            );
        }
        else
        {
            builder.Append(
                "-"
            );
        }


        builder.AppendLine();


        // --------------------------------------------------------
        // PULSE CHARGES
        // --------------------------------------------------------

        builder.Append(
            "PULSE "
        );

        if (pulseController != null)
        {
            builder.Append(
                pulseController.RemainingCharges
            );
        }
        else
        {
            builder.Append(
                "-"
            );
        }


        builder.AppendLine();


        // --------------------------------------------------------
        // SHAPER CHARGES
        // --------------------------------------------------------

        builder.Append(
            "SHAPER "
        );

        if (shaperController != null)
        {
            builder.Append(
                shaperController.RemainingCharges
            );
        }
        else
        {
            builder.Append(
                "-"
            );
        }


        builder.AppendLine();


        // --------------------------------------------------------
        // WARDEN PURSUIT
        // --------------------------------------------------------

        builder.Append(
            "WARDEN "
        );

        if (wardenManager != null)
        {
            builder.Append(
                wardenManager.GetHUDStatus()
            );
        }
        else
        {
            builder.Append(
                "-"
            );
        }


        // --------------------------------------------------------
        // GAME STATE
        // --------------------------------------------------------

        if (playerController != null &&
            !playerController.IsAlive)
        {
            builder.AppendLine();

            builder.Append(
                "GAME OVER"
            );
        }
        else if (runManager != null &&
                 runManager.RunComplete)
        {
            builder.AppendLine();

            builder.Append(
                "RUN COMPLETE"
            );
        }


        return builder.ToString();
    }


    /// <summary>
    /// Creates a simple visual health display.
    ///
    /// Example:
    /// 4 / 5 health = ♥♥♥♥♡
    /// </summary>
    private string BuildHealthHearts(
        int currentHealth,
        int maximumHealth)
    {
        StringBuilder hearts =
            new StringBuilder();

        int safeMaximum =
            Mathf.Max(
                0,
                maximumHealth
            );

        int safeCurrent =
            Mathf.Clamp(
                currentHealth,
                0,
                safeMaximum
            );

        for (int i = 0;
             i < safeMaximum;
             i++)
        {
            hearts.Append(
                i < safeCurrent
                    ? '♥'
                    : '♡'
            );
        }

        return hearts.ToString();
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

        if (dungeonGenerator == null)
        {
            dungeonGenerator =
                FindObjectOfType<DungeonGenerator>();
        }

        if (playerController == null)
        {
            playerController =
                FindObjectOfType<PlayerController>();
        }

        if (pulseController == null)
        {
            pulseController =
                FindObjectOfType<PlayerPulseController>();
        }

        if (shaperController == null)
        {
            shaperController =
                FindObjectOfType<PlayerShaperController>();
        }

        if (wardenManager == null)
        {
            wardenManager =
                FindObjectOfType<WardenManager>();
        }
    }
}
