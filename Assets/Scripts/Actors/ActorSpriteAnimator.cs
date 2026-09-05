using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual-only sprite animation for grid-based actors.
///
/// IMPORTANT:
/// This script does not control movement, AI, collision, damage or pathfinding.
/// It watches the existing actor transform and, where available, the existing
/// PlayerController / EnemyController facing direction.
///
/// The CraftPix Character/Enemy sheets used by this project are arranged as
/// 32x32 actor frames even though the supplied TMX files describe them as
/// groups of 16x16 tiles.
///
/// The first three 32-pixel rows are:
/// Row 0 = down/front walk, 4 frames
/// Row 1 = up/back walk,    4 frames
/// Row 2 = side walk,       4 frames
///
/// Left/right share the side animation and use SpriteRenderer.flipX.
/// </summary>
[DisallowMultipleComponent]
public class ActorSpriteAnimator : MonoBehaviour
{
    [Serializable]
    public class ActorSpriteSet
    {
        [Tooltip("Friendly name shown in debug output.")]
        public string setName = "Actor";

        [Tooltip(
            "Full CraftPix character/enemy PNG. " +
            "The animator extracts the first three 32x32 movement rows."
        )]
        public Texture2D spriteSheet;

        [Tooltip("CraftPix dungeon art uses 16 pixels per world unit.")]
        [Min(1f)]
        public float pixelsPerUnit = 16f;

        [Tooltip(
            "True if the side-facing source row points right. " +
            "Untick this if the actor appears horizontally reversed."
        )]
        public bool sideFramesFaceRight = true;

        [Tooltip(
            "Final visual scale, independent of the old placeholder Quad scale."
        )]
        [Min(0.05f)]
        public float visualScale = 1f;

        [Tooltip(
            "Small local visual offset. With Bottom Centre pivot, (0,0) normally works."
        )]
        public Vector2 visualOffset = Vector2.zero;
    }


    private sealed class RuntimeFrames
    {
        public Sprite[] Down;
        public Sprite[] Side;
        public Sprite[] Up;
    }


    private static readonly Dictionary<string, RuntimeFrames> frameCache =
        new Dictionary<string, RuntimeFrames>();


    [Header("Animation")]

    [Tooltip(
        "TMX animations use 150 ms per frame, which is approximately 6.67 FPS."
    )]
    [Min(1f)]
    [SerializeField]
    private float walkFramesPerSecond = 6.67f;

    [Tooltip(
        "How long a grid step continues to look like movement after the " +
        "actor transform jumps to its next cell."
    )]
    [Min(0.05f)]
    [SerializeField]
    private float movementAnimationHold = 0.60f;

    [Tooltip(
        "Purely visual travel time between grid cells. Gameplay still moves " +
        "instantly on the authoritative grid; only the sprite glides between " +
        "the old and new cells."
    )]
    [Min(0.05f)]
    [SerializeField]
    private float visualStepDuration = 0.55f;

    [Tooltip(
        "Movement larger than this is treated as a teleport/floor spawn and " +
        "is not visually interpolated."
    )]
    [Min(1f)]
    [SerializeField]
    private float maximumAnimatedStepDistance = 1.10f;


    [Header("Rendering")]

    [Tooltip(
        "Local Z offset of the sprite child. Negative keeps it in front of " +
        "the old actor root in this project's top-down rendering."
    )]
    [SerializeField]
    private float visualZOffset = -0.30f;

    [Tooltip(
        "The gameplay root sits at the centre of a grid cell, while these " +
        "32x32 sprites use a bottom-centre pivot. Moving the sprite down by " +
        "half a tile places the actor's feet inside the cell it is actually " +
        "occupying instead of on the cell above."
    )]
    [SerializeField]
    private float feetAnchorYOffset = -0.50f;

    [Tooltip(
        "Normal enemies still show the existing blue Pulse-stun feedback."
    )]
    [SerializeField]
    private Color stunnedTint =
        new Color(
            0.45f,
            0.72f,
            1f,
            1f
        );


    private ActorSpriteSet activeSet;

    private RuntimeFrames frames;

    private GameObject visualObject;

    private SpriteRenderer spriteRenderer;

    private Renderer oldRootRenderer;

    private PlayerController playerController;

    private EnemyController enemyController;


    private Vector3 previousWorldPosition;

    private bool previousPositionRecorded;

    private Vector2Int facingDirection =
        Vector2Int.down;

    private float movingUntilTime;

    private float nextFrameTime;

    private int animationFrame;

    private bool visualStepInProgress;

    private float visualStepStartTime;

    private Vector3 visualStepStartOffset;

    private bool configured;


    public bool IsConfigured =>
        configured;

    public string ActiveSetName =>
        activeSet != null
            ? activeSet.setName
            : string.Empty;


