using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates limited tactical-resource pickups for each procedural
/// floor.
///
/// The system watches DungeonGenerator.GenerationVersion so every new
/// floor receives a fresh deterministic supply layout without changing
/// the dungeon-generation pipeline itself.
/// </summary>
public class DungeonResourceGenerator : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private PlayerPulseController pulseController;

    [SerializeField]
    private PlayerShaperController shaperController;


    [Header("Supplies Per Floor")]

    [SerializeField]
    private int pulsePickupsPerFloor = 1;

    [SerializeField]
    private int shaperPickupsPerFloor = 1;

    [SerializeField]
    private int pulseChargesPerPickup = 1;

    [SerializeField]
    private int shaperChargesPerPickup = 1;


    [Header("Appearance")]

    [SerializeField]
    private Color pulsePickupColour =
        new Color(
            0.20f,
            0.75f,
            1f,
            1f
        );

    [SerializeField]
    private Color shaperPickupColour =
        new Color(
            1f,
            0.35f,
            0.85f,
            1f
        );


    private int observedGenerationVersion =
        -1;


    private GameObject pickupParent;


    private void Update()
    {
        if (dungeonGenerator == null ||
            dungeonGenerator.Grid == null ||
            playerController == null)
        {
            return;
        }


        if (observedGenerationVersion ==
            dungeonGenerator.GenerationVersion)
        {
            return;
        }


        observedGenerationVersion =
            dungeonGenerator.GenerationVersion;


        GenerateResources();
    }


    private void GenerateResources()
    {
        ClearResources();


        pickupParent =
            new GameObject(
                "Generated Resource Pickups"
            );


        HashSet<Vector2Int> occupiedCells =
            BuildOccupiedCells();


        System.Random random =
            new System.Random(
                unchecked(
                    dungeonGenerator.CurrentSeed *
                        1879 +
                    86028121
                )
            );


        HashSet<Room> usedRooms =
            new HashSet<Room>();


        int pulsePlaced =
            0;


        for (int i = 0;
             i < pulsePickupsPerFloor;
             i++)
        {
            if (TrySpawnResource(
                    ResourcePickup.ResourceType.Pulse,
                    pulseChargesPerPickup,
                    pulsePickupColour,
                    random,
                    occupiedCells,
                    usedRooms))
            {
                pulsePlaced++;
            }
        }


        int shaperPlaced =
            0;


        for (int i = 0;
             i < shaperPickupsPerFloor;
             i++)
        {
            if (TrySpawnResource(
                    ResourcePickup.ResourceType.Shaper,
                    shaperChargesPerPickup,
                    shaperPickupColour,
                    random,
                    occupiedCells,
                    usedRooms))
            {
                shaperPlaced++;
            }
        }


        UnityEngine.Debug.Log(
            "========== FLOOR RESOURCE SUPPLIES ==========\n" +
            $"Seed: {dungeonGenerator.CurrentSeed}\n" +
            $"Pulse pickups: {pulsePlaced}\n" +
            $"Shaper pickups: {shaperPlaced}\n" +
            "============================================="
        );
    }


    private HashSet<Vector2Int> BuildOccupiedCells()
    {
        HashSet<Vector2Int> occupied =
            new HashSet<Vector2Int>();


        occupied.Add(
            dungeonGenerator
                .GetPlayerSpawnPosition()
        );


        occupied.Add(
            dungeonGenerator
                .GetExitPosition()
        );


        /*
         * Anchor Sigils were generated before ordinary gameplay
         * content, so their cells must remain reserved.
         */
        if (dungeonGenerator.ObjectiveManager !=
            null)
        {
            foreach (Vector2Int objectiveCell in
                     dungeonGenerator.ObjectiveManager
                         .ObjectiveCells)
            {
                occupied.Add(
                    objectiveCell
                );
            }
        }


        /*
         * Also reserve existing procedural content.
         *
         * DungeonContentGenerator already exposes the generated enemy
         * and item objects, so resources can avoid being placed
         * directly on top of them.
         */
        if (dungeonGenerator.ContentGenerator !=
            null)
        {
            foreach (GameObject enemy in
                     dungeonGenerator.ContentGenerator
                         .EnemyObjects)
            {
                AddObjectCell(
                    occupied,
                    enemy
                );
            }


            foreach (GameObject item in
                     dungeonGenerator.ContentGenerator
                         .ItemObjects)
            {
                AddObjectCell(
                    occupied,
                    item
                );
            }
        }


        return occupied;
    }


    private void AddObjectCell(
        HashSet<Vector2Int> occupied,
        GameObject target)
    {
        if (target == null)
            return;


        Vector2Int cell =
            new Vector2Int(
                Mathf.FloorToInt(
                    target.transform.position.x
                ),
                Mathf.FloorToInt(
                    target.transform.position.y
                )
            );


        occupied.Add(
            cell
        );
    }


    private bool TrySpawnResource(
        ResourcePickup.ResourceType type,
        int amount,
        Color colour,
        System.Random random,
        HashSet<Vector2Int> occupiedCells,
        HashSet<Room> usedRooms)
    {
        List<Room> candidates =
            BuildCandidateRooms(
                usedRooms,
                random
            );


        foreach (Room room in
                 candidates)
        {
            Vector2Int cell;


            if (!TryChooseCell(
                    room,
                    random,
                    occupiedCells,
                    out cell))
            {
                continue;
            }


            SpawnResource(
                type,
                cell,
                amount,
                colour
            );


            occupiedCells.Add(
                cell
            );


            usedRooms.Add(
                room
            );


            return true;
        }


        /*
         * If every suitable unused room failed, allow another resource
         * to use a previously selected room rather than losing the
         * pickup completely.
         */
        candidates =
            BuildCandidateRooms(
                null,
                random
            );


        foreach (Room room in
                 candidates)
        {
            Vector2Int cell;


            if (!TryChooseCell(
                    room,
                    random,
                    occupiedCells,
                    out cell))
            {
                continue;
            }


            SpawnResource(
                type,
                cell,
                amount,
                colour
            );


            occupiedCells.Add(
                cell
            );


            return true;
        }


        UnityEngine.Debug.LogWarning(
            $"Could not place {type} resource pickup."
        );


        return false;
    }


    private List<Room> BuildCandidateRooms(
        HashSet<Room> excludedRooms,
        System.Random random)
    {
        List<Room> rooms =
            new List<Room>();


        foreach (Room room in
                 dungeonGenerator.Rooms)
        {
            if (room == null ||
                room ==
                    dungeonGenerator.StartRoom ||
                room ==
                    dungeonGenerator.ExitRoom)
            {
                continue;
            }


            if (excludedRooms != null &&
                excludedRooms.Contains(
                    room))
            {
                continue;
            }


            rooms.Add(
                room
            );
        }


        /*
         * Prefer gameplay spaces where finding resources makes sense.
         *
         * Reward and Rest rooms come first. Deeper rooms are then
         * slightly preferred so ammunition still requires exploration.
         */
        rooms.Sort(
            (a, b) =>
            {
                int scoreA =
                    GetRoomScore(
                        a,
                        random
                    );


                int scoreB =
                    GetRoomScore(
                        b,
                        random
                    );


                return scoreB.CompareTo(
                    scoreA
                );
            }
        );


        return rooms;
    }


    private int GetRoomScore(
        Room room,
        System.Random random)
    {
        int score =
            room.GraphDistanceFromStart *
            10;


        if (room.Role ==
            RoomRole.Reward)
        {
            score +=
                100;
        }
        else if (room.Role ==
                 RoomRole.Rest)
        {
            score +=
                70;
        }
        else if (room.Role ==
                 RoomRole.Elite)
        {
            score +=
                35;
        }


        score +=
            random.Next(
                0,
                10
            );


        return score;
    }


    private bool TryChooseCell(
        Room room,
        System.Random random,
        HashSet<Vector2Int> occupiedCells,
        out Vector2Int selectedCell)
    {
        selectedCell =
            Vector2Int.zero;


        const int attempts =
            40;


        for (int attempt = 0;
             attempt < attempts;
             attempt++)
        {
            int x =
                random.Next(
                    room.Bounds.xMin,
                    room.Bounds.xMax
                );


            int y =
                random.Next(
                    room.Bounds.yMin,
                    room.Bounds.yMax
                );


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


            if (occupiedCells.Contains(
                    candidate))
            {
                continue;
            }


            selectedCell =
                candidate;


            return true;
        }


        return false;
    }


    private void SpawnResource(
        ResourcePickup.ResourceType type,
        Vector2Int cell,
        int amount,
        Color colour)
    {
        GameObject pickup =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );


        pickup.name =
            type ==
                ResourcePickup.ResourceType.Pulse
                ? $"Pulse Ammo ({cell.x}, {cell.y})"
                : $"Shaper Ammo ({cell.x}, {cell.y})";


        pickup.transform.SetParent(
            pickupParent.transform
        );


        ResourcePickup resourcePickup =
            pickup.AddComponent<ResourcePickup>();


        resourcePickup.Initialise(
            type,
            cell,
            amount,
            playerController,
            pulseController,
            shaperController,
            colour
        );
    }


    private void ClearResources()
    {
        if (pickupParent != null)
        {
            Destroy(
                pickupParent
            );


            pickupParent =
                null;
        }
    }
}