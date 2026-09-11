using UnityEngine;

/// <summary>
/// Generates a deterministic visual identity for each procedural dungeon
/// floor.
///
/// VERSION 2
///
/// Main goals:
///
/// - noticeably stronger visual differences between generated floors;
/// - deterministic output from the run/floor seed;
/// - no immediate repetition of visual archetypes;
/// - support for both short runs and very long Survival runs;
/// - depth affects deterioration without eventually making the game black;
/// - visual randomness remains separate from gameplay generation.
///
/// The archetypes are NOT tied to particular floor numbers.
///
/// Instead, each run receives a seeded ordering of visual archetypes.
/// Within each archetype, colour, contrast and damage amounts are then
/// varied procedurally.
/// </summary>
public static class DungeonVisualThemeGenerator
{
    // ============================================================
    // RUN SEED STRUCTURE
    // ============================================================

    /*
     * DungeonRunManager currently generates floor seeds using:
     *
     * baseRunSeed + (floorNumber - 1) * 1009
     *
     * Knowing the stride allows this visual generator to infer the
     * original run seed from any floor seed.
     *
     * This means the theme sequence can be generated consistently
     * across an entire run without storing additional state.
     *
     * IMPORTANT:
     * FloorSeedStride must match the deterministic floor-seed spacing
     * used by DungeonRunManager.
     */
    private const int FloorSeedStride =
        1009;


    // ============================================================
    // THEME ARCHETYPES
    // ============================================================

    /*
     * These are visual FAMILIES rather than fixed levels.
     *
     * A Cold Stone dungeon on Floor 4 may therefore differ from another
     * Cold Stone dungeon encountered much later in Survival mode.
     */
    private enum ThemeArchetype
    {
        ColdStone,
        AshenRuins,
        AncientBlueVault,
        FadedVioletHalls,
        DeepSlateVault,
        DustTemple,
        VerdigrisCrypt,
        EmberRuins
    }


    // ============================================================
    // PUBLIC GENERATION
    // ============================================================

    public static DungeonVisualTheme Generate(
        int floorSeed,
        int depth)
    {
        depth =
            Mathf.Max(
                1,
                depth
            );


        /*
         * Reconstruct the run seed.
         *
         * Example:
         *
         * floor 1 = 12345
         * floor 2 = 13354
         * floor 3 = 14363
         *
         * All reconstruct back to:
         *
         * 12345
         */
        int inferredRunSeed =
            unchecked(
                floorSeed -
                (depth - 1) *
                FloorSeedStride
            );


        /*
         * Separate visual seed.
         *
         * This is deliberately independent from the System.Random
         * sequence used by DungeonGenerator.
         */
        int themeSeed =
            unchecked(
                floorSeed *
                486187739 +
                depth *
                104729 +
                inferredRunSeed *
                31 +
                9137
            );


        System.Random random =
            new System.Random(
                themeSeed
            );


        ThemeArchetype archetype =
            SelectArchetype(
                inferredRunSeed,
                depth
            );


        /*
         * Unlimited depth curve.
         *
         * Unlike V1, this never suddenly reaches a hard maximum at
         * Floor 20.
         *
         * It approaches 1 gradually:
         *
         * Floor 1  ≈ 0.00
         * Floor 10 ≈ 0.23
         * Floor 30 ≈ 0.49
         * Floor 70 ≈ 0.70
         *
         * Extremely deep floors can continue changing, but the curve
         * slows down so visuals remain usable.
         */
        float depthIntensity =
            CalculateDepthIntensity(
                depth
            );


        DungeonVisualTheme theme =
            new DungeonVisualTheme();


        theme.ThemeSeed =
            themeSeed;


        theme.DepthIntensity =
            depthIntensity;


        ApplyArchetype(
            theme,
            archetype
        );


        /*
         * Colour archetype and tile character are deliberately separate.
         *
         * This lets the same procedural theme control both palette and the
         * kind of floor/wall artwork used by DungeonRenderer.
         */
        ApplyTileStyle(
            theme,
            archetype
        );


        ApplyProceduralVariation(
            theme,
            random
        );


        ApplyDepthInfluence(
            theme,
            depthIntensity,
            random
        );


        ClampTheme(
            theme
        );


        return theme;
    }


