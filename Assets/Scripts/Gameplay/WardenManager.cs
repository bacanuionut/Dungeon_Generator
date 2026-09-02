using UnityEngine;

/// <summary>
/// Controls creation of the physical Warden for the current floor.
///
/// This first iteration deliberately spawns the Warden on every floor
/// after a grace period so its movement and excavation can be tested.
///
/// Once those systems work, this manager will be extended with the
/// actual cross-floor pursuit distance.
/// </summary>
public class WardenManager : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private DungeonTerrainModifier terrainModifier;


    [Header("Prototype Spawn")]

    [Tooltip(
        "Time after a new floor begins before the physical Warden " +
        "appears. This is temporary while testing the pursuit system."
    )]
    [SerializeField]
    private float spawnGracePeriod = 8f;


    private int observedGenerationVersion =
        -1;


    private float scheduledSpawnTime;


    private bool spawnScheduled;


    private WardenController activeWarden;


    public WardenController ActiveWarden =>
        activeWarden;


    private void Update()
    {
        if (dungeonGenerator == null ||
            dungeonGenerator.Grid == null ||
            playerController == null)
        {
            return;
        }


        if (observedGenerationVersion !=
            dungeonGenerator.GenerationVersion)
        {
            HandleNewFloor();
        }


        if (spawnScheduled &&
            Time.time >=
                scheduledSpawnTime)
        {
            SpawnWarden();
        }
    }


    private void HandleNewFloor()
    {
        observedGenerationVersion =
            dungeonGenerator.GenerationVersion;


        if (activeWarden != null)
        {
            Destroy(
                activeWarden.gameObject
            );


            activeWarden =
                null;
        }


        scheduledSpawnTime =
            Time.time +
            spawnGracePeriod;


        spawnScheduled =
            true;


        UnityEngine.Debug.Log(
            $"WARDEN ARRIVAL SCHEDULED - " +
            $"{spawnGracePeriod:0.0}s grace period."
        );
    }


    private void SpawnWarden()
    {
        spawnScheduled =
            false;


        Vector2Int spawnCell;


        if (!TryFindSpawnCell(
                out spawnCell))
        {
            UnityEngine.Debug.LogWarning(
                "WARDEN COULD NOT FIND A VALID SPAWN CELL."
            );

            return;
        }


        GameObject wardenObject =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );


        wardenObject.name =
            "Warden";


        Collider collider =
            wardenObject.GetComponent<Collider>();


        if (collider != null)
        {
            Destroy(
                collider
            );
        }


        activeWarden =
            wardenObject.AddComponent<WardenController>();


        activeWarden.Initialise(
            dungeonGenerator,
            playerController,
            terrainModifier,
            spawnCell
        );
    }


    /// <summary>
    /// Starts the Warden in the generated start room.
    ///
    /// This is suitable for the prototype because the player has already
    /// received the configured grace period before the Warden appears.
    /// </summary>
    private bool TryFindSpawnCell(
        out Vector2Int spawnCell)
    {
        spawnCell =
            Vector2Int.zero;


        Room startRoom =
            dungeonGenerator.StartRoom;


        if (startRoom == null)
            return false;


        Vector2Int centre =
            startRoom.Centre;


        if (dungeonGenerator.Grid.IsWalkable(
                centre))
        {
            spawnCell =
                centre;

            return true;
        }


        for (int x = startRoom.Bounds.xMin;
             x < startRoom.Bounds.xMax;
             x++)
        {
            for (int y = startRoom.Bounds.yMin;
                 y < startRoom.Bounds.yMax;
                 y++)
            {
                Vector2Int candidate =
                    new Vector2Int(
                        x,
                        y
                    );


                if (!dungeonGenerator.Grid.IsWalkable(
                        candidate))
                {
                    continue;
                }


                spawnCell =
                    candidate;

                return true;
            }
        }


        return false;
    }
}