using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Visualises the exact grid cells currently visible to an enemy.
///
/// The visibility calculation remains entirely owned by EnemyController.
/// This component only renders the returned cells.
///
/// Earlier versions represented every visible cell using a pooled Quad
/// GameObject. Pooling avoided repeated Instantiate/Destroy calls, but
/// a dungeon containing several enemies could still require many
/// individual render objects and draw calls.
///
/// The current version combines the complete visible area of one enemy
/// into a single dynamic mesh.
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


    [Tooltip(
        "Maximum frequency at which the visual representation is updated."
    )]
    [SerializeField]
    private float refreshInterval = 0.075f;


    private EnemyController enemyController;


    private GameObject visionMeshObject;

    private MeshFilter visionMeshFilter;

    private MeshRenderer visionMeshRenderer;

    private Mesh visionMesh;

    private Material visionMaterial;


    private float nextRefreshTime;


    /*
     * These buffers are reused rather than allocating new arrays/lists
     * every time an enemy's visible area changes.
     */
    private readonly List<Vector3> vertices =
        new List<Vector3>();


    private readonly List<int> triangles =
        new List<int>();


    /// <summary>
    /// Called after a procedural enemy has been created.
    /// </summary>
    public void Initialise(
        EnemyController controller)
    {
        enemyController =
            controller;


        CreateVisionMesh();


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
            Mathf.Max(
                0.01f,
                refreshInterval
            );


        RefreshVision();
    }


    // ============================================================
    // MESH SETUP
    // ============================================================

    private void CreateVisionMesh()
    {
        if (visionMeshObject != null)
            return;


        visionMeshObject =
            new GameObject(
                "Enemy Vision Mesh"
            );


        /*
         * Vision vertices are generated directly in absolute dungeon/world
         * coordinates.
         *
         * Therefore this mesh must NOT be parented to the moving enemy.
         * Otherwise the enemy's Transform movement would be applied on top
         * of the already world-positioned mesh vertices and visually offset
         * the displayed vision area.
         */
        visionMeshObject.transform.SetParent(
            null
        );


        visionMeshObject.transform.position =
            Vector3.zero;


        visionMeshObject.transform.rotation =
            Quaternion.identity;


        visionMeshObject.transform.localScale =
            Vector3.one;


        visionMeshFilter =
            visionMeshObject.AddComponent<MeshFilter>();


        visionMeshRenderer =
            visionMeshObject.AddComponent<MeshRenderer>();


        visionMesh =
            new Mesh();


        visionMesh.name =
            "Enemy Vision Dynamic Mesh";


        visionMesh.indexFormat =
            IndexFormat.UInt32;


        visionMesh.MarkDynamic();


        visionMeshFilter.sharedMesh =
            visionMesh;


        CreateVisionMaterial();


        if (visionMaterial != null)
        {
            visionMeshRenderer.sharedMaterial =
                visionMaterial;
        }


        visionMeshRenderer.shadowCastingMode =
            ShadowCastingMode.Off;


        visionMeshRenderer.receiveShadows =
            false;


        visionMeshRenderer.lightProbeUsage =
            LightProbeUsage.Off;


        visionMeshRenderer.reflectionProbeUsage =
            ReflectionProbeUsage.Off;
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


        visionMaterial.name =
            "Runtime Enemy Vision Material";


        visionMaterial.color =
            visionColour;
    }


    // ============================================================
    // VISUAL REFRESH
    // ============================================================

    /// <summary>
    /// Rebuilds one combined mesh from the exact cells returned by the
    /// enemy's authoritative visibility calculation.
    /// </summary>
    private void RefreshVision()
    {
        if (enemyController == null ||
            visionMesh == null)
        {
            return;
        }


        List<Vector2Int> visibleCells =
            enemyController.GetVisibleCells();


        vertices.Clear();

        triangles.Clear();


        /*
         * Reserve enough capacity after the first larger visibility
         * result so later refreshes can reuse the same backing arrays.
         */
        int usefulCellCount =
            Mathf.Max(
                0,
                visibleCells.Count - 1
            );


        int requiredVertices =
            usefulCellCount *
            4;


        int requiredTriangleIndices =
            usefulCellCount *
            6;


        if (vertices.Capacity <
            requiredVertices)
        {
            vertices.Capacity =
                requiredVertices;
        }


        if (triangles.Capacity <
            requiredTriangleIndices)
        {
            triangles.Capacity =
                requiredTriangleIndices;
        }


        foreach (Vector2Int cell in
                 visibleCells)
        {
            /*
             * The enemy model already occupies its own grid square.
             */
            if (cell ==
                enemyController.GridPosition)
            {
                continue;
            }


            AddVisibleCell(
                cell
            );
        }


        visionMesh.Clear();


        visionMesh.SetVertices(
            vertices
        );


        visionMesh.SetTriangles(
            triangles,
            0,
            false
        );


        visionMesh.RecalculateBounds();


        if (visionMaterial != null)
        {
            visionMaterial.color =
                visionColour;
        }
    }


    /// <summary>
    /// Adds one visible grid square to the combined enemy vision mesh.
    /// </summary>
    private void AddVisibleCell(
        Vector2Int cell)
    {
        int firstVertex =
            vertices.Count;


        float safeScale =
            Mathf.Clamp(
                visionCellScale,
                0.05f,
                1f
            );


        float inset =
            (1f -
             safeScale) *
            0.5f;


        float minimumX =
            cell.x +
            inset;


        float minimumY =
            cell.y +
            inset;


        float maximumX =
            cell.x +
            1f -
            inset;


        float maximumY =
            cell.y +
            1f -
            inset;


        const float zPosition =
            -1.75f;


        vertices.Add(
            new Vector3(
                minimumX,
                minimumY,
                zPosition
            )
        );


        vertices.Add(
            new Vector3(
                maximumX,
                minimumY,
                zPosition
            )
        );


        vertices.Add(
            new Vector3(
                maximumX,
                maximumY,
                zPosition
            )
        );


        vertices.Add(
            new Vector3(
                minimumX,
                maximumY,
                zPosition
            )
        );


        triangles.Add(
            firstVertex
        );

        triangles.Add(
            firstVertex + 2
        );

        triangles.Add(
            firstVertex + 1
        );


        triangles.Add(
            firstVertex
        );

        triangles.Add(
            firstVertex + 3
        );

        triangles.Add(
            firstVertex + 2
        );
    }


    private void OnDestroy()
    {
        if (visionMesh != null)
        {
            Destroy(
                visionMesh
            );
        }


        if (visionMaterial != null)
        {
            Destroy(
                visionMaterial
            );
        }


        if (visionMeshObject != null)
        {
            Destroy(
                visionMeshObject
            );
        }
    }
}