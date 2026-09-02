using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent-style dungeon pursuer prototype.
///
/// The Warden uses weighted A* to chase the player. Existing floor is
/// cheap to traverse while solid terrain is expensive but possible.
///
/// If weighted A* determines that excavation provides a worthwhile
/// route, the Warden slowly converts the next solid cell into floor
/// through DungeonTerrainModifier before continuing.
/// </summary>
public class WardenController : MonoBehaviour
{
    [Header("Movement")]

    [SerializeField]
    private float movementDelay = 0.42f;


    [Header("Pathfinding")]

    [Tooltip(
        "Relative A* cost of travelling through one solid cell. " +
        "Higher values make the Warden prefer existing corridors."
    )]
    [SerializeField]
    private float solidTerrainCost = 6f;

    [Tooltip(
        "Small area outside the generated floor bounds which A* may " +
        "consider while excavating."
    )]
    [SerializeField]
    private int searchMargin = 2;


    [Header("Combat")]

    [SerializeField]
    private int attackDamage = 2;

    [SerializeField]
    private float attackCooldown = 1.25f;


    [Header("Appearance")]

    [SerializeField]
    private Color normalColour =
        new Color(
            0.55f,
            0.05f,
            0.65f,
            1f
        );

    [SerializeField]
    private Color stunnedColour =
        new Color(
            0.35f,
            0.65f,
            1f,
            1f
        );


    private DungeonGenerator dungeonGenerator;

    private PlayerController playerController;

    private DungeonTerrainModifier terrainModifier;


    private Vector2Int gridPosition;


    private float nextActionTime;

    private float nextAttackTime;


    private bool initialised;

    private bool digging;


    private bool stunned;

    private float stunEndTime;


    private Renderer wardenRenderer;

    private Material wardenMaterial;


    public Vector2Int GridPosition =>
        gridPosition;


    public bool IsStunned =>
        stunned;


