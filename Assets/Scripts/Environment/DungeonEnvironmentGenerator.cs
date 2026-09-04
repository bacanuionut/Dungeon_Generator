using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places deterministic environmental decoration on a generated dungeon floor.
///
/// Solid props are restricted to visually sensible room positions and reserve
/// navigation cells separately from the underlying dungeon geometry.
///
/// Cobwebs are visual-only details. Small cobwebs are attached to top wall
/// faces, while larger cobwebs prefer floor corners and alcoves.
/// </summary>
public class DungeonEnvironmentGenerator : MonoBehaviour
{
    private enum PropFamily
    {
        Pottery,
        Rock,
        Sack,
        SackPile,
        Crate,
        Timber,
        Table
    }


    private struct PropWeights
    {
        public int Pottery;
        public int Rock;
        public int Sack;
        public int SackPile;
        public int Crate;
        public int Timber;
        public int Table;

        public PropWeights(
            int pottery,
            int rock,
            int sack,
            int sackPile,
            int crate,
            int timber,
            int table)
        {
            Pottery = pottery;
            Rock = rock;
            Sack = sack;
            SackPile = sackPile;
            Crate = crate;
            Timber = timber;
            Table = table;
        }
    }


    private struct DetailPlacement
    {
        public Vector2Int AnchorCell;
        public Vector3 WorldPosition;

        public DetailPlacement(
            Vector2Int anchorCell,
            Vector3 worldPosition)
        {
            AnchorCell = anchorCell;
            WorldPosition = worldPosition;
        }
    }


    /// <summary>
    /// Deterministic per-room decoration profile.
    ///
    /// Theme and semantic room role provide broad biases, while these values
    /// introduce reproducible room-to-room variation in density, clustering,
    /// furniture, cobwebs and prop-family preference.
    /// </summary>
    private struct RoomDecorationProfile
    {
        public float DensityScale;
        public float AlcoveScale;
        public float ClusterScale;
        public float WebScale;
        public float TableScale;

        public float PotteryScale;
        public float RockScale;
        public float SackScale;
        public float SackPileScale;
        public float CrateScale;
        public float TimberScale;
        public float TableFamilyScale;

        public int MinimumSolidProps;
    }


    [Header("Solid Prop Sprites")]

    [SerializeField]
    private Sprite[] potterySprites;

    [SerializeField]
    private Sprite[] rockSprites;

    [SerializeField]
    private Sprite[] sackSprites;

    [SerializeField]
    private Sprite sackPileSprite;

    [SerializeField]
    private Sprite[] crateSprites;

    [SerializeField]
    private Sprite[] timberSprites;

    [SerializeField]
    private Sprite[] tableSprites;

    [SerializeField]
    private Sprite chairSprite;


    [Header("Walk-Over Detail Sprites")]

    [SerializeField]
    private Sprite[] webSprites;


    [Header("Placement")]

    [Range(0f, 2f)]
    [SerializeField]
    private float densityMultiplier = 1.25f;

    [Range(0, 8)]
    [SerializeField]
    private int maximumSolidPropsPerRoom = 5;

    [Range(0, 5)]
    [SerializeField]
    private int maximumWebsPerRoom = 3;

    [Range(0, 3)]
    [SerializeField]
    private int protectedCellClearance = 1;

    [Range(0, 2)]
    [SerializeField]
    private int corridorClearance = 1;

    [Range(0f, 1f)]
    [SerializeField]
    private float clusterChance = 0.65f;

    [Range(0, 3)]
    [SerializeField]
    private int maximumExtraPropsPerCluster = 2;

    [Tooltip(
        "How far outside the original BSP room rectangle organic room " +
        "cells may be considered part of that room for decoration."
    )]
    [Range(1, 4)]
    [SerializeField]
    private int organicDecorationReach = 3;

