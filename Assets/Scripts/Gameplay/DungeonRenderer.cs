using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Converts DungeonGrid data into visible GameObjects.
///
/// DungeonGrid contains the actual dungeon data.
/// DungeonRenderer is responsible only for displaying that data
/// inside the Unity scene.
///
/// Keeping rendering separate from generation means the procedural
/// algorithm does not depend on a particular visual style.
/// </summary>
public class DungeonRenderer : MonoBehaviour
{
    [Header("Rendering")]

    [Tooltip("Size of each rendered dungeon cell.")]
    [SerializeField]
    private float cellSize = 1f;

    [Tooltip("Colour used for walkable floor.")]
    [SerializeField]
    private Color floorColour = new Color(
        0.35f,
        0.35f,
        0.35f,
        1f
    );

    [Tooltip("Colour used for walls.")]
    [SerializeField]
    private Color wallColour = new Color(
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

    // Parent objects keep the generated Hierarchy organised.
    private GameObject floorParent;
    private GameObject wallParent;

    // We keep references so a previous dungeon can be removed
    // before rendering another one.
    private readonly List<GameObject> renderedObjects =
        new List<GameObject>();


    /// <summary>
    /// Renders the supplied dungeon grid.
    ///
    /// Floor cells are rendered directly from DungeonGrid.
    /// Walls are calculated by checking the cells surrounding
    /// the walkable floor.
    /// </summary>
    public void Render(DungeonGrid grid)
    {
        Clear();

        if (grid == null || grid.FloorCellCount == 0)
        {
            UnityEngine.Debug.LogWarning(
                "DungeonRenderer received an empty dungeon grid."
            );

            return;
        }

        CreateParents();

        HashSet<Vector2Int> wallCells =
            CalculateWallCells(grid);

        RenderFloor(grid);
        RenderWalls(wallCells);

        // Will be disabled for now because the gameplay camera is controlled by DungeonCameraController.
        //// Position the camera after the dungeon geometry has been created.
        //FrameCamera(grid);

        UnityEngine.Debug.Log(
            $"Dungeon rendered with " +
            $"{grid.FloorCellCount} floor cells and " +
            $"{wallCells.Count} wall cells."
        );
    }


    /// <summary>
    /// Creates parent GameObjects so thousands of generated cells
    /// do not appear directly at the root of the Hierarchy.
    /// </summary>
    private void CreateParents()
    {
        floorParent = new GameObject("Generated Floor");
        floorParent.transform.SetParent(transform);

        wallParent = new GameObject("Generated Walls");
        wallParent.transform.SetParent(transform);
    }


    /// <summary>
    /// Renders every walkable grid coordinate as a square.
    /// </summary>
    private void RenderFloor(DungeonGrid grid)
    {
        foreach (Vector2Int cell in grid.FloorCells)
        {
            CreateCell(
                cell,
                floorColour,
                "Floor",
                floorParent.transform,
                0f
            );
        }
    }


    /// <summary>
    /// Finds wall positions around the outside of the walkable dungeon.
    ///
    /// A non-walkable cell becomes a wall when it is directly adjacent
    /// to at least one floor cell.
    /// </summary>
    private HashSet<Vector2Int> CalculateWallCells(
        DungeonGrid grid)
    {
        HashSet<Vector2Int> walls =
            new HashSet<Vector2Int>();

        // Four cardinal directions.
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        foreach (Vector2Int floorCell in grid.FloorCells)
        {
            foreach (Vector2Int direction in directions)
            {
                Vector2Int neighbour =
                    floorCell + direction;

                // A neighbouring coordinate that is not walkable
                // forms part of the outer wall.
                if (!grid.IsWalkable(neighbour))
                {
                    walls.Add(neighbour);
                }
            }
        }

        return walls;
    }


    /// <summary>
    /// Renders all calculated wall coordinates.
    /// </summary>
    private void RenderWalls(
        HashSet<Vector2Int> wallCells)
    {
        foreach (Vector2Int cell in wallCells)
        {
            CreateCell(
                cell,
                wallColour,
                "Wall",
                wallParent.transform,
                0.1f
            );
        }
    }


    /// <summary>
    /// Creates one visible square for a grid coordinate.
    ///
    /// Primitive Quads are sufficient for the prototype and avoid
    /// requiring external artwork at this stage.
    /// </summary>
    private void CreateCell(
        Vector2Int cell,
        Color colour,
        string objectName,
        Transform parent,
        float zPosition)
    {
        GameObject cellObject =
            GameObject.CreatePrimitive(PrimitiveType.Quad);

        cellObject.name =
            $"{objectName}_{cell.x}_{cell.y}";

        cellObject.transform.SetParent(parent);

        cellObject.transform.position =
            new Vector3(
                (cell.x + 0.5f) * cellSize,
                (cell.y + 0.5f) * cellSize,
                zPosition
            );

        cellObject.transform.localScale =
            new Vector3(
                cellSize,
                cellSize,
                1f
            );

        Renderer objectRenderer =
            cellObject.GetComponent<Renderer>();

        objectRenderer.material.color = colour;

        // Quad primitives contain a collider by default.
        // We don't need it yet and thousands of unnecessary
        // colliders would add overhead.
        Collider collider =
            cellObject.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }

        renderedObjects.Add(cellObject);
    }


    /// <summary>
    /// Removes the previously rendered dungeon.
    ///
    /// This allows the generator to create and display a new dungeon
    /// without leaving the previous cells in the scene.
    /// </summary>
    public void Clear()
    {
        foreach (GameObject renderedObject in renderedObjects)
        {
            if (renderedObject != null)
            {
                Destroy(renderedObject);
            }
        }

        renderedObjects.Clear();

        if (floorParent != null)
        {
            Destroy(floorParent);
        }

        if (wallParent != null)
        {
            Destroy(wallParent);
        }

        floorParent = null;
        wallParent = null;
    }

    /// <summary>
    /// Positions and sizes the orthographic camera so the complete
    /// generated dungeon is visible.
    ///
    /// The bounds are calculated from the actual generated floor cells,
    /// meaning the camera adapts automatically when a different seed
    /// produces a differently shaped dungeon.
    /// </summary>
    private void FrameCamera(DungeonGrid grid)
    {
        if (dungeonCamera == null ||
            grid == null ||
            grid.FloorCellCount == 0)
        {
            return;
        }

        bool firstCell = true;

        int minX = 0;
        int maxX = 0;
        int minY = 0;
        int maxY = 0;

        // Find the outer bounds of the generated floor.
        foreach (Vector2Int cell in grid.FloorCells)
        {
            if (firstCell)
            {
                minX = maxX = cell.x;
                minY = maxY = cell.y;

                firstCell = false;
                continue;
            }

            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);

            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        float dungeonWidth =
            (maxX - minX + 1) * cellSize;

        float dungeonHeight =
            (maxY - minY + 1) * cellSize;

        float centreX =
            ((minX + maxX + 1) * 0.5f) * cellSize;

        float centreY =
            ((minY + maxY + 1) * 0.5f) * cellSize;

        // Keep the camera's existing Z position because this is a 2D scene.
        dungeonCamera.transform.position =
            new Vector3(
                centreX,
                centreY,
                dungeonCamera.transform.position.z
            );

        // Orthographic size represents half of the visible vertical height.
        float verticalSize =
            dungeonHeight * 0.5f;

        // Width must also fit within the camera's aspect ratio.
        float horizontalSize =
            (dungeonWidth / dungeonCamera.aspect) * 0.5f;

        dungeonCamera.orthographicSize =
            Mathf.Max(
                verticalSize,
                horizontalSize
            ) + cameraPadding;
    }
}