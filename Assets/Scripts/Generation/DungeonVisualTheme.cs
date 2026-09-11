using UnityEngine;

/// <summary>
/// Controls which verified floor-detail tiles a generated theme prefers.
/// </summary>
public enum DungeonFloorDetailStyle
{
    Clean,
    LightCracked,
    Fractured,
    Ruined
}


/// <summary>
/// Controls which compatible straight-wall artwork is preferred.
///
/// These styles do not change wall topology. They only bias selection
/// between the already-verified compatible wall variants.
/// </summary>
public enum DungeonWallMasonryStyle
{
    Plain,
    Jointed,
    Weathered
}


/// <summary>
/// Stores the complete procedurally generated visual identity of one
/// dungeon floor.
///
/// The theme is created once when a new floor is generated and retained
/// throughout the lifetime of that floor.
/// </summary>
[System.Serializable]
public class DungeonVisualTheme
{
    public string ThemeName;


    // ============================================================
    // COLOUR
    // ============================================================

    public Color FloorTint;

    public Color WallTint;


    // ============================================================
    // TILE DENSITY
    // ============================================================

    public float SmallFloorVariationChance;

    public float LargeFloorPatternChance;

    public float WallVariationChance;


    // ============================================================
    // TILE CHARACTER
    // ============================================================

    public DungeonFloorDetailStyle FloorDetailStyle;

    public DungeonWallMasonryStyle WallMasonryStyle;


    /*
     * Probability that straight walls favour the masonry variant
     * associated with this theme.
     *
     * A value below 1 prevents an entire dungeon from repeating the
     * exact same wall tile everywhere.
     */
    public float WallStyleStrength;


    // ============================================================
    // ENVIRONMENTAL GENERATION
    // ============================================================

    public float EnvironmentalDetailAmount;

    public float LightWarmth;


    // ============================================================
    // DEPTH / REPRODUCIBILITY
    // ============================================================

    public float DepthIntensity;

    public int ThemeSeed;
}