    [Tooltip(
        "Chance that a suitable U-shaped corner/alcove receives a small " +
        "rubble, timber, crate or sack cluster."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float alcoveFillChance = 0.75f;

    [Tooltip("Maximum dedicated alcove clusters generated in one room region.")]
    [Range(0, 4)]
    [SerializeField]
    private int maximumAlcoveClustersPerRoom = 2;


    [Header("Display")]

    [SerializeField]
    private float solidPropZ = -1.85f;

    [SerializeField]
    private float detailZ = -1.70f;

    [SerializeField]
    private float wallHugOffset = 0.18f;

    [SerializeField]
    private float smallWebWallOffsetY = 0.28f;

    [SerializeField]
    private float largeWebCornerOffset = 0.18f;


    private GameObject environmentParent;

    private DungeonGrid activeGrid;

    private readonly HashSet<Vector2Int> solidPropCells =
        new HashSet<Vector2Int>();

    private readonly HashSet<Vector2Int> detailCells =
        new HashSet<Vector2Int>();

    private readonly HashSet<Vector2Int> protectedCells =
        new HashSet<Vector2Int>();

    // The original rectangular room plus the connected organic CA-grown
    // floor immediately around it. This lets decoration occupy the U-shaped
    // pockets created by room shaping instead of stopping at the BSP rectangle.
    private readonly HashSet<Vector2Int> currentRoomRegion =
        new HashSet<Vector2Int>();

    private int solidPropsGenerated;
    private int webDetailsGenerated;
    private int alcoveClustersGenerated;
    private int tablesGenerated;
    private int chairsGenerated;
    private int roomsDecorated;
    private int roomsUsingFallback;

    private RoomDecorationProfile currentRoomProfile;

    public IReadOnlyCollection<Vector2Int> SolidPropCells =>
        solidPropCells;


    public void ClearEnvironment()
    {
        if (activeGrid != null)
        {
            activeGrid.ClearNavigationBlockers();
        }

        activeGrid = null;

        solidPropCells.Clear();
        detailCells.Clear();
        protectedCells.Clear();
        currentRoomRegion.Clear();

        if (environmentParent != null)
        {
            Destroy(environmentParent);
            environmentParent = null;
        }
    }


    public void GenerateEnvironment(
        DungeonGenerator generator)
    {
        ClearEnvironment();

        if (generator == null ||
            generator.Grid == null ||
            generator.Rooms == null ||
            generator.Rooms.Count == 0)
        {
            UnityEngine.Debug.LogError(
                "Environmental generation failed because dungeon data was incomplete."
            );

            return;
        }

        activeGrid = generator.Grid;

        environmentParent = new GameObject(
            "Generated Environment"
        );

        environmentParent.transform.SetParent(
            transform
        );

        DungeonVisualTheme theme =
            DungeonVisualThemeGenerator.Generate(
                generator.CurrentSeed,
                generator.CurrentFloorDepth
            );

        int environmentSeed =
            unchecked(
                generator.CurrentSeed * 73856093 ^
                theme.ThemeSeed * 19349663 ^
                83492791
            );

        System.Random random =
            new System.Random(environmentSeed);

        BuildProtectedCells(generator);

        solidPropsGenerated = 0;
        webDetailsGenerated = 0;
        alcoveClustersGenerated = 0;
        tablesGenerated = 0;
        chairsGenerated = 0;
        roomsDecorated = 0;
        roomsUsingFallback = 0;

        foreach (Room room in generator.Rooms)
        {
            if (!ShouldDecorateRoom(room))
                continue;

            BuildCurrentRoomRegion(room);

            if (currentRoomRegion.Count == 0)
                continue;

            /*
             * Every room gets its own deterministic random stream.
             * This keeps decoration reproducible without coupling one room's
             * result to how many random values another room consumed.
             */
            int roomEnvironmentSeed =
                CalculateRoomEnvironmentSeed(
                    generator.CurrentSeed,
                    theme.ThemeSeed,
                    room
                );

            System.Random roomRandom =
                new System.Random(
                    roomEnvironmentSeed
                );

            currentRoomProfile =
                GenerateRoomDecorationProfile(
                    room,
                    theme,
                    roomRandom
                );

            int propsBefore =
                solidPropsGenerated;

            int detailsBefore =
                webDetailsGenerated;

            GenerateAlcoveClustersForRoom(
                room,
                theme,
                roomRandom
            );

            TryPlaceFeatureTable(
                room,
                theme,
                roomRandom
            );

            GenerateSolidPropsForRoom(
                room,
                theme,
                roomRandom
            );

            GenerateWebsForRoom(
                room,
                theme,
                roomRandom
            );

            if (solidPropsGenerated == propsBefore &&
                webDetailsGenerated == detailsBefore)
            {
                if (EnsureRoomHasDecoration(
                        room,
                        theme,
                        roomRandom))
                {
                    roomsUsingFallback++;
                }
            }

            if (solidPropsGenerated > propsBefore ||
                webDetailsGenerated > detailsBefore)
            {
                roomsDecorated++;
            }
        }

        activeGrid.AddNavigationBlockers(solidPropCells);

        UnityEngine.Debug.Log(
            "========== PROCEDURAL ENVIRONMENT ==========" +
            "\n" +
            $"Seed: {generator.CurrentSeed}\n" +
            $"Depth: {generator.CurrentFloorDepth}\n" +
            $"Theme: {theme.ThemeName}\n" +
            $"Environmental detail amount: {theme.EnvironmentalDetailAmount:0.00}\n" +
            $"Rooms decorated: {roomsDecorated}/{generator.Rooms.Count}\n" +
            $"Rooms using safety fallback: {roomsUsingFallback}\n" +
            $"Solid props: {solidPropsGenerated}\n" +
            $"Blocked navigation cells: {solidPropCells.Count}\n" +
            $"Alcove clusters: {alcoveClustersGenerated}\n" +
            $"Tables: {tablesGenerated}\n" +
            $"Chairs: {chairsGenerated}\n" +
            $"Walk-over cobweb details: {webDetailsGenerated}\n" +
            "Floor connectivity preserved: YES\n" +
            "============================================"
        );
    }


    private bool ShouldDecorateRoom(Room room)
    {
        /*
         * Every semantic room is decorated.
         *
         * Start, Exit and Puzzle rooms are no longer excluded. Their important
         * gameplay cells remain protected while the surrounding room geometry
         * can still receive procedural dressing.
         */
        return room != null;
    }


    private int CalculateRoomEnvironmentSeed(
        int floorSeed,
        int themeSeed,
        Room room)
    {
        return unchecked(
            floorSeed * 73856093 ^
            themeSeed * 19349663 ^
            room.Centre.x * 83492791 ^
            room.Centre.y * 265443576 ^
            room.GraphDistanceFromStart * 97531 ^
            ((int)room.Role + 1) * 486187739
        );
    }


    private RoomDecorationProfile GenerateRoomDecorationProfile(
        Room room,
        DungeonVisualTheme theme,
        System.Random random)
    {
        RoomDecorationProfile profile =
            new RoomDecorationProfile();

        float roleDensity = 1f;
        float roleTable = 1f;
        float roleWeb = 1f;

        switch (room.Role)
        {
            case RoomRole.Start:
                roleDensity = 0.86f;
                roleTable = 0.70f;
                roleWeb = 0.80f;
                break;

            case RoomRole.Exit:
                roleDensity = 0.90f;
                roleTable = 0.55f;
                roleWeb = 0.85f;
                break;

            case RoomRole.Puzzle:
                roleDensity = 0.82f;
                roleTable = 0.45f;
                roleWeb = 0.95f;
                break;

            case RoomRole.Reward:
                roleDensity = 1.15f;
                roleTable = 1.25f;
                roleWeb = 0.90f;
                break;

            case RoomRole.Rest:
                roleDensity = 1.10f;
                roleTable = 1.35f;
                roleWeb = 0.80f;
                break;

            case RoomRole.Elite:
                roleDensity = 1.08f;
                roleTable = 0.80f;
                roleWeb = 1.05f;
                break;

            case RoomRole.Secret:
                roleDensity = 1.22f;
                roleTable = 1.05f;
                roleWeb = 1.25f;
                break;
        }

        float geometryScale =
            Mathf.Lerp(
                0.90f,
                1.18f,
                Mathf.Clamp01(
                    currentRoomRegion.Count /
                    70f
                )
            );

        float depthScale =
            Mathf.Lerp(
                0.95f,
                1.12f,
                Mathf.Clamp01(
                    theme.EnvironmentalDetailAmount
                )
            );

        profile.DensityScale =
            roleDensity *
            geometryScale *
            depthScale *
            NextFloat(random, 0.82f, 1.20f);

        profile.AlcoveScale =
            NextFloat(random, 0.75f, 1.30f);

        profile.ClusterScale =
            NextFloat(random, 0.72f, 1.35f);

        profile.WebScale =
            roleWeb *
            NextFloat(random, 0.65f, 1.35f);

        profile.TableScale =
            roleTable *
            NextFloat(random, 0.65f, 1.35f);

        profile.PotteryScale =
            NextFloat(random, 0.65f, 1.40f);

        profile.RockScale =
            NextFloat(random, 0.65f, 1.40f);

        profile.SackScale =
            NextFloat(random, 0.65f, 1.40f);

        profile.SackPileScale =
            NextFloat(random, 0.65f, 1.40f);

        profile.CrateScale =
            NextFloat(random, 0.65f, 1.40f);

        profile.TimberScale =
            NextFloat(random, 0.65f, 1.40f);

        profile.TableFamilyScale =
            NextFloat(random, 0.65f, 1.40f);

        int minimum =
            currentRoomRegion.Count >= 30
                ? 2
                : 1;

        if (room.Role == RoomRole.Start ||
            room.Role == RoomRole.Exit ||
            room.Role == RoomRole.Puzzle)
        {
            minimum =
                currentRoomRegion.Count >= 40
                    ? 2
                    : 1;
        }

        profile.MinimumSolidProps =
            minimum;

        return profile;
    }


    private float NextFloat(
        System.Random random,
        float minimum,
        float maximum)
    {
        return minimum +
               (float)random.NextDouble() *
               (maximum - minimum);
    }


    private void BuildProtectedCells(
        DungeonGenerator generator)
    {
        protectedCells.Clear();

        AddProtectedCell(generator.GetPlayerSpawnPosition());
        AddProtectedCell(generator.GetExitPosition());

        if (generator.ObjectiveManager != null)
        {
            foreach (Vector2Int objectiveCell in
                     generator.ObjectiveManager.ObjectiveCells)
            {
                AddProtectedCell(objectiveCell);
            }
        }

        DungeonContentGenerator content = generator.ContentGenerator;

        if (content == null)
            return;

        foreach (GameObject enemy in content.EnemyObjects)
        {
            AddProtectedObjectCell(enemy);
        }

        foreach (GameObject item in content.ItemObjects)
        {
            AddProtectedObjectCell(item);
        }
    }


    private void AddProtectedObjectCell(
        GameObject objectToProtect)
    {
        if (objectToProtect == null)
            return;

        Vector3 position = objectToProtect.transform.position;

        AddProtectedCell(
            new Vector2Int(
                Mathf.FloorToInt(position.x),
                Mathf.FloorToInt(position.y)
            )
        );
    }


    private void AddProtectedCell(
        Vector2Int centre)
    {
        int clearance = Mathf.Max(0, protectedCellClearance);

        for (int x = -clearance; x <= clearance; x++)
        {
            for (int y = -clearance; y <= clearance; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) > clearance)
                    continue;

                protectedCells.Add(
                    centre +
                    new Vector2Int(x, y)
                );
            }
        }
    }


