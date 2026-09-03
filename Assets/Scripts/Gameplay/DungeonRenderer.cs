using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Converts DungeonGrid data into two combined meshes:
///
/// - one mesh for all walkable floor cells
/// - one mesh for all wall cells
///
/// DungeonGrid remains the authoritative dungeon representation.
/// This class is responsible only for visual presentation.
///
/// Earlier versions represented every grid cell using a separate
/// Primitive Quad GameObject. That was simple during development but
/// became expensive once runtime terrain modification repeatedly
/// refreshed the dungeon.
///
/// Combining cells into meshes greatly reduces GameObject creation,
/// material instances and draw calls while preserving exactly the same
/// grid-based appearance.
/// </summary>
public class DungeonRenderer : MonoBehaviour
{
    [Header("Rendering")]

    [Tooltip("Size of each rendered dungeon cell.")]
    [SerializeField]
    private float cellSize = 1f;


    [Tooltip("Colour used for walkable floor.")]
    [SerializeField]
    private Color floorColour =
        new Color(
            0.35f,
            0.35f,
            0.35f,
            1f
        );


    [Tooltip("Colour used for walls.")]
    [SerializeField]
    private Color wallColour =
        new Color(
            0.08f,
            0.08f,
            0.08f,
            1f
        );


    [Header("Camera")]

    [Tooltip("Camera used to display the generated dungeon.")]
    [SerializeField]
    private Camera dungeonCamera;


    [Tooltip("Extra space shown around the outside of the dungeon.")]
    [SerializeField]
    private float cameraPadding = 3f;


    // ------------------------------------------------------------
    // COMBINED RENDER OBJECTS
    // ------------------------------------------------------------

    private GameObject floorObject;

    private GameObject wallObject;


    private Mesh floorMesh;

    private Mesh wallMesh;


    private Material floorMaterial;

    private Material wallMaterial;


    /*
     * Reusable buffers avoid allocating new Lists every time runtime
     * terrain changes.
     *
     * Mesh.SetVertices / SetTriangles copies their contents into the
     * mesh, so the same buffers can safely be reused for floor and wall
     * construction.
     */
    private readonly List<Vector3> vertexBuffer =
        new List<Vector3>();


    private readonly List<int> triangleBuffer =
        new List<int>();


