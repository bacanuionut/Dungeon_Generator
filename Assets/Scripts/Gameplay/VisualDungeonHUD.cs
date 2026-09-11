using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

/// <summary>
/// Image-driven in-game HUD.
///
/// Permanent information:
/// - current hearts (lost hearts disappear);
/// - Pulse / Digger / coin counts;
/// - collected keys and hatch state;
/// - Warden warning block.
///
/// Context prompts / tutorial hints can be layered on separately.
/// </summary>
public class VisualDungeonHUD : MonoBehaviour
{
    [Header("Gameplay References")]

    [SerializeField]
    private RunStatsManager runStatsManager;

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private PlayerPulseController pulseController;

    [SerializeField]
    private PlayerShaperController shaperController;

    [SerializeField]
    private WardenManager wardenManager;

    [SerializeField]
    private ExitHatchController exitHatchController;

    [SerializeField]
    private ResonancePuzzleManager resonancePuzzleManager;

    [SerializeField]
    private GameplayTutorialController gameplayTutorialController;


    [Header("Health")]

    [Tooltip(
        "One Image per possible heart. Lost hearts are hidden completely."
    )]
    [SerializeField]
    private UnityEngine.UI.Image[] heartImages;


    [Header("Resources")]

    [SerializeField]
    private UnityEngine.UI.Text pulseCountText;

    [SerializeField]
    private UnityEngine.UI.Text diggerCountText;

    [SerializeField]
    private UnityEngine.UI.Text coinCountText;

    [SerializeField]
    private UnityEngine.UI.Text pulseKeyText;

    [SerializeField]
    private UnityEngine.UI.Text diggerKeyText;

    [SerializeField]
    private string pulseKeyLabel =
        "Q";

    [SerializeField]
    private string diggerKeyLabel =
        "F";


    [Header("Keys / Objective")]

    [SerializeField]
    private UnityEngine.UI.Image[] keyImages;

    [Range(0f, 1f)]
    [SerializeField]
    private float uncollectedKeyAlpha =
        0.18f;

    [SerializeField]
    private UnityEngine.UI.Text keyCountText;

    [SerializeField]
    private UnityEngine.UI.Text exitStatusText;


    [Header("Context Prompt")]

    [SerializeField]
    private GameObject interactionPromptRoot;

    [SerializeField]
    private GameObject interactionKeycapRoot;

    [SerializeField]
    private UnityEngine.UI.Text interactionKeyText;

    [SerializeField]
    private UnityEngine.UI.Text interactionPromptText;

    [Tooltip(
        "RectTransform on the Interaction Prompt root. " +
        "If left empty it is resolved automatically."
    )]
    [SerializeField]
    private RectTransform interactionPromptRect;

    [Tooltip(
        "Layout Element on the Interaction Text child. " +
        "This is what limits long messages so they wrap."
    )]
    [SerializeField]
    private LayoutElement interactionPromptTextLayout;

    [Tooltip(
        "Camera used to convert generated world targets to HUD positions. " +
        "If left empty Camera.main is used."
    )]
    [SerializeField]
    private Camera promptWorldCamera;

    [Tooltip("Maximum width of the text portion before it wraps.")]
    [Min(120f)]
    [SerializeField]
    private float maximumPromptTextWidth =
        420f;

    [Tooltip(
        "Screen-space gap kept between a visible world target and the nearest edge of the prompt."
    )]
    [Min(0f)]
    [SerializeField]
    private float worldPromptTargetGap =
        18f;

    [Tooltip(
        "Distance kept between a clamped world prompt and the screen edge."
    )]
    [Min(0f)]
    [SerializeField]
    private float promptScreenEdgePadding =
        24f;

    [Tooltip(
        "HUD RectTransforms which a world-attached prompt must never cover. " +
        "Assign Player Status Panel, Warden Warning, Objective Panel and Progress Timeline."
    )]
    [SerializeField]
    private RectTransform[] promptBlockingUiRects;

