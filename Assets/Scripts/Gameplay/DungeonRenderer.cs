using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders DungeonGrid using the CraftPix 16x16 dungeon atlas.
///
/// The underlying procedural dungeon remains unchanged:
///
///     one DungeonGrid cell = one rendered tile
///
/// The renderer uses two persistent combined meshes:
///
///     Generated Floor
///     Generated Walls
///
/// Wall artwork is selected from the exact sides which touch walkable
/// floor. Therefore:
///
///     WALL touching WALL  -> artwork merges
///     WALL touching FLOOR -> visible boundary on that side
///
/// This remains compatible with BSP rooms, cellular-automata growth,
/// corridors, Shaper excavation and Warden excavation.
/// </summary>
public class DungeonRenderer : MonoBehaviour
{
    // ============================================================
    // RENDERING
    // ============================================================

    [Header("Rendering")]

    [SerializeField]
    private float cellSize = 1f;


    //[SerializeField]
    //private Color floorTint =
    //    Color.white;


    //[SerializeField]
    //private Color wallTint =
    //    Color.white;


    // ============================================================
    // TILESET
    // ============================================================

    [Header("Dungeon Pixel Art")]

    [Tooltip(
        "Assign Assets/Art/DungeonTiles/Tileset.png"
    )]
    [SerializeField]
    private Texture2D tilesetTexture;


    /*
     * Confirmed by Dungeon Tileset.tmx:
     *
     * 304 x 176 px
     * 16 x 16 px tiles
     * 19 columns
     * 11 rows
     */
    private const int AtlasColumns =
        19;


    private const int AtlasRows =
        11;


    // ============================================================
    // EXPOSURE MASK
    // ============================================================

    /*
     * Describes which SIDES OF A WALL touch walkable floor.
     *
     * NORTH = 1
     * EAST  = 2
     * SOUTH = 4
     * WEST  = 8
     */

    private const int FloorNorth =
        1;


    private const int FloorEast =
        2;


    private const int FloorSouth =
        4;


    private const int FloorWest =
        8;


    // ============================================================
    // FLOOR TILES
    // ============================================================

    /*
     * Main plain floor.
     */
    private static readonly Vector2Int baseFloorTile =
        new Vector2Int(
            2,
            2
        );


    /*
     * Theme-specific floor-detail pools.
     *
     * Across the visual-theme system these pools use ALL eight verified
     * compatible small floor-detail tiles from the existing renderer.
     *
     * We no longer choose all eight indiscriminately on every floor.
     */


    // Very restrained damage.
    private static readonly Vector2Int[] cleanFloorDetailTiles =
    {
        new Vector2Int(4, 2)
    };


    // Fine cracks / relatively intact stone.
    private static readonly Vector2Int[] lightCrackedFloorDetailTiles =
    {
        new Vector2Int(3, 2),
        new Vector2Int(4, 2),

        new Vector2Int(2, 3)
    };


    // Clearly fractured floor.
    private static readonly Vector2Int[] fracturedFloorDetailTiles =
    {
        new Vector2Int(2, 3),
        new Vector2Int(3, 3),
        new Vector2Int(4, 3),

        new Vector2Int(2, 4)
    };


    // Heaviest damage.
    private static readonly Vector2Int[] ruinedFloorDetailTiles =
    {
        new Vector2Int(3, 3),
        new Vector2Int(4, 3),

        new Vector2Int(2, 4),
        new Vector2Int(3, 4),
        new Vector2Int(4, 4)
    };


    /*
     * This is a COHERENT 3 x 4 floor pattern.
     *
     * It must not be randomly scattered one tile at a time.
     *
     * Atlas:
     *
     * (15,6) (16,6) (17,6)
     * (15,7) (16,7) (17,7)
     * (15,8) (16,8) (17,8)
     * (15,9) (16,9) (17,9)
     */
    private static readonly Vector2Int[,] largeFloorPattern =
    {
        {
            new Vector2Int(15, 6),
            new Vector2Int(16, 6),
            new Vector2Int(17, 6)
        },

        {
            new Vector2Int(15, 7),
            new Vector2Int(16, 7),
            new Vector2Int(17, 7)
        },

        {
            new Vector2Int(15, 8),
            new Vector2Int(16, 8),
            new Vector2Int(17, 8)
        },

        {
            new Vector2Int(15, 9),
            new Vector2Int(16, 9),
            new Vector2Int(17, 9)
        }
    };


    // ============================================================
    // STRAIGHT WALL TILES
    // ============================================================

    /*
     * Wall ABOVE floor.
     *
     * Its SOUTH side is the wall/floor boundary.
     */
    private static readonly Vector2Int[] southExposedWalls =
    {
        new Vector2Int(2, 1),
        new Vector2Int(3, 1),
        new Vector2Int(4, 1),

        // Additional compatible detail.
        new Vector2Int(3, 7)
    };


    /*
     * Wall BELOW floor.
     *
     * Its NORTH side borders floor.
     */
    private static readonly Vector2Int[] northExposedWalls =
    {
        new Vector2Int(2, 5),
        new Vector2Int(3, 5),
        new Vector2Int(4, 5)
    };


