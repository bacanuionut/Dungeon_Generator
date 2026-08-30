using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders dungeon fog using one dynamic mesh.
///
/// Every grid position inside the dungeon bounds receives a fog quad.
/// Vertex transparency is then changed according to whether that cell
/// is currently visible, previously explored or completely unknown.
/// </summary>
public class FogOfWarRenderer : MonoBehaviour
{
    [Header("Appearance")]

    [Tooltip("Darkness applied to areas which have been explored previously.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float exploredAlpha = 0.48f;

    [Tooltip("Darkness applied to the player's current room or corridor.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float currentRegionAlpha = 0.28f;

    [Tooltip("Darkness applied to parts of the dungeon never seen before.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float unexploredAlpha = 0.88f;

    [SerializeField]
    private float fogZ = -3f;


    private Mesh fogMesh;

    private Material fogMaterial;


    private readonly Dictionary<Vector2Int, int> firstVertexByCell =
        new Dictionary<Vector2Int, int>();


    private Color32[] colours;


    private void Awake()
    {
        // Fog mesh vertices are generated directly in dungeon/world
        // grid coordinates, so this renderer must remain at the origin.
        transform.position =
            Vector3.zero;

        transform.rotation =
            Quaternion.identity;

        transform.localScale =
            Vector3.one;


        CreateRenderer();
    }


    private void CreateRenderer()
    {
        MeshFilter meshFilter =
            GetComponent<MeshFilter>();


        if (meshFilter == null)
        {
            meshFilter =
                gameObject.AddComponent<MeshFilter>();
        }


        MeshRenderer meshRenderer =
            GetComponent<MeshRenderer>();


        if (meshRenderer == null)
        {
            meshRenderer =
                gameObject.AddComponent<MeshRenderer>();
        }


        fogMesh =
            new Mesh();


        fogMesh.name =
            "Dungeon Fog Mesh";


        fogMesh.MarkDynamic();


        meshFilter.sharedMesh =
            fogMesh;


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
                "FogOfWarRenderer could not find a suitable shader."
            );

            return;
        }


        fogMaterial =
            new Material(
                shader
            );


        fogMaterial.color =
            Color.white;


        meshRenderer.sharedMaterial =
            fogMaterial;
    }


    /// <summary>
    /// Creates fog covering the complete bounds of the current dungeon.
    /// </summary>
    public void BuildForGrid(
        DungeonGrid grid)
    {
        if (grid == null ||
            grid.FloorCellCount == 0)
        {
            return;
        }


        firstVertexByCell.Clear();


        int minimumX = int.MaxValue;
        int maximumX = int.MinValue;
        int minimumY = int.MaxValue;
        int maximumY = int.MinValue;


        foreach (Vector2Int cell in grid.FloorCells)
        {
            minimumX =
                Mathf.Min(
                    minimumX,
                    cell.x
                );

            maximumX =
                Mathf.Max(
                    maximumX,
                    cell.x
                );

            minimumY =
                Mathf.Min(
                    minimumY,
                    cell.y
                );

            maximumY =
                Mathf.Max(
                    maximumY,
                    cell.y
                );
        }


        // One extra cell hides the external wall boundary too.
        minimumX--;
        maximumX++;

        minimumY--;
        maximumY++;


        int width =
            maximumX -
            minimumX +
            1;

        int height =
            maximumY -
            minimumY +
            1;


        int cellCount =
            width *
            height;


        Vector3[] vertices =
            new Vector3[
                cellCount * 4
            ];


        int[] triangles =
            new int[
                cellCount * 6
            ];


        colours =
            new Color32[
                cellCount * 4
            ];


        Color32 completelyHidden =
            new Color32(
                0,
                0,
                0,
                255
            );


        int cellIndex = 0;


        for (int y = minimumY;
             y <= maximumY;
             y++)
        {
            for (int x = minimumX;
                 x <= maximumX;
                 x++)
            {
                Vector2Int cell =
                    new Vector2Int(
                        x,
                        y
                    );


                int vertex =
                    cellIndex * 4;


                int triangle =
                    cellIndex * 6;


                firstVertexByCell[cell] =
                    vertex;


                vertices[vertex] =
                    new Vector3(
                        x,
                        y,
                        fogZ
                    );


                vertices[vertex + 1] =
                    new Vector3(
                        x + 1f,
                        y,
                        fogZ
                    );


                vertices[vertex + 2] =
                    new Vector3(
                        x + 1f,
                        y + 1f,
                        fogZ
                    );


                vertices[vertex + 3] =
                    new Vector3(
                        x,
                        y + 1f,
                        fogZ
                    );


                triangles[triangle] =
                    vertex;

                triangles[triangle + 1] =
                    vertex + 2;

                triangles[triangle + 2] =
                    vertex + 1;


                triangles[triangle + 3] =
                    vertex;

                triangles[triangle + 4] =
                    vertex + 3;

                triangles[triangle + 5] =
                    vertex + 2;


                colours[vertex] =
                    completelyHidden;

                colours[vertex + 1] =
                    completelyHidden;

                colours[vertex + 2] =
                    completelyHidden;

                colours[vertex + 3] =
                    completelyHidden;


                cellIndex++;
            }
        }


        fogMesh.Clear();


        fogMesh.vertices =
            vertices;

        fogMesh.triangles =
            triangles;

        fogMesh.colors32 =
            colours;


        fogMesh.RecalculateBounds();
    }


    /// <summary>
    /// Applies four visibility states:
    ///
    /// torch visible   = completely clear
    /// current region  = lightly darkened
    /// explored        = more heavily darkened
    /// unexplored      = darkest state
    /// </summary>
    public void UpdateFog(
        HashSet<Vector2Int> fullyVisibleCells,
        HashSet<Vector2Int> currentRegionCells,
        HashSet<Vector2Int> exploredCells)
    {
        if (fogMesh == null ||
            colours == null)
        {
            return;
        }


        byte currentRegionOpacity =
            (byte)Mathf.RoundToInt(
                Mathf.Clamp01(
                    currentRegionAlpha
                ) *
                255f
            );


        byte exploredOpacity =
            (byte)Mathf.RoundToInt(
                Mathf.Clamp01(
                    exploredAlpha
                ) *
                255f
            );


        byte unexploredOpacity =
            (byte)Mathf.RoundToInt(
                Mathf.Clamp01(
                    unexploredAlpha
                ) *
                255f
            );


        foreach (
            KeyValuePair<Vector2Int, int> pair
            in firstVertexByCell)
        {
            byte alpha;


            if (fullyVisibleCells.Contains(
                    pair.Key))
            {
                alpha = 0;
            }
            else if (currentRegionCells.Contains(
                         pair.Key))
            {
                alpha =
                    currentRegionOpacity;
            }
            else if (exploredCells.Contains(
                         pair.Key))
            {
                alpha =
                    exploredOpacity;
            }
            else
            {
                alpha =
                    unexploredOpacity;
            }


            Color32 fogColour =
                new Color32(
                    0,
                    0,
                    0,
                    alpha
                );


            int vertex =
                pair.Value;


            colours[vertex] =
                fogColour;

            colours[vertex + 1] =
                fogColour;

            colours[vertex + 2] =
                fogColour;

            colours[vertex + 3] =
                fogColour;
        }


        fogMesh.colors32 =
            colours;
    }


    private void OnDestroy()
    {
        if (fogMesh != null)
        {
            Destroy(
                fogMesh
            );
        }


        if (fogMaterial != null)
        {
            Destroy(
                fogMaterial
            );
        }
    }
}