    // ============================================================
    // ARCHETYPE SELECTION
    // ============================================================

    /// <summary>
    /// Generates a deterministic ordering of archetypes.
    ///
    /// With eight archetypes, each block of eight floors uses every
    /// archetype once in a procedurally shuffled order.
    ///
    /// Therefore a run does not produce sequences such as:
    ///
    /// Blue
    /// Blue
    /// Blue
    ///
    /// but the actual order is still determined by the run seed.
    /// </summary>
    private static ThemeArchetype SelectArchetype(
        int runSeed,
        int depth)
    {
        int archetypeCount =
            System.Enum.GetValues(
                typeof(ThemeArchetype)
            ).Length;


        int zeroBasedDepth =
            depth -
            1;


        int blockIndex =
            zeroBasedDepth /
            archetypeCount;


        int positionInBlock =
            zeroBasedDepth %
            archetypeCount;


        ThemeArchetype[] currentBlock =
            BuildArchetypeBlock(
                runSeed,
                blockIndex
            );


        /*
         * Prevent the first archetype of a new block matching the final
         * archetype of the previous block.
         *
         * This matters on:
         *
         * Floor 8 -> Floor 9
         * Floor 16 -> Floor 17
         * etc.
         */
        if (blockIndex > 0)
        {
            ThemeArchetype[] previousBlock =
                BuildArchetypeBlock(
                    runSeed,
                    blockIndex - 1
                );


            ThemeArchetype previousFinal =
                previousBlock[
                    previousBlock.Length - 1
                ];


            if (currentBlock[0] ==
                previousFinal)
            {
                for (int i = 1;
                     i < currentBlock.Length;
                     i++)
                {
                    if (currentBlock[i] ==
                        previousFinal)
                    {
                        continue;
                    }


                    ThemeArchetype temporary =
                        currentBlock[0];


                    currentBlock[0] =
                        currentBlock[i];


                    currentBlock[i] =
                        temporary;


                    break;
                }
            }
        }


        return
            currentBlock[
                positionInBlock
            ];
    }


    /// <summary>
    /// Builds one deterministic shuffled block containing every
    /// visual archetype exactly once.
    /// </summary>
    private static ThemeArchetype[] BuildArchetypeBlock(
        int runSeed,
        int blockIndex)
    {
        ThemeArchetype[] archetypes =
            (ThemeArchetype[])
            System.Enum.GetValues(
                typeof(ThemeArchetype)
            );


        int blockSeed =
            unchecked(
                runSeed *
                73856093 +
                blockIndex *
                19349663 +
                83492791
            );


        System.Random random =
            new System.Random(
                blockSeed
            );


        /*
         * Fisher-Yates shuffle.
         */
        for (int i =
                 archetypes.Length - 1;
             i > 0;
             i--)
        {
            int swapIndex =
                random.Next(
                    0,
                    i + 1
                );


            ThemeArchetype temporary =
                archetypes[i];


            archetypes[i] =
                archetypes[swapIndex];


            archetypes[swapIndex] =
                temporary;
        }


        return archetypes;
    }


    // ============================================================
    // DEPTH
    // ============================================================

    private static float CalculateDepthIntensity(
        int depth)
    {
        float adjustedDepth =
            Mathf.Max(
                0f,
                depth - 1f
            );


        /*
         * Asymptotic progression.
         *
         * It never abruptly stops increasing.
         */
        return
            adjustedDepth /
            (adjustedDepth + 30f);
    }


    // ============================================================
    // ARCHETYPE BASE VALUES
    // ============================================================