    [Tooltip(
        "Extra space kept between the contextual prompt and permanent HUD panels."
    )]
    [Min(0f)]
    [SerializeField]
    private float promptUiClearance =
        12f;


    [Header("Warden Warning")]

    [Tooltip(
        "The Warden block remains visible throughout gameplay."
    )]
    [SerializeField]
    private GameObject wardenWarningRoot;

    [SerializeField]
    private UnityEngine.UI.Text wardenWarningText;

    [Tooltip(
        "Shows the current number of floors behind under the main Warden status."
    )]
    [SerializeField]
    private bool showWardenDistance =
        true;

    [SerializeField]
    private UnityEngine.UI.Text wardenDistanceText;


    [Header("Refresh")]

    [Min(0.02f)]
    [SerializeField]
    private float refreshInterval =
        0.08f;


    private float nextRefreshTime;

    private bool currentPromptUsesWorldPosition;

    private Vector3 currentPromptWorldPosition;

    private Vector2 defaultPromptAnchoredPosition;

    private bool defaultPromptPositionRecorded;

    private string previousPromptMessage =
        string.Empty;

    private string previousPromptKey =
        string.Empty;

    private bool previousPromptKeycapVisible;

    private string temporaryPromptMessage =
        string.Empty;

    private float temporaryPromptEndTime;


    private void Start()
    {
        ResolveReferences();

        SubscribeToResourceEvents();

        ResolvePromptUiReferences();

        RecordDefaultPromptPosition();

        if (pulseKeyText != null)
        {
            pulseKeyText.text =
                pulseController != null
                    ? pulseController.DeployKey.ToString()
                    : pulseKeyLabel;
        }

        if (diggerKeyText != null)
        {
            diggerKeyText.text =
                shaperController != null
                    ? shaperController.DiggerKey.ToString()
                    : diggerKeyLabel;
        }

        RefreshAll();
    }


    private void Update()
    {
        if (Time.unscaledTime <
            nextRefreshTime)
        {
            return;
        }

        nextRefreshTime =
            Time.unscaledTime +
            refreshInterval;

        RefreshAll();
    }


    private void LateUpdate()
    {
        /*
         * Data can still refresh at the normal HUD interval, but a prompt
         * attached to a world target needs to follow the moving camera
         * every rendered frame.
         */
        if (currentPromptUsesWorldPosition &&
            interactionPromptRoot != null &&
            interactionPromptRoot.activeSelf)
        {
            PositionPromptAtWorldTarget(
                currentPromptWorldPosition
            );
        }
    }


    private void RefreshAll()
    {
        ResolveDynamicReferences();

        RefreshHearts();

        RefreshResources();

        RefreshKeysAndExit();

        RefreshInteractionPrompt();

        RefreshWardenWarning();
    }


    private void RefreshHearts()
    {
        if (heartImages == null ||
            playerController == null)
        {
            return;
        }

        int currentHealth =
            Mathf.Max(
                0,
                playerController.CurrentHealth
            );

        for (int i = 0;
             i < heartImages.Length;
             i++)
        {
            if (heartImages[i] == null)
                continue;

            /*
             * No empty-heart state.
             * Lost hearts simply disappear.
             */
            heartImages[i].gameObject.SetActive(
                i < currentHealth
            );
        }
    }


    private void RefreshResources()
    {
        if (pulseCountText != null)
        {
            pulseCountText.text =
                pulseController != null
                    ? pulseController
                        .RemainingCharges
                        .ToString()
                    : "-";
        }

        if (diggerCountText != null)
        {
            diggerCountText.text =
                shaperController != null
                    ? shaperController
                        .RemainingCharges
                        .ToString()
                    : "-";
        }

        if (coinCountText != null)
        {
            coinCountText.text =
                runStatsManager != null
                    ? runStatsManager
                        .CoinsCollected
                        .ToString()
                    : "-";
        }
    }


