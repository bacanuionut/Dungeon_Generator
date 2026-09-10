using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

/// <summary>
/// Image-driven in-game HUD.
///
/// Permanent information:
/// - current hearts (lost hearts disappear);
/// - Pulse / Digger / coin counts;
/// - collected keys and hatch state;
/// - Warden warning block.
///
/// Context prompts / tutorial hints can be layered on separately.
/// </summary>
public class VisualDungeonHUD : MonoBehaviour
{
    [Header("Gameplay References")]

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
    private ExitHatchController exitHatchController;

    [SerializeField]
    private ResonancePuzzleManager resonancePuzzleManager;


    [Header("Health")]

    [Tooltip(
        "One Image per possible heart. Lost hearts are hidden completely."
    )]
    [SerializeField]
    private UnityEngine.UI.Image[] heartImages;


    [Header("Resources")]

    [SerializeField]
    private UnityEngine.UI.Text pulseCountText;

    [SerializeField]
    private UnityEngine.UI.Text diggerCountText;

    [SerializeField]
    private UnityEngine.UI.Text coinCountText;

    [SerializeField]
    private UnityEngine.UI.Text pulseKeyText;

    [SerializeField]
    private UnityEngine.UI.Text diggerKeyText;

    [SerializeField]
    private string pulseKeyLabel =
        "Q";

    [SerializeField]
    private string diggerKeyLabel =
        "F";


    [Header("Keys / Objective")]

    [SerializeField]
    private UnityEngine.UI.Image[] keyImages;

    [Range(0f, 1f)]
    [SerializeField]
    private float uncollectedKeyAlpha =
        0.18f;

    [SerializeField]
    private UnityEngine.UI.Text keyCountText;

    [SerializeField]
    private UnityEngine.UI.Text exitStatusText;


    [Header("Context Prompt")]

    [SerializeField]
    private GameObject interactionPromptRoot;

    [SerializeField]
    private GameObject interactionKeycapRoot;

    [SerializeField]
    private UnityEngine.UI.Text interactionKeyText;

    [SerializeField]
    private UnityEngine.UI.Text interactionPromptText;


    [Header("Warden Warning")]

    [Tooltip(
        "The Warden block remains visible throughout gameplay."
    )]
    [SerializeField]
    private GameObject wardenWarningRoot;

    [SerializeField]
    private UnityEngine.UI.Text wardenWarningText;

    [Tooltip(
        "Shows the current number of floors behind under the main Warden status."
    )]
    [SerializeField]
    private bool showWardenDistance =
        true;

    [SerializeField]
    private UnityEngine.UI.Text wardenDistanceText;


    [Header("Refresh")]

    [Min(0.02f)]
    [SerializeField]
    private float refreshInterval =
        0.08f;


    private float nextRefreshTime;


    private void Start()
    {
        ResolveReferences();

        if (pulseKeyText != null)
        {
            pulseKeyText.text =
                pulseKeyLabel;
        }

        if (diggerKeyText != null)
        {
            diggerKeyText.text =
                diggerKeyLabel;
        }

        RefreshAll();
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

        RefreshAll();
    }


    private void RefreshAll()
    {
        ResolveDynamicReferences();

        RefreshHearts();

        RefreshResources();

        RefreshKeysAndExit();

        RefreshInteractionPrompt();

        RefreshWardenWarning();
    }


    private void RefreshHearts()
    {
        if (heartImages == null ||
            playerController == null)
        {
            return;
        }

        int currentHealth =
            Mathf.Max(
                0,
                playerController.CurrentHealth
            );

        for (int i = 0;
             i < heartImages.Length;
             i++)
        {
            if (heartImages[i] == null)
                continue;

            /*
             * No empty-heart state.
             * Lost hearts simply disappear.
             */
            heartImages[i].gameObject.SetActive(
                i < currentHealth
            );
        }
    }


    private void RefreshResources()
    {
        if (pulseCountText != null)
        {
            pulseCountText.text =
                pulseController != null
                    ? pulseController
                        .RemainingCharges
                        .ToString()
                    : "-";
        }

        if (diggerCountText != null)
        {
            diggerCountText.text =
                shaperController != null
                    ? shaperController
                        .RemainingCharges
                        .ToString()
                    : "-";
        }

        if (coinCountText != null)
        {
            coinCountText.text =
                runStatsManager != null
                    ? runStatsManager
                        .CoinsCollected
                        .ToString()
                    : "-";
        }
    }


