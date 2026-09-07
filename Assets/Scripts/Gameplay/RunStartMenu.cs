using System;
using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

/// <summary>
/// Controls the current three-screen menu structure:
/// Main Menu, Additional Options and Run End.
/// </summary>
public class RunStartMenu : MonoBehaviour
{
    [Header("Gameplay References")]
    [SerializeField] private DungeonRunManager runManager;
    [SerializeField] private RunStatsManager runStatsManager;

    [Header("Shared Menu Visuals")]
    [SerializeField] private GameObject menuBackground;
    [SerializeField] private GameObject menuTitle;
    [SerializeField] private GameObject startMenuRoot;
    [SerializeField] private GameObject gameplayHudRoot;

    [Header("Menu Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject additionalOptionsPanel;
    [SerializeField] private GameObject runEndPanel;

    [Header("Mode Selection")]
    [SerializeField] private Toggle standardToggle;
    [SerializeField] private Toggle survivalToggle;
    [SerializeField] private UnityEngine.UI.Text selectedModeDescription;

    [Header("Additional Options")]
    [SerializeField] private Toggle randomSeedToggle;
    [SerializeField] private UnityEngine.UI.InputField seedInput;
    [SerializeField] private UnityEngine.UI.Dropdown difficultyDropdown;
    [SerializeField] private UnityEngine.UI.Text totalFloorsLabel;
    [SerializeField] private UnityEngine.UI.InputField totalFloorsInput;

    [Header("Validation")]
    [SerializeField] private UnityEngine.UI.Text validationText;

    private RunStatsManager.GameMode selectedMode =
        RunStatsManager.GameMode.Standard;

    private void Start()
    {
        ResolveReferences();

        if (runStatsManager != null &&
            runStatsManager.AutoBeginDebugRun)
        {
            UnityEngine.Debug.LogWarning(
                "RUN START MENU - Disable 'Auto Begin Debug Run' on RunStatsManager."
            );
        }

        InitialiseControlsFromRunConfiguration();
        ShowMainMenuPanel();
    }

    public void OnStandardToggleChanged(bool isOn)
    {
        if (!isOn)
            return;

        selectedMode =
            RunStatsManager.GameMode.Standard;

        ApplyModePresentation();
    }

    public void OnSurvivalToggleChanged(bool isOn)
    {
        if (!isOn)
            return;

        selectedMode =
            RunStatsManager.GameMode.Survival;

        ApplyModePresentation();
    }

    public void OnRandomSeedToggleChanged(bool isOn)
    {
        if (seedInput != null)
        {
            seedInput.interactable =
                !isOn;
        }

        ClearValidationMessage();
    }

    public void ShowAdditionalOptionsPanel()
    {
        SetMenuVisible(true);
        SetPanelStates(false, true, false);

        ApplyModePresentation();
        ApplyRandomSeedPresentation();
        ClearValidationMessage();
    }

    public void ShowMainMenuPanel()
    {
        Time.timeScale = 1f;

        SetMenuVisible(true);
        SetPanelStates(true, false, false);

        ApplyModePresentation();
        ClearValidationMessage();
    }

    public void ShowRunEndPanel()
    {
        SetMenuVisible(true);
        SetPanelStates(false, false, true);
    }

    public void HideMenusForGameplay()
    {
        Time.timeScale = 1f;
        SetMenuVisible(false);
    }

    public void StartSelectedRun()
    {
        ResolveReferences();

        if (runManager == null ||
            runStatsManager == null)
        {
            ShowValidationMessage(
                "Run managers are missing."
            );

            UnityEngine.Debug.LogError(
                "RUN START MENU - DungeonRunManager or RunStatsManager could not be found."
            );

            return;
        }

        int selectedSeed;

        bool useRandomSeed =
            randomSeedToggle != null &&
            randomSeedToggle.isOn;

        if (useRandomSeed)
        {
            selectedSeed =
                GenerateRandomSeed();

            if (seedInput != null)
            {
                seedInput.text =
                    selectedSeed.ToString();
            }
        }
        else
        {
            if (seedInput == null ||
                !int.TryParse(
                    seedInput.text,
                    out selectedSeed))
            {
                ShowValidationMessage(
                    "Enter a valid whole-number seed."
                );

                return;
            }
        }

        int selectedFloors = 0;

        if (selectedMode ==
            RunStatsManager.GameMode.Standard)
        {
            if (totalFloorsInput == null ||
                !int.TryParse(
                    totalFloorsInput.text,
                    out selectedFloors))
            {
                ShowValidationMessage(
                    "Enter a valid number of floors."
                );

                return;
            }

            selectedFloors =
                Mathf.Clamp(
                    selectedFloors,
                    1,
                    99
                );

            totalFloorsInput.text =
                selectedFloors.ToString();
        }

        RunStatsManager.RunDifficulty difficulty =
            GetSelectedDifficulty();

        ClearValidationMessage();

        UnityEngine.Debug.Log(
            "========== MENU RUN SELECTION ==========\n" +
            $"Mode: {selectedMode}\n" +
            $"Difficulty: {difficulty}\n" +
            $"Base seed: {selectedSeed}\n" +
            $"Random seed selected: {useRandomSeed}\n" +
            (selectedMode ==
                RunStatsManager.GameMode.Standard
                    ? $"Target floors: {selectedFloors}\n"
                    : "Target floors: Unlimited\n") +
            "========================================"
        );

        HideMenusForGameplay();

        runManager.StartNewRun(
            selectedMode,
            selectedSeed,
            difficulty,
            selectedFloors
        );
    }

    public void ExitGame()
    {
        UnityEngine.Debug.Log(
            "EXIT GAME selected."
        );

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Copies the actual configuration of the last run back into the UI
    /// without resetting the Random Seed toggle itself.
    /// </summary>
    public void RefreshControlsFromLastRun()
    {
        ResolveReferences();

        if (runStatsManager == null)
            return;

        selectedMode =
            runStatsManager.CurrentMode;

        if (standardToggle != null)
        {
            standardToggle.isOn =
                selectedMode ==
                RunStatsManager.GameMode.Standard;
        }

        if (survivalToggle != null)
        {
            survivalToggle.isOn =
                selectedMode ==
                RunStatsManager.GameMode.Survival;
        }

        if (seedInput != null)
        {
            seedInput.text =
                runStatsManager.BaseSeed.ToString();
        }

        if (difficultyDropdown != null)
        {
            difficultyDropdown.value =
                DifficultyToDropdownIndex(
                    runStatsManager.CurrentDifficulty
                );

            difficultyDropdown.RefreshShownValue();
        }

        if (totalFloorsInput != null &&
            runStatsManager.CurrentMode ==
                RunStatsManager.GameMode.Standard)
        {
            totalFloorsInput.text =
                Mathf.Max(
                    1,
                    runStatsManager.TargetFloors
                ).ToString();
        }

        ApplyModePresentation();
        ApplyRandomSeedPresentation();
    }

    private void InitialiseControlsFromRunConfiguration()
    {
        if (runStatsManager == null)
            return;

        selectedMode =
            runStatsManager.CurrentMode;

        if (standardToggle != null)
        {
            standardToggle.isOn =
                selectedMode ==
                RunStatsManager.GameMode.Standard;
        }

        if (survivalToggle != null)
        {
            survivalToggle.isOn =
                selectedMode ==
                RunStatsManager.GameMode.Survival;
        }

        if (seedInput != null)
        {
            seedInput.text =
                runStatsManager.BaseSeed.ToString();
        }

        if (difficultyDropdown != null)
        {
            difficultyDropdown.value =
                DifficultyToDropdownIndex(
                    runStatsManager.CurrentDifficulty
                );

            difficultyDropdown.RefreshShownValue();
        }

        if (totalFloorsInput != null)
        {
            int initialFloors =
                runStatsManager.TargetFloors > 0
                    ? runStatsManager.TargetFloors
                    : 5;

            totalFloorsInput.text =
                initialFloors.ToString();
        }

        ApplyModePresentation();
        ApplyRandomSeedPresentation();
    }

    private void ApplyModePresentation()
    {
        bool standardSelected =
            selectedMode ==
            RunStatsManager.GameMode.Standard;

        if (selectedModeDescription != null)
        {
            selectedModeDescription.text =
                standardSelected
                    ? "STANDARD RUN\nCOMPLETE THE SELECTED NUMBER OF FLOORS.\nYOUR COMPLETION TIME IS THE MAIN SCORE."
                    : "SURVIVAL\nDESCEND AS DEEP AS POSSIBLE.\nTHE RUN ENDS WHEN YOU DIE.";
        }

        if (totalFloorsLabel != null)
        {
            totalFloorsLabel.gameObject.SetActive(
                standardSelected
            );
        }

        if (totalFloorsInput != null)
        {
            totalFloorsInput.gameObject.SetActive(
                standardSelected
            );
        }
    }

    private void ApplyRandomSeedPresentation()
    {
        bool useRandomSeed =
            randomSeedToggle != null &&
            randomSeedToggle.isOn;

        if (seedInput != null)
        {
            seedInput.interactable =
                !useRandomSeed;
        }
    }

    private void SetMenuVisible(bool visible)
    {
        if (startMenuRoot != null)
            startMenuRoot.SetActive(visible);

        if (menuBackground != null)
            menuBackground.SetActive(visible);

        if (menuTitle != null)
            menuTitle.SetActive(visible);

        if (gameplayHudRoot != null)
            gameplayHudRoot.SetActive(!visible);
    }

    private void SetPanelStates(
        bool showMain,
        bool showOptions,
        bool showEnd)
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(showMain);

        if (additionalOptionsPanel != null)
            additionalOptionsPanel.SetActive(showOptions);

        if (runEndPanel != null)
            runEndPanel.SetActive(showEnd);
    }

    private int GenerateRandomSeed()
    {
        int generatedSeed =
            unchecked(
                (int)DateTime.UtcNow.Ticks
            ) &
            int.MaxValue;

        if (generatedSeed == 0)
            generatedSeed = 1;

        return generatedSeed;
    }

    private RunStatsManager.RunDifficulty GetSelectedDifficulty()
    {
        if (difficultyDropdown == null)
            return RunStatsManager.RunDifficulty.Normal;

        switch (difficultyDropdown.value)
        {
            case 0:
                return RunStatsManager.RunDifficulty.Easy;

            case 2:
                return RunStatsManager.RunDifficulty.Hard;

            case 1:
            default:
                return RunStatsManager.RunDifficulty.Normal;
        }
    }

    private int DifficultyToDropdownIndex(
        RunStatsManager.RunDifficulty difficulty)
    {
        switch (difficulty)
        {
            case RunStatsManager.RunDifficulty.Easy:
                return 0;

            case RunStatsManager.RunDifficulty.Hard:
                return 2;

            case RunStatsManager.RunDifficulty.Normal:
            default:
                return 1;
        }
    }

    private void ShowValidationMessage(string message)
    {
        if (validationText != null)
            validationText.text = message;

        UnityEngine.Debug.LogWarning(
            $"RUN START MENU - {message}"
        );
    }

    private void ClearValidationMessage()
    {
        if (validationText != null)
            validationText.text = string.Empty;
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
    }
}
