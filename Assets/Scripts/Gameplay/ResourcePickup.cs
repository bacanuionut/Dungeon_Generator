using UnityEngine;

/// <summary>
/// Collectible tactical resource used to replenish Pulse or Shaper charges.
///
/// Gameplay collection remains grid based. Visual presentation is handled
/// separately through a child SpriteRenderer and CollectibleVisualAnimator so
/// bobbing never changes the authoritative gameplay cell.
/// </summary>
public class ResourcePickup : MonoBehaviour
{
    public enum ResourceType
    {
        Pulse,
        Shaper
    }

    private ResourceType resourceType;
    private Vector2Int gridCell;
    private int amount;

    private PlayerController playerController;
    private PlayerPulseController pulseController;
    private PlayerShaperController shaperController;

    private GameObject visualObject;
    private bool collected;

    /// <summary>
    /// New sprite-based initialiser used by procedural resource generation and
    /// Resonance puzzle rewards.
    /// </summary>
    public void Initialise(
        ResourceType type,
        Vector2Int cell,
        int pickupAmount,
        PlayerController player,
        PlayerPulseController pulse,
        PlayerShaperController shaper,
        Sprite pickupSprite,
        float visualScale)
    {
        resourceType = type;
        gridCell = cell;
        amount = Mathf.Max(1, pickupAmount);

        playerController = player;
        pulseController = pulse;
        shaperController = shaper;

        transform.position =
            new Vector3(
                cell.x + 0.5f,
                cell.y + 0.5f,
                -2.15f
            );

        CreateSpriteVisual(
            pickupSprite,
            Mathf.Max(0.05f, visualScale)
        );
    }

    /// <summary>
    /// Backwards-compatible overload. This keeps any older caller compiling
    /// while the remaining project systems migrate to sprite visuals.
    /// </summary>
    public void Initialise(
        ResourceType type,
        Vector2Int cell,
        int pickupAmount,
        PlayerController player,
        PlayerPulseController pulse,
        PlayerShaperController shaper,
        Color colour)
    {
        resourceType = type;
        gridCell = cell;
        amount = Mathf.Max(1, pickupAmount);

        playerController = player;
        pulseController = pulse;
        shaperController = shaper;

        transform.position =
            new Vector3(
                cell.x + 0.5f,
                cell.y + 0.5f,
                -2.15f
            );

        CreateFallbackVisual(colour);
    }

    private void CreateSpriteVisual(
        Sprite pickupSprite,
        float visualScale)
    {
        ClearExistingVisual();

        visualObject =
            new GameObject("Resource Pickup Visual");

        visualObject.transform.SetParent(
            transform,
            false
        );

        visualObject.transform.localPosition =
            Vector3.zero;

        visualObject.transform.localScale =
            new Vector3(
                visualScale,
                visualScale,
                1f
            );

        SpriteRenderer renderer =
            visualObject.AddComponent<SpriteRenderer>();

        renderer.sprite = pickupSprite;
        renderer.color = Color.white;

        CollectibleVisualAnimator animator =
            gameObject.GetComponent<CollectibleVisualAnimator>();

        if (animator == null)
        {
            animator =
                gameObject.AddComponent<CollectibleVisualAnimator>();
        }

        animator.SetVisualTarget(
            visualObject.transform
        );

        animator.ConfigureAsPickup();
    }

    private void CreateFallbackVisual(
        Color colour)
    {
        ClearExistingVisual();

        visualObject =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );

        visualObject.name =
            "Resource Pickup Fallback Visual";

        visualObject.transform.SetParent(
            transform,
            false
        );

        visualObject.transform.localPosition =
            Vector3.zero;

        visualObject.transform.localScale =
            resourceType == ResourceType.Pulse
                ? new Vector3(0.30f, 0.30f, 1f)
                : new Vector3(0.34f, 0.34f, 1f);

        if (resourceType == ResourceType.Pulse)
        {
            visualObject.transform.localRotation =
                Quaternion.Euler(0f, 0f, 45f);
        }

        Collider collider =
            visualObject.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer =
            visualObject.GetComponent<Renderer>();

        Shader shader =
            Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader =
                Shader.Find("Unlit/Color");
        }

        if (renderer != null && shader != null)
        {
            Material material =
                new Material(shader);

            material.color = colour;
            renderer.material = material;
        }

        CollectibleVisualAnimator animator =
            gameObject.GetComponent<CollectibleVisualAnimator>();

        if (animator == null)
        {
            animator =
                gameObject.AddComponent<CollectibleVisualAnimator>();
        }

        animator.SetVisualTarget(
            visualObject.transform
        );

        animator.ConfigureAsPickup();
    }

    private void ClearExistingVisual()
    {
        if (visualObject != null)
        {
            Destroy(visualObject);
            visualObject = null;
        }

        /*
         * Defensive compatibility: an older caller may still create a Quad
         * before adding ResourcePickup. Hide/remove its collider and renderer
         * so the new sprite becomes the only visible collectible.
         */
        Collider rootCollider =
            GetComponent<Collider>();

        if (rootCollider != null)
        {
            Destroy(rootCollider);
        }

        Renderer rootRenderer =
            GetComponent<Renderer>();

        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }
    }

    private void Update()
    {
        if (collected ||
            playerController == null ||
            !playerController.IsAlive)
        {
            return;
        }

        if (playerController.GridPosition !=
            gridCell)
        {
            return;
        }

        TryCollect();
    }

    private void TryCollect()
    {
        int actuallyAdded = 0;

        if (resourceType == ResourceType.Pulse)
        {
            if (pulseController != null)
            {
                actuallyAdded =
                    pulseController.AddCharges(amount);
            }
        }
        else
        {
            if (shaperController != null)
            {
                actuallyAdded =
                    shaperController.AddCharges(amount);
            }
        }

        /*
         * If the relevant inventory is full, leave the pickup in place so the
         * player can return after spending a charge.
         */
        if (actuallyAdded <= 0)
        {
            return;
        }

        collected = true;

        UnityEngine.Debug.Log(
            $"RESOURCE PICKUP COLLECTED - " +
            $"{resourceType} +{actuallyAdded} at {gridCell}"
        );

        Destroy(gameObject);
    }
}
