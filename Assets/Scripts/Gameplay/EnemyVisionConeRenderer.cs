using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visualises the exact grid cells currently visible to an enemy.
///
/// The renderer deliberately uses the same cell-based visibility
/// calculated by EnemyController rather than a separate raycast
/// representation. This keeps the displayed torch area identical
/// to the enemy's actual perception.
/// </summary>
public class EnemyVisionConeRenderer : MonoBehaviour
{
    [Header("Torch Display")]

    [Tooltip("Colour of the enemy's visible area.")]
    [SerializeField]
    private Color visionColour =
        new Color(
            1f,
            0.82f,
            0.2f,
            0.22f
        );

    [Tooltip(
        "Size of each visible cell. " +
        "1 fills the complete grid square."
    )]
    [SerializeField]
    private float visionCellScale = 1f;

    [Tooltip("How often the visible area is refreshed.")]
    [SerializeField]
    private float refreshInterval = 0.05f;


    private EnemyController enemyController;

    private readonly List<GameObject> visionCellPool =
        new List<GameObject>();

    private Material visionMaterial;

    private float nextRefreshTime;


    /// <summary>
    /// Called after a procedural enemy has been created.
    /// </summary>
    public void Initialise(
        EnemyController controller)
    {
        enemyController =
            controller;

        CreateVisionMaterial();

        RefreshVision();
    }


    private void Update()
    {
        if (enemyController == null)
            return;


        if (Time.time < nextRefreshTime)
            return;


        nextRefreshTime =
            Time.time +
            refreshInterval;


        RefreshVision();
    }


    /// <summary>
    /// Updates the visible torch cells using the exact same
    /// visibility result used by the enemy AI.
    /// </summary>
    private void RefreshVision()
    {
        if (enemyController == null)
            return;


        List<Vector2Int> visibleCells =
            enemyController.GetVisibleCells();


        int markerIndex = 0;


        foreach (Vector2Int cell in visibleCells)
        {
            // The enemy already occupies its own grid square,
            // so it does not need a light tile there.
            if (cell == enemyController.GridPosition)
                continue;


            GameObject marker =
                GetVisionCell(
                    markerIndex
                );


            marker.SetActive(
                true
            );


            marker.transform.position =
                new Vector3(
                    cell.x + 0.5f,
                    cell.y + 0.5f,
                    -1.75f
                );


            markerIndex++;
        }


        // Hide pooled cells which are not needed this frame.
        for (int i = markerIndex;
             i < visionCellPool.Count;
             i++)
        {
            visionCellPool[i].SetActive(
                false
            );
        }
    }


    /// <summary>
    /// Gets an existing visual cell from the pool or creates one.
    ///
    /// Pooling prevents GameObjects constantly being created and
    /// destroyed while enemies move and change direction.
    /// </summary>
    private GameObject GetVisionCell(
        int index)
    {
        while (visionCellPool.Count <= index)
        {
            GameObject marker =
                GameObject.CreatePrimitive(
                    PrimitiveType.Quad
                );


            marker.name =
                "Enemy Vision Cell";


            /*
             * Do not parent the cells to the enemy.
             *
             * They are placed directly using dungeon grid/world
             * coordinates. This is the same approach used by the
             * original working debug renderer.
             */
            marker.transform.SetParent(
                null
            );


            marker.transform.localScale =
                new Vector3(
                    visionCellScale,
                    visionCellScale,
                    1f
                );


            Collider markerCollider =
                marker.GetComponent<Collider>();


            if (markerCollider != null)
            {
                Destroy(
                    markerCollider
                );
            }


            Renderer markerRenderer =
                marker.GetComponent<Renderer>();


            if (markerRenderer != null &&
                visionMaterial != null)
            {
                markerRenderer.sharedMaterial =
                    visionMaterial;
            }


            visionCellPool.Add(
                marker
            );
        }


        return visionCellPool[index];
    }


    private void CreateVisionMaterial()
    {
        Shader shader =
            Shader.Find(
                "Sprites/Default"
            );


        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Unlit/Color"
                );
        }


        if (shader == null)
        {
            UnityEngine.Debug.LogWarning(
                "Could not find a shader for enemy vision."
            );

            return;
        }


        visionMaterial =
            new Material(
                shader
            );


        visionMaterial.color =
            visionColour;
    }


    private void OnDestroy()
    {
        foreach (GameObject marker in visionCellPool)
        {
            if (marker != null)
            {
                Destroy(
                    marker
                );
            }
        }


        visionCellPool.Clear();


        if (visionMaterial != null)
        {
            Destroy(
                visionMaterial
            );
        }
    }
}