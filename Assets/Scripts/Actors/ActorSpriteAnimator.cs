using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles visual sprite animation for grid-based actors without changing
/// their gameplay movement, AI, collision or pathfinding.
///
/// CraftPix actor sheets are read as 32x32 frames. The first three rows
/// provide down, up and side walking animations; left and right share the
/// side row and use SpriteRenderer.flipX.
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
            "Final visual scale, independent of the actor root scale."
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

    [Tooltip("Walking animation speed in frames per second.")]
    [Min(1f)]
    [SerializeField]
    private float walkFramesPerSecond = 6.67f;

    [Tooltip(
        "How long the walking animation remains active after the latest " +
        "grid movement."
    )]
    [Min(0.05f)]
    [SerializeField]
    private float movementVisualHold = 0.18f;


    [Header("Visual Movement")]

    [Tooltip("Visual travel time used for the first player grid step.")]
    [Min(0.03f)]
    [SerializeField]
    private float playerInitialStepDuration = 0.10f;

    [Tooltip("Visual travel time used for the first normal-enemy grid step.")]
    [Min(0.03f)]
    [SerializeField]
    private float enemyInitialStepDuration = 0.28f;

    [Tooltip("Initial travel time for actors without a player or normal-enemy controller.")]
    [Min(0.03f)]
    [SerializeField]
    private float otherInitialStepDuration = 0.20f;

    [Tooltip(
        "After consecutive steps, visual travel uses this fraction of the " +
        "observed gameplay step interval."
    )]
    [Range(0.50f, 0.98f)]
    [SerializeField]
    private float stepCadenceFraction = 0.88f;

    [Tooltip("Lower limit for one visual grid-step interpolation.")]
    [Min(0.02f)]
    [SerializeField]
    private float minimumVisualStepDuration = 0.06f;

    [Tooltip("Upper limit for one visual grid-step interpolation.")]
    [Min(0.05f)]
    [SerializeField]
    private float maximumVisualStepDuration = 0.32f;

    [Tooltip(
        "A gap longer than this starts a new movement sequence instead of " +
        "being treated as the actor's normal movement cadence."
    )]
    [Min(0.1f)]
    [SerializeField]
    private float cadenceResetDelay = 0.75f;

    [Tooltip(
        "Movement larger than this is treated as a teleport or floor spawn " +
        "and is not visually interpolated."
    )]
    [Min(1f)]
    [SerializeField]
    private float maximumAnimatedStepDistance = 1.10f;


    [Header("Rendering")]

    [Tooltip(
        "Local Z offset of the sprite child. Negative keeps it in front of " +
        "the actor root in this project's top-down rendering."
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

    private Vector3 renderedWorldPosition;

    private Vector3 visualStepStartWorldPosition;

    private Vector3 visualStepTargetWorldPosition;

    private Vector2Int facingDirection =
        Vector2Int.down;

    private float movingUntilTime;

    private float nextFrameTime;

    private float lastMovementDetectedTime = -1f;

    private float activeVisualStepDuration;

    private int animationFrame;

    private bool visualStepInProgress;

    private float visualStepStartTime;

    private bool wasMovingLastFrame;

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


        // Only the root placeholder renderer is hidden. Child renderers can
        // belong to independent visual systems such as enemy vision cones.
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


        renderedWorldPosition =
            GetBaseVisualWorldPosition();


        visualStepStartWorldPosition =
            renderedWorldPosition;


        visualStepTargetWorldPosition =
            renderedWorldPosition;


        movingUntilTime =
            0f;


        lastMovementDetectedTime =
            -1f;


        activeVisualStepDuration =
            GetInitialVisualStepDuration();


        visualStepInProgress =
            false;


        animationFrame =
            0;


        wasMovingLastFrame =
            false;


        visualObject.transform.position =
            renderedWorldPosition;


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


        SetFacingFromExistingController();


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


    private void LateUpdate()
    {
        if (!configured ||
            frames == null ||
            visualObject == null ||
            spriteRenderer == null ||
            activeSet == null)
        {
            return;
        }


        float now =
            Time.time;


        // Advance active interpolation before checking for a new grid move.
        AdvanceVisualStep(now);


        DetectGridMovement(now);


        bool moving =
            now <
            movingUntilTime;


        UpdateAnimationFrame(
            moving,
            now
        );


        UpdateStateTint();


        AdvanceVisualStep(now);


        visualObject.transform.position =
            renderedWorldPosition;


        wasMovingLastFrame =
            moving;
    }


    private void DetectGridMovement(
        float now)
    {
        Vector3 current =
            transform.position;


        if (!previousPositionRecorded)
        {
            previousWorldPosition =
                current;

            previousPositionRecorded =
                true;

            renderedWorldPosition =
                GetBaseVisualWorldPosition();

            return;
        }


        Vector3 delta =
            current -
            previousWorldPosition;


        if (delta.sqrMagnitude <=
            0.0001f)
        {
            return;
        }


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


        Vector3 destination =
            GetBaseVisualWorldPosition();


        if (planarDistance >
            maximumAnimatedStepDistance)
        {
            renderedWorldPosition =
                destination;

            visualStepStartWorldPosition =
                destination;

            visualStepTargetWorldPosition =
                destination;

            visualStepInProgress =
                false;

            movingUntilTime =
                0f;

            lastMovementDetectedTime =
                -1f;

            animationFrame =
                0;

            nextFrameTime =
                0f;

            previousWorldPosition =
                current;

            return;
        }


        activeVisualStepDuration =
            CalculateVisualStepDuration(now);


        visualStepStartWorldPosition =
            renderedWorldPosition;


        visualStepTargetWorldPosition =
            destination;


        visualStepStartTime =
            now;


        visualStepInProgress =
            true;


        movingUntilTime =
            now +
            Mathf.Max(
                movementVisualHold,
                activeVisualStepDuration
            );


        if (!wasMovingLastFrame)
        {
            animationFrame =
                0;

            nextFrameTime =
                now +
                GetAnimationFrameDuration();
        }


        lastMovementDetectedTime =
            now;


        previousWorldPosition =
            current;
    }


    private float CalculateVisualStepDuration(
        float now)
    {
        float initialDuration =
            GetInitialVisualStepDuration();


        if (lastMovementDetectedTime <
            0f)
        {
            return initialDuration;
        }


        float observedInterval =
            now -
            lastMovementDetectedTime;


        if (observedInterval <= 0f ||
            observedInterval >
                cadenceResetDelay)
        {
            return initialDuration;
        }


        return
            Mathf.Clamp(
                observedInterval *
                    stepCadenceFraction,
                minimumVisualStepDuration,
                maximumVisualStepDuration
            );
    }


    private float GetInitialVisualStepDuration()
    {
        float duration;


        if (playerController != null)
        {
            duration =
                playerInitialStepDuration;
        }
        else if (enemyController != null)
        {
            duration =
                enemyInitialStepDuration;
        }
        else
        {
            duration =
                otherInitialStepDuration;
        }


        return
            Mathf.Clamp(
                duration,
                minimumVisualStepDuration,
                maximumVisualStepDuration
            );
    }


    private void AdvanceVisualStep(
        float now)
    {
        if (!visualStepInProgress)
        {
            return;
        }


        float progress =
            Mathf.Clamp01(
                (now -
                 visualStepStartTime) /
                Mathf.Max(
                    0.01f,
                    activeVisualStepDuration
                )
            );


        renderedWorldPosition =
            Vector3.Lerp(
                visualStepStartWorldPosition,
                visualStepTargetWorldPosition,
                progress
            );


        if (progress >= 1f)
        {
            renderedWorldPosition =
                visualStepTargetWorldPosition;

            visualStepInProgress =
                false;
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
        bool moving,
        float now)
    {
        if (!moving)
        {
            animationFrame =
                0;


            nextFrameTime =
                0f;


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


        float frameDuration =
            GetAnimationFrameDuration();


        if (nextFrameTime <= 0f)
        {
            nextFrameTime =
                now +
                frameDuration;
        }


        while (now >=
               nextFrameTime)
        {
            animationFrame =
                (animationFrame + 1) %
                frameCount;


            nextFrameTime +=
                frameDuration;
        }


        ApplyCurrentSprite(
            true
        );
    }


    private float GetAnimationFrameDuration()
    {
        return
            1f /
            Mathf.Max(
                1f,
                walkFramesPerSecond
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
    /// Compensates for scale inherited from the actor root so each sprite set
    /// keeps a predictable world-space size.
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


        // CraftPix row order: 0 = down/front, 1 = up/back, 2 = side.
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
