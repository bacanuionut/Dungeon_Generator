using System.Collections;
using UnityEngine;

/// <summary>
/// Controls the visual and proximity behaviour of the procedural floor exit.
///
/// Collecting the required Anchor Sigils makes the hatch eligible to open,
/// but it remains visibly closed until the player approaches within the
/// configured tile range.
///
/// The proximity message is exposed through public properties so the same
/// state can later be displayed through the final GUI instead of the Console.
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
    private float shaftSize = 0.90f;

    [SerializeField]
    private float shaftYOffset = -0.45f;

    [SerializeField]
    private float shaftZOffset = 0.05f;

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

    [Tooltip(
        "Temporary Console text. Later the GUI can display " +
        "CurrentProximityMessage instead.")]
    [TextArea(2, 4)]
    [SerializeField]
    private string lockedMessage =
        "The descent hatch is sealed. Collect all keys to open it.";


    private SpriteRenderer shaftRenderer;

    private static Sprite whitePixelSprite;

    private SpriteRenderer spriteRenderer;
    private Coroutine openingRoutine;

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

        // Once the objective is complete, proximity opens the hatch.
        // Collecting the final Sigil somewhere else does NOT open it remotely.
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
            return;

        Transform existing =
            transform.Find("Exit Shaft");

        if (existing != null)
        {
            shaftRenderer =
                existing.GetComponent<SpriteRenderer>();

            if (shaftRenderer != null)
                return;
        }

        GameObject shaft =
            new GameObject("Exit Shaft");

        shaft.transform.SetParent(transform);
        shaft.transform.localPosition =
            new Vector3(
                0f,
                shaftYOffset,
                shaftZOffset
            );
        shaft.transform.localScale =
            new Vector3(shaftSize, shaftSize, 1f);

        shaftRenderer =
            shaft.AddComponent<SpriteRenderer>();

        shaftRenderer.sprite =
            GetWhitePixelSprite();

        shaftRenderer.color =
            shaftColour;

        if (spriteRenderer != null)
        {
            shaftRenderer.sortingLayerID =
                spriteRenderer.sortingLayerID;

            shaftRenderer.sortingOrder =
                spriteRenderer.sortingOrder;
        }
    }

    private void SetShaftVisible(bool visible)
    {
        if (shaftRenderer == null)
            return;

        shaftRenderer.enabled =
            visible;
    }

    private Sprite GetWhitePixelSprite()
    {
        if (whitePixelSprite != null)
            return whitePixelSprite;

        whitePixelSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );

        return whitePixelSprite;
    }

    /// <summary>
    /// Restores the hatch to the closed state for a newly generated floor.
    /// DungeonGenerator remains responsible for positioning the object.
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

        SetShaftVisible(false);

        ClearProximityMessage();

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
    /// Meeting the requirement does not itself open the hatch.
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

        // If the player is already standing beside the hatch when the final
        // Sigil is collected, Update() will open it on the next frame.
        if (playerWasInRange && !objectiveComplete)
        {
            ShowLockedProximityMessage();
        }
    }


    /// <summary>
    /// Compatibility method for systems that only know the boolean state.
    /// True means the objective requirement is complete, not that the hatch
    /// should open immediately from anywhere on the floor.
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
        // DungeonGenerator positions the hatch at cell centre + 0.5.
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

        // Do not spam the Console every frame. Log once when entering the
        // range and again only if objective progress changes while nearby.
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
            "Anchor Sigils complete and player entered " +
            $"the {activationRangeTiles}-tile activation range."
        );
    }
}
