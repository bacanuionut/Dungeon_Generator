using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles visual sprite animation for grid-based actors without changing
/// their gameplay movement, AI, collision or pathfinding.
///
/// CraftPix actor sheets are read as 32x32 frames. Directional idle and
/// walking sequences are selected separately; left and right share the side
/// rows and use SpriteRenderer.flipX.
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
            "Full CraftPix character/enemy PNG used for idle, movement and action animation."
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
        public Sprite[] IdleDown;
        public Sprite[] IdleSide;
        public Sprite[] IdleUp;

        public Sprite[] WalkDown;
        public Sprite[] WalkSide;
        public Sprite[] WalkUp;

        public Sprite[] ImpactDown;
        public Sprite[] ImpactSide;
        public Sprite[] ImpactUp;

        public Sprite[] AttackDown;
        public Sprite[] AttackSide;
        public Sprite[] AttackUp;
    }


    private enum OneShotVisualAction
    {
        None,
        Hurt,
        Attack
    }


    private static readonly Dictionary<string, RuntimeFrames> frameCache =
        new Dictionary<string, RuntimeFrames>();


    [Header("Animation")]

    [Tooltip("Idle animation speed in frames per second.")]
    [Min(1f)]
    [SerializeField]
    private float idleFramesPerSecond = 3f;

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


    [Header("Action Animation")]

    [Tooltip("Playback speed for the enemy attack frames.")]
    [Min(1f)]
    [SerializeField]
    private float attackFramesPerSecond = 10f;

    [Tooltip("Playback speed for the player hurt frames.")]
    [Min(1f)]
    [SerializeField]
    private float hurtFramesPerSecond = 10f;

    [Tooltip("Playback speed for the enemy stun fall animation.")]
    [Min(1f)]
    [SerializeField]
    private float stunFramesPerSecond = 6f;


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

    private WardenController wardenController;


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

    private OneShotVisualAction oneShotAction =
        OneShotVisualAction.None;

    private float actionFrameDuration;

    private float actionEndTime;

    private float nextActionFrameTime;

    private int actionFrame;

    private bool stunAnimationActive;

    private float nextStunFrameTime;

    private int stunFrame;

    private bool tutorialStunPreviewActive;

    private bool controllerEventsSubscribed;

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

        wardenController =
            GetComponent<WardenController>();

        oldRootRenderer =
            GetComponent<Renderer>();
    }


    private void OnEnable()
    {
        SubscribeControllerEvents();
    }


    private void OnDisable()
    {
        UnsubscribeControllerEvents();
    }


    private void SubscribeControllerEvents()
    {
        if (controllerEventsSubscribed)
            return;

        if (playerController != null)
        {
            playerController.Damaged +=
                HandlePlayerDamaged;
        }

        if (enemyController != null)
        {
            enemyController.AttackPerformed +=
                HandleEnemyAttackPerformed;
        }

        if (wardenController != null)
        {
            wardenController.AttackPerformed +=
                HandleWardenAttackPerformed;
        }

        controllerEventsSubscribed =
            true;
    }


    private void UnsubscribeControllerEvents()
    {
        if (!controllerEventsSubscribed)
            return;

        if (playerController != null)
        {
            playerController.Damaged -=
                HandlePlayerDamaged;
        }

        if (enemyController != null)
        {
            enemyController.AttackPerformed -=
                HandleEnemyAttackPerformed;
        }

        if (wardenController != null)
        {
            wardenController.AttackPerformed -=
                HandleWardenAttackPerformed;
        }

        controllerEventsSubscribed =
            false;
    }


    private void HandlePlayerDamaged(
        int damage)
    {
        if (!configured || damage <= 0)
            return;

        StartOneShotAction(
            OneShotVisualAction.Hurt,
            hurtFramesPerSecond
        );
    }


    private void HandleEnemyAttackPerformed()
    {
        if (!configured)
            return;

        StartOneShotAction(
            OneShotVisualAction.Attack,
            attackFramesPerSecond
        );
    }


    private void HandleWardenAttackPerformed()
    {
        if (!configured)
            return;

        StartOneShotAction(
            OneShotVisualAction.Attack,
            attackFramesPerSecond
        );
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


        oneShotAction =
            OneShotVisualAction.None;


        actionFrame =
            0;


        actionEndTime =
            0f;


        nextActionFrameTime =
            0f;


        stunAnimationActive =
            false;


        stunFrame =
            0;


        nextStunFrameTime =
            0f;


        tutorialStunPreviewActive =
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


    /// <summary>
    /// Starts the visual stun sequence without changing the enemy AI state.
    /// </summary>
    public void BeginTutorialStunPreview()
    {
        if (!configured)
            return;

        tutorialStunPreviewActive =
            true;

        ResetStunAnimation();

        SetFacingFromExistingController();
    }


    /// <summary>
    /// Ends a tutorial-only stun sequence and restores normal animation.
    /// </summary>
    public void EndTutorialStunPreview()
    {
        if (!tutorialStunPreviewActive)
            return;

        tutorialStunPreviewActive =
            false;

        ResetStunAnimation();

        ApplyCurrentSprite(
            false
        );

        UpdateStateTint();
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

            return;
        }

        if (wardenController != null)
        {
            SetFacing(
                wardenController.FacingDirection
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


        bool enemyStunned =
            tutorialStunPreviewActive ||
            (enemyController != null &&
             enemyController.CurrentState ==
                EnemyController.EnemyState.Stunned) ||
            (wardenController != null &&
             wardenController.IsStunned);


        if (enemyStunned)
        {
            UpdateStunAnimation(
                now
            );
        }
        else
        {
            ResetStunAnimation();

            if (IsOneShotActionActive(
                    now))
            {
                UpdateOneShotAction(
                    now
                );
            }
            else
            {
                UpdateAnimationFrame(
                    moving,
                    now
                );
            }
        }


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
                GetAnimationFrameDuration(
                    true
                );
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
        else if (enemyController != null ||
                 wardenController != null)
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


    private void StartOneShotAction(
        OneShotVisualAction action,
        float framesPerSecond)
    {
        Sprite[] actionFrames =
            GetFramesForAction(
                action
            );

        if (actionFrames == null ||
            actionFrames.Length == 0)
        {
            return;
        }

        SetFacingFromExistingController();

        oneShotAction =
            action;

        actionFrame =
            0;

        actionFrameDuration =
            1f /
            Mathf.Max(
                1f,
                framesPerSecond
            );

        float now =
            Time.time;

        actionEndTime =
            now +
            actionFrameDuration *
            actionFrames.Length;

        nextActionFrameTime =
            now +
            actionFrameDuration;

        ApplyActionSprite(
            actionFrames,
            actionFrame
        );
    }


    private bool IsOneShotActionActive(
        float now)
    {
        if (oneShotAction ==
            OneShotVisualAction.None)
        {
            return false;
        }

        if (now < actionEndTime)
        {
            return true;
        }

        oneShotAction =
            OneShotVisualAction.None;

        actionFrame =
            0;

        nextActionFrameTime =
            0f;

        return false;
    }


    private void UpdateOneShotAction(
        float now)
    {
        Sprite[] actionFrames =
            GetFramesForAction(
                oneShotAction
            );

        if (actionFrames == null ||
            actionFrames.Length == 0)
        {
            oneShotAction =
                OneShotVisualAction.None;

            return;
        }

        while (now >=
               nextActionFrameTime &&
               actionFrame <
                   actionFrames.Length - 1)
        {
            actionFrame++;

            nextActionFrameTime +=
                actionFrameDuration;
        }

        ApplyActionSprite(
            actionFrames,
            actionFrame
        );
    }


    private void UpdateStunAnimation(
        float now)
    {
        Sprite[] stunFrames =
            GetImpactFramesForFacing();

        if (stunFrames == null ||
            stunFrames.Length == 0)
        {
            return;
        }

        float frameDuration =
            1f /
            Mathf.Max(
                1f,
                stunFramesPerSecond
            );

        if (!stunAnimationActive)
        {
            stunAnimationActive =
                true;

            stunFrame =
                0;

            nextStunFrameTime =
                now +
                frameDuration;
        }

        while (now >=
               nextStunFrameTime &&
               stunFrame <
                   stunFrames.Length - 1)
        {
            stunFrame++;

            nextStunFrameTime +=
                frameDuration;
        }

        ApplyActionSprite(
            stunFrames,
            stunFrame
        );
    }


    private void ResetStunAnimation()
    {
        if (!stunAnimationActive)
            return;

        stunAnimationActive =
            false;

        stunFrame =
            0;

        nextStunFrameTime =
            0f;
    }


    private Sprite[] GetFramesForAction(
        OneShotVisualAction action)
    {
        if (action ==
            OneShotVisualAction.Hurt)
        {
            return GetImpactFramesForFacing();
        }

        if (action ==
            OneShotVisualAction.Attack)
        {
            return GetAttackFramesForFacing();
        }

        return null;
    }


    private Sprite[] GetImpactFramesForFacing()
    {
        if (facingDirection ==
            Vector2Int.up)
        {
            return frames.ImpactUp;
        }

        if (facingDirection ==
                Vector2Int.left ||
            facingDirection ==
                Vector2Int.right)
        {
            return frames.ImpactSide;
        }

        return frames.ImpactDown;
    }


    private Sprite[] GetAttackFramesForFacing()
    {
        if (facingDirection ==
            Vector2Int.up)
        {
            return frames.AttackUp;
        }

        if (facingDirection ==
                Vector2Int.left ||
            facingDirection ==
                Vector2Int.right)
        {
            return frames.AttackSide;
        }

        return frames.AttackDown;
    }


    private void ApplyActionSprite(
        Sprite[] actionFrames,
        int frameIndex)
    {
        if (actionFrames == null ||
            actionFrames.Length == 0 ||
            spriteRenderer == null)
        {
            return;
        }

        int index =
            Mathf.Clamp(
                frameIndex,
                0,
                actionFrames.Length - 1
            );

        if (actionFrames[index] != null)
        {
            spriteRenderer.sprite =
                actionFrames[index];
        }

        ApplyHorizontalFlip();
    }


    private void UpdateAnimationFrame(
        bool moving,
        float now)
    {
        if (moving !=
            wasMovingLastFrame)
        {
            animationFrame =
                0;

            nextFrameTime =
                0f;
        }


        Sprite[] activeFrames =
            moving
                ? GetWalkFramesForFacing()
                : GetIdleFramesForFacing();


        int frameCount =
            activeFrames != null
                ? activeFrames.Length
                : 0;


        if (frameCount <= 0)
        {
            return;
        }


        float frameDuration =
            GetAnimationFrameDuration(
                moving
            );


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
            moving
        );
    }


    private float GetAnimationFrameDuration(
        bool moving)
    {
        float framesPerSecond =
            moving
                ? walkFramesPerSecond
                : idleFramesPerSecond;


        return
            1f /
            Mathf.Max(
                1f,
                framesPerSecond
            );
    }


    private void ApplyCurrentSprite(
        bool moving)
    {
        Sprite[] activeFrames =
            moving
                ? GetWalkFramesForFacing()
                : GetIdleFramesForFacing();


        if (activeFrames == null ||
            activeFrames.Length == 0)
        {
            return;
        }


        int index =
            Mathf.Clamp(
                animationFrame,
                0,
                activeFrames.Length - 1
            );


        if (activeFrames[index] != null)
        {
            spriteRenderer.sprite =
                activeFrames[index];
        }


        ApplyHorizontalFlip();
    }


    private void ApplyHorizontalFlip()
    {
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


    private Sprite[] GetIdleFramesForFacing()
    {
        if (facingDirection ==
            Vector2Int.up)
        {
            return frames.IdleUp;
        }


        if (facingDirection ==
                Vector2Int.left ||
            facingDirection ==
                Vector2Int.right)
        {
            return frames.IdleSide;
        }


        return frames.IdleDown;
    }


    private Sprite[] GetWalkFramesForFacing()
    {
        if (facingDirection ==
            Vector2Int.up)
        {
            return frames.WalkUp;
        }


        if (facingDirection ==
                Vector2Int.left ||
            facingDirection ==
                Vector2Int.right)
        {
            return frames.WalkSide;
        }


        return frames.WalkDown;
    }


    private void UpdateStateTint()
    {
        if (tutorialStunPreviewActive ||
            (enemyController != null &&
             enemyController.CurrentState ==
                EnemyController.EnemyState.Stunned) ||
            (wardenController != null &&
             wardenController.IsStunned))
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
            spriteSet.pixelsPerUnit +
            "|idle-walk-layout-v2";


        RuntimeFrames cached;


        if (frameCache.TryGetValue(
                cacheKey,
                out cached))
        {
            return cached;
        }


        RuntimeFrames created =
            new RuntimeFrames();


        // Rows are counted from the top of the CraftPix sheet.
        created.IdleDown =
            CreateAnimationRow(
                spriteSet,
                0,
                4,
                "IdleDown"
            );

        created.IdleUp =
            CreateAnimationRow(
                spriteSet,
                1,
                4,
                "IdleUp"
            );

        created.IdleSide =
            CreateAnimationRow(
                spriteSet,
                2,
                4,
                "IdleSide"
            );


        created.WalkDown =
            CreateAnimationRow(
                spriteSet,
                6,
                6,
                "WalkDown"
            );

        created.WalkUp =
            CreateAnimationRow(
                spriteSet,
                7,
                6,
                "WalkUp"
            );

        created.WalkSide =
            CreateAnimationRow(
                spriteSet,
                8,
                6,
                "WalkSide"
            );


        created.ImpactDown =
            CreateAnimationRow(
                spriteSet,
                9,
                4,
                "ImpactDown"
            );

        created.ImpactUp =
            CreateAnimationRow(
                spriteSet,
                10,
                4,
                "ImpactUp"
            );

        created.ImpactSide =
            CreateAnimationRow(
                spriteSet,
                11,
                4,
                "ImpactSide"
            );


        created.AttackDown =
            CreateAnimationRow(
                spriteSet,
                12,
                4,
                "AttackDown"
            );

        created.AttackUp =
            CreateAnimationRow(
                spriteSet,
                13,
                4,
                "AttackUp"
            );

        created.AttackSide =
            CreateAnimationRow(
                spriteSet,
                14,
                4,
                "AttackSide"
            );


        frameCache[cacheKey] =
            created;


        return created;
    }


    /// <summary>
    /// Extracts a sequence of 32x32 frames from one animation row.
    /// Sprite.Create uses bottom-left texture coordinates, so the source row
    /// number is inverted when calculating its texture Y coordinate.
    /// </summary>
    private static Sprite[] CreateAnimationRow(
        ActorSpriteSet spriteSet,
        int rowFromTop,
        int frameCount,
        string directionName)
    {
        const int frameWidth =
            32;


        const int frameHeight =
            32;


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
