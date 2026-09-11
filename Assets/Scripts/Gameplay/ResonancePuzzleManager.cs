using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Upgraded deterministic Resonance puzzle for semantic Puzzle rooms.
///
/// Visual design:
/// - three animated clue torches on the upper wall;
/// - three coloured levers inside the room;
/// - one replay tile placed close to the clue torches;
/// - replay shows the SAME sequence again;
/// - player presses E near a lever to use it;
/// - success reveals the existing Pulse/Shaper rewards.
/// </summary>
public class ResonancePuzzleManager : MonoBehaviour
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

    [SerializeField]
    private RunStatsManager runStatsManager;

    [Header("Torch Sprites")]

    [SerializeField]
    private Sprite unlitTorchSprite;

    [SerializeField]
    private Sprite[] flameAnimationFrames;


    [Header("Lever Sprites")]

    [SerializeField]
    private Sprite[] leverFrames;


    [Header("Replay Push Panel Sprites")]

    [Tooltip("Fully pressed frame: Push_Panel_01.")]
    [SerializeField]
    private Sprite pushPanelPressedSprite;

    [Tooltip("Middle transition frame: Push_Panel_02.")]
    [SerializeField]
    private Sprite pushPanelMiddleSprite;

    [Tooltip("Unpressed/resting frame: Push_Panel_03.")]
    [SerializeField]
    private Sprite pushPanelReleasedSprite;

    [Min(0.02f)]
    [SerializeField]
    private float pushPanelFrameDuration = 0.08f;

    [Min(0.02f)]
    [SerializeField]
    private float pushPanelPressedHoldDuration = 0.12f;


    [Header("Sequence Timing")]

    [Min(0.05f)]
    [SerializeField]
    private float initialSequenceDelay = 0.40f;

    [Min(0.10f)]
    [SerializeField]
    private float nodeFlashDuration = 0.55f;

    [Min(0.05f)]
    [SerializeField]
    private float gapBetweenFlashes = 0.18f;

    [Min(0.02f)]
    [SerializeField]
    private float leverFrameDuration = 0.08f;

    [Min(1f)]
    [SerializeField]
    private float flameFramesPerSecond = 9f;


    [Header("Interaction")]

    [Tooltip("Key used to interact with puzzle levers.")]
    [SerializeField]
    private KeyCode interactionKey = KeyCode.E;

    [Min(0)]
    [SerializeField]
    private int leverInteractionRange = 1;


    [Header("Replay Tile Placement")]

    [Tooltip("Minimum tiles below the upper wall for the replay tile.")]
    [Range(2, 4)]
    [SerializeField]
    private int minimumReplayRowsBelowTopWall = 2;

    [Tooltip("Maximum tiles below the upper wall for the replay tile.")]
    [Range(2, 4)]
    [SerializeField]
    private int maximumReplayRowsBelowTopWall = 4;


    [Header("Visual Positioning")]

    [SerializeField]
    private float leverZ = -1.93f;

    [SerializeField]
    private float replayTileZ = -1.82f;

    [Range(0.5f, 1.5f)]
    [SerializeField]
    private float replayPanelScale = 0.90f;

    [SerializeField]
    private float torchMountZ = -1.90f;

    [SerializeField]
    private float torchFlameZ = -1.95f;

    [SerializeField]
    private float torchVerticalOffset = 0.90f;

    [SerializeField]
    private float torchFlameVerticalOffset = 0.50f;


    [Header("Reward")]

    [SerializeField]
    private int pulseReward = 1;

    [SerializeField]
    private int shaperReward = 1;

    [Header("Reward Sprites")]

    [Tooltip("Assign Collectibles_Pulse_01.")]
    [SerializeField]
    private Sprite pulseRewardSprite;

    [Tooltip("Assign Collectibles_Digger_01.")]
    [SerializeField]
    private Sprite shaperRewardSprite;

    [Range(0.25f, 1.5f)]
    [SerializeField]
    private float pulseRewardScale = 0.80f;

    [Range(0.25f, 1.5f)]
    [SerializeField]
    private float shaperRewardScale = 0.85f;


    private sealed class PuzzleLever
    {
        public int Index;
        public string ColourName;
        public Color Colour;
        public Vector2Int Cell;
        public SpriteRenderer Renderer;
    }


    private sealed class PuzzleColourOption
    {
        public string Name;
        public Color Colour;

        public PuzzleColourOption(
            string name,
            Color colour)
        {
            Name = name;
            Colour = colour;
        }
    }


    private readonly List<PuzzleLever> levers =
        new List<PuzzleLever>();

    private readonly List<int> sequence =
        new List<int>();

    private readonly List<SpriteRenderer> clueFlames =
        new List<SpriteRenderer>();

    private readonly List<PuzzleColourOption> chosenColours =
        new List<PuzzleColourOption>();


    private Room puzzleRoom;

    private GameObject puzzleParent;

    private SpriteRenderer replayTileRenderer;

    private Vector2Int replayTileCell;

    private int observedGenerationVersion = -1;

    private int currentSequencePosition;

    private bool puzzleSolved;
    private bool showingSequence;
    private bool acceptingInput;
    private bool initialSequenceShown;
    private bool playerWasInsideRoom;

    private Vector2Int previousPlayerCell;
    private bool previousPlayerCellRecorded;

    private Coroutine activeSequenceCoroutine;
    private Coroutine leverCoroutine;
    private Coroutine replayPanelCoroutine;

    private bool hasInteractionMessage;

    private string currentInteractionMessage =
        string.Empty;

    private bool currentInteractionUsesKeycap;

    private string currentInteractionKeyLabel =
        string.Empty;

    private bool hasInteractionWorldPosition;

    private Vector3 currentInteractionWorldPosition;

    private bool replayPanelActivatedThisFloor;

    private static Sprite whitePixelSprite;


    public bool HasInteractionMessage =>
        hasInteractionMessage;

    public string CurrentInteractionMessage =>
        currentInteractionMessage;

    public bool CurrentInteractionUsesKeycap =>
        currentInteractionUsesKeycap;

    public string CurrentInteractionKeyLabel =>
        currentInteractionKeyLabel;

    public bool HasInteractionWorldPosition =>
        hasInteractionWorldPosition;

    public Vector3 CurrentInteractionWorldPosition =>
        currentInteractionWorldPosition;

    public bool PlayerCurrentlyInsidePuzzleRoom =>
        puzzleRoom != null &&
        playerController != null &&
        puzzleRoom.Contains(
            playerController.GridPosition
        );

    public bool HasReplayPanel =>
        replayTileRenderer != null;

    public Vector3 ReplayPanelWorldPosition =>
        replayTileRenderer != null
            ? replayTileRenderer.transform.position
            : new Vector3(
                replayTileCell.x + 0.5f,
                replayTileCell.y + 0.5f,
                replayTileZ
            );

    public bool ReplayPanelActivatedThisFloor =>
        replayPanelActivatedThisFloor;

    public bool InitialSequenceShown =>
        initialSequenceShown;

    public bool IsShowingSequence =>
        showingSequence;

    public bool PuzzleSolved =>
        puzzleSolved;


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
            observedGenerationVersion =
                dungeonGenerator.GenerationVersion;

            GeneratePuzzleForCurrentFloor();
        }

        if (puzzleRoom == null ||
            puzzleSolved ||
            !playerController.IsAlive)
        {
            ClearInteractionMessage();
            RecordPlayerCell();
            return;
        }

        Vector2Int playerCell =
            playerController.GridPosition;

        bool playerInside =
            puzzleRoom.Contains(playerCell);

        if (playerInside &&
            !playerWasInsideRoom &&
            !initialSequenceShown &&
            !showingSequence)
        {
            BeginSequenceDisplay(false);
        }

        bool enteredReplayTile =
            playerInside &&
            playerCell == replayTileCell &&
            (!previousPlayerCellRecorded ||
             previousPlayerCell != playerCell);

        if (enteredReplayTile &&
            !showingSequence &&
            leverCoroutine == null)
        {
            PressReplayPanel();

            BeginSequenceDisplay(true);
        }

        UpdateInteractionMessage(
            playerCell,
            playerInside
        );

        if (playerInside &&
            acceptingInput &&
            !showingSequence &&
            leverCoroutine == null &&
            Input.GetKeyDown(interactionKey))
        {
            TryActivateNearbyLever(playerCell);
        }

        playerWasInsideRoom =
            playerInside;

        previousPlayerCell =
            playerCell;

        previousPlayerCellRecorded =
            true;
    }


    // ============================================================
    // FLOOR GENERATION
    // ============================================================

    private void GeneratePuzzleForCurrentFloor()
    {
        ClearPuzzle();

        puzzleRoom =
            FindPuzzleRoom();

        if (puzzleRoom == null)
        {
            UnityEngine.Debug.LogWarning(
                "RESONANCE PUZZLE - No semantic Puzzle room was available on this floor."
            );
            return;
        }

        if (unlitTorchSprite == null ||
            flameAnimationFrames == null ||
            flameAnimationFrames.Length == 0 ||
            leverFrames == null ||
            leverFrames.Length == 0 ||
            pushPanelPressedSprite == null ||
            pushPanelMiddleSprite == null ||
            pushPanelReleasedSprite == null)
        {
            UnityEngine.Debug.LogWarning(
                "RESONANCE PUZZLE - Assign the torch sprite, flame frames, " +
                "lever frames and all three Push_Panel sprites in the Inspector."
            );
            return;
        }

        puzzleParent =
            new GameObject("Generated Resonance Puzzle");

        int puzzleSeed =
            unchecked(
                dungeonGenerator.CurrentSeed * 2281 ^
                puzzleRoom.Centre.x * 7919 ^
                puzzleRoom.Centre.y * 104729 ^
                dungeonGenerator.CurrentFloorDepth * 6700417
            );

        System.Random random =
            new System.Random(puzzleSeed);

        ChoosePuzzleColours(random);

        if (!CreatePuzzleLayout(random))
        {
            UnityEngine.Debug.LogWarning(
                "RESONANCE PUZZLE - Could not create a safe visual layout in the Puzzle room."
            );
            ClearPuzzle();
            return;
        }

        BuildSequence(random);

        currentSequencePosition = 0;
        puzzleSolved = false;
        showingSequence = false;
        acceptingInput = false;
        initialSequenceShown = false;
        playerWasInsideRoom = false;
        previousPlayerCellRecorded = false;
        replayPanelActivatedThisFloor = false;

        UnityEngine.Debug.Log(
            "========== RESONANCE PUZZLE ==========\n" +
            $"Seed: {dungeonGenerator.CurrentSeed}\n" +
            $"Room centre: {puzzleRoom.Centre}\n" +
            $"Colours: {BuildColourSetDebugString()}\n" +
            $"Sequence: {BuildSequenceDebugString()}\n" +
            $"Replay tile: ({replayTileCell.x}, {replayTileCell.y})\n" +
            "Input: stand near a lever and press E\n" +
            "======================================"
        );
    }


    private Room FindPuzzleRoom()
    {
        if (dungeonGenerator.Rooms == null)
            return null;

        foreach (Room room in dungeonGenerator.Rooms)
        {
            if (room != null &&
                room.Role == RoomRole.Puzzle)
            {
                return room;
            }
        }

        return null;
    }


    private void ChoosePuzzleColours(
        System.Random random)
    {
        chosenColours.Clear();

        List<PuzzleColourOption> palette =
            BuildColourPalette();

        ShuffleColourList(
            palette,
            random
        );

        /*
         * Puzzle colours must be immediately distinguishable during a short
         * memory sequence.
         *
         * RGB distance alone is not enough because colours such as:
         * - red / orange
         * - yellow / orange
         * - blue / cyan
         * - purple / pink
         *
         * can still look very similar once multiplied through the original
         * pixel-art flame and lever sprites.
         *
         * Instead, colours are accepted only when their HSV hue is separated
         * by at least 0.24 around the colour wheel (about 86 degrees).
         *
         * With only three puzzle colours this still leaves plenty of valid
         * combinations while making the sequence much easier to read.
         */
        const float minimumHueSeparation =
            0.24f;

        foreach (PuzzleColourOption candidate in
                 palette)
        {
            bool sufficientlyDifferent =
                true;

            foreach (PuzzleColourOption existing in
                     chosenColours)
            {
                if (HueDistance(
                        candidate.Colour,
                        existing.Colour) <
                    minimumHueSeparation)
                {
                    sufficientlyDifferent =
                        false;

                    break;
                }
            }

            if (!sufficientlyDifferent)
            {
                continue;
            }

            chosenColours.Add(
                candidate
            );

            if (chosenColours.Count >= 3)
            {
                break;
            }
        }

        /*
         * The palette below is deliberately spaced widely enough that the
         * normal path should always find three colours. This fallback is only
         * defensive in case the palette is changed later.
         */
        if (chosenColours.Count < 3)
        {
            chosenColours.Clear();

            chosenColours.Add(
                FindColourByName(
                    palette,
                    "Red"
                )
            );

            chosenColours.Add(
                FindColourByName(
                    palette,
                    "Green"
                )
            );

            chosenColours.Add(
                FindColourByName(
                    palette,
                    "Blue"
                )
            );
        }
    }


    private float HueDistance(
        Color a,
        Color b)
    {
        Color.RGBToHSV(
            a,
            out float hueA,
            out _,
            out _
        );

        Color.RGBToHSV(
            b,
            out float hueB,
            out _,
            out _
        );

        float directDistance =
            Mathf.Abs(
                hueA -
                hueB
            );

        return Mathf.Min(
            directDistance,
            1f - directDistance
        );
    }


    private PuzzleColourOption FindColourByName(
        List<PuzzleColourOption> palette,
        string colourName)
    {
        foreach (PuzzleColourOption option in
                 palette)
        {
            if (option.Name ==
                colourName)
            {
                return option;
            }
        }

        return palette[0];
    }


    private List<PuzzleColourOption> BuildColourPalette()
    {
        /*
         * Deliberately avoid neutral and warm intermediate colours.
         *
         * Removed:
         * - White / Grey / Black: poor tinting on the existing artwork.
         * - Brown: too close to the lever sprite's original colour.
         * - Orange: too close to the normal flame / red / yellow.
         *
         * Yellow is retained because it is visually strong, but the hue
         * separation rule prevents it appearing beside another similar
         * warm colour.
         */
        return new List<PuzzleColourOption>
        {
            new PuzzleColourOption(
                "Red",
                new Color(
                    1.00f,
                    0.10f,
                    0.10f,
                    1f
                )
            ),

            new PuzzleColourOption(
                "Green",
                new Color(
                    0.08f,
                    1.00f,
                    0.18f,
                    1f
                )
            ),

            new PuzzleColourOption(
                "Blue",
                new Color(
                    0.08f,
                    0.32f,
                    1.00f,
                    1f
                )
            ),

            new PuzzleColourOption(
                "Purple",
                new Color(
                    0.62f,
                    0.10f,
                    1.00f,
                    1f
                )
            ),

            new PuzzleColourOption(
                "Cyan",
                new Color(
                    0.05f,
                    0.95f,
                    1.00f,
                    1f
                )
            ),

            new PuzzleColourOption(
                "Yellow",
                new Color(
                    1.00f,
                    0.95f,
                    0.05f,
                    1f
                )
            ),

            new PuzzleColourOption(
                "Pink",
                new Color(
                    1.00f,
                    0.08f,
                    0.62f,
                    1f
                )
            )
        };
    }


    private void ShuffleColourList(
        List<PuzzleColourOption> list,
        System.Random random)
    {
        for (int i = list.Count - 1;
             i > 0;
             i--)
        {
            int swapIndex =
                random.Next(0, i + 1);

            PuzzleColourOption temporary =
                list[i];

            list[i] =
                list[swapIndex];

            list[swapIndex] =
                temporary;
        }
    }


    // ============================================================
    // PROCEDURAL LAYOUT
    // ============================================================

    private bool CreatePuzzleLayout(
        System.Random random)
    {
        HashSet<Vector2Int> usedCells =
            new HashSet<Vector2Int>();

        if (!TryPlaceReplayTile(random, usedCells))
        {
            return false;
        }

        List<int> clueXs =
            ChooseClueColumns(random);

        for (int i = 0;
             i < 3;
             i++)
        {
            CreateClueStation(
                i,
                clueXs[i],
                chosenColours[i].Colour,
                random
            );
        }

        if (!TryPlaceLevers(random, usedCells))
        {
            return false;
        }

        return true;
    }


    private bool TryPlaceReplayTile(
        System.Random random,
        HashSet<Vector2Int> usedCells)
    {
        int centreX =
            puzzleRoom.Centre.x;

        for (int rowsDown =
                 minimumReplayRowsBelowTopWall;
             rowsDown <=
                 maximumReplayRowsBelowTopWall;
             rowsDown++)
        {
            Vector2Int topAnchor =
                FindNorthWallFloorAnchor(centreX);

            Vector2Int preferred =
                new Vector2Int(
                    topAnchor.x,
                    topAnchor.y - rowsDown
                );

            if (TryFindSafePuzzleCell(
                    preferred,
                    usedCells,
                    3,
                    out replayTileCell))
            {
                usedCells.Add(replayTileCell);
                CreateReplayTile();
                return true;
            }
        }

        return false;
    }


    private List<int> ChooseClueColumns(
        System.Random random)
    {
        List<int> candidates =
            new List<int>();

        int minX =
            puzzleRoom.Bounds.xMin + 1;

        int maxX =
            puzzleRoom.Bounds.xMax - 2;

        int replayX =
            replayTileCell.x;

        AddUniqueClampedColumn(
            candidates,
            replayX - 4,
            minX,
            maxX
        );

        AddUniqueClampedColumn(
            candidates,
            replayX - 2,
            minX,
            maxX
        );

        AddUniqueClampedColumn(
            candidates,
            replayX,
            minX,
            maxX
        );

        AddUniqueClampedColumn(
            candidates,
            replayX + 2,
            minX,
            maxX
        );

        AddUniqueClampedColumn(
            candidates,
            replayX + 4,
            minX,
            maxX
        );

        AddUniqueClampedColumn(
            candidates,
            minX,
            minX,
            maxX
        );

        AddUniqueClampedColumn(
            candidates,
            maxX,
            minX,
            maxX
        );

        ShuffleIntList(candidates, random);

        List<int> selected =
            new List<int>();

        for (int i = 0;
             i < candidates.Count &&
             selected.Count < 3;
             i++)
        {
            bool tooClose = false;

            for (int j = 0;
                 j < selected.Count;
                 j++)
            {
                if (Mathf.Abs(
                        selected[j] -
                        candidates[i]) < 2)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                selected.Add(candidates[i]);
            }
        }

        while (selected.Count < 3)
        {
            selected.Add(replayX);
        }

        return selected;
    }


    private void AddUniqueClampedColumn(
        List<int> list,
        int x,
        int minX,
        int maxX)
    {
        int clamped =
            Mathf.Clamp(x, minX, maxX);

        if (!list.Contains(clamped))
        {
            list.Add(clamped);
        }
    }


    private void ShuffleIntList(
        List<int> list,
        System.Random random)
    {
        for (int i = list.Count - 1;
             i > 0;
             i--)
        {
            int swapIndex =
                random.Next(0, i + 1);

            int temporary =
                list[i];

            list[i] =
                list[swapIndex];

            list[swapIndex] =
                temporary;
        }
    }


    private bool TryPlaceLevers(
        System.Random random,
        HashSet<Vector2Int> usedCells)
    {
        List<Vector2Int[]> patterns =
            BuildLeverPatterns();

        ShufflePatternList(patterns, random);

        for (int p = 0;
             p < patterns.Count;
             p++)
        {
            Vector2Int[] pattern =
                patterns[p];

            List<Vector2Int> chosenCells =
                new List<Vector2Int>();

            HashSet<Vector2Int> temporaryUsed =
                new HashSet<Vector2Int>(usedCells);

            bool validPattern = true;

            for (int i = 0;
                 i < pattern.Length;
                 i++)
            {
                Vector2Int preferred =
                    puzzleRoom.Centre +
                    pattern[i];

                if (!TryFindSafePuzzleCell(
                        preferred,
                        temporaryUsed,
                        2,
                        out Vector2Int foundCell))
                {
                    validPattern = false;
                    break;
                }

                chosenCells.Add(foundCell);
                temporaryUsed.Add(foundCell);
            }

            if (!validPattern)
            {
                continue;
            }

            for (int i = 0;
                 i < chosenCells.Count;
                 i++)
            {
                usedCells.Add(chosenCells[i]);

                CreateLever(
                    i,
                    chosenCells[i],
                    chosenColours[i].Name,
                    chosenColours[i].Colour
                );
            }

            return true;
        }

        return false;
    }


    private List<Vector2Int[]> BuildLeverPatterns()
    {
        return new List<Vector2Int[]>
        {
            new[]
            {
                new Vector2Int(-2, -1),
                new Vector2Int( 0,  0),
                new Vector2Int( 2, -1)
            },

            new[]
            {
                new Vector2Int(-2,  0),
                new Vector2Int( 0, -1),
                new Vector2Int( 2,  1)
            },

            new[]
            {
                new Vector2Int(-1, -1),
                new Vector2Int( 1,  0),
                new Vector2Int(-2,  1)
            },

            new[]
            {
                new Vector2Int( 0, -1),
                new Vector2Int(-2,  1),
                new Vector2Int( 2,  1)
            },

            new[]
            {
                new Vector2Int(-1,  1),
                new Vector2Int( 1, -1),
                new Vector2Int( 2,  0)
            },

            new[]
            {
                new Vector2Int(-2,  0),
                new Vector2Int( 1,  0),
                new Vector2Int( 0,  2)
            },

            new[]
            {
                new Vector2Int(-1,  2),
                new Vector2Int( 1,  1),
                new Vector2Int( 0, -1)
            }
        };
    }


    private void ShufflePatternList(
        List<Vector2Int[]> list,
        System.Random random)
    {
        for (int i = list.Count - 1;
             i > 0;
             i--)
        {
            int swapIndex =
                random.Next(0, i + 1);

            Vector2Int[] temporary =
                list[i];

            list[i] =
                list[swapIndex];

            list[swapIndex] =
                temporary;
        }
    }


    private bool TryFindSafePuzzleCell(
        Vector2Int preferred,
        HashSet<Vector2Int> usedCells,
        int maximumRadius,
        out Vector2Int selected)
    {
        for (int radius = 0;
             radius <= maximumRadius;
             radius++)
        {
            for (int x = -radius;
                 x <= radius;
                 x++)
            {
                for (int y = -radius;
                     y <= radius;
                     y++)
                {
                    if (Mathf.Abs(x) +
                        Mathf.Abs(y) !=
                        radius)
                    {
                        continue;
                    }

                    Vector2Int candidate =
                        preferred +
                        new Vector2Int(x, y);

                    if (!puzzleRoom.Contains(candidate))
                    {
                        continue;
                    }

                    if (!dungeonGenerator.Grid.IsNavigable(candidate))
                    {
                        continue;
                    }

                    if (IsObjectiveCell(candidate))
                    {
                        continue;
                    }

                    if (usedCells != null &&
                        usedCells.Contains(candidate))
                    {
                        continue;
                    }

                    selected = candidate;
                    return true;
                }
            }
        }

        selected = Vector2Int.zero;
        return false;
    }


    private bool IsObjectiveCell(
        Vector2Int cell)
    {
        if (dungeonGenerator.ObjectiveManager == null)
        {
            return false;
        }

        foreach (Vector2Int objectiveCell in
                 dungeonGenerator.ObjectiveManager.ObjectiveCells)
        {
            if (objectiveCell == cell)
            {
                return true;
            }
        }

        return false;
    }


    private void CreateLever(
        int index,
        Vector2Int cell,
        string colourName,
        Color colour)
    {
        GameObject leverObject =
            new GameObject(
                $"Resonance Lever - {colourName}"
            );

        leverObject.transform.SetParent(
            puzzleParent.transform
        );

        leverObject.transform.position =
            new Vector3(
                cell.x + 0.5f,
                cell.y + 0.5f,
                leverZ
            );

        SpriteRenderer renderer =
            leverObject.AddComponent<SpriteRenderer>();

        renderer.sprite =
            leverFrames[0];

        /*
         * The lever sprite itself carries the puzzle colour.
         * No separate colour square is generated.
         */
        renderer.color =
            colour;

        PuzzleLever lever =
            new PuzzleLever();

        lever.Index = index;
        lever.ColourName = colourName;
        lever.Colour = colour;
        lever.Cell = cell;
        lever.Renderer = renderer;

        levers.Add(lever);
    }


    private void CreateClueStation(
        int index,
        int preferredX,
        Color colour,
        System.Random random)
    {
        Vector2Int anchor =
            FindNorthWallFloorAnchor(preferredX);

        GameObject station =
            new GameObject(
                $"Resonance Clue - {chosenColours[index].Name}"
            );

        station.transform.SetParent(
            puzzleParent.transform
        );

        Vector3 floorCentre =
            new Vector3(
                anchor.x + 0.5f,
                anchor.y + 0.5f,
                0f
            );

        GameObject torch =
            new GameObject("Unlit Torch");

        torch.transform.SetParent(
            station.transform
        );

        torch.transform.position =
            new Vector3(
                floorCentre.x,
                floorCentre.y + torchVerticalOffset,
                torchMountZ
            );

        SpriteRenderer torchRenderer =
            torch.AddComponent<SpriteRenderer>();

        torchRenderer.sprite =
            unlitTorchSprite;

        torchRenderer.color =
            Color.white;

        GameObject flame =
            new GameObject("Clue Flame");

        flame.transform.SetParent(
            torch.transform,
            false
        );

        flame.transform.localPosition =
            new Vector3(
                0f,
                torchFlameVerticalOffset,
                torchFlameZ - torchMountZ
            );

        SpriteRenderer flameRenderer =
            flame.AddComponent<SpriteRenderer>();

        int startFrame =
            random.Next(0, flameAnimationFrames.Length);

        flameRenderer.sprite =
            flameAnimationFrames[startFrame];

        flameRenderer.color =
            colour;

        EnvironmentSpriteAnimator animator =
            flame.AddComponent<EnvironmentSpriteAnimator>();

        animator.Initialise(
            flameRenderer,
            flameAnimationFrames,
            flameFramesPerSecond,
            startFrame,
            1f
        );

        flameRenderer.enabled = false;

        clueFlames.Add(flameRenderer);
    }


    private Vector2Int FindNorthWallFloorAnchor(
        int preferredX)
    {
        DungeonGrid grid =
            dungeonGenerator.Grid;

        int minX =
            puzzleRoom.Bounds.xMin;

        int maxX =
            puzzleRoom.Bounds.xMax - 1;

        int startingX =
            Mathf.Clamp(
                preferredX,
                minX,
                maxX
            );

        int maximumHorizontalSearch =
            Mathf.Max(2, puzzleRoom.Bounds.width);

        for (int xDistance = 0;
             xDistance <= maximumHorizontalSearch;
             xDistance++)
        {
            int leftX =
                startingX - xDistance;

            int rightX =
                startingX + xDistance;

            if (TryFindNorthAnchorInColumn(
                    grid,
                    leftX,
                    minX,
                    maxX,
                    out Vector2Int leftAnchor))
            {
                return leftAnchor;
            }

            if (rightX != leftX &&
                TryFindNorthAnchorInColumn(
                    grid,
                    rightX,
                    minX,
                    maxX,
                    out Vector2Int rightAnchor))
            {
                return rightAnchor;
            }
        }

        return new Vector2Int(
            startingX,
            puzzleRoom.Bounds.yMax - 1
        );
    }


    private bool TryFindNorthAnchorInColumn(
        DungeonGrid grid,
        int x,
        int minX,
        int maxX,
        out Vector2Int anchor)
    {
        if (x < minX ||
            x > maxX)
        {
            anchor = Vector2Int.zero;
            return false;
        }

        for (int y = puzzleRoom.Bounds.yMax + 6;
             y >= puzzleRoom.Bounds.yMin;
             y--)
        {
            Vector2Int floorCell =
                new Vector2Int(x, y);

            if (!grid.IsWalkable(floorCell))
            {
                continue;
            }

            Vector2Int above =
                floorCell + Vector2Int.up;

            if (grid.IsWalkable(above))
            {
                continue;
            }

            anchor = floorCell;
            return true;
        }

        anchor = Vector2Int.zero;
        return false;
    }


    private void CreateReplayTile()
    {
        GameObject replay =
            new GameObject(
                "Resonance Replay Push Panel"
            );

        replay.transform.SetParent(
            puzzleParent.transform
        );

        replay.transform.position =
            new Vector3(
                replayTileCell.x + 0.5f,
                replayTileCell.y + 0.5f,
                replayTileZ
            );

        replay.transform.localScale =
            new Vector3(
                replayPanelScale,
                replayPanelScale,
                1f
            );

        replayTileRenderer =
            replay.AddComponent<SpriteRenderer>();

        /*
         * Push_Panel_03 is the normal unpressed state.
         * No coloured square is generated anymore.
         */
        replayTileRenderer.sprite =
            pushPanelReleasedSprite;

        replayTileRenderer.color =
            Color.white;
    }


    private void PressReplayPanel()
    {
        replayPanelActivatedThisFloor =
            true;

        if (replayTileRenderer == null)
        {
            return;
        }

        if (replayPanelCoroutine != null)
        {
            StopCoroutine(
                replayPanelCoroutine
            );
        }

        replayPanelCoroutine =
            StartCoroutine(
                AnimateReplayPanelPress()
            );
    }


    private IEnumerator AnimateReplayPanelPress()
    {
        replayTileRenderer.color =
            Color.white;

        replayTileRenderer.sprite =
            pushPanelReleasedSprite;

        yield return new WaitForSeconds(
            pushPanelFrameDuration
        );

        replayTileRenderer.sprite =
            pushPanelMiddleSprite;

        yield return new WaitForSeconds(
            pushPanelFrameDuration
        );

        replayTileRenderer.sprite =
            pushPanelPressedSprite;

        yield return new WaitForSeconds(
            pushPanelPressedHoldDuration
        );

        /*
         * Release the button after the press so it is ready for another
         * replay later.
         */
        replayTileRenderer.sprite =
            pushPanelMiddleSprite;

        yield return new WaitForSeconds(
            pushPanelFrameDuration
        );

        replayTileRenderer.sprite =
            pushPanelReleasedSprite;

        replayPanelCoroutine =
            null;
    }


    // ============================================================
    // PROCEDURAL SEQUENCE
    // ============================================================

    private void BuildSequence(
        System.Random random)
    {
        sequence.Clear();

        sequence.Add(0);
        sequence.Add(1);
        sequence.Add(2);

        for (int i = sequence.Count - 1;
             i > 0;
             i--)
        {
            int swapIndex =
                random.Next(0, i + 1);

            int temporary =
                sequence[i];

            sequence[i] =
                sequence[swapIndex];

            sequence[swapIndex] =
                temporary;
        }
    }


    private string BuildColourSetDebugString()
    {
        if (chosenColours.Count == 0)
        {
            return "-";
        }

        return chosenColours[0].Name + ", " +
               chosenColours[1].Name + ", " +
               chosenColours[2].Name;
    }


    private string BuildSequenceDebugString()
    {
        if (sequence.Count == 0)
            return "-";

        string result =
            string.Empty;

        for (int i = 0;
             i < sequence.Count;
             i++)
        {
            if (i > 0)
            {
                result += " -> ";
            }

            result += chosenColours[sequence[i]].Name;
        }

        return result;
    }


    // ============================================================
    // SEQUENCE PRESENTATION
    // ============================================================

    private void BeginSequenceDisplay(
        bool replayed)
    {
        if (puzzleSolved ||
            showingSequence)
        {
            return;
        }

        acceptingInput = false;
        currentSequencePosition = 0;

        ResetLeverVisuals();

        if (activeSequenceCoroutine != null)
        {
            StopCoroutine(activeSequenceCoroutine);
        }

        activeSequenceCoroutine =
            StartCoroutine(
                ShowSequence(replayed)
            );
    }


    private IEnumerator ShowSequence(
        bool replayed)
    {
        showingSequence = true;
        initialSequenceShown = true;

        SetAllClueFlames(false);

        if (replayed)
        {
            UnityEngine.Debug.Log(
                "RESONANCE REPLAY TILE - Replaying sequence: " +
                BuildSequenceDebugString()
            );
        }

        yield return new WaitForSeconds(initialSequenceDelay);

        for (int i = 0;
             i < sequence.Count;
             i++)
        {
            int clueIndex =
                sequence[i];

            SpriteRenderer flame =
                clueFlames[clueIndex];

            if (flame != null)
            {
                flame.enabled = true;
            }

            yield return new WaitForSeconds(nodeFlashDuration);

            if (flame != null)
            {
                flame.enabled = false;
            }

            if (i < sequence.Count - 1)
            {
                yield return new WaitForSeconds(gapBetweenFlashes);
            }
        }

        currentSequencePosition = 0;
        acceptingInput = true;
        showingSequence = false;
        activeSequenceCoroutine = null;

        previousPlayerCell =
            playerController.GridPosition;

        previousPlayerCellRecorded = true;

        UnityEngine.Debug.Log(
            "RESONANCE PUZZLE - Awaiting lever sequence."
        );
    }


    // ============================================================
    // PLAYER INPUT
    // ============================================================

    private void UpdateInteractionMessage(
        Vector2Int playerCell,
        bool playerInside)
    {
        ClearInteractionMessage();

        if (!playerInside)
        {
            return;
        }

        /*
         * Only real button-driven interactions belong here now.
         *
         * The replay-panel explanation is a one-run tutorial handled by
         * GameplayTutorialController, so it no longer creates a permanent
         * proximity message or a misleading E keycap.
         */
        if (acceptingInput &&
            !showingSequence &&
            leverCoroutine == null)
        {
            PuzzleLever nearest =
                FindNearestLeverInRange(playerCell);

            if (nearest != null)
            {
                hasInteractionMessage =
                    true;

                currentInteractionMessage =
                    "PULL " +
                    nearest.ColourName.ToUpperInvariant() +
                    " LEVER";

                currentInteractionUsesKeycap =
                    true;

                currentInteractionKeyLabel =
                    interactionKey.ToString();

                hasInteractionWorldPosition =
                    true;


                if (nearest.Renderer != null)
                {
                    currentInteractionWorldPosition =
                        nearest.Renderer.transform.position;
                }
                else
                {
                    currentInteractionWorldPosition =
                        new Vector3(
                            nearest.Cell.x + 0.5f,
                            nearest.Cell.y + 0.5f,
                            leverZ
                        );
                }
            }
        }
    }


    private void TryActivateNearbyLever(
        Vector2Int playerCell)
    {
        PuzzleLever lever =
            FindNearestLeverInRange(playerCell);

        if (lever == null)
        {
            return;
        }

        leverCoroutine =
            StartCoroutine(
                PullLeverAndCheckInput(lever)
            );
    }


    private PuzzleLever FindNearestLeverInRange(
        Vector2Int playerCell)
    {
        PuzzleLever nearest =
            null;

        int nearestDistance =
            int.MaxValue;

        foreach (PuzzleLever lever in levers)
        {
            int distance =
                Mathf.Abs(playerCell.x - lever.Cell.x) +
                Mathf.Abs(playerCell.y - lever.Cell.y);

            if (distance >
                    leverInteractionRange ||
                distance >= nearestDistance)
            {
                continue;
            }

            nearest = lever;
            nearestDistance = distance;
        }

        return nearest;
    }


    private IEnumerator PullLeverAndCheckInput(
        PuzzleLever lever)
    {
        acceptingInput = false;

        if (lever != null &&
            lever.Renderer != null)
        {
            for (int i = 0;
                 i < leverFrames.Length;
                 i++)
            {
                if (leverFrames[i] != null)
                {
                    lever.Renderer.sprite =
                        leverFrames[i];
                }

                yield return new WaitForSeconds(
                    leverFrameDuration
                );
            }
        }

        int expectedLeverIndex =
            sequence[currentSequencePosition];

        if (lever.Index != expectedLeverIndex)
        {
            UnityEngine.Debug.Log(
                "RESONANCE INCORRECT - " +
                $"Expected {chosenColours[expectedLeverIndex].Name}, " +
                $"received {lever.ColourName}. " +
                "Sequence reset. Use the replay tile if you need the clue again."
            );

            currentSequencePosition = 0;

            yield return new WaitForSeconds(0.25f);

            ResetLeverVisuals();

            acceptingInput = true;
            leverCoroutine = null;
            yield break;
        }

        currentSequencePosition++;

        UnityEngine.Debug.Log(
            "RESONANCE CORRECT - " +
            $"{lever.ColourName}. " +
            $"{currentSequencePosition}/{sequence.Count}"
        );

        if (currentSequencePosition >= sequence.Count)
        {
            leverCoroutine = null;
            CompletePuzzle();
            yield break;
        }

        acceptingInput = true;
        leverCoroutine = null;
    }


    private void ResetLeverVisuals()
    {
        if (leverFrames == null ||
            leverFrames.Length == 0)
        {
            return;
        }

        foreach (PuzzleLever lever in levers)
        {
            if (lever.Renderer != null &&
                leverFrames[0] != null)
            {
                lever.Renderer.sprite =
                    leverFrames[0];
            }
        }
    }


    // ============================================================
    // SUCCESS
    // ============================================================

    private void CompletePuzzle()
    {
        puzzleSolved = true;

        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
        }

        if (runStatsManager != null)
        {
            runStatsManager.RecordPuzzleCompleted(
                1
            );
        }

        acceptingInput = false;

        SetAllClueFlames(true);

        if (replayTileRenderer != null)
        {
            /*
             * Once the puzzle is solved the replay control is no longer
             * needed, so leave the physical panel visibly pressed.
             */
            replayTileRenderer.color =
                Color.white;

            replayTileRenderer.sprite =
                pushPanelPressedSprite;
        }

        UnityEngine.Debug.Log(
            "========== RESONANCE PUZZLE COMPLETE ==========\n" +
            $"Colours: {BuildColourSetDebugString()}\n" +
            $"Sequence: {BuildSequenceDebugString()}\n" +
            "Puzzle reward unlocked.\n" +
            "================================================"
        );

        SpawnRewards();
    }


    private void SpawnRewards()
    {
        List<Vector2Int> rewardCells =
            FindRewardCells();

        int rewardIndex = 0;

        if (pulseReward > 0 &&
            rewardIndex < rewardCells.Count)
        {
            CreateReward(
                ResourcePickup.ResourceType.Pulse,
                pulseReward,
                rewardCells[rewardIndex],
                new Color(0.20f, 0.75f, 1f, 1f)
            );

            rewardIndex++;
        }

        if (shaperReward > 0 &&
            rewardIndex < rewardCells.Count)
        {
            CreateReward(
                ResourcePickup.ResourceType.Shaper,
                shaperReward,
                rewardCells[rewardIndex],
                new Color(1f, 0.35f, 0.85f, 1f)
            );
        }
    }


    private List<Vector2Int> FindRewardCells()
    {
        List<Vector2Int> result =
            new List<Vector2Int>();

        Vector2Int centre =
            puzzleRoom.Centre;

        Vector2Int[] offsets =
        {
            Vector2Int.up,
            Vector2Int.up + Vector2Int.left,
            Vector2Int.up + Vector2Int.right,
            Vector2Int.zero,
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.down
        };

        foreach (Vector2Int offset in offsets)
        {
            Vector2Int cell =
                centre + offset;

            if (!puzzleRoom.Contains(cell))
            {
                continue;
            }

            if (!dungeonGenerator.Grid.IsNavigable(cell))
            {
                continue;
            }

            if (FindLeverAtCell(cell) != null)
            {
                continue;
            }

            if (cell == replayTileCell)
            {
                continue;
            }

            if (IsObjectiveCell(cell))
            {
                continue;
            }

            result.Add(cell);

            if (result.Count >= 2)
                break;
        }

        return result;
    }


    private void CreateReward(
        ResourcePickup.ResourceType type,
        int amount,
        Vector2Int cell,
        Color colour)
    {
        GameObject reward =
            new GameObject(
                type == ResourcePickup.ResourceType.Pulse
                    ? "Puzzle Reward - Pulse"
                    : "Puzzle Reward - Shaper"
            );

        reward.transform.SetParent(
            puzzleParent.transform
        );

        Sprite rewardSprite =
            type == ResourcePickup.ResourceType.Pulse
                ? pulseRewardSprite
                : shaperRewardSprite;

        float rewardScale =
            type == ResourcePickup.ResourceType.Pulse
                ? pulseRewardScale
                : shaperRewardScale;

        if (rewardSprite == null)
        {
            UnityEngine.Debug.LogWarning(
                $"{type} puzzle reward sprite is not assigned. " +
                "The legacy coloured fallback will be used."
            );
        }

        ResourcePickup pickup =
            reward.AddComponent<ResourcePickup>();

        if (rewardSprite != null)
        {
            pickup.Initialise(
                type,
                cell,
                amount,
                playerController,
                pulseController,
                shaperController,
                rewardSprite,
                rewardScale
            );
        }
        else
        {
            pickup.Initialise(
                type,
                cell,
                amount,
                playerController,
                pulseController,
                shaperController,
                colour
            );
        }
    }


    // ============================================================
    // HELPERS
    // ============================================================

    private PuzzleLever FindLeverAtCell(
        Vector2Int cell)
    {
        foreach (PuzzleLever lever in levers)
        {
            if (lever.Cell == cell)
            {
                return lever;
            }
        }

        return null;
    }


    private void SetAllClueFlames(
        bool visible)
    {
        foreach (SpriteRenderer flame in clueFlames)
        {
            if (flame != null)
            {
                flame.enabled = visible;
            }
        }
    }


    private void RecordPlayerCell()
    {
        if (playerController == null)
            return;

        previousPlayerCell =
            playerController.GridPosition;

        previousPlayerCellRecorded =
            true;
    }


    private void ClearInteractionMessage()
    {
        hasInteractionMessage =
            false;

        currentInteractionMessage =
            string.Empty;

        currentInteractionUsesKeycap =
            false;

        currentInteractionKeyLabel =
            string.Empty;

        hasInteractionWorldPosition =
            false;

        currentInteractionWorldPosition =
            Vector3.zero;
    }


    private void ClearPuzzle()
    {
        if (activeSequenceCoroutine != null)
        {
            StopCoroutine(activeSequenceCoroutine);
            activeSequenceCoroutine = null;
        }

        if (leverCoroutine != null)
        {
            StopCoroutine(leverCoroutine);
            leverCoroutine = null;
        }

        if (replayPanelCoroutine != null)
        {
            StopCoroutine(replayPanelCoroutine);
            replayPanelCoroutine = null;
        }

        levers.Clear();
        sequence.Clear();
        clueFlames.Clear();
        chosenColours.Clear();

        if (puzzleParent != null)
        {
            Destroy(puzzleParent);
            puzzleParent = null;
        }

        puzzleRoom = null;
        replayTileRenderer = null;
        replayTileCell = Vector2Int.zero;
        currentSequencePosition = 0;
        puzzleSolved = false;
        showingSequence = false;
        acceptingInput = false;
        initialSequenceShown = false;
        playerWasInsideRoom = false;
        previousPlayerCellRecorded = false;
        replayPanelActivatedThisFloor = false;

        ClearInteractionMessage();
    }


    private Sprite GetWhitePixelSprite()
    {
        if (whitePixelSprite != null)
        {
            return whitePixelSprite;
        }

        whitePixelSprite =
            Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f
            );

        return whitePixelSprite;
    }
}