    private static void ApplyArchetype(
        DungeonVisualTheme theme,
        ThemeArchetype archetype)
    {
        switch (archetype)
        {
            // ----------------------------------------------------
            // COLD STONE
            //
            // Pale blue-grey.
            // Cleanest and brightest family.
            // ----------------------------------------------------

            case ThemeArchetype.ColdStone:

                theme.ThemeName =
                    "Cold Stone";


                theme.FloorTint =
                    new Color(
                        0.72f,
                        0.82f,
                        0.96f,
                        1f
                    );


                theme.WallTint =
                    new Color(
                        0.94f,
                        0.97f,
                        1.00f,
                        1f
                    );


                theme.SmallFloorVariationChance =
                    0.055f;


                theme.LargeFloorPatternChance =
                    0.010f;


                theme.WallVariationChance =
                    0.11f;


                theme.EnvironmentalDetailAmount =
                    0.18f;


                theme.LightWarmth =
                    0.12f;

                break;


            // ----------------------------------------------------
            // ASHEN RUINS
            //
            // Warm grey / faded brown.
            // Deliberately separated from the blue families.
            // ----------------------------------------------------

            case ThemeArchetype.AshenRuins:

                theme.ThemeName =
                    "Ashen Ruins";


                theme.FloorTint =
                    new Color(
                        0.72f,
                        0.64f,
                        0.56f,
                        1f
                    );


                theme.WallTint =
                    new Color(
                        0.92f,
                        0.84f,
                        0.74f,
                        1f
                    );


                theme.SmallFloorVariationChance =
                    0.17f;


                theme.LargeFloorPatternChance =
                    0.045f;


                theme.WallVariationChance =
                    0.27f;


                theme.EnvironmentalDetailAmount =
                    0.40f;


                theme.LightWarmth =
                    0.72f;

                break;


            // ----------------------------------------------------
            // ANCIENT BLUE VAULT
            //
            // Strong cyan / blue.
            // ----------------------------------------------------

            case ThemeArchetype.AncientBlueVault:

                theme.ThemeName =
                    "Ancient Blue Vault";


                theme.FloorTint =
                    new Color(
                        0.46f,
                        0.70f,
                        0.92f,
                        1f
                    );


                theme.WallTint =
                    new Color(
                        0.70f,
                        0.88f,
                        1.00f,
                        1f
                    );


                theme.SmallFloorVariationChance =
                    0.10f;


                theme.LargeFloorPatternChance =
                    0.025f;


                theme.WallVariationChance =
                    0.18f;


                theme.EnvironmentalDetailAmount =
                    0.28f;


                theme.LightWarmth =
                    0.05f;

                break;


            // ----------------------------------------------------
            // FADED VIOLET HALLS
            //
            // Obviously violet rather than slightly blue.
            // ----------------------------------------------------

            case ThemeArchetype.FadedVioletHalls:

                theme.ThemeName =
                    "Faded Violet Halls";


                theme.FloorTint =
                    new Color(
                        0.66f,
                        0.52f,
                        0.82f,
                        1f
                    );


                theme.WallTint =
                    new Color(
                        0.88f,
                        0.72f,
                        0.98f,
                        1f
                    );


                theme.SmallFloorVariationChance =
                    0.14f;


                theme.LargeFloorPatternChance =
                    0.038f;


                theme.WallVariationChance =
                    0.24f;


                theme.EnvironmentalDetailAmount =
                    0.33f;


                theme.LightWarmth =
                    0.25f;

                break;


            // ----------------------------------------------------
            // DEEP SLATE VAULT
            //
            // Dark neutral / desaturated.
            // ----------------------------------------------------

            case ThemeArchetype.DeepSlateVault:

                theme.ThemeName =
                    "Deep Slate Vault";


                theme.FloorTint =
                    new Color(
                        0.50f,
                        0.57f,
                        0.67f,
                        1f
                    );


                theme.WallTint =
                    new Color(
                        0.74f,
                        0.81f,
                        0.89f,
                        1f
                    );


                theme.SmallFloorVariationChance =
                    0.20f;


                theme.LargeFloorPatternChance =
                    0.058f;


                theme.WallVariationChance =
                    0.33f;


                theme.EnvironmentalDetailAmount =
                    0.47f;


                theme.LightWarmth =
                    0.32f;

                break;


            // ----------------------------------------------------
            // DUST TEMPLE
            //
            // Sandstone / old gold.
            // ----------------------------------------------------

            case ThemeArchetype.DustTemple:

                theme.ThemeName =
                    "Dust Temple";


                theme.FloorTint =
                    new Color(
                        0.78f,
                        0.68f,
                        0.45f,
                        1f
                    );


                theme.WallTint =
                    new Color(
                        0.98f,
                        0.88f,
                        0.62f,
                        1f
                    );


                theme.SmallFloorVariationChance =
                    0.12f;


                theme.LargeFloorPatternChance =
                    0.026f;


                theme.WallVariationChance =
                    0.20f;


                theme.EnvironmentalDetailAmount =
                    0.31f;


                theme.LightWarmth =
                    0.88f;

                break;


            // ----------------------------------------------------
            // VERDIGRIS CRYPT
            //
            // Green / turquoise oxidised stone.
            // ----------------------------------------------------

            case ThemeArchetype.VerdigrisCrypt:

                theme.ThemeName =
                    "Verdigris Crypt";


                theme.FloorTint =
                    new Color(
                        0.45f,
                        0.68f,
                        0.61f,
                        1f
                    );


                theme.WallTint =
                    new Color(
                        0.68f,
                        0.90f,
                        0.79f,
                        1f
                    );


                theme.SmallFloorVariationChance =
                    0.16f;


                theme.LargeFloorPatternChance =
                    0.036f;


                theme.WallVariationChance =
                    0.25f;


                theme.EnvironmentalDetailAmount =
                    0.38f;


                theme.LightWarmth =
                    0.18f;

                break;


            // ----------------------------------------------------
            // EMBER RUINS
            //
            // Rust / dark red-brown.
            // ----------------------------------------------------

            default:

                theme.ThemeName =
                    "Ember Ruins";


                theme.FloorTint =
                    new Color(
                        0.72f,
                        0.48f,
                        0.40f,
                        1f
                    );


                theme.WallTint =
                    new Color(
                        0.96f,
                        0.69f,
                        0.55f,
                        1f
                    );


                theme.SmallFloorVariationChance =
                    0.21f;


                theme.LargeFloorPatternChance =
                    0.052f;


                theme.WallVariationChance =
                    0.31f;


                theme.EnvironmentalDetailAmount =
                    0.44f;


                theme.LightWarmth =
                    0.95f;

                break;
        }
    }