    /*
     * Wall LEFT of floor.
     *
     * Its EAST side borders floor.
     */
    private static readonly Vector2Int[] eastExposedWalls =
    {
        new Vector2Int(1, 2),
        new Vector2Int(1, 3),
        new Vector2Int(1, 4)
    };


    /*
     * Wall RIGHT of floor.
     *
     * Its WEST side borders floor.
     */
    private static readonly Vector2Int[] westExposedWalls =
    {
        new Vector2Int(5, 2),
        new Vector2Int(5, 3),
        new Vector2Int(5, 4)
    };


    // ============================================================
    // CORNERS
    // ============================================================

    /*
     * IMPORTANT:
     *
     * These mappings were corrected after examining the actual
     * supplied TMX rather than guessing from the spritesheet.
     */


    /*
     * Floor NORTH + EAST.
     *
     * Border is therefore on TOP + RIGHT.
     */
    private static readonly Vector2Int northEastCorner =
        new Vector2Int(
            5,
            1
        );


    /*
     * Floor EAST + SOUTH.
     *
     * This is the exact case from the screenshot where the tile
     * needed the slight boundary on RIGHT + BOTTOM.
     */
    private static readonly Vector2Int southEastCorner =
        new Vector2Int(
            5,
            5
        );


    /*
     * Floor SOUTH + WEST.
     */
    private static readonly Vector2Int southWestCorner =
        new Vector2Int(
            1,
            5
        );


    /*
     * Floor WEST + NORTH.
     */
    private static readonly Vector2Int northWestCorner =
        new Vector2Int(
            1,
            1
        );


    // ============================================================
    // OPPOSING-SIDE WALLS
    // ============================================================

    /*
     * Floor both NORTH and SOUTH.
     *
     * These occur in narrow procedural spaces and one-cell dividers.
     *
     * The TMX uses several horizontal wall variants for this case.
     */
    private static readonly Vector2Int[] northSouthWalls =
    {
        new Vector2Int(2, 1),
        new Vector2Int(3, 1),
        new Vector2Int(4, 1),

        new Vector2Int(2, 5),
        new Vector2Int(3, 5),
        new Vector2Int(4, 5),

        new Vector2Int(3, 7),

        new Vector2Int(11, 8),
        new Vector2Int(2, 9)
    };


    /*
     * Floor EAST and WEST.
     *
     * Vertical divider / narrow-wall cases.
     */
    private static readonly Vector2Int[] eastWestWalls =
    {
        new Vector2Int(1, 2),
        new Vector2Int(1, 3),
        new Vector2Int(1, 4),

        new Vector2Int(5, 2),
        new Vector2Int(5, 3),
        new Vector2Int(5, 4),

        new Vector2Int(7, 4),
        new Vector2Int(7, 5)
    };


    // ============================================================
    // THREE-SIDED SPECIAL WALLS
    // ============================================================

    /*
     * The supplied TMX actually uses these specialised tiles in
     * T-junction style cases.
     */


    /*
     * Floor NORTH + EAST + WEST.
     */
    private static readonly Vector2Int[] northEastWestWalls =
    {
        new Vector2Int(8, 7),
        new Vector2Int(12, 7)
    };


    /*
     * Floor EAST + SOUTH + WEST.
     *
     * This also uses the extra tiles specifically pointed out during
     * visual review.
     */
    private static readonly Vector2Int[] eastSouthWestWalls =
    {
        new Vector2Int(8, 9),
        new Vector2Int(12, 9)
    };


    // ============================================================
    // RENDER OBJECTS
    // ============================================================

    private GameObject floorObject;

    private GameObject wallObject;


    private Mesh floorMesh;

    private Mesh wallMesh;


    private Material floorMaterial;

    private Material wallMaterial;


    // ============================================================
    // REUSABLE COLLECTIONS
    // ============================================================

    private readonly List<Vector3> vertexBuffer =
        new List<Vector3>();


    private readonly List<int> triangleBuffer =
        new List<int>();


    private readonly List<Vector2> uvBuffer =
        new List<Vector2>();


    private readonly HashSet<Vector2Int> wallCellBuffer =
        new HashSet<Vector2Int>();


    /*
     * Floor overrides are used for coherent decorative floor stamps.
     */
    private readonly Dictionary<Vector2Int, Vector2Int>
        floorTileOverrides =
            new Dictionary<Vector2Int, Vector2Int>();


    private readonly List<Vector2Int> sortedFloorCells =
        new List<Vector2Int>();



    private DungeonVisualTheme activeVisualTheme;

    private int activeVisualThemeSeed = int.MinValue;


    private static readonly Vector2Int[] wallNeighbourDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right,

        new Vector2Int(-1, 1),
        new Vector2Int(1, 1),

