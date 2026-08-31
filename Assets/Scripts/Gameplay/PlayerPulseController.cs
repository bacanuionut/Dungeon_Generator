using UnityEngine;

/// <summary>
/// Controls the player's limited Pulse Charges.
///
/// Charges are deliberately placed in the player's facing direction
/// rather than automatically centred on the player.
/// </summary>
public class PlayerPulseController : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerController playerController;


    [Header("Pulse Inventory")]

    [Tooltip("Pulse Charges available at the start of a run.")]
    [SerializeField]
    private int startingCharges = 2;


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


    private int remainingCharges;

    private bool initialised;


    public int RemainingCharges =>
        remainingCharges;


    private void Start()
    {
        remainingCharges =
            Mathf.Max(
                0,
                startingCharges
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


        pulseCharge.Initialise(
            dungeonGenerator,
            fuseDuration,
            blastRadius,
            normalEnemyStunDuration
        );


        remainingCharges--;


        UnityEngine.Debug.Log(
            "PULSE DEPLOYED - " +
            $"Cell {gridCell}. " +
            $"Remaining charges: {remainingCharges}"
        );
    }
}