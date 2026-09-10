using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

/// <summary>
/// Displays player progression and Warden pursuit.
///
/// STANDARD:
/// START -> Floor 1 -> ... -> Final Floor -> EXIT
///
/// SURVIVAL:
/// Shows a sliding window around the player's current floor.
/// </summary>
public class RunProgressTimeline : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonRunManager runManager;

    [SerializeField]
    private RunStatsManager runStatsManager;

    [SerializeField]
    private WardenManager wardenManager;


    [Header("Timeline")]

    [SerializeField]
    private RectTransform progressLine;

    [SerializeField]
    private RectTransform[] floorNodeRoots;

    [SerializeField]
    private UnityEngine.UI.Image[] floorNodeImages;

    [SerializeField]
    private UnityEngine.UI.Text[] floorNodeLabels;


    [Header("Markers")]

    [SerializeField]
    private RectTransform playerMarker;

    [SerializeField]
    private RectTransform wardenMarker;

    [SerializeField]
    private RectTransform finalExitMarker;


    [Header("Node Appearance")]

    [SerializeField]
    private Color completedNodeColour =
        Color.white;

    [SerializeField]
    private Color currentNodeColour =
        Color.white;

    [SerializeField]
    private Color futureNodeColour =
        new Color(
            1f,
            1f,
            1f,
            0.30f
        );

    [SerializeField]
    private Color endpointNodeColour =
        new Color(
            1f,
            1f,
            1f,
            0.75f
        );


    [Header("Refresh")]

    [SerializeField]
    private float refreshInterval =
        0.05f;


    private float nextRefreshTime;

    private int firstVisibleCoordinate;
    private int lastVisibleCoordinate;
    private int activeNodeCount;


    private void Start()
    {
        ResolveReferences();

        RefreshTimeline();
    }


    private void Update()
    {
        if (Time.unscaledTime <
            nextRefreshTime)
        {
            return;
        }

        nextRefreshTime =
            Time.unscaledTime +
            refreshInterval;

        RefreshTimeline();
    }


    private void RefreshTimeline()
    {
        ResolveReferences();

        if (runManager == null ||
            progressLine == null ||
            floorNodeRoots == null ||
            floorNodeRoots.Length == 0)
        {
            return;
        }

        CalculateVisibleRange();

        PositionAndLabelNodes();

        PositionPlayerMarker();

        PositionWardenMarker();

        PositionExitMarker();
    }


    private void CalculateVisibleRange()
    {
        int slotCount =
            floorNodeRoots.Length;

        int currentFloor =
            Mathf.Max(
                1,
                runManager.CurrentFloor
            );


        // ========================================================
        // STANDARD
        // ========================================================

        if (!runManager.IsSurvival)
        {
            int totalFloors =
                Mathf.Max(
                    1,
                    runManager.TotalFloors
                );

            /*
             * Coordinate system:
             *
             * 0     = START
             * 1..N  = dungeon floors
             * N + 1 = EXIT
             */
            int exitCoordinate =
                totalFloors + 1;

            int totalCoordinates =
                exitCoordinate + 1;


            // Whole run fits on screen.
            if (totalCoordinates <=
                slotCount)
            {
                firstVisibleCoordinate =
                    0;

                lastVisibleCoordinate =
                    exitCoordinate;

                activeNodeCount =
                    totalCoordinates;

                return;
            }


            // Long custom Standard run: sliding window.
            int halfWindow =
                slotCount / 2;

            firstVisibleCoordinate =
                Mathf.Max(
                    0,
                    currentFloor -
                    halfWindow
                );

            if (firstVisibleCoordinate +
                slotCount - 1 >
                exitCoordinate)
            {
                firstVisibleCoordinate =
                    Mathf.Max(
                        0,
                        exitCoordinate -
                        slotCount +
                        1
                    );
            }

            lastVisibleCoordinate =
                Mathf.Min(
                    exitCoordinate,
                    firstVisibleCoordinate +
                    slotCount - 1
                );

            activeNodeCount =
                lastVisibleCoordinate -
                firstVisibleCoordinate +
                1;

            return;
        }


        // ========================================================
        // SURVIVAL
        // ========================================================

        int half =
            slotCount / 2;

        firstVisibleCoordinate =
            Mathf.Max(
                0,
                currentFloor -
                half
            );

        lastVisibleCoordinate =
            firstVisibleCoordinate +
            slotCount - 1;

        activeNodeCount =
            slotCount;
    }


    private void PositionAndLabelNodes()
    {
        float width =
            progressLine.rect.width;


        for (int i = 0;
             i < floorNodeRoots.Length;
             i++)
        {
            RectTransform node =
                floorNodeRoots[i];

            if (node == null)
                continue;


            bool active =
                i < activeNodeCount;

            node.gameObject.SetActive(
                active
            );

            if (!active)
                continue;


            int coordinate =
                firstVisibleCoordinate + i;


            float normalised =
                activeNodeCount <= 1
                    ? 0.5f
                    : (float)i /
                      (activeNodeCount - 1);


            Vector2 position =
                node.anchoredPosition;

            float left =
                -width * 0.5f;

            float right =
                width * 0.5f;


            position.x =
                Mathf.Lerp(
                    left,
                    right,
                    normalised
                );

            node.anchoredPosition =
                position;


            if (floorNodeLabels != null &&
                 i < floorNodeLabels.Length &&
                 floorNodeLabels[i] != null)
            {
                UnityEngine.UI.Text label =
                    floorNodeLabels[i];

                label.gameObject.SetActive(
                    true
                );

                label.enabled =
                    true;

                label.text =
                    GetLabel(
                        coordinate
                    );


                RectTransform labelRect =
                    label.rectTransform;

                Vector2 labelPosition =
                    labelRect.anchoredPosition;


                /*
                 * START and EXIT sit at the absolute ends of the progress line.
                 *
                 * Keep the NODE itself exactly on the end of the line,
                 * but move the text slightly inward so it cannot be clipped.
                 */
                if (coordinate == 0)
                {
                    label.alignment =
                        UnityEngine.TextAnchor.MiddleLeft;

                    labelPosition.x =
                        8f;
                }
                else if (!runManager.IsSurvival &&
                         coordinate ==
                            runManager.TotalFloors + 1)
                {
                    label.alignment =
                        UnityEngine.TextAnchor.MiddleRight;

                    labelPosition.x =
                        -8f;
                }
                else
                {
                    label.alignment =
                        UnityEngine.TextAnchor.MiddleCenter;

                    labelPosition.x =
                        0f;
                }


                labelRect.anchoredPosition =
                    labelPosition;
            }


            if (floorNodeImages != null &&
                i < floorNodeImages.Length &&
                floorNodeImages[i] != null)
            {
                bool isExit =
                    !runManager.IsSurvival &&
                    coordinate ==
                        runManager.TotalFloors + 1;

                /*
                 * At EXIT the hatch sprite itself becomes the timeline node,
                 * so hide the ordinary square/dot underneath it.
                 */
                floorNodeImages[i].gameObject.SetActive(
                    !isExit
                );

                if (!isExit)
                {
                    floorNodeImages[i].color =
                        GetNodeColour(
                            coordinate
                        );
                }
            }
        }
    }


    private string GetLabel(
        int coordinate)
    {
        if (coordinate == 0)
        {
            return
                "START";
        }


        if (!runManager.IsSurvival &&
            coordinate ==
                runManager.TotalFloors + 1)
        {
            return
                "EXIT";
        }


        return
            coordinate.ToString();
    }


    private Color GetNodeColour(
        int coordinate)
    {
        if (coordinate == 0)
        {
            return
                endpointNodeColour;
        }


        if (!runManager.IsSurvival &&
            coordinate ==
                runManager.TotalFloors + 1)
        {
            return
                endpointNodeColour;
        }


        if (coordinate <
            runManager.CurrentFloor)
        {
            return
                completedNodeColour;
        }


        if (coordinate ==
            runManager.CurrentFloor)
        {
            return
                currentNodeColour;
        }


        return
            futureNodeColour;
    }


    private void PositionPlayerMarker()
    {
        if (playerMarker == null)
            return;


        SetMarkerPosition(
            playerMarker,
            runManager.CurrentFloor
        );
    }


    private void PositionWardenMarker()
    {
        if (wardenMarker == null ||
            wardenManager == null)
        {
            return;
        }


        wardenMarker.gameObject.SetActive(
            true
        );


        float wardenPosition;


        if (wardenManager.IsPhysicalWardenPresent ||
            wardenManager.IsWardenArriving)
        {
            wardenPosition =
                runManager.CurrentFloor;
        }
        else
        {
            wardenPosition =
                wardenManager.WardenFloor +
                Mathf.Clamp01(
                    wardenManager.PursuitProgress
                );
        }


        SetMarkerPosition(
            wardenMarker,
            wardenPosition
        );
    }


    private void PositionExitMarker()
    {
        if (finalExitMarker == null)
            return;


        if (runManager.IsSurvival)
        {
            finalExitMarker.gameObject.SetActive(
                false
            );

            return;
        }


        int exitCoordinate =
            runManager.TotalFloors + 1;


        bool visible =
            exitCoordinate >=
                firstVisibleCoordinate &&
            exitCoordinate <=
                lastVisibleCoordinate;


        finalExitMarker.gameObject.SetActive(
            visible
        );


        if (visible)
        {
            SetMarkerPosition(
                finalExitMarker,
                exitCoordinate
            );
        }
    }


    private void SetMarkerPosition(
        RectTransform marker,
        float coordinate)
    {
        float clampedCoordinate =
            Mathf.Clamp(
                coordinate,
                firstVisibleCoordinate,
                lastVisibleCoordinate
            );


        float range =
            Mathf.Max(
                1f,
                lastVisibleCoordinate -
                firstVisibleCoordinate
            );


        float normalised =
            (clampedCoordinate -
             firstVisibleCoordinate) /
            range;


        float width =
            progressLine.rect.width;

        float left =
            -width * 0.5f;

        float right =
            width * 0.5f;


        float x =
            Mathf.Lerp(
                left,
                right,
                normalised
            );


        Vector2 position =
            marker.anchoredPosition;

        position.x =
            x;

        marker.anchoredPosition =
            position;
    }


    private void ResolveReferences()
    {
        if (runManager == null)
        {
            runManager =
                FindObjectOfType<DungeonRunManager>();
        }


        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
        }


        if (wardenManager == null)
        {
            wardenManager =
                FindObjectOfType<WardenManager>();
        }
    }
}