        new Vector2Int(-1, -1),
        new Vector2Int(1, -1)
    };


    // ============================================================
    // CAMERA
    // ============================================================

    [Header("Camera")]

    [SerializeField]
    private Camera dungeonCamera;


    [SerializeField]
    private float cameraPadding =
        3f;


    // ============================================================
    // INITIALISATION
    // ============================================================

    private void Awake()
    {
        transform.position =
            Vector3.zero;


        transform.rotation =
            Quaternion.identity;


        transform.localScale =
            Vector3.one;
    }


    /// <summary>
    /// Generates and stores the visual identity for a NEW dungeon floor.
    ///
    /// Call this once when a new procedural floor is created.
    ///
    /// Do not call it during ordinary terrain refreshes because the Shaper
    /// and Warden must preserve the floor's existing appearance.
    /// </summary>
    public void GenerateVisualTheme(
        int floorSeed,
        int depth)
    {
        activeVisualTheme =
            DungeonVisualThemeGenerator.Generate(
                floorSeed,
                depth
            );


        activeVisualThemeSeed =
            floorSeed;


        /*
         * Force materials to pick up the new palette immediately if they
         * already exist.
         */
        SynchroniseMaterials();


        UnityEngine.Debug.Log(
            "========== PROCEDURAL VISUAL THEME ==========\n" +
            $"Floor depth: {depth}\n" +
            $"Floor seed: {floorSeed}\n" +
            $"Theme: {activeVisualTheme.ThemeName}\n" +
            $"Theme seed: {activeVisualTheme.ThemeSeed}\n" +
            $"Small floor detail: " +
            $"{activeVisualTheme.SmallFloorVariationChance:F3}\n" +
            $"Large floor pattern: " +
            $"{activeVisualTheme.LargeFloorPatternChance:F3}\n" +
            $"Wall variation: " +
            $"{activeVisualTheme.WallVariationChance:F3}\n" +
            $"Floor detail style: {activeVisualTheme.FloorDetailStyle}\n" +
            $"Wall masonry style: {activeVisualTheme.WallMasonryStyle}\n" +
            "============================================="
        );
    }


    // ============================================================
    // PUBLIC RENDER
    // ============================================================

    public void Render(
        DungeonGrid grid)
    {
        if (grid == null ||
            grid.FloorCellCount == 0)
        {
            Clear();


            UnityEngine.Debug.LogWarning(
                "DungeonRenderer received an empty dungeon grid."
            );


            return;
        }


        EnsureRenderObjects();


        if (tilesetTexture == null)
        {
            UnityEngine.Debug.LogWarning(
                "DungeonRenderer has no Tileset.png assigned."
            );
        }


        SynchroniseMaterials();


        CalculateVisualWallCells(
            grid
        );


        BuildFloorVisualMap(
            grid
        );


        BuildFloorMesh(
            grid
        );


        BuildWallMesh(
            grid
        );


        UnityEngine.Debug.Log(
            "Dungeon rendered with " +
            $"{grid.FloorCellCount} floor cells and " +
            $"{wallCellBuffer.Count} visual wall cells."
        );
    }


    // ============================================================
    // FLOOR VISUAL MAP
    // ============================================================

    private void BuildFloorVisualMap(
        DungeonGrid grid)
    {
        floorTileOverrides.Clear();

        sortedFloorCells.Clear();


        foreach (Vector2Int cell in
                 grid.FloorCells)
        {
            sortedFloorCells.Add(
                cell
            );
        }


        /*
         * Sorting makes decorative placement deterministic even if
         * HashSet iteration order changes.
         */
        sortedFloorCells.Sort(
            CompareCells
        );


        /*
         * First add sparse single-cell detail.
         */
        foreach (Vector2Int cell in
                 sortedFloorCells)
        {
            uint hash =
                CalculateCoordinateHash(
                    cell
                );


            float roll =
                (hash %
                 10000u) /
                10000f;


            if (roll >=
                GetSmallFloorVariationChance())
            {
                continue;
            }


            uint variantHash =
                MixHash(
                    hash,
                    0x71A54BCDu
                );


            floorTileOverrides[cell] =
                SelectFloorDetailTile(
                    variantHash
                );
        }


        /*
         * Then attempt coherent 3x4 cracked-floor patterns.
         *
         * These overwrite small detail where necessary.
         */
        foreach (Vector2Int candidate in
                 sortedFloorCells)
        {
            uint hash =
                MixHash(
                    CalculateCoordinateHash(
                        candidate
                    ),
                    0x4F1BBCDCu
                );


            float roll =
                (hash %
                 10000u) /
                10000f;


            if (roll >=
                GetLargeFloorPatternChance())
            {
                continue;
            }


            if (!CanPlaceLargeFloorPattern(
                    grid,
                    candidate))
            {
                continue;
            }


            ApplyLargeFloorPattern(
                candidate
            );
        }
    }


    private Vector2Int SelectFloorDetailTile(
    uint hash)
    {
        Vector2Int[] pool =
            GetFloorDetailPool();


        if (pool == null ||
            pool.Length == 0)
        {
            return baseFloorTile;
        }


        int index =
            (int)(
                hash %
                (uint)pool.Length
            );


        return pool[index];
    }


    private Vector2Int[] GetFloorDetailPool()
    {
        if (activeVisualTheme == null)
        {
            return
                lightCrackedFloorDetailTiles;
        }


        switch (activeVisualTheme.FloorDetailStyle)
        {
            case DungeonFloorDetailStyle.Clean:

                return
                    cleanFloorDetailTiles;


            case DungeonFloorDetailStyle.LightCracked:

                return
                    lightCrackedFloorDetailTiles;


            case DungeonFloorDetailStyle.Fractured:

                return
                    fracturedFloorDetailTiles;


            case DungeonFloorDetailStyle.Ruined:

                return
                    ruinedFloorDetailTiles;
        }


        return
            lightCrackedFloorDetailTiles;
    }


    private bool CanPlaceLargeFloorPattern(
        DungeonGrid grid,
        Vector2Int bottomLeft)
    {
        /*
         * Pattern is:
         *
         * 3 cells wide
         * 4 cells high
         */

        for (int localY = 0;
             localY < 4;
             localY++)
        {
            for (int localX = 0;
                 localX < 3;
                 localX++)
            {
                Vector2Int cell =
                    bottomLeft +
                    new Vector2Int(
                        localX,
                        localY
                    );


                if (!grid.IsWalkable(
                        cell))
                {
                    return false;
                }
            }
        }


        return true;
    }


    private void ApplyLargeFloorPattern(
        Vector2Int bottomLeft)
    {
        /*
         * Atlas row 6 is visually the TOP of the pattern.
         *
         * Unity/world Y increases upward.
         *
         * Therefore:
         *
         * local world row 3 -> atlas row 6
         * local world row 2 -> atlas row 7
         * local world row 1 -> atlas row 8
         * local world row 0 -> atlas row 9
         */

        for (int localY = 0;
             localY < 4;
             localY++)
        {
            int atlasPatternRow =
                3 -
                localY;


            for (int localX = 0;
                 localX < 3;
                 localX++)
            {
                Vector2Int dungeonCell =
                    bottomLeft +
                    new Vector2Int(
                        localX,
                        localY
                    );


                floorTileOverrides[dungeonCell] =
                    largeFloorPattern[
                        atlasPatternRow,
                        localX
                    ];
            }
        }
    }


    private int CompareCells(
        Vector2Int first,
        Vector2Int second)
    {
        int yComparison =
            first.y.CompareTo(
                second.y
            );


        if (yComparison != 0)
        {
            return yComparison;
        }


        return
            first.x.CompareTo(
                second.x
            );
    }


    // ============================================================
    // FLOOR MESH
    // ============================================================

    private void BuildFloorMesh(
        DungeonGrid grid)
    {
        if (floorMesh == null)
            return;


        PrepareBuffers(
            grid.FloorCellCount
        );


        foreach (Vector2Int cell in
                 grid.FloorCells)
        {
            Vector2Int tile;


            if (!floorTileOverrides.TryGetValue(
                    cell,
                    out tile))
            {
                tile =
                    baseFloorTile;
            }


            AddCellGeometry(
                cell,
                0f,
                tile.x,
                tile.y
            );
        }


        ApplyBuffersToMesh(
            floorMesh
        );
    }


    // ============================================================
    // WALL MESH
    // ============================================================

    private void BuildWallMesh(
        DungeonGrid grid)
    {
        if (wallMesh == null)
            return;


        PrepareBuffers(
            wallCellBuffer.Count
        );


        foreach (Vector2Int wallCell in
                 wallCellBuffer)
        {
            Vector2Int tile =
                SelectWallTile(
                    grid,
                    wallCell
                );


            AddCellGeometry(
                wallCell,
                -0.02f,
                tile.x,
                tile.y
            );
        }


        ApplyBuffersToMesh(
            wallMesh
        );
    }


    // ============================================================
    // WALL TILE SELECTION
    // ============================================================

    private Vector2Int SelectWallTile(
        DungeonGrid grid,
        Vector2Int wallCell)
    {
        int mask =
            GetFloorExposureMask(
                grid,
                wallCell
            );


        switch (mask)
        {
            // ----------------------------------------------------
            // DIAGONAL-ONLY OUTER CORNER
            // ----------------------------------------------------

            case 0:

                return SelectDiagonalOuterCorner(
                    grid,
                    wallCell
                );


            // ----------------------------------------------------
            // ONE FLOOR-FACING SIDE
            // ----------------------------------------------------

            case FloorNorth:

                return SelectStraightWallVariant(
                    northExposedWalls,
                    wallCell,
                    101u
                );


            case FloorEast:

                return SelectStraightWallVariant(
                    eastExposedWalls,
                    wallCell,
                    102u
                );


            case FloorSouth:

                return SelectStraightWallVariant(
                    southExposedWalls,
                    wallCell,
                    103u
                );


            case FloorWest:

                return SelectStraightWallVariant(
                    westExposedWalls,
                    wallCell,
                    104u
                );


            // ----------------------------------------------------
            // ADJACENT TWO-SIDE CORNERS
            //
            // Verified against the supplied TMX.
            // ----------------------------------------------------

            case FloorNorth |
                 FloorEast:

                return
                    northEastCorner;


            case FloorEast |
                 FloorSouth:

                return
                    southEastCorner;


            case FloorSouth |
                 FloorWest:

                return
                    southWestCorner;


            case FloorWest |
                 FloorNorth:

                return
                    northWestCorner;


            // ----------------------------------------------------
            // OPPOSING FLOOR SIDES
            // ----------------------------------------------------

            case FloorNorth |
                 FloorSouth:

                return SelectVariant(
                    northSouthWalls,
                    wallCell,
                    201u
                );


            case FloorEast |
                 FloorWest:

                return SelectVariant(
                    eastWestWalls,
                    wallCell,
                    202u
                );


            // ----------------------------------------------------
            // THREE-SIDED CASES SEEN IN TMX
            // ----------------------------------------------------

            case FloorNorth |
                 FloorEast |
                 FloorWest:

                return SelectVariant(
                    northEastWestWalls,
                    wallCell,
                    301u
                );


            case FloorEast |
                 FloorSouth |
                 FloorWest:

                return SelectVariant(
                    eastSouthWestWalls,
                    wallCell,
                    302u
                );
        }


        /*
         * The remaining masks are uncommon in normal room borders.
         *
         * They are mostly generated by extremely thin CA geometry.
         *
         * Use a deterministic best-fitting tile instead of putting a
         * random wall orientation there.
         */
        return SelectRareWallCase(
            mask,
            wallCell
        );
    }

    private Vector2Int SelectStraightWallVariant(
    Vector2Int[] variants,
    Vector2Int cell,
    uint salt)
    {
        if (variants == null ||
            variants.Length == 0)
        {
            return new Vector2Int(
                2,
                1
            );
        }


        uint hash =
            CalculateVisualHash(
                cell,
                salt
            );


        /*
         * The first three entries in each verified straight-wall pool are
         * the standard compatible alternatives.
         */
        int basicCount =
            Mathf.Min(
                3,
                variants.Length
            );


        /*
         * Some orientations contain additional decorative pieces after
         * their three standard variants.
         */
        if (variants.Length >
            basicCount)
        {
            float detailRoll =
                (hash %
                 10000u) /
                10000f;


            if (detailRoll <
                GetWallVariationChance())
            {
                int detailCount =
                    variants.Length -
                    basicCount;


                int detailIndex =
                    basicCount +
                    (int)(
                        (hash /
                         113u) %
                        (uint)detailCount
                    );


                return
                    variants[
                        detailIndex
                    ];
            }
        }


        int preferredIndex =
            GetPreferredWallVariantIndex(
                basicCount
            );


        float preferredRoll =
            ((hash /
              173u) %
             10000u) /
            10000f;


        if (preferredRoll <
            GetWallStyleStrength())
        {
            return
                variants[
                    preferredIndex
                ];
        }


        /*
         * Occasionally use one of the other compatible masonry variants
         * so the wall does not become completely uniform.
         */
        if (basicCount <= 1)
        {
            return
                variants[0];
        }


        int offset =
            1 +
            (int)(
                (hash /
                 271u) %
                (uint)(
                    basicCount -
                    1
                )
            );


        int alternateIndex =
            (preferredIndex +
             offset) %
            basicCount;


        return
            variants[
                alternateIndex
            ];
    }


    private int GetPreferredWallVariantIndex(
        int availableVariantCount)
    {
        if (availableVariantCount <= 1 ||
            activeVisualTheme == null)
        {
            return 0;
        }


        int requestedIndex;


        switch (activeVisualTheme.WallMasonryStyle)
        {
            case DungeonWallMasonryStyle.Plain:

                requestedIndex =
                    0;

                break;


            case DungeonWallMasonryStyle.Jointed:

                requestedIndex =
                    1;

                break;


            case DungeonWallMasonryStyle.Weathered:

                requestedIndex =
                    2;

                break;


            default:

                requestedIndex =
                    0;

                break;
        }


        return Mathf.Clamp(
            requestedIndex,
            0,
            availableVariantCount - 1
        );
    }


    private float GetWallStyleStrength()
    {
        if (activeVisualTheme == null)
            return 0.65f;


        return
            activeVisualTheme
                .WallStyleStrength;
    }

    private uint CalculateVisualHash(
    Vector2Int cell,
    uint salt)
    {
        uint hash =
            CalculateCoordinateHash(
                cell
            );


        uint themeSalt =
            activeVisualTheme != null
                ? unchecked(
                    (uint)activeVisualTheme.ThemeSeed
                )
                : 0u;


        hash =
            MixHash(
                hash,
                themeSalt
            );


        hash =
            MixHash(
                hash,
                salt
            );


        return hash;
    }

    private Vector2Int SelectRareWallCase(
        int mask,
        Vector2Int wallCell)
    {
        /*
         * FLOOR NORTH + EAST + SOUTH.
         *
         * The wall continues to the WEST.
         */
        if (mask ==
            (FloorNorth |
             FloorEast |
             FloorSouth))
        {
            return new Vector2Int(
                13,
                8
            );
        }


        /*
         * FLOOR NORTH + SOUTH + WEST.
         *
         * The wall continues to the EAST.
         */
        if (mask ==
            (FloorNorth |
             FloorSouth |
             FloorWest))
        {
            return new Vector2Int(
                11,
                8
            );
        }


        /*
         * Floor on all four sides.
         *
         * This is basically a tiny one-cell wall pillar.
         */
        if (mask ==
            (FloorNorth |
             FloorEast |
             FloorSouth |
             FloorWest))
        {
            return new Vector2Int(
                13,
                4
            );
        }


        /*
         * Safe fallback.
         */
        if ((mask &
             FloorSouth) != 0)
        {
            return SelectVariant(
                southExposedWalls,
                wallCell,
                403u
            );
        }


        if ((mask &
             FloorNorth) != 0)
        {
            return SelectVariant(
                northExposedWalls,
                wallCell,
                404u
            );
        }


        if ((mask &
             FloorEast) != 0)
        {
            return SelectVariant(
                eastExposedWalls,
                wallCell,
                405u
            );
        }


        return SelectVariant(
            westExposedWalls,
            wallCell,
            406u
        );
    }


    // ============================================================
    // OUTER DIAGONAL CORNERS
    // ============================================================

    private Vector2Int SelectDiagonalOuterCorner(
        DungeonGrid grid,
        Vector2Int wallCell)
    {
        /*
         * Floor diagonally SOUTH-EAST means this is the
         * NORTH-WEST outside corner.
         */
        if (grid.IsWalkable(
                wallCell +
                new Vector2Int(
                    1,
                    -1
                )))
        {
            return
                northWestCorner;
        }


        /*
         * SOUTH-WEST floor.
         */
        if (grid.IsWalkable(
                wallCell +
                new Vector2Int(
                    -1,
                    -1
                )))
        {
            return
                northEastCorner;
        }


        /*
         * NORTH-EAST floor.
         */
        if (grid.IsWalkable(
                wallCell +
                new Vector2Int(
                    1,
                    1
                )))
        {
            return
                southWestCorner;
        }


        /*
         * NORTH-WEST floor.
         */
        if (grid.IsWalkable(
                wallCell +
                new Vector2Int(
                    -1,
                    1
                )))
        {
            return
                southEastCorner;
        }


        return
            northWestCorner;
    }


    // ============================================================
    // EXPOSURE MASK
    // ============================================================

    private int GetFloorExposureMask(
        DungeonGrid grid,
        Vector2Int wallCell)
    {
        int mask =
            0;


        if (grid.IsWalkable(
                wallCell +
                Vector2Int.up))
        {
            mask |=
                FloorNorth;
        }


        if (grid.IsWalkable(
                wallCell +
                Vector2Int.right))
        {
            mask |=
                FloorEast;
        }


        if (grid.IsWalkable(
                wallCell +
                Vector2Int.down))
        {
            mask |=
                FloorSouth;
        }


        if (grid.IsWalkable(
                wallCell +
                Vector2Int.left))
        {
            mask |=
                FloorWest;
        }


        return mask;
    }


    // ============================================================
    // VARIANT SELECTION
    // ============================================================

    private Vector2Int SelectVariant(
        Vector2Int[] variants,
        Vector2Int cell,
        uint salt)
    {
        if (variants == null ||
            variants.Length == 0)
        {
            return
                new Vector2Int(
                    2,
                    1
                );
        }


        uint hash =
            CalculateVisualHash(
                cell,
                salt
            );


        /*
         * Keep the primary tile slightly more common so decorative
         * variants add diversity without making every wall noisy.
         */
        float roll =
            (hash %
             10000u) /
            10000f;


        if (variants.Length >
                3 &&
            roll >
                GetWallVariationChance())
        {
            int basicCount =
                Mathf.Min(
                    3,
                    variants.Length
                );


            int basicIndex =
                (int)(
                    (hash /
                     101u) %
                    (uint)basicCount
                );


            return
                variants[basicIndex];
        }


        int index =
            (int)(
                hash %
                (uint)variants.Length
            );


        return
            variants[index];
    }


    private float GetSmallFloorVariationChance()
    {
        if (activeVisualTheme == null)
            return 0.10f;


        return
            activeVisualTheme
                .SmallFloorVariationChance;
    }


    private float GetLargeFloorPatternChance()
    {
        if (activeVisualTheme == null)
            return 0.035f;


        return
            activeVisualTheme
                .LargeFloorPatternChance;
    }


    private float GetWallVariationChance()
    {
        if (activeVisualTheme == null)
            return 0.25f;


        return
            activeVisualTheme
                .WallVariationChance;
    }

    // ============================================================
    // VISUAL WALL POSITIONS
    // ============================================================

    private void CalculateVisualWallCells(
        DungeonGrid grid)
    {
        wallCellBuffer.Clear();


        foreach (Vector2Int floorCell in
                 grid.FloorCells)
        {
            foreach (Vector2Int direction in
                     wallNeighbourDirections)
            {
                Vector2Int neighbour =
                    floorCell +
                    direction;


                if (!grid.IsWalkable(
                        neighbour))
                {
                    wallCellBuffer.Add(
                        neighbour
                    );
                }
            }
        }
    }


    // ============================================================
    // OBJECT SETUP
    // ============================================================

    private void EnsureRenderObjects()
    {
        EnsureMaterials();


        if (floorObject == null)
        {
            floorObject =
                CreateMeshObject(
                    "Generated Floor",
                    floorMaterial,
                    out floorMesh
                );
        }


        if (wallObject == null)
        {
            wallObject =
                CreateMeshObject(
                    "Generated Walls",
                    wallMaterial,
                    out wallMesh
                );
        }
    }


    private GameObject CreateMeshObject(
        string objectName,
        Material material,
        out Mesh mesh)
    {
        GameObject meshObject =
            new GameObject(
                objectName
            );


        meshObject.transform.SetParent(
            transform,
            false
        );


        meshObject.transform.localPosition =
            Vector3.zero;


        meshObject.transform.localRotation =
            Quaternion.identity;


        meshObject.transform.localScale =
            Vector3.one;


        MeshFilter meshFilter =
            meshObject.AddComponent<MeshFilter>();


        MeshRenderer meshRenderer =
            meshObject.AddComponent<MeshRenderer>();


        mesh =
            new Mesh();


        mesh.name =
            objectName +
            " Mesh";


        mesh.indexFormat =
            IndexFormat.UInt32;


        mesh.MarkDynamic();


        meshFilter.sharedMesh =
            mesh;


        meshRenderer.sharedMaterial =
            material;


        meshRenderer.shadowCastingMode =
            ShadowCastingMode.Off;


        meshRenderer.receiveShadows =
            false;


        meshRenderer.lightProbeUsage =
            LightProbeUsage.Off;


        meshRenderer.reflectionProbeUsage =
            ReflectionProbeUsage.Off;


        return meshObject;
    }


    // ============================================================
    // MATERIALS
    // ============================================================

    private void EnsureMaterials()
    {
        Shader shader =
            Shader.Find(
                "Sprites/Default"
            );


        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Unlit/Texture"
                );
        }


        if (shader == null)
        {
            UnityEngine.Debug.LogError(
                "DungeonRenderer could not find a texture-capable shader."
            );


            return;
        }


        if (floorMaterial == null)
        {
            floorMaterial =
                new Material(
                    shader
                );


            floorMaterial.name =
                "Runtime Dungeon Floor Material";
        }


        if (wallMaterial == null)
        {
            wallMaterial =
                new Material(
                    shader
                );


            wallMaterial.name =
                "Runtime Dungeon Wall Material";
        }


        SynchroniseMaterials();
    }


    private void SynchroniseMaterials()
    {
        Color selectedFloorTint =
            activeVisualTheme != null
                ? activeVisualTheme.FloorTint
                : Color.white;


        Color selectedWallTint =
            activeVisualTheme != null
                ? activeVisualTheme.WallTint
                : Color.white;


        if (floorMaterial != null)
        {
            floorMaterial.color =
                selectedFloorTint;


            floorMaterial.mainTexture =
                tilesetTexture;
        }


        if (wallMaterial != null)
        {
            wallMaterial.color =
                selectedWallTint;


            wallMaterial.mainTexture =
                tilesetTexture;
        }
    }


    // ============================================================
    // MESH BUFFERS
    // ============================================================

    private void PrepareBuffers(
        int cellCount)
    {
        vertexBuffer.Clear();

        triangleBuffer.Clear();

        uvBuffer.Clear();


        int requiredVertices =
            Mathf.Max(
                0,
                cellCount *
                4
            );


        int requiredTriangles =
            Mathf.Max(
                0,
                cellCount *
                6
            );


        if (vertexBuffer.Capacity <
            requiredVertices)
        {
            vertexBuffer.Capacity =
                requiredVertices;
        }


        if (uvBuffer.Capacity <
            requiredVertices)
        {
            uvBuffer.Capacity =
                requiredVertices;
        }


        if (triangleBuffer.Capacity <
            requiredTriangles)
        {
            triangleBuffer.Capacity =
                requiredTriangles;
        }
    }


    private void ApplyBuffersToMesh(
        Mesh targetMesh)
    {
        targetMesh.Clear();


        targetMesh.SetVertices(
            vertexBuffer
        );


        targetMesh.SetUVs(
            0,
            uvBuffer
        );


        targetMesh.SetTriangles(
            triangleBuffer,
            0,
            false
        );


        targetMesh.RecalculateBounds();
    }


    // ============================================================
    // CELL GEOMETRY
    // ============================================================

    private void AddCellGeometry(
        Vector2Int cell,
        float zPosition,
        int tileColumn,
        int tileRow)
    {
        int firstVertex =
            vertexBuffer.Count;


        float minimumX =
            cell.x *
            cellSize;


        float minimumY =
            cell.y *
            cellSize;


        float maximumX =
            minimumX +
            cellSize;


        float maximumY =
            minimumY +
            cellSize;


        vertexBuffer.Add(
            new Vector3(
                minimumX,
                minimumY,
                zPosition
            )
        );


        vertexBuffer.Add(
            new Vector3(
                maximumX,
                minimumY,
                zPosition
            )
        );


        vertexBuffer.Add(
            new Vector3(
                maximumX,
                maximumY,
                zPosition
            )
        );


        vertexBuffer.Add(
            new Vector3(
                minimumX,
                maximumY,
                zPosition
            )
        );


        triangleBuffer.Add(
            firstVertex
        );

        triangleBuffer.Add(
            firstVertex + 2
        );

        triangleBuffer.Add(
            firstVertex + 1
        );


        triangleBuffer.Add(
            firstVertex
        );

        triangleBuffer.Add(
            firstVertex + 3
        );

        triangleBuffer.Add(
            firstVertex + 2
        );


        AddTileUVs(
            tileColumn,
            tileRow
        );
    }


    // ============================================================
    // UV MAPPING
    // ============================================================

    private void AddTileUVs(
        int tileColumn,
        int tileRow)
    {
        tileColumn =
            Mathf.Clamp(
                tileColumn,
                0,
                AtlasColumns - 1
            );


        tileRow =
            Mathf.Clamp(
                tileRow,
                0,
                AtlasRows - 1
            );


        float tileWidth =
            1f /
            AtlasColumns;


        float tileHeight =
            1f /
            AtlasRows;


        float minimumU =
            tileColumn *
            tileWidth;


        float maximumU =
            minimumU +
            tileWidth;


        int invertedRow =
            AtlasRows -
            1 -
            tileRow;


        float minimumV =
            invertedRow *
            tileHeight;


        float maximumV =
            minimumV +
            tileHeight;


        uvBuffer.Add(
            new Vector2(
                minimumU,
                minimumV
            )
        );


        uvBuffer.Add(
            new Vector2(
                maximumU,
                minimumV
            )
        );


        uvBuffer.Add(
            new Vector2(
                maximumU,
                maximumV
            )
        );


        uvBuffer.Add(
            new Vector2(
                minimumU,
                maximumV
            )
        );
    }


    // ============================================================
    // HASHING
    // ============================================================

    private uint CalculateCoordinateHash(
        Vector2Int cell)
    {
        unchecked
        {
            uint hash =
                (uint)cell.x *
                73856093u;


            hash ^=
                (uint)cell.y *
                19349663u;


            hash ^=
                hash >>
                13;


            hash *=
                1274126177u;


            return hash;
        }
    }


    private uint MixHash(
        uint value,
        uint salt)
    {
        unchecked
        {
            value ^=
                salt +
                0x9E3779B9u +
                (value << 6) +
                (value >> 2);


            value ^=
                value >>
                16;


            value *=
                0x7FEB352Du;


            value ^=
                value >>
                15;


            return value;
        }
    }


    // ============================================================
    // CLEAR
    // ============================================================

    public void Clear()
    {
        wallCellBuffer.Clear();

        floorTileOverrides.Clear();


        if (floorMesh != null)
        {
            floorMesh.Clear();
        }


        if (wallMesh != null)
        {
            wallMesh.Clear();
        }
    }


    // ============================================================
    // OPTIONAL CAMERA
    // ============================================================

    private void FrameCamera(
        DungeonGrid grid)
    {
        if (dungeonCamera == null ||
            grid == null ||
            grid.FloorCellCount == 0)
        {
            return;
        }


        bool first =
            true;


        int minX = 0;
        int maxX = 0;

        int minY = 0;
        int maxY = 0;


        foreach (Vector2Int cell in
                 grid.FloorCells)
        {
            if (first)
            {
                minX =
                    maxX =
                        cell.x;


                minY =
                    maxY =
                        cell.y;


                first =
                    false;


                continue;
            }


            minX =
                Mathf.Min(
                    minX,
                    cell.x
                );


            maxX =
                Mathf.Max(
                    maxX,
                    cell.x
                );


            minY =
                Mathf.Min(
                    minY,
                    cell.y
                );


            maxY =
                Mathf.Max(
                    maxY,
                    cell.y
                );
        }


        float width =
            (maxX -
             minX +
             1) *
            cellSize;


        float height =
            (maxY -
             minY +
             1) *
            cellSize;


        float centreX =
            ((minX +
              maxX +
              1) *
             0.5f) *
            cellSize;


        float centreY =
            ((minY +
              maxY +
              1) *
             0.5f) *
            cellSize;


        dungeonCamera.transform.position =
            new Vector3(
                centreX,
                centreY,
                dungeonCamera.transform.position.z
            );


        float vertical =
            height *
            0.5f;


        float horizontal =
            (width /
             dungeonCamera.aspect) *
            0.5f;


        dungeonCamera.orthographicSize =
            Mathf.Max(
                vertical,
                horizontal
            ) +
            cameraPadding;
    }


    // ============================================================
    // CLEANUP
    // ============================================================

    private void OnDestroy()
    {
        if (floorMesh != null)
        {
            Destroy(
                floorMesh
            );
        }


        if (wallMesh != null)
        {
            Destroy(
                wallMesh
            );
        }


        if (floorMaterial != null)
        {
            Destroy(
                floorMaterial
            );
        }


        if (wallMaterial != null)
        {
            Destroy(
                wallMaterial
            );
        }
    }
}