    private void RefreshKeysAndExit()
    {
        FloorObjectiveManager objectiveManager =
            dungeonGenerator != null
                ? dungeonGenerator.ObjectiveManager
                : null;

        int collected =
            objectiveManager != null
                ? objectiveManager.CollectedSigils
                : 0;

        int required =
            objectiveManager != null
                ? objectiveManager.RequiredSigils
                : 0;


        if (keyImages != null)
        {
            for (int i = 0;
                 i < keyImages.Length;
                 i++)
            {
                UnityEngine.UI.Image image =
                    keyImages[i];

                if (image == null)
                    continue;

                bool slotExists =
                    i < required;

                image.gameObject.SetActive(
                    slotExists
                );

                if (!slotExists)
                    continue;

                Color colour =
                    image.color;

                colour.a =
                    i < collected
                        ? 1f
                        : uncollectedKeyAlpha;

                image.color =
                    colour;
            }
        }


        if (keyCountText != null)
        {
            keyCountText.text =
                required > 0
                    ? $"{collected}/{required}"
                    : "-";
        }


        if (exitStatusText != null)
        {
            if (exitHatchController != null &&
                exitHatchController.IsOpen)
            {
                exitStatusText.text =
                    "DESCENT OPEN";
            }
            else if (objectiveManager != null &&
                     objectiveManager.ExitUnlocked)
            {
                exitStatusText.text =
                    "HATCH READY";
            }
            else
            {
                exitStatusText.text =
                    "HATCH SEALED";
            }
        }
    }


    private void RefreshInteractionPrompt()
    {
        if (interactionPromptRoot == null)
            return;


        if (resonancePuzzleManager != null &&
            resonancePuzzleManager.HasInteractionMessage)
        {
            interactionPromptRoot.SetActive(
                true
            );

            if (interactionKeycapRoot != null)
            {
                interactionKeycapRoot.SetActive(
                    true
                );
            }

            if (interactionKeyText != null)
            {
                interactionKeyText.text =
                    "E";
            }

            if (interactionPromptText != null)
            {
                interactionPromptText.text =
                    resonancePuzzleManager
                        .CurrentInteractionMessage;
            }

            return;
        }


        if (exitHatchController != null &&
            exitHatchController.HasProximityMessage)
        {
            interactionPromptRoot.SetActive(
                true
            );

            if (interactionKeycapRoot != null)
            {
                interactionKeycapRoot.SetActive(
                    false
                );
            }

            if (interactionPromptText != null)
            {
                interactionPromptText.text =
                    exitHatchController
                        .CurrentProximityMessage;
            }

            return;
        }


        interactionPromptRoot.SetActive(
            false
        );
    }


    private void RefreshWardenWarning()
    {
        if (wardenWarningRoot == null)
            return;


        if (wardenManager == null)
        {
            wardenWarningRoot.SetActive(
                false
            );

            return;
        }


        /*
         * Keep the Warden block visible continuously.
         *
         * This fixes the previous behaviour where it disappeared
         * after changing floor if the Warden became more than
         * one floor behind.
         */
        wardenWarningRoot.SetActive(
            true
        );


        string mainMessage;


        if (wardenManager.IsPhysicalWardenPresent)
        {
            mainMessage =
                "WARDEN HERE";
        }
        else if (wardenManager.IsWardenArriving)
        {
            mainMessage =
                "WARDEN ARRIVING";
        }
        else if (wardenManager.FloorsBehind <= 1)
        {
            mainMessage =
                "WARDEN APPROACHING";
        }
        else
        {
            mainMessage =
                "WARDEN TRACKING";
        }


        if (wardenWarningText != null)
        {
            wardenWarningText.text =
                mainMessage;
        }


        if (wardenDistanceText != null)
        {
            wardenDistanceText.gameObject.SetActive(
                showWardenDistance
            );

            if (showWardenDistance)
            {
                if (wardenManager.IsPhysicalWardenPresent)
                {
                    wardenDistanceText.text =
                        "ON THIS FLOOR";
                }
                else
                {
                    int floorsBehind =
                        Mathf.Max(
                            0,
                            wardenManager.FloorsBehind
                        );

                    wardenDistanceText.text =
                        floorsBehind == 1
                            ? "1 FLOOR BEHIND"
                            : $"{floorsBehind} FLOORS BEHIND";
                }
            }
        }
    }


    private void ResolveDynamicReferences()
    {
        if (exitHatchController == null)
        {
            exitHatchController =
                FindObjectOfType<ExitHatchController>();
        }

        if (resonancePuzzleManager == null)
        {
            resonancePuzzleManager =
                FindObjectOfType<ResonancePuzzleManager>();
        }
    }


    private void ResolveReferences()
    {
        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
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

        ResolveDynamicReferences();
    }
}