using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates and controls one deterministic resonance puzzle on each
/// floor which contains a semantic Puzzle room.
///
/// Three nodes flash in a seed-derived sequence. The player then walks
/// over those nodes in the same order.
///
/// Correct completion creates Pulse and Shaper resource rewards.
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


    [Header("Puzzle")]

    [Tooltip("Number of resonance nodes used by the puzzle.")]
    [Range(3, 5)]
    [SerializeField]
    private int nodeCount = 3;

    [Tooltip("Minimum Manhattan separation between puzzle nodes.")]
    [SerializeField]
    private int minimumNodeSeparation = 3;


    [Header("Sequence Timing")]

    [SerializeField]
    private float initialSequenceDelay = 0.4f;

    [SerializeField]
    private float nodeFlashDuration = 0.55f;

    [SerializeField]
    private float gapBetweenFlashes = 0.18f;

    [SerializeField]
    private float failureReplayDelay = 0.75f;


    [Header("Node Appearance")]

    [SerializeField]
    private float nodeScale = 0.48f;

    [SerializeField]
    private Color idleColour =
        new Color(
            0.18f,
            0.45f,
            0.55f,
            1f
        );

    [SerializeField]
    private Color flashColour =
        new Color(
            1f,
            0.85f,
            0.20f,
            1f
        );

    [SerializeField]
    private Color correctColour =
        new Color(
            0.25f,
            1f,
            0.45f,
            1f
        );

    [SerializeField]
    private Color failureColour =
        new Color(
            1f,
            0.20f,
            0.20f,
            1f
        );


    [Header("Reward")]

    [SerializeField]
    private int pulseReward = 1;

    [SerializeField]
    private int shaperReward = 1;


    private class PuzzleNode
    {
        public int Index;

        public Vector2Int Cell;

        public GameObject Object;

        public Renderer Renderer;

        public Material Material;
    }


    private readonly List<PuzzleNode> nodes =
        new List<PuzzleNode>();


    private readonly List<int> sequence =
        new List<int>();


    private Room puzzleRoom;

    private GameObject puzzleParent;


    private int observedGenerationVersion =
        -1;


    private int currentSequencePosition;


    private bool puzzleSolved;

    private bool showingSequence;

    private bool acceptingInput;

    private bool playerWasInsideRoom;


    private Vector2Int previousPlayerCell;

    private bool previousPlayerCellRecorded;


    private Coroutine activeSequenceCoroutine;


    private void Update()
    {
        if (dungeonGenerator == null ||
            dungeonGenerator.Grid == null ||
            playerController == null)
        {
            return;
        }


        /*
         * A new procedural floor needs a fresh puzzle.
         */
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
            RecordPlayerCell();

            return;
        }


        Vector2Int playerCell =
            playerController.GridPosition;


        bool playerInside =
            IsPlayerInsidePuzzleRoom(
                playerCell
            );


        /*
         * Entering the chamber shows the sequence.
         *
         * If the player leaves without solving it, entering again will
         * replay the sequence.
         */
        if (playerInside &&
            !playerWasInsideRoom &&
            !showingSequence)
        {
            BeginSequenceDisplay();
        }


        if (!playerInside &&
            playerWasInsideRoom)
        {
            acceptingInput =
                false;


            currentSequencePosition =
                0;
        }


        /*
         * A node only activates when the player actually ENTERS its
         * cell, rather than triggering repeatedly while standing on it.
         */
        if (playerInside &&
            acceptingInput &&
            previousPlayerCellRecorded &&
            playerCell !=
                previousPlayerCell)
        {
            CheckNodeEntry(
                playerCell
            );
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
                "RESONANCE PUZZLE - No semantic Puzzle room was " +
                "available on this floor."
            );


            return;
        }


        puzzleParent =
            new GameObject(
                "Generated Resonance Puzzle"
            );


        System.Random random =
            new System.Random(
                unchecked(
                    dungeonGenerator.CurrentSeed *
                        2281 +
                    6700417
                )
            );


        List<Vector2Int> nodeCells =
            ChooseNodeCells(
                puzzleRoom,
                random
            );


        if (nodeCells.Count <
            nodeCount)
        {
            UnityEngine.Debug.LogWarning(
                "RESONANCE PUZZLE - Could not place enough nodes."
            );


            ClearPuzzle();

            return;
        }


        for (int i = 0;
             i < nodeCells.Count;
             i++)
        {
            CreateNode(
                i,
                nodeCells[i]
            );
        }


        BuildSequence(
            random
        );


        currentSequencePosition =
            0;


        puzzleSolved =
            false;

        showingSequence =
            false;

        acceptingInput =
            false;

        playerWasInsideRoom =
            false;

        previousPlayerCellRecorded =
            false;


        UnityEngine.Debug.Log(
            "========== RESONANCE PUZZLE ==========\n" +
            $"Seed: {dungeonGenerator.CurrentSeed}\n" +
            $"Room centre: {puzzleRoom.Centre}\n" +
            $"Nodes: {nodes.Count}\n" +
            $"Sequence: {BuildSequenceDebugString()}\n" +
            "======================================"
        );
    }


    private Room FindPuzzleRoom()
    {
        if (dungeonGenerator.Rooms == null)
            return null;


        foreach (Room room in
                 dungeonGenerator.Rooms)
        {
            if (room != null &&
                room.Role ==
                    RoomRole.Puzzle)
            {
                return room;
            }
        }


        return null;
    }


    // ============================================================
    // NODE PLACEMENT
    // ============================================================

    private List<Vector2Int> ChooseNodeCells(
        Room room,
        System.Random random)
    {
        List<Vector2Int> selected =
            new List<Vector2Int>();


        List<Vector2Int> candidates =
            new List<Vector2Int>();


        /*
         * Keep nodes away from the immediate outer edge of the
         * original room when possible.
         */
        for (int x =
                 room.Bounds.xMin + 1;
             x <
                 room.Bounds.xMax - 1;
             x++)
        {
            for (int y =
                     room.Bounds.yMin + 1;
                 y <
                     room.Bounds.yMax - 1;
                 y++)
            {
                Vector2Int cell =
                    new Vector2Int(
                        x,
                        y
                    );


                if (!dungeonGenerator.Grid.IsWalkable(
                        cell))
                {
                    continue;
                }


                if (IsObjectiveCell(cell))
                {
                    continue;
                }


                candidates.Add(
                    cell
                );
            }
        }


        /*
         * Deterministic Fisher-Yates shuffle.
         */
        for (int i =
                 candidates.Count - 1;
             i > 0;
             i--)
        {
            int swapIndex =
                random.Next(
                    0,
                    i + 1
                );


            Vector2Int temporary =
                candidates[i];


            candidates[i] =
                candidates[swapIndex];


            candidates[swapIndex] =
                temporary;
        }


        foreach (Vector2Int candidate in
                 candidates)
        {
            bool sufficientlySeparated =
                true;


            foreach (Vector2Int existing in
                     selected)
            {
                int distance =
                    Mathf.Abs(
                        candidate.x -
                        existing.x
                    ) +
                    Mathf.Abs(
                        candidate.y -
                        existing.y
                    );


                if (distance <
                    minimumNodeSeparation)
                {
                    sufficientlySeparated =
                        false;

                    break;
                }
            }


            if (!sufficientlySeparated)
                continue;


            selected.Add(
                candidate
            );


            if (selected.Count >=
                nodeCount)
            {
                break;
            }
        }


        return selected;
    }

    private bool IsObjectiveCell(
    Vector2Int cell)
    {
        if (dungeonGenerator.ObjectiveManager ==
            null)
        {
            return false;
        }


        foreach (Vector2Int objectiveCell in
                 dungeonGenerator.ObjectiveManager
                     .ObjectiveCells)
        {
            if (objectiveCell ==
                cell)
            {
                return true;
            }
        }


        return false;
    }


    private void CreateNode(
        int index,
        Vector2Int cell)
    {
        GameObject nodeObject =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );


        nodeObject.name =
            $"Resonance Node {index + 1}";


        nodeObject.transform.SetParent(
            puzzleParent.transform
        );


        nodeObject.transform.position =
            new Vector3(
                cell.x + 0.5f,
                cell.y + 0.5f,
                -2.12f
            );


        nodeObject.transform.localScale =
            new Vector3(
                nodeScale,
                nodeScale,
                1f
            );


        Collider collider =
            nodeObject.GetComponent<Collider>();


        if (collider != null)
        {
            Destroy(
                collider
            );
        }


        Renderer renderer =
            nodeObject.GetComponent<Renderer>();


        Material material =
            null;


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


        if (renderer != null &&
            shader != null)
        {
            material =
                new Material(
                    shader
                );


            material.color =
                idleColour;


            renderer.sharedMaterial =
                material;
        }


        PuzzleNode node =
            new PuzzleNode();


        node.Index =
            index;

        node.Cell =
            cell;

        node.Object =
            nodeObject;

        node.Renderer =
            renderer;

        node.Material =
            material;


        nodes.Add(
            node
        );
    }


    // ============================================================
    // PROCEDURAL SEQUENCE
    // ============================================================

    private void BuildSequence(
        System.Random random)
    {
        sequence.Clear();


        for (int i = 0;
             i < nodes.Count;
             i++)
        {
            sequence.Add(
                i
            );
        }


        /*
         * Shuffle the node order using the dungeon-derived random
         * stream so replaying the same seed reproduces the puzzle.
         */
        for (int i =
                 sequence.Count - 1;
             i > 0;
             i--)
        {
            int swapIndex =
                random.Next(
                    0,
                    i + 1
                );


            int temporary =
                sequence[i];


            sequence[i] =
                sequence[swapIndex];


            sequence[swapIndex] =
                temporary;
        }
    }


    private string BuildSequenceDebugString()
    {
        if (sequence.Count == 0)
            return "-";


        string result =
            "";


        for (int i = 0;
             i < sequence.Count;
             i++)
        {
            if (i > 0)
            {
                result +=
                    " -> ";
            }


            result +=
                (sequence[i] + 1)
                    .ToString();
        }


        return result;
    }


    // ============================================================
    // SEQUENCE PRESENTATION
    // ============================================================

    private void BeginSequenceDisplay()
    {
        acceptingInput =
            false;


        currentSequencePosition =
            0;


        if (activeSequenceCoroutine !=
            null)
        {
            StopCoroutine(
                activeSequenceCoroutine
            );
        }


        activeSequenceCoroutine =
            StartCoroutine(
                ShowSequence()
            );
    }


    private IEnumerator ShowSequence()
    {
        showingSequence =
            true;


        SetAllNodeColours(
            idleColour
        );


        yield return new WaitForSeconds(
            initialSequenceDelay
        );


        for (int i = 0;
             i < sequence.Count;
             i++)
        {
            PuzzleNode node =
                nodes[
                    sequence[i]
                ];


            SetNodeColour(
                node,
                flashColour
            );


            yield return new WaitForSeconds(
                nodeFlashDuration
            );


            SetNodeColour(
                node,
                idleColour
            );


            yield return new WaitForSeconds(
                gapBetweenFlashes
            );
        }


        currentSequencePosition =
            0;


        acceptingInput =
            true;


        showingSequence =
            false;


        previousPlayerCell =
            playerController.GridPosition;


        previousPlayerCellRecorded =
            true;


        UnityEngine.Debug.Log(
            "RESONANCE PUZZLE - Awaiting player sequence."
        );
    }


    // ============================================================
    // PLAYER INPUT
    // ============================================================

    private void CheckNodeEntry(
        Vector2Int playerCell)
    {
        PuzzleNode enteredNode =
            FindNodeAtCell(
                playerCell
            );


        if (enteredNode == null)
        {
            return;
        }


        int expectedNodeIndex =
            sequence[
                currentSequencePosition
            ];


        if (enteredNode.Index !=
            expectedNodeIndex)
        {
            HandleIncorrectNode(
                enteredNode
            );


            return;
        }


        SetNodeColour(
            enteredNode,
            correctColour
        );


        currentSequencePosition++;


        UnityEngine.Debug.Log(
            $"RESONANCE CORRECT - " +
            $"{currentSequencePosition}/{sequence.Count}"
        );


        if (currentSequencePosition >=
            sequence.Count)
        {
            CompletePuzzle();
        }
    }


    private void HandleIncorrectNode(
        PuzzleNode incorrectNode)
    {
        acceptingInput =
            false;


        currentSequencePosition =
            0;


        UnityEngine.Debug.Log(
            "RESONANCE INCORRECT - Sequence reset."
        );


        StartCoroutine(
            FailureSequence(
                incorrectNode
            )
        );
    }


    private IEnumerator FailureSequence(
        PuzzleNode incorrectNode)
    {
        SetAllNodeColours(
            failureColour
        );


        yield return new WaitForSeconds(
            failureReplayDelay
        );


        SetAllNodeColours(
            idleColour
        );


        yield return new WaitForSeconds(
            0.25f
        );


        BeginSequenceDisplay();
    }


    // ============================================================
    // SUCCESS
    // ============================================================

    private void CompletePuzzle()
    {
        puzzleSolved =
            true;


        acceptingInput =
            false;


        SetAllNodeColours(
            correctColour
        );


        UnityEngine.Debug.Log(
            "========== RESONANCE PUZZLE COMPLETE =========="
        );


        SpawnRewards();
    }


    private void SpawnRewards()
    {
        List<Vector2Int> rewardCells =
            FindRewardCells();


        int rewardIndex =
            0;


        if (pulseReward > 0 &&
            rewardIndex <
                rewardCells.Count)
        {
            CreateReward(
                ResourcePickup.ResourceType.Pulse,
                pulseReward,
                rewardCells[
                    rewardIndex
                ],
                new Color(
                    0.20f,
                    0.75f,
                    1f,
                    1f
                )
            );


            rewardIndex++;
        }


        if (shaperReward > 0 &&
            rewardIndex <
                rewardCells.Count)
        {
            CreateReward(
                ResourcePickup.ResourceType.Shaper,
                shaperReward,
                rewardCells[
                    rewardIndex
                ],
                new Color(
                    1f,
                    0.35f,
                    0.85f,
                    1f
                )
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
            Vector2Int.zero,
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.down
        };


        foreach (Vector2Int offset in
                 offsets)
        {
            Vector2Int cell =
                centre +
                offset;


            if (!dungeonGenerator.Grid.IsWalkable(
                    cell))
            {
                continue;
            }


            if (FindNodeAtCell(
                    cell) != null)
            {
                continue;
            }


            result.Add(
                cell
            );


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
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );


        reward.name =
            type ==
                ResourcePickup.ResourceType.Pulse
                ? "Puzzle Reward - Pulse"
                : "Puzzle Reward - Shaper";


        reward.transform.SetParent(
            puzzleParent.transform
        );


        ResourcePickup pickup =
            reward.AddComponent<ResourcePickup>();


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


    // ============================================================
    // HELPERS
    // ============================================================

    private bool IsPlayerInsidePuzzleRoom(
        Vector2Int cell)
    {
        /*
         * First test the original logical room.
         */
        if (puzzleRoom.Contains(
                cell))
        {
            return true;
        }


        /*
         * CA post-processing can grow the physical room outside its
         * original rectangle.
         *
         * For this first puzzle version the interaction starts once the
         * player reaches the logical room core. The surrounding organic
         * shape remains normal traversable space.
         */
        return false;
    }


    private PuzzleNode FindNodeAtCell(
        Vector2Int cell)
    {
        foreach (PuzzleNode node in
                 nodes)
        {
            if (node.Cell ==
                cell)
            {
                return node;
            }
        }


        return null;
    }


    private void SetAllNodeColours(
        Color colour)
    {
        foreach (PuzzleNode node in
                 nodes)
        {
            SetNodeColour(
                node,
                colour
            );
        }
    }


    private void SetNodeColour(
        PuzzleNode node,
        Color colour)
    {
        if (node != null &&
            node.Material != null)
        {
            node.Material.color =
                colour;
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


    private void ClearPuzzle()
    {
        if (activeSequenceCoroutine !=
            null)
        {
            StopCoroutine(
                activeSequenceCoroutine
            );


            activeSequenceCoroutine =
                null;
        }


        foreach (PuzzleNode node in
                 nodes)
        {
            if (node.Material != null)
            {
                Destroy(
                    node.Material
                );
            }
        }


        nodes.Clear();

        sequence.Clear();


        if (puzzleParent != null)
        {
            Destroy(
                puzzleParent
            );


            puzzleParent =
                null;
        }


        puzzleRoom =
            null;


        puzzleSolved =
            false;

        showingSequence =
            false;

        acceptingInput =
            false;

        playerWasInsideRoom =
            false;

        previousPlayerCellRecorded =
            false;
    }
}