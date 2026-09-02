using System.Text;
using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

/// <summary>
/// Displays the important run and player state during gameplay.
///
/// The HUD currently shows:
/// - current procedural floor
/// - Anchor Sigil progress
/// - player health
/// - remaining Pulse Charges
///
/// Further resources such as Shaper Charges and Warden distance can
/// be added here later without changing the underlying game systems.
/// </summary>
public class DungeonHUD : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonRunManager runManager;

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private PlayerPulseController pulseController;

    [SerializeField]
    private PlayerShaperController shaperController;

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
    ///
    /// The text component is only changed when the displayed values
    /// have actually changed.
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
        // FLOOR
        // --------------------------------------------------------

        if (runManager != null)
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
        else
        {
            builder.Append(
                "FLOOR -"
            );
        }


        builder.AppendLine();


        // --------------------------------------------------------
        // ANCHOR SIGILS
        // --------------------------------------------------------

        FloorObjectiveManager objectiveManager =
            dungeonGenerator != null
                ? dungeonGenerator.ObjectiveManager
                : null;


        if (objectiveManager != null)
        {
            builder.Append(
                "SIGILS "
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
                "SIGILS -"
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


        // --------------------------------------------------------
        // SHAPER CHARGES
        // --------------------------------------------------------

        builder.AppendLine();


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
}