    // ============================================================
    // TILE CHARACTER
    // ============================================================

    private static void ApplyTileStyle(
        DungeonVisualTheme theme,
        ThemeArchetype archetype)
    {
        switch (archetype)
        {
            case ThemeArchetype.ColdStone:

                theme.FloorDetailStyle =
                    DungeonFloorDetailStyle.Clean;

                theme.WallMasonryStyle =
                    DungeonWallMasonryStyle.Plain;

                theme.WallStyleStrength =
                    0.82f;

                break;


            case ThemeArchetype.AshenRuins:

                theme.FloorDetailStyle =
                    DungeonFloorDetailStyle.Fractured;

                theme.WallMasonryStyle =
                    DungeonWallMasonryStyle.Weathered;

                theme.WallStyleStrength =
                    0.76f;

                break;


            case ThemeArchetype.AncientBlueVault:

                theme.FloorDetailStyle =
                    DungeonFloorDetailStyle.LightCracked;

                theme.WallMasonryStyle =
                    DungeonWallMasonryStyle.Jointed;

                theme.WallStyleStrength =
                    0.78f;

                break;


            case ThemeArchetype.FadedVioletHalls:

                theme.FloorDetailStyle =
                    DungeonFloorDetailStyle.Fractured;

                theme.WallMasonryStyle =
                    DungeonWallMasonryStyle.Jointed;

                theme.WallStyleStrength =
                    0.72f;

                break;


            case ThemeArchetype.DeepSlateVault:

                theme.FloorDetailStyle =
                    DungeonFloorDetailStyle.Ruined;

                theme.WallMasonryStyle =
                    DungeonWallMasonryStyle.Weathered;

                theme.WallStyleStrength =
                    0.80f;

                break;


            case ThemeArchetype.DustTemple:

                theme.FloorDetailStyle =
                    DungeonFloorDetailStyle.LightCracked;

                theme.WallMasonryStyle =
                    DungeonWallMasonryStyle.Plain;

                theme.WallStyleStrength =
                    0.78f;

                break;


            case ThemeArchetype.VerdigrisCrypt:

                theme.FloorDetailStyle =
                    DungeonFloorDetailStyle.Fractured;

                theme.WallMasonryStyle =
                    DungeonWallMasonryStyle.Jointed;

                theme.WallStyleStrength =
                    0.74f;

                break;


            default:

                // Ember Ruins
                theme.FloorDetailStyle =
                    DungeonFloorDetailStyle.Ruined;

                theme.WallMasonryStyle =
                    DungeonWallMasonryStyle.Weathered;

                theme.WallStyleStrength =
                    0.84f;

                break;
        }
    }


