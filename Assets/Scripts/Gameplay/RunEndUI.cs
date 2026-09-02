using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

/// <summary>
/// Displays the end state of a dungeon run.
///
/// The gameplay systems remain responsible for deciding whether the
/// player has won or died. This component only presents that state
/// and provides a simple restart option.
/// </summary>
public class RunEndUI : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonRunManager runManager;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private GameObject endPanel;

    [SerializeField]
    private UnityEngine.UI.Text titleText;

    [SerializeField]
    private UnityEngine.UI.Text detailText;


    [Header("Controls")]

    [SerializeField]
    private KeyCode restartKey =
        KeyCode.Return;


    private bool endStateShown;


    private void Start()
    {
        HideEndPanel();
    }


    private void Update()
    {
        if (endStateShown)
        {
            if (Input.GetKeyDown(
                    restartKey))
            {
                RestartRun();
            }


            return;
        }


        if (playerController != null &&
            !playerController.IsAlive)
        {
            ShowGameOver();

            return;
        }


        if (runManager != null &&
            runManager.RunComplete)
        {
            ShowRunComplete();
        }
    }


    private void ShowGameOver()
    {
        endStateShown =
            true;


        if (endPanel != null)
        {
            endPanel.SetActive(
                true
            );
        }


        if (titleText != null)
        {
            titleText.text =
                "GAME OVER";
        }


        if (detailText != null)
        {
            int floor =
                runManager != null
                    ? runManager.CurrentFloor
                    : 0;


            detailText.text =
                "The Warden claimed the run.\n" +
                $"Reached Floor {floor}.\n\n" +
                "Press ENTER to try again.";
        }


        UnityEngine.Debug.Log(
            "========== GAME OVER ==========\n" +
            $"Floor reached: " +
            $"{(runManager != null ? runManager.CurrentFloor : 0)}\n" +
            "==============================="
        );
    }


    private void ShowRunComplete()
    {
        endStateShown =
            true;


        if (endPanel != null)
        {
            endPanel.SetActive(
                true
            );
        }


        if (titleText != null)
        {
            titleText.text =
                "RUN COMPLETE";
        }


        if (detailText != null)
        {
            detailText.text =
                "You escaped the Deepfall.\n" +
                "All five floors survived.\n\n" +
                "Press ENTER to begin another run.";
        }


        UnityEngine.Debug.Log(
            "========== RUN COMPLETE ==========\n" +
            "All dungeon floors completed.\n" +
            "=================================="
        );
    }


    private void HideEndPanel()
    {
        endStateShown =
            false;


        if (endPanel != null)
        {
            endPanel.SetActive(
                false
            );
        }
    }


    /// <summary>
    /// Reloads the active scene to return every gameplay system to its
    /// normal initial state.
    ///
    /// This is intentionally simple and reliable for the prototype.
    /// </summary>
    private void RestartRun()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement
                .SceneManager
                .GetActiveScene()
                .buildIndex
        );
    }
}