    /// <summary>
    /// Builds the usable visual region for one room.
    ///
    /// The original room rectangle is used as the seed. Connected organic
    /// cells created by the CA room-shaping stage are then included within a
    /// small distance of that rectangle. Corridors and runtime Shaper/Warden
    /// cells are deliberately excluded.
    /// </summary>
    private void BuildCurrentRoomRegion(
        Room room)
    {
        currentRoomRegion.Clear();

        if (room == null || activeGrid == null)
            return;

        Queue<Vector2Int> frontier =
            new Queue<Vector2Int>();

        for (int x = room.Bounds.xMin;
             x < room.Bounds.xMax;
             x++)
        {
            for (int y = room.Bounds.yMin;
                 y < room.Bounds.yMax;
                 y++)
            {
                Vector2Int cell =
                    new Vector2Int(x, y);

                if (!activeGrid.IsRoomCell(cell) ||
                    !activeGrid.IsWalkable(cell) ||
                    activeGrid.IsCorridorCell(cell))
                {
                    continue;
                }

                if (currentRoomRegion.Add(cell))
                {
                    frontier.Enqueue(cell);
                }
            }
        }

        while (frontier.Count > 0)
        {
            Vector2Int current =
                frontier.Dequeue();

            TryAddRoomRegionNeighbour(
                room,
                current + Vector2Int.up,
                frontier
            );

            TryAddRoomRegionNeighbour(
                room,
                current + Vector2Int.down,
                frontier
            );

            TryAddRoomRegionNeighbour(
                room,
                current + Vector2Int.left,
                frontier
            );

            TryAddRoomRegionNeighbour(
                room,
                current + Vector2Int.right,
                frontier
            );
        }
    }


    private void TryAddRoomRegionNeighbour(
        Room room,
        Vector2Int candidate,
        Queue<Vector2Int> frontier)
    {
        if (currentRoomRegion.Contains(candidate) ||
            !activeGrid.IsWalkable(candidate) ||
            activeGrid.IsCorridorCell(candidate) ||
            activeGrid.IsDynamicFloorCell(candidate))
        {
            return;
        }

        bool originalRoomCell =
            room.Contains(candidate) &&
            activeGrid.IsRoomCell(candidate);

        bool connectedOrganicCell =
            activeGrid.IsOrganicRoomCell(candidate) &&
            IsWithinOrganicDecorationReach(room, candidate);

        if (!originalRoomCell && !connectedOrganicCell)
            return;

        currentRoomRegion.Add(candidate);
        frontier.Enqueue(candidate);
    }


    private bool IsWithinOrganicDecorationReach(
        Room room,
        Vector2Int cell)
    {
        int reach =
            Mathf.Max(1, organicDecorationReach);

        int minX =
            room.Bounds.xMin - reach;

        int maxX =
            room.Bounds.xMax - 1 + reach;

        int minY =
            room.Bounds.yMin - reach;

        int maxY =
            room.Bounds.yMax - 1 + reach;

        return cell.x >= minX &&
               cell.x <= maxX &&
               cell.y >= minY &&
               cell.y <= maxY;
    }


    /// <summary>
    /// Gives U-shaped pockets and deep corners their own decoration pass.
    /// These locations are visually useful but were missed when decoration
    /// was limited to the original rectangular BSP room cells.
    /// </summary>
    private void GenerateAlcoveClustersForRoom(
        Room room,
        DungeonVisualTheme theme,
        System.Random random)
    {
        if (maximumAlcoveClustersPerRoom <= 0 ||
            currentRoomRegion.Count == 0)
        {
            return;
        }

        float density =
            Mathf.Clamp01(
                theme.EnvironmentalDetailAmount *
                densityMultiplier
            );

        List<Vector2Int> deepAlcoves =
            new List<Vector2Int>();

        List<Vector2Int> broadPockets =
            new List<Vector2Int>();

        foreach (Vector2Int cell in currentRoomRegion)
        {
            if (!IsSafeSolidCell(room, cell))
                continue;

            int immediateWalls =
                CountCardinalWallNeighbours(cell);

            if (immediateWalls >= 2)
            {
                deepAlcoves.Add(cell);
                continue;
            }

            if (GetNearbyWallScore(cell) >= 4)
            {
                broadPockets.Add(cell);
            }
        }

        Shuffle(deepAlcoves, random);
        Shuffle(broadPockets, random);

        List<Vector2Int> candidates =
            new List<Vector2Int>();

        candidates.AddRange(deepAlcoves);
        candidates.AddRange(broadPockets);

        int clustersPlaced = 0;

        foreach (Vector2Int candidate in candidates)
        {
            if (clustersPlaced >= maximumAlcoveClustersPerRoom)
                break;

            float chance =
                alcoveFillChance *
                currentRoomProfile.AlcoveScale *
                Mathf.Lerp(0.65f, 1.15f, density);

            if (random.NextDouble() > chance)
                continue;

            PropFamily family =
                ChooseAlcoveFamily(
                    theme,
                    random
                );

            if (!TryPlaceFamily(
                    room,
                    candidate,
                    family,
                    random))
            {
                continue;
            }

            clustersPlaced++;
            alcoveClustersGenerated++;

            if (family == PropFamily.Rock ||
                family == PropFamily.Timber ||
                family == PropFamily.Crate ||
                family == PropFamily.Sack)
            {
                TryPlaceCluster(
                    room,
                    candidate,
                    family,
                    random
                );
            }
        }
    }