    private void RefreshKeysAndExit()
    {
        FloorObjectiveManager objectiveManager =
            dungeonGenerator != null
                ? dungeonGenerator.ObjectiveManager
                : null;

        int collected =
            objectiveManager != null
                ? objectiveManager.CollectedSigils
                : 0;

        int required =
            objectiveManager != null
                ? objectiveManager.RequiredSigils
                : 0;


        if (keyImages != null)
        {
            for (int i = 0;
                 i < keyImages.Length;
                 i++)
            {
                UnityEngine.UI.Image image =
                    keyImages[i];

                if (image == null)
                    continue;

                bool slotExists =
                    i < required;

                image.gameObject.SetActive(
                    slotExists
                );

                if (!slotExists)
                    continue;

                Color colour =
                    image.color;

                colour.a =
                    i < collected
                        ? 1f
                        : uncollectedKeyAlpha;

                image.color =
                    colour;
            }
        }


        if (keyCountText != null)
        {
            keyCountText.text =
                required > 0
                    ? $"{collected}/{required}"
                    : "-";
        }


        if (exitStatusText != null)
        {
            if (exitHatchController != null &&
                exitHatchController.IsOpen)
            {
                exitStatusText.text =
                    "DESCENT OPEN";
            }
            else if (objectiveManager != null &&
                     objectiveManager.ExitUnlocked)
            {
                exitStatusText.text =
                    "HATCH READY";
            }
            else
            {
                exitStatusText.text =
                    "HATCH SEALED";
            }
        }
    }


    private void RefreshInteractionPrompt()
    {
        if (interactionPromptRoot == null)
            return;


        /*
         * Tutorial prompts have first priority while a tutorial is active.
         * They use the same small prompt presentation rather than a second
         * large HUD panel.
         */
        if (gameplayTutorialController != null &&
            gameplayTutorialController.HasPrompt)
        {
            ShowInteractionPrompt(
                gameplayTutorialController.PromptText,
                gameplayTutorialController.PromptUsesKeycap,
                gameplayTutorialController.PromptKeyLabel,
                gameplayTutorialController.PromptHasWorldTarget,
                gameplayTutorialController.PromptWorldPosition
            );

            return;
        }


        if (!string.IsNullOrEmpty(temporaryPromptMessage))
        {
            if (Time.unscaledTime <
                temporaryPromptEndTime)
            {
                ShowInteractionPrompt(
                    temporaryPromptMessage,
                    false,
                    string.Empty,
                    false,
                    Vector3.zero
                );

                return;
            }

            temporaryPromptMessage =
                string.Empty;
        }

        /*
         * Normal Resonance interaction represents actual lever input.
         * The replay-panel explanation is handled by the tutorial controller.
         */
        if (resonancePuzzleManager != null &&
            resonancePuzzleManager.HasInteractionMessage)
        {
            ShowInteractionPrompt(
                resonancePuzzleManager.CurrentInteractionMessage,
                resonancePuzzleManager.CurrentInteractionUsesKeycap,
                resonancePuzzleManager.CurrentInteractionKeyLabel,
                resonancePuzzleManager.HasInteractionWorldPosition,
                resonancePuzzleManager.CurrentInteractionWorldPosition
            );

            return;
        }


        if (exitHatchController != null &&
            exitHatchController.HasProximityMessage)
        {
            ShowInteractionPrompt(
                exitHatchController.CurrentProximityMessage,
                false,
                string.Empty,
                false,
                Vector3.zero
            );

            return;
        }


        HideInteractionPrompt();
    }


    public void ShowTemporaryMessage(
        string message,
        float duration = 1.75f)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        temporaryPromptMessage =
            message;

