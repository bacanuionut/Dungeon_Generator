using UnityEngine;

/// <summary>
/// A collectible gameplay-resource pickup.
///
/// Resource pickups are intentionally separate from treasure/items
/// because they directly replenish one of the player's limited
/// tactical abilities.
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


    private Material pickupMaterial;

    private bool collected;


    public void Initialise(
        ResourceType type,
        Vector2Int cell,
        int pickupAmount,
        PlayerController player,
        PlayerPulseController pulse,
        PlayerShaperController shaper,
        Color colour)
    {
        resourceType =
            type;


        gridCell =
            cell;


        amount =
            Mathf.Max(
                1,
                pickupAmount
            );


        playerController =
            player;

        pulseController =
            pulse;

        shaperController =
            shaper;


        transform.position =
            new Vector3(
                cell.x + 0.5f,
                cell.y + 0.5f,
                -2.15f
            );


        /*
         * Pulse cells are rotated to produce a diamond-like marker.
         * Shaper ammunition remains square, helping distinguish them
         * even before final artwork exists.
         */
        if (resourceType ==
            ResourceType.Pulse)
        {
            transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    45f
                );
        }


        transform.localScale =
            resourceType ==
            ResourceType.Pulse
                ? new Vector3(
                    0.30f,
                    0.30f,
                    1f
                )
                : new Vector3(
                    0.34f,
                    0.34f,
                    1f
                );


        Collider pickupCollider =
            GetComponent<Collider>();


        if (pickupCollider != null)
        {
            Destroy(
                pickupCollider
            );
        }


        Renderer renderer =
            GetComponent<Renderer>();


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
            pickupMaterial =
                new Material(
                    shader
                );


            pickupMaterial.color =
                colour;


            renderer.sharedMaterial =
                pickupMaterial;
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
        int actuallyAdded =
            0;


        if (resourceType ==
            ResourceType.Pulse)
        {
            if (pulseController != null)
            {
                actuallyAdded =
                    pulseController.AddCharges(
                        amount
                    );
            }
        }
        else
        {
            if (shaperController != null)
            {
                actuallyAdded =
                    shaperController.AddCharges(
                        amount
                    );
            }
        }


        /*
         * If the relevant inventory is already full, leave the pickup
         * on the floor. The player can return after spending a charge.
         */
        if (actuallyAdded <= 0)
        {
            return;
        }


        collected =
            true;


        UnityEngine.Debug.Log(
            $"RESOURCE PICKUP COLLECTED - " +
            $"{resourceType} +{actuallyAdded} at {gridCell}"
        );


        Destroy(
            gameObject
        );
    }


    private void OnDestroy()
    {
        if (pickupMaterial != null)
        {
            Destroy(
                pickupMaterial
            );
        }
    }
}