    private void Awake()
    {
        playerController =
            GetComponent<PlayerController>();

        enemyController =
            GetComponent<EnemyController>();

        oldRootRenderer =
            GetComponent<Renderer>();
    }


    /// <summary>
    /// Applies one sprite sheet to this actor.
    /// Safe to call once after an actor is spawned.
    /// </summary>
    public void Initialise(
        ActorSpriteSet spriteSet)
    {
        if (spriteSet == null ||
            spriteSet.spriteSheet == null)
        {
            UnityEngine.Debug.LogWarning(
                $"ACTOR SPRITE - {name} could not initialise because its sprite sheet was not assigned."
            );

            return;
        }


        if (configured &&
            ReferenceEquals(
                activeSet,
                spriteSet))
        {
            return;
        }


        activeSet =
            spriteSet;


        frames =
            GetOrCreateFrames(
                spriteSet
            );


        EnsureVisualObject();


        /*
         * Hide only the actor root's old placeholder renderer.
         *
         * We deliberately do NOT disable renderers on child objects because
         * systems such as enemy vision cones may render separately.
         */
        if (oldRootRenderer != null &&
            oldRootRenderer !=
                spriteRenderer)
        {
            oldRootRenderer.enabled =
                false;
        }


        ApplyWorldIndependentVisualScale();


        configured =
            true;


        previousWorldPosition =
            transform.position;


        previousPositionRecorded =
            true;


        movingUntilTime =
            0f;


        visualStepInProgress =
            false;


        animationFrame =
            0;


        SetFacingFromExistingController();


        ApplyCurrentSprite(
            false
        );


        UnityEngine.Debug.Log(
            $"ACTOR SPRITE READY - {name} -> {spriteSet.setName}"
        );
    }


    /// <summary>
    /// Lets another gameplay system explicitly update facing without giving
    /// this visual component responsibility for movement.
    /// </summary>
    public void SetFacing(
        Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return;


        if (Mathf.Abs(direction.x) >
            Mathf.Abs(direction.y))
        {
            facingDirection =
                direction.x >= 0
                    ? Vector2Int.right
                    : Vector2Int.left;
        }
        else
        {
            facingDirection =
                direction.y >= 0
                    ? Vector2Int.up
                    : Vector2Int.down;
        }
    }


    private void Update()
    {
        if (!configured ||
            frames == null ||
            spriteRenderer == null)
        {
            return;
        }


        /*
         * PlayerController and EnemyController already expose the gameplay
         * facing direction used by the vision systems. Reusing it keeps sprite
         * orientation consistent with the torch/view cone.
         */
        SetFacingFromExistingController();


        UpdateMovementDetection();


        bool moving =
            Time.time <
            movingUntilTime;


        UpdateAnimationFrame(
            moving
        );


        UpdateStateTint();
    }


    private void SetFacingFromExistingController()
    {
        if (playerController != null)
        {
            SetFacing(
                playerController.FacingDirection
            );

            return;
        }


        if (enemyController != null)
        {
            SetFacing(
                enemyController.FacingDirection
            );
        }
    }


    private void UpdateMovementDetection()
    {
        Vector3 current =
            transform.position;


        if (!previousPositionRecorded)
        {
            previousWorldPosition =
                current;

            previousPositionRecorded =
                true;

            return;
        }


        Vector3 delta =
            current -
            previousWorldPosition;


        if (delta.sqrMagnitude >
            0.0001f)
        {
            Vector2Int movementDirection =
                Vector2Int.zero;


            if (Mathf.Abs(delta.x) >
                Mathf.Abs(delta.y))
            {
                movementDirection =
                    delta.x >= 0f
                        ? Vector2Int.right
                        : Vector2Int.left;
            }
            else if (Mathf.Abs(delta.y) >
                     0.0001f)
            {
                movementDirection =
                    delta.y >= 0f
                        ? Vector2Int.up
                        : Vector2Int.down;
            }


            if (movementDirection !=
                Vector2Int.zero)
            {
                SetFacing(
                    movementDirection
                );
            }


            float planarDistance =
                new Vector2(
                    delta.x,
                    delta.y
                ).magnitude;


            /*
             * Floor regeneration / spawning can move an actor many cells in
             * one frame. That should snap immediately rather than making the
             * sprite fly across the dungeon.
             */
            if (planarDistance <=
                maximumAnimatedStepDistance)
            {
                Vector3 destinationBase =
                    GetBaseVisualWorldPosition();


                /*
                 * The actor root has already jumped to the destination grid
                 * cell. Start the visual sprite from wherever it was rendered
                 * last frame, then glide it into the new root position.
                 */
                visualStepStartOffset =
                    visualObject.transform.position -
                    destinationBase;


                visualStepStartTime =
                    Time.time;


                visualStepInProgress =
                    true;


                movingUntilTime =
                    Time.time +
                    Mathf.Max(
                        movementAnimationHold,
                        visualStepDuration
                    );


                /*
                 * One grid movement now corresponds to one complete four-frame
                 * walk cycle, so every new cell begins again at frame 0.
                 */
                animationFrame =
                    0;


                nextFrameTime =
                    Time.time;
            }
            else
            {
                visualStepInProgress =
                    false;


                movingUntilTime =
                    0f;


                animationFrame =
                    0;
            }
        }


        previousWorldPosition =
            current;
    }


