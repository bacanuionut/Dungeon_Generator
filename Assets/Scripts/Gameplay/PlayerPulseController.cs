using UnityEngine;

/// <summary>
/// Controls the player's limited Pulse Charges.
///
/// Charges are placed in the player's facing direction rather than
/// automatically centred on the player.
/// </summary>
public class PlayerPulseController : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private RunStatsManager runStatsManager;

    [SerializeField]
    private GameplayTutorialController gameplayTutorialController;

    [Header("Pulse Inventory")]

    [Tooltip("Pulse Charges available at the start of a run.")]
    [SerializeField]
    private int startingCharges = 1;

    [Tooltip("Maximum number of Pulse Charges the player may carry.")]
    [SerializeField]
    private int maximumCharges = 5;


    [Header("Placement")]

    [Tooltip("Key used to deploy a Pulse Charge.")]
    [SerializeField]
    private KeyCode deployKey =
        KeyCode.Q;

    [Tooltip("Maximum number of cells ahead where a charge can be placed.")]
    [SerializeField]
    private int placementDistance = 2;


    [Header("Pulse Effect")]

    [SerializeField]
    private float fuseDuration = 1f;

    [SerializeField]
    private float blastRadius = 3.5f;

    [SerializeField]
    private float normalEnemyStunDuration = 15f;

    [Tooltip(
    "The Warden is much more resistant to Pulse Charges than " +
    "ordinary enemies."
)]
    [SerializeField]
    private float wardenStunDuration = 5f;

    private int remainingCharges;

    private bool initialised;


    public int RemainingCharges =>
        remainingCharges;

    public int MaximumCharges =>
        maximumCharges;

    public KeyCode DeployKey =>
        deployKey;

    public float BlastRadius =>
        blastRadius;


    private void Start()
    {
        ResetInventoryForNewRun();
    }


    public void ResetInventoryForNewRun()
    {
        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
        }

        int targetStartingCharges =
            runStatsManager != null &&
            runStatsManager.RunActive
                ? runStatsManager.StartingPulseCharges
                : startingCharges;

        remainingCharges =
            Mathf.Clamp(
                targetStartingCharges,
                0,
                Mathf.Max(
                    0,
                    maximumCharges
                )
            );

        initialised =
            true;

        UnityEngine.Debug.Log(
            $"PULSE CHARGES: {remainingCharges}"
        );
    }


    private void Update()
    {
        if (!initialised ||
            dungeonGenerator == null ||
            dungeonGenerator.Grid == null ||
            playerController == null ||
            !playerController.IsAlive)
        {
            return;
        }


        if (Input.GetKeyDown(
                deployKey))
        {
            TryDeployPulse();
        }
    }


    /// <summary>
    /// Attempts to place a Pulse Charge forward from the player.
    ///
    /// The furthest valid walkable cell up to placementDistance is used.
    /// A wall therefore shortens the throw rather than allowing the
    /// charge to pass through geometry.
    /// </summary>
    private void TryDeployPulse()
    {
        if (remainingCharges <= 0)
        {
            UnityEngine.Debug.Log(
                "NO PULSE CHARGES REMAINING"
            );

            return;
        }


        Vector2Int facing =
            playerController.FacingDirection;


        if (facing ==
            Vector2Int.zero)
        {
            return;
        }


        Vector2Int playerCell =
            playerController.GridPosition;


        Vector2Int selectedCell =
            playerCell;


        for (int step = 1;
             step <= placementDistance;
             step++)
        {
            Vector2Int candidate =
                playerCell +
                facing *
                step;


            if (!dungeonGenerator.Grid.IsWalkable(
                    candidate))
            {
                break;
            }


            selectedCell =
                candidate;
        }


        // No valid floor directly ahead.
        if (selectedCell ==
            playerCell)
        {
            UnityEngine.Debug.Log(
                "PULSE BLOCKED - no walkable placement cell ahead."
            );

            return;
        }


        DeployPulse(
            selectedCell
        );
    }


    private void DeployPulse(
        Vector2Int gridCell)
    {
        GameObject charge =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );


        charge.name =
            $"Pulse Charge ({gridCell.x}, {gridCell.y})";


        charge.transform.position =
            new Vector3(
                gridCell.x + 0.5f,
                gridCell.y + 0.5f,
                -2.3f
            );


        Collider collider =
            charge.GetComponent<Collider>();


        if (collider != null)
        {
            Destroy(
                collider
            );
        }


        PulseCharge pulseCharge =
            charge.AddComponent<PulseCharge>();


        float effectiveNormalEnemyStunDuration =
            normalEnemyStunDuration;

        if (runStatsManager != null)
        {
            effectiveNormalEnemyStunDuration *=
                runStatsManager
                    .NormalEnemyStunDurationMultiplier;
        }

        pulseCharge.Initialise(
            dungeonGenerator,
            fuseDuration,
            blastRadius,
            effectiveNormalEnemyStunDuration,
            wardenStunDuration
        );


        remainingCharges--;

        if (runStatsManager != null)
        {
            runStatsManager.RecordPulseChargeUsed(
                1
            );
        }

        UnityEngine.Debug.Log(
            "PULSE DEPLOYED - " +
            $"Cell {gridCell}. " +
            $"Remaining charges: {remainingCharges}"
        );
    }

    /// <summary>
    /// Adds Pulse ammunition collected during exploration.
    ///
    /// Returns the number of charges actually added.
    /// </summary>
    public int AddCharges(
        int amount)
    {
        if (amount <= 0)
            return 0;


        int previous =
            remainingCharges;


        remainingCharges =
            Mathf.Clamp(
                remainingCharges +
                    amount,
                0,
                Mathf.Max(
                    0,
                    maximumCharges
                )
            );


        int added =
            remainingCharges -
            previous;


        if (added > 0)
        {
            UnityEngine.Debug.Log(
                $"PULSE AMMO COLLECTED +{added}. " +
                $"Current charges: {remainingCharges}/{maximumCharges}"
            );

            if (gameplayTutorialController == null)
            {
                gameplayTutorialController =
                    FindObjectOfType<GameplayTutorialController>();
            }

            if (gameplayTutorialController != null)
            {
                gameplayTutorialController.NotifyPulseCollected();
            }
        }


        return added;
    }
}