    // ============================================================
    // PROCEDURAL VARIATION
    // ============================================================

    private static void ApplyProceduralVariation(
        DungeonVisualTheme theme,
        System.Random random)
    {
        /*
         * Archetypes provide recognisable identities.
         *
         * These variations ensure two instances of the same archetype
         * are still not exact copies.
         */

        float floorHueShift =
            RandomRange(
                random,
                -0.018f,
                0.018f
            );


        float wallHueShift =
            RandomRange(
                random,
                -0.014f,
                0.014f
            );


        float saturationMultiplier =
            RandomRange(
                random,
                0.90f,
                1.12f
            );


        float floorBrightness =
            RandomRange(
                random,
                0.91f,
                1.04f
            );


        float wallBrightness =
            RandomRange(
                random,
                0.96f,
                1.08f
            );


        theme.FloorTint =
            ModifyHSV(
                theme.FloorTint,
                floorHueShift,
                saturationMultiplier,
                floorBrightness
            );


        theme.WallTint =
            ModifyHSV(
                theme.WallTint,
                wallHueShift,
                saturationMultiplier,
                wallBrightness
            );


        /*
         * Procedural wall/floor contrast.
         *
         * Positive values make the floor darker and the walls brighter.
         * Negative values bring them slightly closer together.
         */
        float contrastShift =
            RandomRange(
                random,
                -0.035f,
                0.075f
            );


        theme.FloorTint =
            MultiplyColour(
                theme.FloorTint,
                1f -
                contrastShift
            );


        theme.WallTint =
            MultiplyColour(
                theme.WallTint,
                1f +
                contrastShift *
                0.55f
            );


        theme.SmallFloorVariationChance +=
            RandomRange(
                random,
                -0.030f,
                0.055f
            );


        theme.LargeFloorPatternChance +=
            RandomRange(
                random,
                -0.010f,
                0.020f
            );


        theme.WallVariationChance +=
            RandomRange(
                random,
                -0.045f,
                0.070f
            );


        theme.EnvironmentalDetailAmount +=
            RandomRange(
                random,
                -0.07f,
                0.09f
            );


        theme.LightWarmth +=
            RandomRange(
                random,
                -0.07f,
                0.07f
            );

        theme.WallStyleStrength +=
            RandomRange(
                random,
                -0.08f,
                0.08f
            );
    }


    // ============================================================
    // DEPTH INFLUENCE
    // ============================================================

    private static void ApplyDepthInfluence(
        DungeonVisualTheme theme,
        float depthIntensity,
        System.Random random)
    {
        /*
         * Depth primarily controls deterioration.
         *
         * It intentionally has only a SMALL effect on brightness because
         * Fog Of War already makes the dungeon dark.
         */

        float floorDarkening =
            Mathf.Lerp(
                1f,
                0.88f,
                depthIntensity
            );


        float wallDarkening =
            Mathf.Lerp(
                1f,
                0.93f,
                depthIntensity
            );


        theme.FloorTint =
            MultiplyColour(
                theme.FloorTint,
                floorDarkening
            );


        theme.WallTint =
            MultiplyColour(
                theme.WallTint,
                wallDarkening
            );


        /*
         * Deeper floors increasingly use damaged/detail artwork.
         */
        theme.SmallFloorVariationChance +=
            0.16f *
            depthIntensity;


        theme.LargeFloorPatternChance +=
            0.070f *
            depthIntensity;


        theme.WallVariationChance +=
            0.16f *
            depthIntensity;


        theme.EnvironmentalDetailAmount +=
            0.28f *
            depthIntensity;


        /*
         * Small deterministic deterioration variation means two floors
         * at similar depth do not deteriorate identically.
         */
        float deteriorationNoise =
            RandomRange(
                random,
                -0.025f,
                0.035f
            ) *
            depthIntensity;


        theme.SmallFloorVariationChance +=
            deteriorationNoise;


        theme.WallVariationChance +=
            deteriorationNoise;

        /*
         * Deep Survival floors can gradually progress toward a more damaged
         * version of the archetype.
         *
         * This means a Cold Stone floor at depth 70 does not have to look as
         * clean as Cold Stone encountered near the beginning of a run.
         *
         * Only the visual style is changed. Dungeon topology is unaffected.
         */
        if (random.NextDouble() <
            0.55 *
            depthIntensity)
        {
            PromoteFloorDetailStyle(
                theme
            );
        }


        /*
         * Extremely deep floors have a small possibility of receiving a
         * second deterioration step.
         */
        if (depthIntensity >
                0.75f &&
            random.NextDouble() <
                (depthIntensity - 0.75f) *
                0.80f)
        {
            PromoteFloorDetailStyle(
                theme
            );
        }
    }