    private static readonly Vector2Int[] directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };


    // ============================================================
    // PUBLIC RENDERING
    // ============================================================

    /// <summary>
    /// Rebuilds the visual meshes from the authoritative DungeonGrid.
    ///
    /// Unlike the original implementation, this does not destroy and
    /// recreate thousands of individual GameObjects.
    ///
    /// The same two mesh objects are reused throughout the floor.
    /// </summary>
    public void Render(
        DungeonGrid grid)
    {
        if (grid == null ||
            grid.FloorCellCount == 0)
        {
            Clear();


            UnityEngine.Debug.LogWarning(
                "DungeonRenderer received an empty dungeon grid."
            );


            return;
        }


        EnsureRenderObjects();


        if (floorMaterial != null)
        {
            floorMaterial.color =
                floorColour;
        }


        if (wallMaterial != null)
        {
            wallMaterial.color =
                wallColour;
        }


        HashSet<Vector2Int> wallCells =
            CalculateWallCells(
                grid
            );


        BuildMesh(
            floorMesh,
            grid.FloorCells,
            grid.FloorCellCount,
            0f
        );


        BuildMesh(
            wallMesh,
            wallCells,
            wallCells.Count,
            0.1f
        );


        UnityEngine.Debug.Log(
            $"Dungeon rendered with " +
            $"{grid.FloorCellCount} floor cells and " +
            $"{wallCells.Count} wall cells."
        );
    }


    // ============================================================
    // OBJECT SETUP
    // ============================================================

    /// <summary>
    /// Creates the two persistent render objects if they do not already
    /// exist.
    ///
    /// They are then reused for every dungeon refresh.
    /// </summary>
    private void EnsureRenderObjects()
    {
        EnsureMaterials();


        if (floorObject == null)
        {
            floorObject =
                CreateMeshObject(
                    "Generated Floor",
                    floorMaterial,
                    out floorMesh
                );
        }


        if (wallObject == null)
        {
            wallObject =
                CreateMeshObject(
                    "Generated Walls",
                    wallMaterial,
                    out wallMesh
                );
        }
    }


    private GameObject CreateMeshObject(
        string objectName,
        Material material,
        out Mesh mesh)
    {
        GameObject meshObject =
            new GameObject(
                objectName
            );


        /*
         * Mesh vertices are generated directly in dungeon/world coordinates.
         *
         * Keep the render object at world-space identity even though it is
         * parented beneath Dungeon Renderer for Hierarchy organisation.
         *
         * This matches the behaviour of the original per-cell renderer, where
         * each Quad was positioned using Transform.position rather than
         * Transform.localPosition.
         */
        meshObject.transform.SetParent(
            transform,
            true
        );


        meshObject.transform.position =
            Vector3.zero;


        meshObject.transform.rotation =
            Quaternion.identity;


        MeshFilter meshFilter =
            meshObject.AddComponent<MeshFilter>();


        MeshRenderer meshRenderer =
            meshObject.AddComponent<MeshRenderer>();


        mesh =
            new Mesh();


        mesh.name =
            objectName +
            " Mesh";


        /*
         * Dungeon sizes are currently comfortably below the 16-bit
         * vertex limit, but UInt32 keeps this renderer safe if dungeon
         * dimensions are increased later.
         */
        mesh.indexFormat =
            IndexFormat.UInt32;


        mesh.MarkDynamic();


        meshFilter.sharedMesh =
            mesh;


        meshRenderer.sharedMaterial =
            material;


        /*
         * These are flat unlit 2D meshes. Lighting, shadows and probes
         * provide no benefit and would only add rendering work.
         */
        meshRenderer.shadowCastingMode =
            ShadowCastingMode.Off;


        meshRenderer.receiveShadows =
            false;


        meshRenderer.lightProbeUsage =
            LightProbeUsage.Off;


        meshRenderer.reflectionProbeUsage =
            ReflectionProbeUsage.Off;


        return meshObject;
    }


    // ============================================================
    // MATERIALS
    // ============================================================

    /// <summary>
    /// Creates exactly two shared materials for the dungeon.
    ///
    /// The previous per-cell renderer.material access generated
    /// separate material instances for thousands of cells.
    /// </summary>
    private void EnsureMaterials()
    {
        if (floorMaterial != null &&
            wallMaterial != null)
        {
            return;
        }


        //Shader shader =
        //    Shader.Find(
        //        "Sprites/Default"
        //    );


        //if (shader == null)
        //{
        //    shader =
        //        Shader.Find(
        //            "Unlit/Color"
        //        );
        //}

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


        if (shader == null)
        {
            UnityEngine.Debug.LogError(
                "DungeonRenderer could not find a suitable unlit shader."
            );


            return;
        }


        if (floorMaterial == null)
        {
            floorMaterial =
                new Material(
                    shader
                );


            floorMaterial.name =
                "Runtime Dungeon Floor Material";


            floorMaterial.color =
                floorColour;
        }


        if (wallMaterial == null)
        {
            wallMaterial =
                new Material(
                    shader
                );


            wallMaterial.name =
                "Runtime Dungeon Wall Material";


            wallMaterial.color =
                wallColour;
        }
    }


    // ============================================================
    // MESH CONSTRUCTION
    // ============================================================

    /// <summary>
    /// Creates one combined mesh containing one quad for every supplied
    /// grid coordinate.
    ///
    /// Four vertices and six triangle indices are generated per cell.
    /// </summary>
    private void BuildMesh(
        Mesh targetMesh,
        IEnumerable<Vector2Int> cells,
        int cellCount,
        float zPosition)
    {
        if (targetMesh == null)
            return;


        vertexBuffer.Clear();

        triangleBuffer.Clear();


        int requiredVertices =
            Mathf.Max(
                0,
                cellCount * 4
            );


        int requiredTriangleIndices =
            Mathf.Max(
                0,
                cellCount * 6
            );


        /*
         * Increase capacity only when necessary. Once a sufficiently
         * large dungeon has been rendered, these allocations can be
         * reused by subsequent runtime terrain updates.
         */
        if (vertexBuffer.Capacity <
            requiredVertices)
        {
            vertexBuffer.Capacity =
                requiredVertices;
        }


        if (triangleBuffer.Capacity <
            requiredTriangleIndices)
        {
            triangleBuffer.Capacity =
                requiredTriangleIndices;
        }


        foreach (Vector2Int cell in
                 cells)
        {
            AddCellGeometry(
                cell,
                zPosition
            );
        }


        targetMesh.Clear();


        targetMesh.SetVertices(
            vertexBuffer
        );


        targetMesh.SetTriangles(
            triangleBuffer,
            0,
            false
        );


        targetMesh.RecalculateBounds();
    }


    /// <summary>
    /// Appends one grid square to the currently active mesh buffers.
    ///
    /// Triangle winding faces toward the normal 2D gameplay camera.
    /// </summary>
    private void AddCellGeometry(
        Vector2Int cell,
        float zPosition)
    {
        int firstVertex =
            vertexBuffer.Count;


        float minimumX =
            cell.x *
            cellSize;


        float minimumY =
            cell.y *
            cellSize;


        float maximumX =
            minimumX +
            cellSize;


        float maximumY =
            minimumY +
            cellSize;


        // Bottom-left.
        vertexBuffer.Add(
            new Vector3(
                minimumX,
                minimumY,
                zPosition
            )
        );


        // Bottom-right.
        vertexBuffer.Add(
            new Vector3(
                maximumX,
                minimumY,
                zPosition
            )
        );


        // Top-right.
        vertexBuffer.Add(
            new Vector3(
                maximumX,
                maximumY,
                zPosition
            )
        );


        // Top-left.
        vertexBuffer.Add(
            new Vector3(
                minimumX,
                maximumY,
                zPosition
            )
        );


        /*
         * The winding order produces a normal facing the camera along
         * negative Z, matching the old PrimitiveType.Quad rendering.
         */
        triangleBuffer.Add(
            firstVertex
        );

        triangleBuffer.Add(
            firstVertex + 2
        );

        triangleBuffer.Add(
            firstVertex + 1
        );


        triangleBuffer.Add(
            firstVertex
        );

        triangleBuffer.Add(
            firstVertex + 3
        );

        triangleBuffer.Add(
            firstVertex + 2
        );
    }


    // ============================================================
    // WALL CALCULATION
    // ============================================================

    /// <summary>
    /// Finds every non-walkable cell directly adjacent to floor.
    /// </summary>
    private HashSet<Vector2Int> CalculateWallCells(
        DungeonGrid grid)
    {
        HashSet<Vector2Int> walls =
            new HashSet<Vector2Int>();


        foreach (Vector2Int floorCell in
                 grid.FloorCells)
        {
            foreach (Vector2Int direction in
                     directions)
            {
                Vector2Int neighbour =
                    floorCell +
                    direction;


                if (!grid.IsWalkable(
                        neighbour))
                {
                    walls.Add(
                        neighbour
                    );
                }
            }
        }


        return walls;
    }


    // ============================================================
    // CLEARING
    // ============================================================

    /// <summary>
    /// Clears the current dungeon geometry without destroying the
    /// reusable render objects.
    /// </summary>
    public void Clear()
    {
        if (floorMesh != null)
        {
            floorMesh.Clear();
        }


        if (wallMesh != null)
        {
            wallMesh.Clear();
        }
    }


    // ============================================================
    // CAMERA SUPPORT
    // ============================================================

    /// <summary>
    /// Positions and sizes the orthographic camera around the complete
    /// dungeon.
    ///
    /// This remains available for debugging although gameplay normally
    /// uses DungeonCameraController.
    /// </summary>
    private void FrameCamera(
        DungeonGrid grid)
    {
        if (dungeonCamera == null ||
            grid == null ||
            grid.FloorCellCount == 0)
        {
            return;
        }


        bool firstCell =
            true;


        int minX = 0;
        int maxX = 0;
        int minY = 0;
        int maxY = 0;


        foreach (Vector2Int cell in
                 grid.FloorCells)
        {
            if (firstCell)
            {
                minX =
                    maxX =
                        cell.x;


                minY =
                    maxY =
                        cell.y;


                firstCell =
                    false;


                continue;
            }


            minX =
                Mathf.Min(
                    minX,
                    cell.x
                );


            maxX =
                Mathf.Max(
                    maxX,
                    cell.x
                );


            minY =
                Mathf.Min(
                    minY,
                    cell.y
                );


            maxY =
                Mathf.Max(
                    maxY,
                    cell.y
                );
        }


        float dungeonWidth =
            (maxX -
             minX +
             1) *
            cellSize;


        float dungeonHeight =
            (maxY -
             minY +
             1) *
            cellSize;


        float centreX =
            ((minX +
              maxX +
              1) *
             0.5f) *
            cellSize;


        float centreY =
            ((minY +
              maxY +
              1) *
             0.5f) *
            cellSize;


        dungeonCamera.transform.position =
            new Vector3(
                centreX,
                centreY,
                dungeonCamera
                    .transform
                    .position
                    .z
            );


        float verticalSize =
            dungeonHeight *
            0.5f;


        float horizontalSize =
            (dungeonWidth /
             dungeonCamera.aspect) *
            0.5f;


        dungeonCamera.orthographicSize =
            Mathf.Max(
                verticalSize,
                horizontalSize
            ) +
            cameraPadding;
    }


    // ============================================================
    // CLEANUP
    // ============================================================

    private void OnDestroy()
    {
        if (floorMesh != null)
        {
            Destroy(
                floorMesh
            );
        }


        if (wallMesh != null)
        {
            Destroy(
                wallMesh
            );
        }


        if (floorMaterial != null)
        {
            Destroy(
                floorMaterial
            );
        }


        if (wallMaterial != null)
        {
            Destroy(
                wallMaterial
            );
        }
    }
}