    private PropFamily ChooseAlcoveFamily(
        DungeonVisualTheme theme,
        System.Random random)
    {
        List<PropFamily> families =
            new List<PropFamily>();

        List<int> weights =
            new List<int>();

        AddAvailableFamily(
            families,
            weights,
            PropFamily.Rock,
            5
        );

        AddAvailableFamily(
            families,
            weights,
            PropFamily.Timber,
            theme.ThemeName == "Ashen Ruins" ||
            theme.ThemeName == "Ember Ruins"
                ? 7
                : 4
        );

        AddAvailableFamily(
            families,
            weights,
            PropFamily.Crate,
            4
        );

        AddAvailableFamily(
            families,
            weights,
            PropFamily.Sack,
            3
        );

        AddAvailableFamily(
            families,
            weights,
            PropFamily.Pottery,
            theme.ThemeName == "Dust Temple" ||
            theme.ThemeName == "Verdigris Crypt"
                ? 5
                : 2
        );

        if (families.Count == 0)
            return PropFamily.Pottery;

        int total = 0;

        for (int i = 0; i < weights.Count; i++)
        {
            total += weights[i];
        }

        int roll =
            random.Next(0, Mathf.Max(1, total));

        int running = 0;

        for (int i = 0; i < families.Count; i++)
        {
            running += weights[i];

            if (roll < running)
                return families[i];
        }

        return families[families.Count - 1];
    }


    private int CountCardinalWallNeighbours(
        Vector2Int cell)
    {
        int count = 0;

        if (IsWallCell(cell + Vector2Int.up))
            count++;

        if (IsWallCell(cell + Vector2Int.down))
            count++;

        if (IsWallCell(cell + Vector2Int.left))
            count++;

        if (IsWallCell(cell + Vector2Int.right))
            count++;

        return count;
    }


    private int GetNearbyWallScore(
        Vector2Int cell)
    {
        int score =
            CountCardinalWallNeighbours(cell) * 3;

        if (IsWallCell(cell + Vector2Int.up * 2))
            score++;

        if (IsWallCell(cell + Vector2Int.down * 2))
            score++;

        if (IsWallCell(cell + Vector2Int.left * 2))
            score++;

        if (IsWallCell(cell + Vector2Int.right * 2))
            score++;

        return score;
    }


    private void TryPlaceFeatureTable(
        Room room,
        DungeonVisualTheme theme,
        System.Random random)
    {
        if (!HasSprites(tableSprites) ||
            chairSprite == null ||
            room == null ||
            currentRoomRegion.Count == 0 ||
            room.Width < 6 ||
            room.Height < 5)
        {
            return;
        }

        float chance = 0f;

        switch (room.Role)
        {
            case RoomRole.Rest:
                chance = 0.85f;
                break;

            case RoomRole.Reward:
                chance = 0.75f;
                break;

            case RoomRole.Combat:
                chance = 0.48f;
                break;

            case RoomRole.Elite:
                chance = 0.42f;
                break;

            case RoomRole.Start:
                chance = 0.26f;
                break;

            case RoomRole.Exit:
                chance = 0.22f;
                break;

            case RoomRole.Puzzle:
                chance = 0.18f;
                break;

            case RoomRole.Secret:
                chance = 0.58f;
                break;

            default:
                chance = 0.32f;
                break;
        }

        chance *=
            currentRoomProfile.TableScale *
            Mathf.Lerp(0.85f, 1.30f, theme.EnvironmentalDetailAmount);

        chance =
            Mathf.Clamp01(
                chance
            );

        if (currentRoomRegion.Count >= 45)
        {
            chance += 0.12f;
        }

        if (random.NextDouble() > chance)
            return;

        List<Vector2Int> tableAnchors = BuildTwoCellEdgeCandidates(room);
        Shuffle(tableAnchors, random);

        foreach (Vector2Int anchor in tableAnchors)
        {
            if (TryPlaceTwoCellProp(
                    room,
                    anchor,
                    ChooseSprite(tableSprites, random),
                    "Table",
                    random,
                    true))
            {
                return;
            }
        }
    }


    private void GenerateSolidPropsForRoom(
        Room room,
        DungeonVisualTheme theme,
        System.Random random)
    {
        int targetCount = CalculateSolidPropTarget(room, theme, random);

        if (targetCount <= 0)
            return;

        List<Vector2Int> candidates = BuildSolidCandidates(room, random);

        int placed = 0;

        foreach (Vector2Int candidate in candidates)
        {
            if (placed >= targetCount)
                break;

            PropFamily family = ChoosePropFamily(room, theme, random);

            bool success = TryPlaceFamily(room, candidate, family, random);

            if (!success)
                continue;

            placed++;

            if (family == PropFamily.Sack ||
                family == PropFamily.Crate ||
                family == PropFamily.Timber ||
                family == PropFamily.Rock)
            {
                placed += TryPlaceCluster(room, candidate, family, random);
            }
        }
    }


    private int CalculateSolidPropTarget(
        Room room,
        DungeonVisualTheme theme,
        System.Random random)
    {
        float density = Mathf.Clamp01(
            theme.EnvironmentalDetailAmount * densityMultiplier
        );

        float expected =
            currentRoomRegion.Count *
            density *
            0.0425f *
            currentRoomProfile.DensityScale;

        if (room.Role == RoomRole.Reward)
        {
            expected += 1.10f;
        }
        else if (room.Role == RoomRole.Rest)
        {
            expected += 0.90f;
        }
        else if (room.Role == RoomRole.Elite)
        {
            expected += 0.75f;
        }
        else if (room.Role == RoomRole.Combat)
        {
            expected += 0.35f;
        }

        if (room.Width >= 7 && room.Height >= 7)
        {
            expected += 0.40f;
        }

        int target = Mathf.FloorToInt(expected);
        float fractional = expected - target;

        if (random.NextDouble() < fractional)
        {
            target++;
        }

        target =
            Mathf.Max(
                target,
                currentRoomProfile.MinimumSolidProps
            );

        return Mathf.Clamp(target, 0, maximumSolidPropsPerRoom);
    }


    private List<Vector2Int> BuildSolidCandidates(
        Room room,
        System.Random random)
    {
        List<Vector2Int> deepAlcoves =
            new List<Vector2Int>();

        List<Vector2Int> corners =
            new List<Vector2Int>();

        List<Vector2Int> wallAdjacent =
            new List<Vector2Int>();

        List<Vector2Int> broadPockets =
            new List<Vector2Int>();

        foreach (Vector2Int cell in currentRoomRegion)
        {
            if (!IsSafeSolidCell(room, cell))
                continue;

            int wallCount =
                CountCardinalWallNeighbours(cell);

            if (wallCount >= 3)
            {
                deepAlcoves.Add(cell);
            }
            else if (IsCornerOrAlcoveCell(cell))
            {
                corners.Add(cell);
            }
            else if (IsAdjacentToAnyWall(cell))
            {
                wallAdjacent.Add(cell);
            }
            else if (GetNearbyWallScore(cell) >= 2)
            {
                broadPockets.Add(cell);
            }
        }

        Shuffle(deepAlcoves, random);
        Shuffle(corners, random);
        Shuffle(wallAdjacent, random);
        Shuffle(broadPockets, random);

        List<Vector2Int> ordered =
            new List<Vector2Int>();

        HashSet<Vector2Int> seen =
            new HashSet<Vector2Int>();

        AddUnique(ordered, seen, deepAlcoves);
        AddUnique(ordered, seen, corners);
        AddUnique(ordered, seen, wallAdjacent);
        AddUnique(ordered, seen, broadPockets);

        return ordered;
    }