    public void Initialise(
        DungeonGenerator generator,
        PlayerController player,
        DungeonTerrainModifier modifier,
        Vector2Int spawnCell)
    {
        dungeonGenerator =
            generator;

        playerController =
            player;

        terrainModifier =
            modifier;

        gridPosition =
            spawnCell;


        transform.position =
            CellToWorld(
                gridPosition
            );


        InitialiseVisual();


        initialised =
            true;


        nextActionTime =
            Time.time +
            movementDelay;


        UnityEngine.Debug.Log(
            "========== WARDEN ENTERED FLOOR ==========\n" +
            $"Seed: {dungeonGenerator.CurrentSeed}\n" +
            $"Spawn: {gridPosition}\n" +
            $"Solid terrain cost: {solidTerrainCost:0.0}\n" +
            "=========================================="
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


        if (stunned)
        {
            UpdateStun();

            return;
        }


        if (digging)
        {
            return;
        }


        if (IsAdjacentToPlayer())
        {
            TryAttackPlayer();

            return;
        }


        if (Time.time <
            nextActionTime)
        {
            return;
        }


        TakePursuitStep();
    }


    private void TakePursuitStep()
    {
        List<Vector2Int> path =
            WardenPathfinder.FindPath(
                dungeonGenerator.Grid,
                gridPosition,
                playerController.GridPosition,
                solidTerrainCost,
                searchMargin
            );


        if (path == null ||
            path.Count < 2)
        {
            nextActionTime =
                Time.time +
                movementDelay;

            return;
        }


        Vector2Int nextCell =
            path[1];


        if (dungeonGenerator.Grid.IsWalkable(
                nextCell))
        {
            MoveTo(
                nextCell
            );


            nextActionTime =
                Time.time +
                movementDelay;


            return;
        }


        /*
         * Weighted A* deliberately chose a solid cell.
         *
         * The Warden remains on its current cell while that terrain is
         * slowly opened.
         */
        TryExcavate(
            nextCell
        );
    }


    private void TryExcavate(
        Vector2Int targetCell)
    {
        if (terrainModifier == null ||
            terrainModifier.IsModifyingTerrain)
        {
            nextActionTime =
                Time.time +
                0.1f;

            return;
        }


        int operationSeed =
            unchecked(
                dungeonGenerator.CurrentSeed *
                    92821 +
                gridPosition.x *
                    73856093 +
                gridPosition.y *
                    19349663 +
                targetCell.x *
                    83492791 +
                targetCell.y *
                    297121507
            );


        bool started =
            terrainModifier.TryCarveWardenCell(
                targetCell,
                operationSeed,
                () =>
                {
                    digging =
                        false;


                    /*
                     * The Warden does not teleport into the newly
                     * created floor. On its next decision it will
                     * physically move into it.
                     */
                    nextActionTime =
                        Time.time +
                        0.05f;
                }
            );


        if (!started)
        {
            nextActionTime =
                Time.time +
                0.1f;

            return;
        }


        digging =
            true;


        UnityEngine.Debug.Log(
            $"WARDEN CHOSE EXCAVATION - " +
            $"{gridPosition} -> {targetCell}"
        );
    }


    private void MoveTo(
        Vector2Int cell)
    {
        gridPosition =
            cell;


        transform.position =
            CellToWorld(
                gridPosition
            );
    }


    private bool IsAdjacentToPlayer()
    {
        Vector2Int difference =
            playerController.GridPosition -
            gridPosition;


        int distance =
            Mathf.Abs(
                difference.x
            ) +
            Mathf.Abs(
                difference.y
            );


        return distance <=
               1;
    }


    private void TryAttackPlayer()
    {
        if (Time.time <
            nextAttackTime)
        {
            return;
        }


        playerController.TakeDamage(
            attackDamage
        );


        nextAttackTime =
            Time.time +
            attackCooldown;


        UnityEngine.Debug.Log(
            $"WARDEN ATTACKED PLAYER - Damage {attackDamage}"
        );
    }


    /// <summary>
    /// Pulse Charges can interrupt the Warden, but for a much shorter
    /// period than ordinary enemies.
    /// </summary>
    public void ApplyStun(
        float duration)
    {
        if (!initialised ||
            duration <= 0f)
        {
            return;
        }


        stunned =
            true;


        stunEndTime =
            Mathf.Max(
                stunEndTime,
                Time.time +
                duration
            );


        if (wardenMaterial != null)
        {
            wardenMaterial.color =
                stunnedColour;
        }


        UnityEngine.Debug.Log(
            $"WARDEN STUNNED for {duration:0.0} seconds."
        );
    }


    private void UpdateStun()
    {
        if (Time.time <
            stunEndTime)
        {
            return;
        }


        stunned =
            false;


        if (wardenMaterial != null)
        {
            wardenMaterial.color =
                normalColour;
        }


        nextActionTime =
            Time.time +
            movementDelay;


        UnityEngine.Debug.Log(
            "WARDEN RECOVERED FROM STUN"
        );
    }


    private void InitialiseVisual()
    {
        transform.localScale =
            new Vector3(
                0.85f,
                0.85f,
                1f
            );


        wardenRenderer =
            GetComponent<Renderer>();


        if (wardenRenderer == null)
            return;


        Shader shader =
            Shader.Find(
                "Unlit/Color"
            );


        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Sprites/Default"
                );
        }


        if (shader == null)
            return;


        wardenMaterial =
            new Material(
                shader
            );


        wardenMaterial.color =
            normalColour;


        wardenRenderer.sharedMaterial =
            wardenMaterial;
    }


    private Vector3 CellToWorld(
        Vector2Int cell)
    {
        return new Vector3(
            cell.x + 0.5f,
            cell.y + 0.5f,
            -2.1f
        );
    }


    private void OnDestroy()
    {
        if (wardenMaterial != null)
        {
            Destroy(
                wardenMaterial
            );
        }
    }
}