    private void LateUpdate()
    {
        if (!configured ||
            visualObject == null ||
            activeSet == null)
        {
            return;
        }


        Vector3 basePosition =
            GetBaseVisualWorldPosition();


        if (!visualStepInProgress)
        {
            visualObject.transform.position =
                basePosition;

            return;
        }


        float progress =
            Mathf.Clamp01(
                (Time.time -
                 visualStepStartTime) /
                Mathf.Max(
                    0.01f,
                    visualStepDuration
                )
            );


        /*
         * Use linear visual travel here. The gameplay remains grid based, but
         * a constant visual speed makes the one-cell walk much easier to read
         * than the previous eased movement, which could still feel like a
         * snap near the ends.
         */
        Vector3 offset =
            Vector3.Lerp(
                visualStepStartOffset,
                Vector3.zero,
                progress
            );


        visualObject.transform.position =
            basePosition +
            offset;


        if (progress >= 1f)
        {
            visualStepInProgress =
                false;


            visualObject.transform.position =
                basePosition;
        }
    }


    private Vector3 GetBaseVisualWorldPosition()
    {
        return
            transform.position +
            new Vector3(
                activeSet.visualOffset.x,
                activeSet.visualOffset.y +
                    feetAnchorYOffset,
                visualZOffset
            );
    }


    private void UpdateAnimationFrame(
        bool moving)
    {
        if (!moving)
        {
            animationFrame =
                0;


            ApplyCurrentSprite(
                false
            );

            return;
        }


        Sprite[] activeFrames =
            GetFramesForFacing();


        int frameCount =
            activeFrames != null
                ? activeFrames.Length
                : 0;


        if (frameCount <= 0)
        {
            return;
        }


        /*
         * IMPORTANT:
         *
         * Do not advance the walk animation using an independent FPS timer.
         *
         * This project moves actors one complete grid cell at a time. With
         * the old timer a 0.22-second visual step finished before a 4-frame
         * 6.67 FPS animation could get through all four frames, so only one
         * or two poses were visible.
         *
         * Instead, one grid step IS one complete walk cycle.
         *
         *   0% - 25%   -> frame 0
         *  25% - 50%   -> frame 1
         *  50% - 75%   -> frame 2
         *  75% - 100%  -> frame 3
         *
         * Therefore every successful single-cell movement uses every frame
         * regardless of frame rate.
         */
        float progress;


        if (visualStepInProgress)
        {
            progress =
                Mathf.Clamp01(
                    (Time.time -
                     visualStepStartTime) /
                    Mathf.Max(
                        0.01f,
                        visualStepDuration
                    )
                );
        }
        else
        {
            /*
             * A tiny tail after positional interpolation keeps the final walk
             * pose readable if Update and LateUpdate finish on neighbouring
             * frames.
             */
            float tailDuration =
                Mathf.Max(
                    0.01f,
                    movementAnimationHold -
                    visualStepDuration
                );


            progress =
                1f -
                Mathf.Clamp01(
                    (movingUntilTime -
                     Time.time) /
                    tailDuration
                );
        }


        animationFrame =
            Mathf.Clamp(
                Mathf.FloorToInt(
                    progress *
                    frameCount
                ),
                0,
                frameCount - 1
            );


        ApplyCurrentSprite(
            true
        );
    }


    private void ApplyCurrentSprite(
        bool moving)
    {
        Sprite[] activeFrames =
            GetFramesForFacing();


        if (activeFrames == null ||
            activeFrames.Length == 0)
        {
            return;
        }


        int index =
            moving
                ? Mathf.Clamp(
                    animationFrame,
                    0,
                    activeFrames.Length - 1
                )
                : 0;


        if (activeFrames[index] != null)
        {
            spriteRenderer.sprite =
                activeFrames[index];
        }


        bool facingLeft =
            facingDirection ==
            Vector2Int.left;


        bool facingRight =
            facingDirection ==
            Vector2Int.right;


        if (facingLeft ||
            facingRight)
        {
            bool sourceFacesRight =
                activeSet.sideFramesFaceRight;


            spriteRenderer.flipX =
                sourceFacesRight
                    ? facingLeft
                    : facingRight;
        }
        else
        {
            spriteRenderer.flipX =
                false;
        }
    }