    private static void PromoteFloorDetailStyle(
    DungeonVisualTheme theme)
    {
        switch (theme.FloorDetailStyle)
        {
            case DungeonFloorDetailStyle.Clean:

                theme.FloorDetailStyle =
                    DungeonFloorDetailStyle.LightCracked;

                break;


            case DungeonFloorDetailStyle.LightCracked:

                theme.FloorDetailStyle =
                    DungeonFloorDetailStyle.Fractured;

                break;


            case DungeonFloorDetailStyle.Fractured:

                theme.FloorDetailStyle =
                    DungeonFloorDetailStyle.Ruined;

                break;


            case DungeonFloorDetailStyle.Ruined:

                // Already at the most deteriorated visual state.
                break;
        }
    }


    // ============================================================
    // HSV COLOUR MODIFICATION
    // ============================================================

    private static Color ModifyHSV(
        Color colour,
        float hueShift,
        float saturationMultiplier,
        float valueMultiplier)
    {
        float hue;
        float saturation;
        float value;


        Color.RGBToHSV(
            colour,
            out hue,
            out saturation,
            out value
        );


        hue =
            Mathf.Repeat(
                hue +
                hueShift,
                1f
            );


        saturation =
            Mathf.Clamp01(
                saturation *
                saturationMultiplier
            );


        value =
            Mathf.Clamp01(
                value *
                valueMultiplier
            );


        Color result =
            Color.HSVToRGB(
                hue,
                saturation,
                value
            );


        result.a =
            colour.a;


        return result;
    }


    // ============================================================
    // SAFETY / CLAMPING
    // ============================================================

    private static void ClampTheme(
        DungeonVisualTheme theme)
    {
        theme.FloorTint =
            ClampColour(
                theme.FloorTint
            );


        theme.WallTint =
            ClampColour(
                theme.WallTint
            );


        theme.SmallFloorVariationChance =
            Mathf.Clamp(
                theme.SmallFloorVariationChance,
                0.025f,
                0.42f
            );


        theme.LargeFloorPatternChance =
            Mathf.Clamp(
                theme.LargeFloorPatternChance,
                0.005f,
                0.16f
            );


        theme.WallVariationChance =
            Mathf.Clamp(
                theme.WallVariationChance,
                0.05f,
                0.55f
            );


        theme.EnvironmentalDetailAmount =
            Mathf.Clamp01(
                theme.EnvironmentalDetailAmount
            );


        theme.LightWarmth =
            Mathf.Clamp01(
                theme.LightWarmth
            );


        theme.DepthIntensity =
            Mathf.Clamp01(
                theme.DepthIntensity
            );

        theme.WallStyleStrength =
            Mathf.Clamp(
                theme.WallStyleStrength,
                0.55f,
                0.92f
            );
    }


    private static Color ClampColour(
        Color colour)
    {
        return new Color(
            Mathf.Clamp01(
                colour.r
            ),

            Mathf.Clamp01(
                colour.g
            ),

            Mathf.Clamp01(
                colour.b
            ),

            1f
        );
    }


    private static Color MultiplyColour(
        Color colour,
        float amount)
    {
        return new Color(
            colour.r *
                amount,

            colour.g *
                amount,

            colour.b *
                amount,

            colour.a
        );
    }


    private static float RandomRange(
        System.Random random,
        float minimum,
        float maximum)
    {
        return
            minimum +
            (float)random.NextDouble() *
            (maximum - minimum);
    }
}