        temporaryPromptEndTime =
            Time.unscaledTime +
            Mathf.Max(0.1f, duration);
    }


    private void ShowInteractionPrompt(
        string message,
        bool showKeycap,
        string keyLabel,
        bool useWorldPosition,
        Vector3 worldPosition)
    {
        interactionPromptRoot.SetActive(
            true
        );


        if (interactionKeycapRoot != null)
        {
            interactionKeycapRoot.SetActive(
                showKeycap
            );
        }


        if (interactionKeyText != null &&
            showKeycap)
        {
            interactionKeyText.text =
                keyLabel;
        }


        bool contentChanged =
            previousPromptMessage != message ||
            previousPromptKeycapVisible != showKeycap ||
            previousPromptKey != keyLabel;


        if (interactionPromptText != null)
        {
            interactionPromptText.text =
                message;

            interactionPromptText.horizontalOverflow =
                HorizontalWrapMode.Wrap;

            interactionPromptText.verticalOverflow =
                VerticalWrapMode.Overflow;
        }


        previousPromptMessage =
            message;

        previousPromptKeycapVisible =
            showKeycap;

        previousPromptKey =
            keyLabel;


        if (contentChanged)
        {
            RebuildPromptLayout();
        }


        currentPromptUsesWorldPosition =
            useWorldPosition;

        currentPromptWorldPosition =
            worldPosition;


        if (useWorldPosition)
        {
            PositionPromptAtWorldTarget(
                worldPosition
            );
        }
        else
        {
            RestoreDefaultPromptPosition();
        }
    }


    private void HideInteractionPrompt()
    {
        currentPromptUsesWorldPosition =
            false;

        interactionPromptRoot.SetActive(
            false
        );
    }


    private void RebuildPromptLayout()
    {
        ResolvePromptUiReferences();


        if (interactionPromptText == null ||
            interactionPromptRect == null)
        {
            return;
        }


        if (interactionPromptTextLayout != null)
        {
            /*
             * Let short messages use their natural width.
             * Cap longer messages so Unity Text wraps them to another line.
             */
            float naturalWidth =
                interactionPromptText.preferredWidth;

            interactionPromptTextLayout.preferredWidth =
                Mathf.Min(
                    naturalWidth,
                    maximumPromptTextWidth
                );

            interactionPromptTextLayout.preferredHeight =
                -1f;
        }


        Canvas.ForceUpdateCanvases();

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            interactionPromptRect
        );


        /*
         * After the width has been constrained, ask Text for the height
         * required by the wrapped lines and rebuild once more.
         */
        if (interactionPromptTextLayout != null)
        {
            interactionPromptTextLayout.preferredHeight =
                interactionPromptText.preferredHeight;
        }


        LayoutRebuilder.ForceRebuildLayoutImmediate(
            interactionPromptRect
        );
    }


    private void PositionPromptAtWorldTarget(
        Vector3 worldPosition)
    {
        ResolvePromptUiReferences();


        if (interactionPromptRect == null ||
            promptWorldCamera == null)
        {
            return;
        }


        RectTransform parentRect =
            interactionPromptRect.parent
                as RectTransform;


        if (parentRect == null)
        {
            return;
        }


        Vector3 screenPoint =
            promptWorldCamera.WorldToScreenPoint(
                worldPosition
            );


        Canvas canvas =
            interactionPromptRect.GetComponentInParent<Canvas>();

        Camera uiCamera =
            null;


        if (canvas != null &&
            canvas.renderMode !=
                RenderMode.ScreenSpaceOverlay)
        {
            uiCamera =
                canvas.worldCamera;
        }


        Vector2 targetLocalPoint;


        if (!RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    parentRect,
                    (Vector2)screenPoint,
                    uiCamera,
                    out targetLocalPoint
                ))
        {
            return;
        }


        float halfWidth =
            interactionPromptRect.rect.width *
            0.5f;

        float halfHeight =
            interactionPromptRect.rect.height *
            0.5f;


        float minimumX =
            parentRect.rect.xMin +
            halfWidth +
            promptScreenEdgePadding;

        float maximumX =
            parentRect.rect.xMax -
            halfWidth -
            promptScreenEdgePadding;

        float minimumY =
            parentRect.rect.yMin +
            halfHeight +
            promptScreenEdgePadding;

        float maximumY =
            parentRect.rect.yMax -
            halfHeight -
            promptScreenEdgePadding;


        bool targetVisibleOnScreen =
            screenPoint.z > 0f &&
            screenPoint.x >= 0f &&
            screenPoint.x <= Screen.width &&
            screenPoint.y >= 0f &&
            screenPoint.y <= Screen.height;


        Vector2 desiredPosition;


        if (targetVisibleOnScreen)
        {
            /*
             * When the actual target is visible, put the prompt diagonally
             * beside it rather than directly over it. This leaves the replay
             * panel / lever itself clear so the player never has to walk
             * through a large UI box to reach the interaction point.
             *
             * The preferred corner points toward the centre of the screen,
             * which normally gives the most room. If that corner is occupied
             * by permanent HUD, the other corners are tried automatically.
             */
            desiredPosition =
                ChooseVisibleTargetCorner(
                    targetLocalPoint,
                    parentRect,
                    minimumX,
                    maximumX,
                    minimumY,
                    maximumY
                );
        }
        else
        {
            /*
             * For an off-screen target, keep the existing sticky directional
             * behaviour. The target position is clamped to the screen edge.
             */
            desiredPosition =
                targetLocalPoint;


            if (minimumX <= maximumX)
            {
                desiredPosition.x =
                    Mathf.Clamp(
                        desiredPosition.x,
                        minimumX,
                        maximumX
                    );
            }


            if (minimumY <= maximumY)
            {
                desiredPosition.y =
                    Mathf.Clamp(
                        desiredPosition.y,
                        minimumY,
                        maximumY
                    );
            }
        }


        desiredPosition =
            AvoidBlockingUi(
                desiredPosition,
                parentRect,
                minimumX,
                maximumX,
                minimumY,
                maximumY
            );


        interactionPromptRect.anchoredPosition =
            desiredPosition;
    }


    private Vector2 ChooseVisibleTargetCorner(
        Vector2 targetLocalPoint,
        RectTransform parentRect,
        float minimumX,
        float maximumX,
        float minimumY,
        float maximumY)
    {
        float halfWidth =
            interactionPromptRect.rect.width *
            0.5f;

        float halfHeight =
            interactionPromptRect.rect.height *
            0.5f;


        float horizontalOffset =
            halfWidth +
            worldPromptTargetGap;

        float verticalOffset =
            halfHeight +
            worldPromptTargetGap;


        /*
         * Prefer the corner which points back toward screen centre.
         */
        float preferredXSign =
            targetLocalPoint.x <=
                parentRect.rect.center.x
                ? 1f
                : -1f;

        float preferredYSign =
            targetLocalPoint.y <=
                parentRect.rect.center.y
                ? 1f
                : -1f;


        Vector2[] candidates =
        {
            targetLocalPoint +
                new Vector2(
                    preferredXSign * horizontalOffset,
                    preferredYSign * verticalOffset
                ),

            targetLocalPoint +
                new Vector2(
                    -preferredXSign * horizontalOffset,
                    preferredYSign * verticalOffset
                ),

            targetLocalPoint +
                new Vector2(
                    preferredXSign * horizontalOffset,
                    -preferredYSign * verticalOffset
                ),

            targetLocalPoint +
                new Vector2(
                    -preferredXSign * horizontalOffset,
                    -preferredYSign * verticalOffset
                )
        };


        Vector2 bestFallback =
            candidates[0];

        float bestFallbackMovement =
            float.MaxValue;


        foreach (Vector2 rawCandidate in candidates)
        {
            Vector2 clampedCandidate =
                rawCandidate;


            if (minimumX <= maximumX)
            {
                clampedCandidate.x =
                    Mathf.Clamp(
                        clampedCandidate.x,
                        minimumX,
                        maximumX
                    );
            }


            if (minimumY <= maximumY)
            {
                clampedCandidate.y =
                    Mathf.Clamp(
                        clampedCandidate.y,
                        minimumY,
                        maximumY
                    );
            }


            Rect candidateRect =
                new Rect(
                    clampedCandidate.x - halfWidth,
                    clampedCandidate.y - halfHeight,
                    halfWidth * 2f,
                    halfHeight * 2f
                );


            bool candidateWasClamped =
                (clampedCandidate - rawCandidate)
                .sqrMagnitude > 0.01f;


            if (!candidateWasClamped &&
                !OverlapsAnyBlockingUi(
                    candidateRect,
                    parentRect))
            {
                return clampedCandidate;
            }


            float fallbackMovement =
                (clampedCandidate - rawCandidate)
                .sqrMagnitude;


            if (!OverlapsAnyBlockingUi(
                    candidateRect,
                    parentRect) &&
                fallbackMovement <
                    bestFallbackMovement)
            {
                bestFallbackMovement =
                    fallbackMovement;

                bestFallback =
                    clampedCandidate;
            }
        }


        /*
         * Rare cramped-screen fallback. AvoidBlockingUi() still gets a final
         * chance to move this away from permanent HUD after this method.
         */
        if (bestFallbackMovement <
            float.MaxValue)
        {
            return bestFallback;
        }


        Vector2 finalFallback =
            candidates[0];


        if (minimumX <= maximumX)
        {
            finalFallback.x =
                Mathf.Clamp(
                    finalFallback.x,
                    minimumX,
                    maximumX
                );
        }


        if (minimumY <= maximumY)
        {
            finalFallback.y =
                Mathf.Clamp(
                    finalFallback.y,
                    minimumY,
                    maximumY
                );
        }


        return finalFallback;
    }


    private Vector2 AvoidBlockingUi(
        Vector2 desiredPosition,
        RectTransform parentRect,
        float minimumX,
        float maximumX,
        float minimumY,
        float maximumY)
    {
        if (promptBlockingUiRects == null ||
            promptBlockingUiRects.Length == 0 ||
            interactionPromptRect == null)
        {
            return desiredPosition;
        }


        Vector2 resolvedPosition =
            desiredPosition;

        float halfWidth =
            interactionPromptRect.rect.width *
            0.5f;

        float halfHeight =
            interactionPromptRect.rect.height *
            0.5f;


        /*
         * A few passes are enough when two reserved HUD areas are close
         * together, for example the Warden block near the objective panel.
         */
        for (int pass = 0;
             pass < 4;
             pass++)
        {
            bool movedThisPass =
                false;


            foreach (RectTransform blockingRectTransform
                     in promptBlockingUiRects)
            {
                if (blockingRectTransform == null ||
                    !blockingRectTransform.gameObject.activeInHierarchy)
                {
                    continue;
                }


                Rect blockingRect =
                    GetRectInParentSpace(
                        blockingRectTransform,
                        parentRect
                    );


                blockingRect.xMin -=
                    promptUiClearance;

                blockingRect.xMax +=
                    promptUiClearance;

                blockingRect.yMin -=
                    promptUiClearance;

                blockingRect.yMax +=
                    promptUiClearance;


                Rect promptRect =
                    new Rect(
                        resolvedPosition.x - halfWidth,
                        resolvedPosition.y - halfHeight,
                        halfWidth * 2f,
                        halfHeight * 2f
                    );


                if (!promptRect.Overlaps(
                        blockingRect))
                {
                    continue;
                }


                Vector2[] candidates =
                {
                    new Vector2(
                        blockingRect.xMin - halfWidth,
                        resolvedPosition.y
                    ),

                    new Vector2(
                        blockingRect.xMax + halfWidth,
                        resolvedPosition.y
                    ),

                    new Vector2(
                        resolvedPosition.x,
                        blockingRect.yMin - halfHeight
                    ),

                    new Vector2(
                        resolvedPosition.x,
                        blockingRect.yMax + halfHeight
                    )
                };


                float bestDistance =
                    float.MaxValue;

                Vector2 bestPosition =
                    resolvedPosition;

                bool foundClearCandidate =
                    false;


                foreach (Vector2 rawCandidate
                         in candidates)
                {
                    Vector2 candidate =
                        new Vector2(
                            minimumX <= maximumX
                                ? Mathf.Clamp(
                                    rawCandidate.x,
                                    minimumX,
                                    maximumX
                                )
                                : rawCandidate.x,

                            minimumY <= maximumY
                                ? Mathf.Clamp(
                                    rawCandidate.y,
                                    minimumY,
                                    maximumY
                                )
                                : rawCandidate.y
                        );


                    Rect candidateRect =
                        new Rect(
                            candidate.x - halfWidth,
                            candidate.y - halfHeight,
                            halfWidth * 2f,
                            halfHeight * 2f
                        );


                    if (OverlapsAnyBlockingUi(
                            candidateRect,
                            parentRect))
                    {
                        continue;
                    }


                    float distance =
                        (candidate - desiredPosition)
                        .sqrMagnitude;


                    if (distance < bestDistance)
                    {
                        bestDistance =
                            distance;

                        bestPosition =
                            candidate;

                        foundClearCandidate =
                            true;
                    }
                }


                if (foundClearCandidate)
                {
                    resolvedPosition =
                        bestPosition;

                    movedThisPass =
                        true;
                }
            }


            if (!movedThisPass)
            {
                break;
            }
        }


        return resolvedPosition;
    }


    private bool OverlapsAnyBlockingUi(
        Rect promptRect,
        RectTransform parentRect)
    {
        if (promptBlockingUiRects == null)
        {
            return false;
        }


        foreach (RectTransform blockingRectTransform
                 in promptBlockingUiRects)
        {
            if (blockingRectTransform == null ||
                !blockingRectTransform.gameObject.activeInHierarchy)
            {
                continue;
            }


            Rect blockingRect =
                GetRectInParentSpace(
                    blockingRectTransform,
                    parentRect
                );


            blockingRect.xMin -=
                promptUiClearance;

            blockingRect.xMax +=
                promptUiClearance;

            blockingRect.yMin -=
                promptUiClearance;

            blockingRect.yMax +=
                promptUiClearance;


            if (promptRect.Overlaps(
                    blockingRect))
            {
                return true;
            }
        }


        return false;
    }


    private Rect GetRectInParentSpace(
        RectTransform sourceRect,
        RectTransform parentRect)
    {
        Vector3[] worldCorners =
            new Vector3[4];

        sourceRect.GetWorldCorners(
            worldCorners
        );


        Vector3 firstLocalCorner =
            parentRect.InverseTransformPoint(
                worldCorners[0]
            );


        float minimumX =
            firstLocalCorner.x;

        float maximumX =
            firstLocalCorner.x;

        float minimumY =
            firstLocalCorner.y;

        float maximumY =
            firstLocalCorner.y;


        for (int i = 1;
             i < worldCorners.Length;
             i++)
        {
            Vector3 localCorner =
                parentRect.InverseTransformPoint(
                    worldCorners[i]
                );


            minimumX =
                Mathf.Min(
                    minimumX,
                    localCorner.x
                );

            maximumX =
                Mathf.Max(
                    maximumX,
                    localCorner.x
                );

            minimumY =
                Mathf.Min(
                    minimumY,
                    localCorner.y
                );

            maximumY =
                Mathf.Max(
                    maximumY,
                    localCorner.y
                );
        }


        return Rect.MinMaxRect(
            minimumX,
            minimumY,
            maximumX,
            maximumY
        );
    }


    private void ResolvePromptUiReferences()
    {
        if (interactionPromptRect == null &&
            interactionPromptRoot != null)
        {
            interactionPromptRect =
                interactionPromptRoot
                    .GetComponent<RectTransform>();
        }


        if (interactionPromptTextLayout == null &&
            interactionPromptText != null)
        {
            interactionPromptTextLayout =
                interactionPromptText
                    .GetComponent<LayoutElement>();
        }


        if (promptWorldCamera == null)
        {
            promptWorldCamera =
                Camera.main;
        }
    }


    private void RecordDefaultPromptPosition()
    {
        if (defaultPromptPositionRecorded)
        {
            return;
        }


        ResolvePromptUiReferences();


        if (interactionPromptRect == null)
        {
            return;
        }


        defaultPromptAnchoredPosition =
            interactionPromptRect.anchoredPosition;

        defaultPromptPositionRecorded =
            true;
    }


    private void RestoreDefaultPromptPosition()
    {
        RecordDefaultPromptPosition();


        if (interactionPromptRect == null ||
            !defaultPromptPositionRecorded)
        {
            return;
        }


        interactionPromptRect.anchoredPosition =
            defaultPromptAnchoredPosition;
    }


    private void RefreshWardenWarning()
    {
        if (wardenWarningRoot == null)
            return;


        if (wardenManager == null)
        {
            wardenWarningRoot.SetActive(
                false
            );

            return;
        }


        /*
         * Keep the Warden block visible continuously.
         *
         * This fixes the previous behaviour where it disappeared
         * after changing floor if the Warden became more than
         * one floor behind.
         */
        wardenWarningRoot.SetActive(
            true
        );


        string mainMessage;


        if (wardenManager.IsPhysicalWardenPresent)
        {
            mainMessage =
                "WARDEN HERE";
        }
        else if (wardenManager.IsWardenArriving)
        {
            mainMessage =
                "WARDEN ARRIVING";
        }
        else if (wardenManager.FloorsBehind <= 1)
        {
            mainMessage =
                "WARDEN APPROACHING";
        }
        else
        {
            mainMessage =
                "WARDEN TRACKING";
        }


        if (wardenWarningText != null)
        {
            wardenWarningText.text =
                mainMessage;
        }


        if (wardenDistanceText != null)
        {
            wardenDistanceText.gameObject.SetActive(
                showWardenDistance
            );

            if (showWardenDistance)
            {
                if (wardenManager.IsPhysicalWardenPresent)
                {
                    wardenDistanceText.text =
                        "ON THIS FLOOR";
                }
                else
                {
                    int floorsBehind =
                        Mathf.Max(
                            0,
                            wardenManager.FloorsBehind
                        );

                    wardenDistanceText.text =
                        floorsBehind == 1
                            ? "1 FLOOR BEHIND"
                            : $"{floorsBehind} FLOORS BEHIND";
                }
            }
        }
    }


    private void SubscribeToResourceEvents()
    {
        if (shaperController != null)
        {
            shaperController.ChargesChanged -= HandleDiggerChargesChanged;
            shaperController.ChargesChanged += HandleDiggerChargesChanged;
        }
    }


    private void HandleDiggerChargesChanged(int charges)
    {
        if (diggerCountText != null)
        {
            diggerCountText.text =
                charges.ToString();
        }
    }


    private void OnDestroy()
    {
        if (shaperController != null)
        {
            shaperController.ChargesChanged -= HandleDiggerChargesChanged;
        }
    }


    private void ResolveDynamicReferences()
    {
        if (exitHatchController == null)
        {
            exitHatchController =
                FindObjectOfType<ExitHatchController>();
        }

        if (resonancePuzzleManager == null)
        {
            resonancePuzzleManager =
                FindObjectOfType<ResonancePuzzleManager>();
        }

        if (gameplayTutorialController == null)
        {
            gameplayTutorialController =
                FindObjectOfType<GameplayTutorialController>();
        }

        ResolvePromptUiReferences();
    }


    private void ResolveReferences()
    {
        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
        }

        if (dungeonGenerator == null)
        {
            dungeonGenerator =
                FindObjectOfType<DungeonGenerator>();
        }

        if (playerController == null)
        {
            playerController =
                FindObjectOfType<PlayerController>();
        }

        if (pulseController == null)
        {
            pulseController =
                FindObjectOfType<PlayerPulseController>();
        }

        if (shaperController == null)
        {
            shaperController =
                FindObjectOfType<PlayerShaperController>();
        }

        if (wardenManager == null)
        {
            wardenManager =
                FindObjectOfType<WardenManager>();
        }

        ResolveDynamicReferences();
    }
}