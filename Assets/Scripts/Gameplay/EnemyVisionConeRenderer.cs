using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visualises the grid cells currently visible to one enemy.
///
/// It does not calculate perception itself. EnemyController provides
/// the visible cells, ensuring the visual torch and the AI detection
/// rules always agree.
/// </summary>
public class EnemyVisionConeRenderer : MonoBehaviour
{
    [Header("Display")]

    [SerializeField]
    private Color visionColour =
        new Color(
            1f,
            0.85f,
            0.25f,
            0.20f
        );

    [SerializeField]
    private float cellScale =
        0.92f;

    [SerializeField]
    private float refreshInterval =
        0.08f;


    private EnemyController enemyController;


    private readonly List<GameObject> markerPool =
        new List<GameObject>();


    private Material visionMaterial;


    private float nextRefreshTime;


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


        if (Time.time <
            nextRefreshTime)
        {
            return;
        }


        nextRefreshTime =
            Time.time +
            refreshInterval;


        RefreshVision();
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
            return;


        visionMaterial =
            new Material(
                shader
            );


        visionMaterial.color =
            visionColour;
    }


    private void RefreshVision()
    {
        if (enemyController == null)
            return;


        List<Vector2Int> visibleCells =
            enemyController.GetVisibleCells();


        int markerIndex =
            0;


        foreach (Vector2Int cell in
                 visibleCells)
        {
            // The enemy itself does not need a torch marker.
            if (cell ==
                enemyController.GridPosition)
            {
                continue;
            }


            GameObject marker =
                GetMarker(
                    markerIndex
                );


            marker.SetActive(
                true
            );


            marker.transform.position =
                new Vector3(
                    cell.x + 0.5f,
                    cell.y + 0.5f,
                    -1.8f
                );


            markerIndex++;
        }


        // Hide pooled markers that are no longer inside the cone.
        for (int i = markerIndex;
             i < markerPool.Count;
             i++)
        {
            markerPool[i].SetActive(
                false
            );
        }
    }


    private GameObject GetMarker(
        int index)
    {
        while (markerPool.Count <=
               index)
        {
            GameObject marker =
                GameObject.CreatePrimitive(
                    PrimitiveType.Quad
                );


            marker.name =
                "Vision Cell";


            marker.transform.SetParent(
                transform,
                true
            );


            marker.transform.localScale =
                new Vector3(
                    cellScale,
                    cellScale,
                    1f
                );


            Collider collider =
                marker.GetComponent<Collider>();


            if (collider != null)
            {
                Destroy(
                    collider
                );
            }


            Renderer renderer =
                marker.GetComponent<Renderer>();


            if (renderer != null &&
                visionMaterial != null)
            {
                renderer.sharedMaterial =
                    visionMaterial;
            }


            markerPool.Add(
                marker
            );
        }


        return markerPool[index];
    }


    private void OnDestroy()
    {
        if (visionMaterial != null)
        {
            Destroy(
                visionMaterial
            );
        }
    }
}