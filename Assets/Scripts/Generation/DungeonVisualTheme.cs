using UnityEngine;

/// <summary>
/// Stores the procedurally generated visual identity of one dungeon floor.
///
/// This contains presentation data only. It does not affect dungeon
/// topology, collision, AI or gameplay.
///
/// One theme is generated when a floor is created and then retained for
/// the complete lifetime of that floor. This is important because the
/// Shaper and Warden may cause DungeonRenderer to rebuild its meshes many
/// times during gameplay.
/// </summary>
[System.Serializable]
public class DungeonVisualTheme
{
    public string ThemeName;


    public Color FloorTint;

    public Color WallTint;


    public float SmallFloorVariationChance;

    public float LargeFloorPatternChance;

    public float WallVariationChance;

    public float EnvironmentalDetailAmount;

    public float LightWarmth;

    public float DepthIntensity;


    public int ThemeSeed;
}