    private void AddUnique(
        List<Vector2Int> destination,
        HashSet<Vector2Int> seen,
        List<Vector2Int> source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            if (seen.Add(source[i]))
            {
                destination.Add(source[i]);
            }
        }
    }


    private List<Vector2Int> BuildTwoCellEdgeCandidates(
        Room room)
    {
        List<Vector2Int> candidates =
            new List<Vector2Int>();

        foreach (Vector2Int anchor in currentRoomRegion)
        {
            Vector2Int second =
                anchor + Vector2Int.right;

            if (!currentRoomRegion.Contains(second))
                continue;

            bool northWall =
                IsWallCell(anchor + Vector2Int.up) &&
                IsWallCell(second + Vector2Int.up);

            bool southWall =
                IsWallCell(anchor + Vector2Int.down) &&
                IsWallCell(second + Vector2Int.down);

            if (!northWall && !southWall)
                continue;

            candidates.Add(anchor);
        }

        return candidates;
    }


    private bool IsCornerOrAlcoveCell(
        Vector2Int cell)
    {
        bool wallUp = IsWallCell(cell + Vector2Int.up);
        bool wallDown = IsWallCell(cell + Vector2Int.down);
        bool wallLeft = IsWallCell(cell + Vector2Int.left);
        bool wallRight = IsWallCell(cell + Vector2Int.right);

        if ((wallUp && wallLeft) ||
            (wallUp && wallRight) ||
            (wallDown && wallLeft) ||
            (wallDown && wallRight))
        {
            return true;
        }

        return false;
    }


    private bool IsAdjacentToAnyWall(
        Vector2Int cell)
    {
        return IsWallCell(cell + Vector2Int.up) ||
               IsWallCell(cell + Vector2Int.down) ||
               IsWallCell(cell + Vector2Int.left) ||
               IsWallCell(cell + Vector2Int.right);
    }


    private bool IsWallCell(
        Vector2Int cell)
    {
        if (activeGrid == null)
            return true;

        return !activeGrid.IsWalkable(cell);
    }


    private bool IsRoomPerimeterCell(
        Room room,
        Vector2Int cell)
    {
        RectInt bounds = room.Bounds;

        return cell.x == bounds.xMin ||
               cell.x == bounds.xMax - 1 ||
               cell.y == bounds.yMin ||
               cell.y == bounds.yMax - 1;
    }


    private bool IsSafeSolidCell(
        Room room,
        Vector2Int cell)
    {
        if (activeGrid == null)
            return false;

        if (!currentRoomRegion.Contains(cell))
            return false;

        if (!activeGrid.IsWalkable(cell))
            return false;

        if (activeGrid.IsCorridorCell(cell))
            return false;

        if (solidPropCells.Contains(cell))
            return false;

        if (detailCells.Contains(cell))
            return false;

        if (protectedCells.Contains(cell))
            return false;

        /*
         * Puzzle rooms still receive props, but a small central working area
         * remains clear for the resonance puzzle.
         */
        if (room.Role == RoomRole.Puzzle &&
            Mathf.Abs(cell.x - room.Centre.x) +
            Mathf.Abs(cell.y - room.Centre.y) <= 2)
        {
            return false;
        }

        if (IsNearCorridor(cell))
            return false;

        return true;
    }


    private bool IsNearCorridor(
        Vector2Int cell)
    {
        if (activeGrid == null)
            return true;

        int clearance = Mathf.Max(0, corridorClearance);

        for (int x = -clearance; x <= clearance; x++)
        {
            for (int y = -clearance; y <= clearance; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) > clearance)
                    continue;

                Vector2Int check = cell + new Vector2Int(x, y);

                if (activeGrid.IsCorridorCell(check))
                    return true;
            }
        }

        return false;
    }


    private PropFamily ChoosePropFamily(
        Room room,
        DungeonVisualTheme theme,
        System.Random random)
    {
        PropWeights weights = GetThemeWeights(theme.ThemeName);
        ApplyRoomRoleWeights(room, ref weights);
        ApplyProceduralFamilyVariation(ref weights);

        List<PropFamily> families = new List<PropFamily>();
        List<int> familyWeights = new List<int>();

        AddAvailableFamily(families, familyWeights, PropFamily.Pottery, weights.Pottery);
        AddAvailableFamily(families, familyWeights, PropFamily.Rock, weights.Rock);
        AddAvailableFamily(families, familyWeights, PropFamily.Sack, weights.Sack);
        AddAvailableFamily(families, familyWeights, PropFamily.SackPile, weights.SackPile);
        AddAvailableFamily(families, familyWeights, PropFamily.Crate, weights.Crate);
        AddAvailableFamily(families, familyWeights, PropFamily.Timber, weights.Timber);
        AddAvailableFamily(families, familyWeights, PropFamily.Table, weights.Table);

        if (families.Count == 0)
            return PropFamily.Pottery;

        int totalWeight = 0;

        for (int i = 0; i < familyWeights.Count; i++)
        {
            totalWeight += familyWeights[i];
        }

        int roll = random.Next(0, Mathf.Max(1, totalWeight));
        int running = 0;

        for (int i = 0; i < families.Count; i++)
        {
            running += familyWeights[i];

            if (roll < running)
                return families[i];
        }

        return families[families.Count - 1];
    }


    private PropWeights GetThemeWeights(
        string themeName)
    {
        switch (themeName)
        {
            case "Cold Stone":
                return new PropWeights(2, 5, 1, 1, 2, 1, 0);

            case "Ashen Ruins":
                return new PropWeights(2, 4, 2, 2, 4, 6, 1);

            case "Ancient Blue Vault":
                return new PropWeights(5, 2, 1, 1, 2, 1, 1);

            case "Faded Violet Halls":
                return new PropWeights(4, 2, 2, 1, 3, 3, 3);

            case "Deep Slate Vault":
                return new PropWeights(1, 6, 1, 1, 2, 5, 0);

            case "Dust Temple":
                return new PropWeights(7, 2, 2, 2, 1, 1, 1);

            case "Verdigris Crypt":
                return new PropWeights(6, 3, 1, 1, 1, 2, 1);

            default:
                return new PropWeights(2, 5, 1, 1, 3, 7, 0);
        }
    }


    private void ApplyRoomRoleWeights(
        Room room,
        ref PropWeights weights)
    {
        switch (room.Role)
        {
            case RoomRole.Reward:
                weights.Sack += 4;
                weights.SackPile += 4;
                weights.Crate += 5;
                weights.Table += 2;
                break;

            case RoomRole.Rest:
                weights.Pottery += 2;
                weights.Table += 4;
                weights.Timber = Mathf.Max(0, weights.Timber - 2);
                break;

            case RoomRole.Elite:
                weights.Rock += 2;
                weights.Crate += 1;
                weights.Timber += 2;
                break;

            case RoomRole.Start:
                weights.Pottery += 2;
                weights.Crate += 1;
                weights.Table += 1;
                break;

            case RoomRole.Exit:
                weights.Rock += 2;
                weights.Pottery += 1;
                weights.Timber += 1;
                break;

            case RoomRole.Puzzle:
                weights.Pottery += 2;
                weights.Rock += 2;
                weights.Table += 1;
                weights.Crate =
                    Mathf.Max(
                        0,
                        weights.Crate - 1
                    );
                break;

            case RoomRole.Secret:
                weights.Crate += 3;
                weights.Sack += 2;
                weights.Pottery += 2;
                weights.Timber += 2;
                break;
        }
    }


    private void ApplyProceduralFamilyVariation(
        ref PropWeights weights)
    {
        weights.Pottery =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    weights.Pottery *
                    currentRoomProfile.PotteryScale
                )
            );

        weights.Rock =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    weights.Rock *
                    currentRoomProfile.RockScale
                )
            );

        weights.Sack =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    weights.Sack *
                    currentRoomProfile.SackScale
                )
            );

        weights.SackPile =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    weights.SackPile *
                    currentRoomProfile.SackPileScale
                )
            );

        weights.Crate =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    weights.Crate *
                    currentRoomProfile.CrateScale
                )
            );

        weights.Timber =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    weights.Timber *
                    currentRoomProfile.TimberScale
                )
            );

        weights.Table =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    weights.Table *
                    currentRoomProfile.TableFamilyScale
                )
            );
    }


    private void AddAvailableFamily(
        List<PropFamily> families,
        List<int> weights,
        PropFamily family,
        int weight)
    {
        if (weight <= 0 || !HasSpriteForFamily(family))
            return;

        families.Add(family);
        weights.Add(weight);
    }


    private bool HasSpriteForFamily(
        PropFamily family)
    {
        switch (family)
        {
            case PropFamily.Pottery:
                return HasSprites(potterySprites);

            case PropFamily.Rock:
                return HasSprites(rockSprites);

            case PropFamily.Sack:
                return HasSprites(sackSprites);

            case PropFamily.SackPile:
                return sackPileSprite != null;

            case PropFamily.Crate:
                return HasSprites(crateSprites);

            case PropFamily.Timber:
                return HasSprites(timberSprites);

            case PropFamily.Table:
                return HasSprites(tableSprites);
        }

        return false;
    }


    private bool TryPlaceFamily(
        Room room,
        Vector2Int anchor,
        PropFamily family,
        System.Random random)
    {
        switch (family)
        {
            case PropFamily.SackPile:
                return TryPlaceTwoCellProp(
                    room,
                    anchor,
                    sackPileSprite,
                    "Sack Pile",
                    random,
                    false
                );

            case PropFamily.Table:
                return TryPlaceTwoCellProp(
                    room,
                    anchor,
                    ChooseSprite(tableSprites, random),
                    "Table",
                    random,
                    true
                );

            default:
                return TryPlaceSingleCellProp(
                    room,
                    anchor,
                    ChooseSpriteForFamily(family, random),
                    family.ToString()
                );
        }
    }


    private bool TryPlaceSingleCellProp(
        Room room,
        Vector2Int cell,
        Sprite sprite,
        string label)
    {
        if (sprite == null || !IsSafeSolidCell(room, cell))
            return false;

        List<Vector2Int> footprint = SingleCellFootprint(cell);

        if (!WouldPreserveConnectivity(footprint))
            return false;

        CreateSpriteObject(label, sprite, room, footprint, solidPropZ);
        ReserveSolidFootprint(footprint);

        return true;
    }


    private bool TryPlaceTwoCellProp(
        Room room,
        Vector2Int anchor,
        Sprite sprite,
        string label,
        System.Random random,
        bool allowChair)
    {
        if (sprite == null)
            return false;

        Vector2Int second =
            anchor + Vector2Int.right;

        if (!IsSafeSolidCell(room, anchor) ||
            !IsSafeSolidCell(room, second))
        {
            return false;
        }

        bool northWall =
            IsWallCell(anchor + Vector2Int.up) &&
            IsWallCell(second + Vector2Int.up);

        bool southWall =
            IsWallCell(anchor + Vector2Int.down) &&
            IsWallCell(second + Vector2Int.down);

        if (!northWall && !southWall)
            return false;

        if (IsNearProtectedEntrance(anchor) ||
            IsNearProtectedEntrance(second))
        {
            return false;
        }

        List<Vector2Int> footprint = new List<Vector2Int>();
        footprint.Add(anchor);
        footprint.Add(second);

        if (!WouldPreserveConnectivity(footprint))
            return false;

        CreateSpriteObject(label, sprite, room, footprint, solidPropZ);
        ReserveSolidFootprint(footprint);

        if (label == "Table")
        {
            tablesGenerated++;
        }

        if (allowChair && chairSprite != null && random.NextDouble() < 0.75)
        {
            TryPlaceChairBesideTable(room, anchor, second, random);
        }

        return true;
    }


    private bool IsNearProtectedEntrance(
        Vector2Int cell)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int check = cell + new Vector2Int(x, y);

                if (activeGrid.IsCorridorCell(check))
                    return true;
            }
        }

        return false;
    }


    private void TryPlaceChairBesideTable(
        Room room,
        Vector2Int firstTableCell,
        Vector2Int secondTableCell,
        System.Random random)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        bool wallBelow =
            IsWallCell(firstTableCell + Vector2Int.down) &&
            IsWallCell(secondTableCell + Vector2Int.down);

        Vector2Int inwardDirection =
            wallBelow
                ? Vector2Int.up
                : Vector2Int.down;

        candidates.Add(firstTableCell + inwardDirection);
        candidates.Add(secondTableCell + inwardDirection);

        Shuffle(candidates, random);

        foreach (Vector2Int candidate in candidates)
        {
            if (!IsSafeSolidCell(room, candidate))
                continue;

            List<Vector2Int> footprint = SingleCellFootprint(candidate);

            if (!WouldPreserveConnectivity(footprint))
                continue;

            CreateSpriteObject(
                "Chair",
                chairSprite,
                room,
                footprint,
                solidPropZ - 0.01f
            );

            ReserveSolidFootprint(footprint);
            chairsGenerated++;
            return;
        }
    }


    private int TryPlaceCluster(
        Room room,
        Vector2Int anchor,
        PropFamily family,
        System.Random random)
    {
        float effectiveClusterChance =
            Mathf.Clamp01(
                clusterChance *
                currentRoomProfile.ClusterScale
            );

        if (random.NextDouble() > effectiveClusterChance)
            return 0;

        List<Vector2Int> neighbours = new List<Vector2Int>();

        neighbours.Add(anchor + Vector2Int.left);
        neighbours.Add(anchor + Vector2Int.right);
        neighbours.Add(anchor + Vector2Int.up);
        neighbours.Add(anchor + Vector2Int.down);
        neighbours.Add(anchor + Vector2Int.left + Vector2Int.up);
        neighbours.Add(anchor + Vector2Int.right + Vector2Int.up);
        neighbours.Add(anchor + Vector2Int.left + Vector2Int.down);
        neighbours.Add(anchor + Vector2Int.right + Vector2Int.down);

        Shuffle(neighbours, random);

        int placed = 0;

        for (int i = 0;
             i < neighbours.Count && placed < maximumExtraPropsPerCluster;
             i++)
        {
            if (random.NextDouble() > 0.70)
                continue;

            Vector2Int candidate = neighbours[i];

            if (!IsSafeSolidCell(room, candidate))
                continue;

            if (!IsAdjacentToAnyWall(candidate) &&
                !IsCornerOrAlcoveCell(candidate) &&
                GetNearbyWallScore(candidate) < 2)
            {
                continue;
            }

            Sprite sprite = ChooseSpriteForFamily(family, random);

            if (sprite == null)
                continue;

            if (!TryPlaceSingleCellProp(room, candidate, sprite, family.ToString()))
                continue;

            placed++;
        }

        return placed;
    }


    private Sprite ChooseSpriteForFamily(
        PropFamily family,
        System.Random random)
    {
        switch (family)
        {
            case PropFamily.Pottery:
                return ChooseSprite(potterySprites, random);

            case PropFamily.Rock:
                return ChooseSprite(rockSprites, random);

            case PropFamily.Sack:
                return ChooseSprite(sackSprites, random);

            case PropFamily.Crate:
                return ChooseSprite(crateSprites, random);

            case PropFamily.Timber:
                return ChooseSprite(timberSprites, random);
        }

        return null;
    }


    private Sprite ChooseSprite(
        Sprite[] sprites,
        System.Random random)
    {
        if (!HasSprites(sprites))
            return null;

        int start = random.Next(0, sprites.Length);

        for (int offset = 0; offset < sprites.Length; offset++)
        {
            Sprite candidate = sprites[(start + offset) % sprites.Length];

            if (candidate != null)
                return candidate;
        }

        return null;
    }


    private bool HasSprites(
        Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return false;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                return true;
        }

        return false;
    }


    private void ReserveSolidFootprint(
        IEnumerable<Vector2Int> footprint)
    {
        foreach (Vector2Int cell in footprint)
        {
            solidPropCells.Add(cell);
        }

        solidPropsGenerated++;
    }


    private bool WouldPreserveConnectivity(
        IEnumerable<Vector2Int> proposedCells)
    {
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>(solidPropCells);

        foreach (Vector2Int cell in proposedCells)
        {
            blocked.Add(cell);
        }

        Vector2Int start = Vector2Int.zero;
        bool hasStart = false;
        int expectedReachable = 0;

        foreach (Vector2Int cell in activeGrid.FloorCells)
        {
            if (blocked.Contains(cell))
                continue;

            expectedReachable++;

            if (!hasStart)
            {
                start = cell;
                hasStart = true;
            }
        }

        if (!hasStart)
            return false;

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        frontier.Enqueue(start);
        visited.Add(start);

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();

            TryVisitConnectivityNeighbour(current + Vector2Int.up, blocked, visited, frontier);
            TryVisitConnectivityNeighbour(current + Vector2Int.down, blocked, visited, frontier);
            TryVisitConnectivityNeighbour(current + Vector2Int.left, blocked, visited, frontier);
            TryVisitConnectivityNeighbour(current + Vector2Int.right, blocked, visited, frontier);
        }

        return visited.Count == expectedReachable;
    }


    private void TryVisitConnectivityNeighbour(
        Vector2Int neighbour,
        HashSet<Vector2Int> blocked,
        HashSet<Vector2Int> visited,
        Queue<Vector2Int> frontier)
    {
        if (blocked.Contains(neighbour) ||
            visited.Contains(neighbour) ||
            !activeGrid.IsWalkable(neighbour))
        {
            return;
        }

        visited.Add(neighbour);
        frontier.Enqueue(neighbour);
    }


    /// <summary>
    /// Final safety net so a semantic room does not finish completely empty.
    ///
    /// The fallback still uses the room's deterministic random stream and the
    /// same placement/connectivity checks. It is not a hand-authored layout.
    /// </summary>
    private bool EnsureRoomHasDecoration(
        Room room,
        DungeonVisualTheme theme,
        System.Random random)
    {
        List<Vector2Int> candidates =
            BuildSolidCandidates(
                room,
                random
            );

        foreach (Vector2Int candidate in candidates)
        {
            PropFamily family =
                ChoosePropFamily(
                    room,
                    theme,
                    random
                );

            if (TryPlaceFamily(
                    room,
                    candidate,
                    family,
                    random))
            {
                return true;
            }
        }

        if (!HasSprites(webSprites))
            return false;

        Sprite smallWeb = null;
        Sprite largeWeb = null;

        for (int i = 0; i < webSprites.Length; i++)
        {
            Sprite sprite = webSprites[i];

            if (sprite == null)
                continue;

            if (sprite.rect.width > 16f ||
                sprite.rect.height > 16f)
            {
                if (largeWeb == null)
                    largeWeb = sprite;
            }
            else
            {
                if (smallWeb == null)
                    smallWeb = sprite;
            }
        }

        List<DetailPlacement> smallCandidates =
            BuildSmallWebCandidates(
                room,
                random
            );

        if (smallWeb != null &&
            smallCandidates.Count > 0)
        {
            DetailPlacement placement =
                smallCandidates[0];

            CreateDetailObject(
                "Cobweb Small",
                smallWeb,
                placement.WorldPosition,
                detailZ
            );

            detailCells.Add(
                placement.AnchorCell
            );

            webDetailsGenerated++;

            return true;
        }

        List<DetailPlacement> largeCandidates =
            BuildLargeWebCandidates(
                room,
                random
            );

        if (largeWeb != null &&
            largeCandidates.Count > 0)
        {
            DetailPlacement placement =
                largeCandidates[0];

            CreateDetailObject(
                "Cobweb Large",
                largeWeb,
                placement.WorldPosition,
                detailZ
            );

            detailCells.Add(
                placement.AnchorCell
            );

            webDetailsGenerated++;

            return true;
        }

        return false;
    }


    private void GenerateWebsForRoom(
        Room room,
        DungeonVisualTheme theme,
        System.Random random)
    {
        if (!HasSprites(webSprites))
            return;

        float density = Mathf.Clamp01(
            theme.EnvironmentalDetailAmount * densityMultiplier
        );

        float expected =
            density *
            1.75f *
            currentRoomProfile.WebScale;

        if (room.Role == RoomRole.Rest || room.Role == RoomRole.Reward)
        {
            expected += 0.25f;
        }

        int target = Mathf.FloorToInt(expected);

        if (random.NextDouble() < expected - target)
        {
            target++;
        }

        target = Mathf.Clamp(target, 0, maximumWebsPerRoom);

        if (target <= 0)
            return;

        Sprite smallWeb = null;
        Sprite largeWeb = null;

        for (int i = 0; i < webSprites.Length; i++)
        {
            Sprite sprite = webSprites[i];

            if (sprite == null)
                continue;

            if (sprite.rect.width > 16f || sprite.rect.height > 16f)
            {
                if (largeWeb == null)
                    largeWeb = sprite;
            }
            else
            {
                if (smallWeb == null)
                    smallWeb = sprite;
            }
        }

        List<DetailPlacement> largeCandidates = BuildLargeWebCandidates(room, random);
        List<DetailPlacement> smallCandidates = BuildSmallWebCandidates(room, random);

        int placed = 0;

        if (largeWeb != null &&
            largeCandidates.Count > 0 &&
            random.NextDouble() < 0.60)
        {
            DetailPlacement placement = largeCandidates[0];

            CreateDetailObject(
                "Cobweb Large",
                largeWeb,
                placement.WorldPosition,
                detailZ
            );

            detailCells.Add(placement.AnchorCell);
            webDetailsGenerated++;
            placed++;
        }

        for (int i = 0; i < smallCandidates.Count && placed < target; i++)
        {
            if (smallWeb == null)
                break;

            DetailPlacement placement = smallCandidates[i];

            if (detailCells.Contains(placement.AnchorCell))
                continue;

            CreateDetailObject(
                "Cobweb Small",
                smallWeb,
                placement.WorldPosition,
                detailZ
            );

            detailCells.Add(placement.AnchorCell);
            webDetailsGenerated++;
            placed++;
        }

        for (int i = 0; i < largeCandidates.Count && placed < target; i++)
        {
            if (largeWeb == null)
                break;

            DetailPlacement placement = largeCandidates[i];

            if (detailCells.Contains(placement.AnchorCell))
                continue;

            CreateDetailObject(
                "Cobweb Large",
                largeWeb,
                placement.WorldPosition,
                detailZ
            );

            detailCells.Add(placement.AnchorCell);
            webDetailsGenerated++;
            placed++;
        }
    }


    private List<DetailPlacement> BuildSmallWebCandidates(
        Room room,
        System.Random random)
    {
        List<DetailPlacement> candidates =
            new List<DetailPlacement>();

        foreach (Vector2Int cell in currentRoomRegion)
        {
            if (!activeGrid.IsWalkable(cell) ||
                protectedCells.Contains(cell) ||
                solidPropCells.Contains(cell) ||
                detailCells.Contains(cell))
            {
                continue;
            }

            // Small web art reads as a web attached to a wall face. Only use
            // cells that have a solid wall immediately above them.
            if (!IsWallCell(cell + Vector2Int.up))
                continue;

            Vector3 worldPosition =
                new Vector3(
                    cell.x + 0.5f,
                    cell.y + 0.5f + smallWebWallOffsetY,
                    detailZ
                );

            candidates.Add(
                new DetailPlacement(
                    cell,
                    worldPosition
                )
            );
        }

        ShuffleDetailPlacements(candidates, random);
        return candidates;
    }


    private List<DetailPlacement> BuildLargeWebCandidates(
        Room room,
        System.Random random)
    {
        List<DetailPlacement> candidates =
            new List<DetailPlacement>();

        foreach (Vector2Int cell in currentRoomRegion)
        {
            if (!activeGrid.IsWalkable(cell) ||
                protectedCells.Contains(cell) ||
                solidPropCells.Contains(cell) ||
                detailCells.Contains(cell))
            {
                continue;
            }

            Vector2 cornerBias =
                GetCornerBias(cell);

            if (cornerBias == Vector2.zero)
                continue;

            Vector3 worldPosition =
                new Vector3(
                    cell.x + 0.5f +
                        cornerBias.x * largeWebCornerOffset,
                    cell.y + 0.5f +
                        cornerBias.y * largeWebCornerOffset,
                    detailZ
                );

            candidates.Add(
                new DetailPlacement(
                    cell,
                    worldPosition
                )
            );
        }

        ShuffleDetailPlacements(candidates, random);
        return candidates;
    }


    private Vector2 GetCornerBias(
        Vector2Int cell)
    {
        bool wallUp = IsWallCell(cell + Vector2Int.up);
        bool wallDown = IsWallCell(cell + Vector2Int.down);
        bool wallLeft = IsWallCell(cell + Vector2Int.left);
        bool wallRight = IsWallCell(cell + Vector2Int.right);

        if (wallUp && wallLeft)
            return new Vector2(-1f, 1f);

        if (wallUp && wallRight)
            return new Vector2(1f, 1f);

        if (wallDown && wallLeft)
            return new Vector2(-1f, -1f);

        if (wallDown && wallRight)
            return new Vector2(1f, -1f);

        return Vector2.zero;
    }


    private List<Vector2Int> SingleCellFootprint(
        Vector2Int cell)
    {
        List<Vector2Int> footprint = new List<Vector2Int>();
        footprint.Add(cell);
        return footprint;
    }


    private void CreateSpriteObject(
        string label,
        Sprite sprite,
        Room room,
        IReadOnlyList<Vector2Int> footprint,
        float zPosition)
    {
        if (sprite == null || footprint == null || footprint.Count == 0)
            return;

        GameObject prop = new GameObject($"Environment {label}");
        prop.transform.SetParent(environmentParent.transform);

        SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.white;

        Vector3 basePosition = CalculateFootprintCentre(footprint, zPosition);
        Vector2 bias = CalculateWallBias(footprint);

        prop.transform.position = new Vector3(
            basePosition.x + bias.x * wallHugOffset,
            basePosition.y + bias.y * wallHugOffset,
            zPosition
        );
    }


    private void CreateDetailObject(
        string label,
        Sprite sprite,
        Vector3 worldPosition,
        float zPosition)
    {
        if (sprite == null)
            return;

        GameObject prop = new GameObject($"Environment {label}");
        prop.transform.SetParent(environmentParent.transform);

        SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.white;

        prop.transform.position = new Vector3(
            worldPosition.x,
            worldPosition.y,
            zPosition
        );
    }


    private Vector3 CalculateFootprintCentre(
        IReadOnlyList<Vector2Int> footprint,
        float zPosition)
    {
        float minX = footprint[0].x;
        float maxX = footprint[0].x;
        float minY = footprint[0].y;
        float maxY = footprint[0].y;

        for (int i = 1; i < footprint.Count; i++)
        {
            minX = Mathf.Min(minX, footprint[i].x);
            maxX = Mathf.Max(maxX, footprint[i].x);
            minY = Mathf.Min(minY, footprint[i].y);
            maxY = Mathf.Max(maxY, footprint[i].y);
        }

        return new Vector3(
            (minX + maxX + 1f) * 0.5f,
            (minY + maxY + 1f) * 0.5f,
            zPosition
        );
    }


    private Vector2 CalculateWallBias(
        IReadOnlyList<Vector2Int> footprint)
    {
        int leftBias = 0;
        int rightBias = 0;
        int upBias = 0;
        int downBias = 0;

        for (int i = 0; i < footprint.Count; i++)
        {
            Vector2Int cell = footprint[i];

            if (IsWallCell(cell + Vector2Int.left))
                leftBias++;

            if (IsWallCell(cell + Vector2Int.right))
                rightBias++;

            if (IsWallCell(cell + Vector2Int.up))
                upBias++;

            if (IsWallCell(cell + Vector2Int.down))
                downBias++;
        }

        float x = 0f;
        float y = 0f;

        if (leftBias > rightBias)
            x = -1f;
        else if (rightBias > leftBias)
            x = 1f;

        if (downBias > upBias)
            y = -1f;
        else if (upBias > downBias)
            y = 1f;

        return new Vector2(x, y);
    }


    private void Shuffle(
        List<Vector2Int> values,
        System.Random random)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(0, i + 1);

            Vector2Int temporary = values[i];
            values[i] = values[swapIndex];
            values[swapIndex] = temporary;
        }
    }


    private void ShuffleDetailPlacements(
        List<DetailPlacement> values,
        System.Random random)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(0, i + 1);

            DetailPlacement temporary = values[i];
            values[i] = values[swapIndex];
            values[swapIndex] = temporary;
        }
    }
}
