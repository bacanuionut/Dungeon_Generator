using System.Collections;
using UnityEngine;

/// <summary>
/// Controls the visual and proximity behaviour of the procedural floor exit.
/// Collecting the required keys makes the hatch eligible to open, but it
/// remains closed until the player approaches within the configured range.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ExitHatchController : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private PlayerController playerController;


    [Header("Hatch Sprites")]

    [SerializeField]
    private Sprite closedSprite;

    [Tooltip("Opening frames in order, excluding the closed frame.")]
    [SerializeField]
    private Sprite[] openingFrames;


    [Header("Hatch Shaft")]

    [SerializeField]
    private Color shaftColour =
        new Color(0.02f, 0.02f, 0.04f, 1f);

    [SerializeField]
    private float shaftSize = 0.82f;

    [Tooltip("Vertical offset of the dark opening relative to the hatch.")]
    [SerializeField]
    private float shaftYOffset = -0.45f;

    [Tooltip("Local Z offset used to keep the shaft behind the hatch sprite.")]
    [SerializeField]
    private float shaftZOffset = 0.10f;


    [Header("Animation")]

    [Min(0.02f)]
    [SerializeField]
    private float frameDuration = 0.10f;


    [Header("Proximity Activation")]

    [Tooltip(
        "Maximum Manhattan grid distance from the hatch at which it can " +
        "open or show its locked message.")]
    [Min(0)]
    [SerializeField]
    private int activationRangeTiles = 2;

    [TextArea(2, 4)]
    [SerializeField]
    private string lockedMessage =
        "The descent hatch is sealed. Collect all keys to open it.";


    private SpriteRenderer spriteRenderer;
    private SpriteRenderer shaftRenderer;
    private Coroutine openingRoutine;

    private static Sprite whitePixelSprite;

    private bool objectiveComplete;
    private bool isOpen;
    private bool playerWasInRange;

    private int collectedSigils;
    private int requiredSigils;
    private int lastLoggedCollectedSigils = -1;

    private bool hasProximityMessage;
    private string currentProximityMessage = string.Empty;


    public bool IsObjectiveComplete => objectiveComplete;
    public bool IsOpen => isOpen;
    public bool IsPlayerInRange => playerWasInRange;

    public int ActivationRangeTiles => activationRangeTiles;

    public bool HasProximityMessage => hasProximityMessage;
    public string CurrentProximityMessage => currentProximityMessage;


    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        EnsureShaftRenderer();
        SetShaftVisible(false);

        ResetForNewFloor();
    }


    private void Update()
    {
        if (playerController == null || isOpen)
        {
            return;
        }

        bool inRange =
            IsWithinActivationRange(
                playerController.GridPosition
            );

        if (!inRange)
        {
            if (playerWasInRange)
            {
                ClearProximityMessage();
                lastLoggedCollectedSigils = -1;
            }

            playerWasInRange = false;
            return;
        }

        playerWasInRange = true;

        if (objectiveComplete)
        {
            ClearProximityMessage();
            BeginOpeningIfNeeded();
            return;
        }

        ShowLockedProximityMessage();
    }


    private void EnsureShaftRenderer()
    {
        if (shaftRenderer != null)
        {
            UpdateShaftTransform();
            return;
        }

        Transform existing =
            transform.Find("Exit Shaft");

        if (existing != null)
        {
            shaftRenderer =
                existing.GetComponent<SpriteRenderer>();
        }

        if (shaftRenderer == null)
        {
            GameObject shaft =
                new GameObject("Exit Shaft");

            shaft.transform.SetParent(
                transform,
                false
            );

            shaftRenderer =
                shaft.AddComponent<SpriteRenderer>();
        }

        shaftRenderer.gameObject.layer =
            gameObject.layer;

        shaftRenderer.sprite =
            GetWhitePixelSprite();

        shaftRenderer.color =
            shaftColour;

        if (spriteRenderer != null)
        {
            shaftRenderer.sortingLayerID =
                spriteRenderer.sortingLayerID;

            // The shaft uses the same sorting order as the hatch and sits
            // slightly farther from the camera. This keeps it above the floor
            // while still allowing the hatch sprite to render over it.
            shaftRenderer.sortingOrder =
                spriteRenderer.sortingOrder;
        }

        UpdateShaftTransform();
    }


    private void UpdateShaftTransform()
    {
        if (shaftRenderer == null)
        {
            return;
        }

        shaftRenderer.transform.localPosition =
            new Vector3(
                0f,
                shaftYOffset,
                shaftZOffset
            );

        shaftRenderer.transform.localScale =
            new Vector3(
                shaftSize,
                shaftSize,
                1f
            );
    }


    private void SetShaftVisible(bool visible)
    {
        EnsureShaftRenderer();

        if (shaftRenderer != null)
        {
            shaftRenderer.gameObject.SetActive(visible);
            shaftRenderer.enabled = visible;

            if (visible)
            {
                UpdateShaftTransform();

                UnityEngine.Debug.Log(
                    "EXIT SHAFT VISIBLE - " +
                    $"world position {shaftRenderer.transform.position}, " +
                    $"layer {shaftRenderer.gameObject.layer}, " +
                    $"sorting order {shaftRenderer.sortingOrder}."
                );
            }
        }
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

        whitePixelSprite.name =
            "Exit Shaft Pixel";

        return whitePixelSprite;
    }


    /// <summary>
    /// Restores the hatch to its closed state for a newly generated floor.
    /// </summary>
    public void ResetForNewFloor()
    {
        if (openingRoutine != null)
        {
            StopCoroutine(openingRoutine);
            openingRoutine = null;
        }

        objectiveComplete = false;
        isOpen = false;
        playerWasInRange = false;

        collectedSigils = 0;
        requiredSigils = 0;
        lastLoggedCollectedSigils = -1;

        ClearProximityMessage();
        SetShaftVisible(false);

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null && closedSprite != null)
        {
            spriteRenderer.sprite = closedSprite;
        }
    }


    /// <summary>
    /// Receives the current floor objective progress.
    /// </summary>
    public void SetObjectiveProgress(
        int collected,
        int required)
    {
        collectedSigils =
            Mathf.Max(
                0,
                collected
            );

        requiredSigils =
            Mathf.Max(
                0,
                required
            );

        objectiveComplete =
            requiredSigils > 0 &&
            collectedSigils >= requiredSigils;

        if (playerWasInRange && !objectiveComplete)
        {
            ShowLockedProximityMessage();
        }
    }


    /// <summary>
    /// Compatibility method for systems that only provide the completed state.
    /// </summary>
    public void SetUnlocked(bool unlocked)
    {
        objectiveComplete = unlocked;

        if (!unlocked)
        {
            ResetForNewFloor();
        }
    }


    private bool IsWithinActivationRange(
        Vector2Int playerCell)
    {
        Vector2Int hatchCell =
            GetHatchGridPosition();

        int distance =
            Mathf.Abs(
                playerCell.x - hatchCell.x
            ) +
            Mathf.Abs(
                playerCell.y - hatchCell.y
            );

        return distance <=
               Mathf.Max(
                   0,
                   activationRangeTiles
               );
    }


    private Vector2Int GetHatchGridPosition()
    {
        return new Vector2Int(
            Mathf.FloorToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y)
        );
    }


    private void ShowLockedProximityMessage()
    {
        string progressText =
            requiredSigils > 0
                ? $" {collectedSigils}/{requiredSigils} collected."
                : string.Empty;

        currentProximityMessage =
            lockedMessage +
            progressText;

        hasProximityMessage = true;

        if (lastLoggedCollectedSigils == collectedSigils)
        {
            return;
        }

        lastLoggedCollectedSigils =
            collectedSigils;

        UnityEngine.Debug.Log(
            "DESCENT HATCH MESSAGE - " +
            currentProximityMessage
        );
    }


    private void ClearProximityMessage()
    {
        hasProximityMessage = false;
        currentProximityMessage = string.Empty;
    }


    private void BeginOpeningIfNeeded()
    {
        if (isOpen || openingRoutine != null)
        {
            return;
        }

        openingRoutine =
            StartCoroutine(
                PlayOpeningSequence()
            );
    }


    private IEnumerator PlayOpeningSequence()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            openingRoutine = null;
            yield break;
        }

        SetShaftVisible(true);

        if (openingFrames == null ||
            openingFrames.Length == 0)
        {
            isOpen = true;
            openingRoutine = null;
            yield break;
        }

        WaitForSeconds wait =
            new WaitForSeconds(
                Mathf.Max(
                    0.02f,
                    frameDuration
                )
            );

        for (int i = 0;
             i < openingFrames.Length;
             i++)
        {
            if (openingFrames[i] == null)
            {
                continue;
            }

            spriteRenderer.sprite =
                openingFrames[i];

            yield return wait;
        }

        isOpen = true;
        openingRoutine = null;

        UnityEngine.Debug.Log(
            "EXIT HATCH OPENED - " +
            "all keys collected and player entered " +
            $"the {activationRangeTiles}-tile activation range."
        );
    }
}