    private Sprite[] GetFramesForFacing()
    {
        if (facingDirection ==
            Vector2Int.up)
        {
            return frames.Up;
        }


        if (facingDirection ==
                Vector2Int.left ||
            facingDirection ==
                Vector2Int.right)
        {
            return frames.Side;
        }


        return frames.Down;
    }


    private void UpdateStateTint()
    {
        if (enemyController != null &&
            enemyController.CurrentState ==
                EnemyController.EnemyState.Stunned)
        {
            spriteRenderer.color =
                stunnedTint;

            return;
        }


        spriteRenderer.color =
            Color.white;
    }


    private void EnsureVisualObject()
    {
        if (visualObject == null)
        {
            Transform existing =
                transform.Find(
                    "Actor Sprite Visual"
                );


            if (existing != null)
            {
                visualObject =
                    existing.gameObject;


                spriteRenderer =
                    existing.GetComponent<SpriteRenderer>();
            }
        }


        if (visualObject == null)
        {
            visualObject =
                new GameObject(
                    "Actor Sprite Visual"
                );


            visualObject.transform.SetParent(
                transform,
                false
            );
        }


        if (spriteRenderer == null)
        {
            spriteRenderer =
                visualObject.GetComponent<SpriteRenderer>();


            if (spriteRenderer == null)
            {
                spriteRenderer =
                    visualObject.AddComponent<SpriteRenderer>();
            }
        }


        visualObject.transform.position =
            GetBaseVisualWorldPosition();


        spriteRenderer.color =
            Color.white;
    }


    /// <summary>
    /// Cancels the old placeholder object's inherited scale so the sprite-set
    /// scale has a predictable meaning.
    ///
    /// Example: procedural enemies currently use a small Quad scale. Without
    /// this correction the 32x32 actor sprite would inherit that placeholder
    /// scale and become unnecessarily tiny.
    /// </summary>
    private void ApplyWorldIndependentVisualScale()
    {
        if (visualObject == null ||
            activeSet == null)
        {
            return;
        }


        Vector3 parentLossyScale =
            transform.lossyScale;


        float safeX =
            Mathf.Abs(parentLossyScale.x) >
                0.0001f
                ? Mathf.Abs(parentLossyScale.x)
                : 1f;


        float safeY =
            Mathf.Abs(parentLossyScale.y) >
                0.0001f
                ? Mathf.Abs(parentLossyScale.y)
                : 1f;


        visualObject.transform.localScale =
            new Vector3(
                activeSet.visualScale /
                    safeX,
                activeSet.visualScale /
                    safeY,
                1f
            );
    }


    private static RuntimeFrames GetOrCreateFrames(
        ActorSpriteSet spriteSet)
    {
        string cacheKey =
            spriteSet.spriteSheet.GetInstanceID() +
            "|" +
            spriteSet.pixelsPerUnit;


        RuntimeFrames cached;


        if (frameCache.TryGetValue(
                cacheKey,
                out cached))
        {
            return cached;
        }


        RuntimeFrames created =
            new RuntimeFrames();


        created.Down =
            CreateMovementRow(
                spriteSet,
                0,
                "Down"
            );


        /*
         * CraftPix row order:
         * 0 = down/front
         * 1 = up/back
         * 2 = side
         *
         * The previous version had rows 1 and 2 swapped, which is why an
         * actor showed its back while moving left or right.
         */
        created.Up =
            CreateMovementRow(
                spriteSet,
                1,
                "Up"
            );


        created.Side =
            CreateMovementRow(
                spriteSet,
                2,
                "Side"
            );


        frameCache[cacheKey] =
            created;


        return created;
    }


    /// <summary>
    /// Extracts the four useful movement frames from one 32-pixel row.
    ///
    /// Sprite.Create uses bottom-left texture coordinates, while the source
    /// sheet is described visually from top to bottom, so Y is inverted here.
    /// </summary>
    private static Sprite[] CreateMovementRow(
        ActorSpriteSet spriteSet,
        int rowFromTop,
        string directionName)
    {
        const int frameWidth =
            32;


        const int frameHeight =
            32;


        const int frameCount =
            4;


        Sprite[] result =
            new Sprite[
                frameCount
            ];


        Texture2D texture =
            spriteSet.spriteSheet;


        int y =
            texture.height -
            (rowFromTop + 1) *
            frameHeight;


        for (int frame = 0;
             frame < frameCount;
             frame++)
        {
            int x =
                frame *
                frameWidth;


            Rect rectangle =
                new Rect(
                    x,
                    y,
                    frameWidth,
                    frameHeight
                );


            Sprite sprite =
                Sprite.Create(
                    texture,
                    rectangle,
                    new Vector2(
                        0.5f,
                        0f
                    ),
                    spriteSet.pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect
                );


            sprite.name =
                spriteSet.setName +
                "_" +
                directionName +
                "_" +
                frame;


            result[frame] =
                sprite;
        }


        return result;
    }
}
