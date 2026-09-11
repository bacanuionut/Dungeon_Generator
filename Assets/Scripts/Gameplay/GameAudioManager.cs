using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central audio controller for gameplay music and sound effects.
/// </summary>
public class GameAudioManager : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private RunStatsManager runStatsManager;

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private PlayerPulseController pulseController;

    [SerializeField]
    private PlayerShaperController shaperController;

    [SerializeField]
    private DungeonTerrainModifier terrainModifier;

    [SerializeField]
    private WardenManager wardenManager;

    [SerializeField]
    private ResonancePuzzleManager puzzleManager;


    [Header("Music")]

    [SerializeField]
    private AudioClip backgroundMusic;

    [SerializeField]
    private AudioClip wardenMusic;

    [Range(0f, 1f)]
    [SerializeField]
    private float musicVolume = 0.55f;

    [Min(0f)]
    [SerializeField]
    private float musicCrossfadeDuration = 0.75f;


    [Header("Player")]

    [SerializeField]
    private AudioClip playerHurtSound;

    [SerializeField]
    private AudioClip playerDeathSound;


    [Header("Pickups")]

    [SerializeField]
    private AudioClip coinPickupSound;

    [SerializeField]
    private AudioClip keyPickupSound;

    [SerializeField]
    private AudioClip utilityPickupSound;


    [Header("Abilities")]

    [SerializeField]
    private AudioClip pulseUseSound;

    [SerializeField]
    private AudioClip diggerUseSound;


    [Header("Exit")]

    [SerializeField]
    private AudioClip hatchOpenSound;


    [Header("Enemy Attacks")]

    [SerializeField]
    private AudioClip enemy1AttackSound;

    [SerializeField]
    private AudioClip enemy2AttackSound;

    [SerializeField]
    private AudioClip enemy3AttackSound;

    [SerializeField]
    private AudioClip wardenAttackSound;

    [SerializeField]
    private AudioClip wardenArrivalSound;


    [Header("Puzzle")]

    [SerializeField]
    private AudioClip leverSound;

    [SerializeField]
    private AudioClip puzzleCompleteSound;


    [Header("Sound Effect Volume")]

    [Range(0f, 1f)]
    [SerializeField]
    private float soundEffectVolume = 0.90f;


    [Header("Runtime Audio Sources")]

    [SerializeField]
    private AudioSource backgroundMusicSource;

    [SerializeField]
    private AudioSource wardenMusicSource;

    [SerializeField]
    private AudioSource soundEffectSource;

    [SerializeField]
    private AudioSource diggerLoopSource;


    [Header("Runtime Discovery")]

    [Min(0.05f)]
    [SerializeField]
    private float actorDiscoveryInterval = 0.20f;


    private readonly Dictionary<int, EnemyController>
        subscribedEnemies =
            new Dictionary<int, EnemyController>();

    private readonly Dictionary<int, Action>
        enemyAttackHandlers =
            new Dictionary<int, Action>();

    private readonly Dictionary<int, WardenController>
        subscribedWardens =
            new Dictionary<int, WardenController>();

    private readonly Dictionary<int, Action>
        wardenAttackHandlers =
            new Dictionary<int, Action>();


    private PlayerController subscribedPlayerController;

    private ExitHatchController hatchController;

    private float nextActorDiscoveryTime;

    private bool previousRunActive;

    private bool playerDeathPlayedThisRun;

    private bool counterBaselinesInitialised;

    private int previousCoinsCollected;

    private int previousPulseChargesUsed;

    private int previousShaperChargesUsed;

    private int previousPuzzlesCompleted;

    private bool pulseInventoryBaselineInitialised;

    private int previousPulseInventory;

    private bool shaperInventoryBaselineInitialised;

    private int previousShaperInventory;

    private float utilityPickupGraceUntil;

    private bool keyBaselineInitialised;

    private int previousKeysCollectedThisFloor;

    private int observedGenerationVersion = -1;

    private bool hatchOpenSoundPlayed;

    private bool previousHatchOpen;

    private bool previousWardenThreatActive;

    private bool wardenThreatActive;


    private void Awake()
    {
        EnsureAudioSources();
    }


    private void Start()
    {
        ResolveReferences();
        EnsurePlayerSubscription();

        previousRunActive =
            runStatsManager != null &&
            runStatsManager.RunActive;

        if (previousRunActive)
        {
            BeginRunAudioState();
        }
        else
        {
            StopMusicImmediately();
        }
    }


    private void Update()
    {
        ResolveReferences();
        EnsurePlayerSubscription();

        bool runActive =
            runStatsManager != null &&
            runStatsManager.RunActive;

        if (runActive &&
            !previousRunActive)
        {
            BeginRunAudioState();
        }
        else if (!runActive &&
                 previousRunActive)
        {
            EndRunAudioState();
        }

        previousRunActive =
            runActive;

        if (!runActive)
        {
            wardenThreatActive = false;
            StopDiggerLoop();
            UpdateMusic();
            return;
        }

        ObserveFloorGeneration();

        UpdateCounterSounds();
        UpdateDiggerLoop();
        UpdateUtilityPickupSound();
        UpdateKeyPickupSound();
        UpdateLeverSound();
        UpdateHatchSound();

        if (Time.unscaledTime >=
            nextActorDiscoveryTime)
        {
            nextActorDiscoveryTime =
                Time.unscaledTime +
                Mathf.Max(
                    0.05f,
                    actorDiscoveryInterval
                );

            DiscoverAttackSources();
        }

        UpdateWardenThreatState();
        UpdatePlayerLifeState();
        UpdateMusic();
    }


    private void OnDisable()
    {
        RemovePlayerSubscription();
        ClearAttackSubscriptions();
    }


    private void ResolveReferences()
    {
        if (dungeonGenerator == null)
        {
            dungeonGenerator =
                FindObjectOfType<DungeonGenerator>();
        }

        if (runStatsManager == null)
        {
            runStatsManager =
                FindObjectOfType<RunStatsManager>();
        }

        if (playerController == null)
        {
            playerController =
                FindObjectOfType<PlayerController>();
        }

        if (pulseController == null)
        {
            pulseController =
                FindObjectOfType<PlayerPulseController>();
        }

        if (shaperController == null)
        {
            shaperController =
                FindObjectOfType<PlayerShaperController>();
        }

        if (terrainModifier == null)
        {
            terrainModifier =
                FindObjectOfType<DungeonTerrainModifier>();
        }

        if (wardenManager == null)
        {
            wardenManager =
                FindObjectOfType<WardenManager>();
        }

        if (puzzleManager == null)
        {
            puzzleManager =
                FindObjectOfType<ResonancePuzzleManager>();
        }

        if (hatchController == null)
        {
            hatchController =
                FindObjectOfType<ExitHatchController>();
        }
    }


    private void EnsureAudioSources()
    {
        if (backgroundMusicSource == null)
        {
            backgroundMusicSource =
                gameObject.AddComponent<AudioSource>();
        }

        if (wardenMusicSource == null)
        {
            wardenMusicSource =
                gameObject.AddComponent<AudioSource>();
        }

        if (soundEffectSource == null)
        {
            soundEffectSource =
                gameObject.AddComponent<AudioSource>();
        }

        if (diggerLoopSource == null)
        {
            diggerLoopSource =
                gameObject.AddComponent<AudioSource>();
        }

        ConfigureMusicSource(
            backgroundMusicSource
        );

        ConfigureMusicSource(
            wardenMusicSource
        );

        soundEffectSource.playOnAwake = false;
        soundEffectSource.loop = false;
        soundEffectSource.spatialBlend = 0f;

        diggerLoopSource.playOnAwake = false;
        diggerLoopSource.loop = true;
        diggerLoopSource.spatialBlend = 0f;
        diggerLoopSource.volume =
            Mathf.Clamp01(soundEffectVolume);
    }


    private void ConfigureMusicSource(
        AudioSource source)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }


    private void BeginRunAudioState()
    {
        playerDeathPlayedThisRun = false;

        previousWardenThreatActive = false;
        wardenThreatActive = false;

        observedGenerationVersion = -1;

        hatchOpenSoundPlayed = false;
        previousHatchOpen = false;

        keyBaselineInitialised = false;

        BaselineCounters();

        pulseInventoryBaselineInitialised = false;
        shaperInventoryBaselineInitialised = false;

        utilityPickupGraceUntil =
            Time.unscaledTime + 1f;

        ClearAttackSubscriptions();

        nextActorDiscoveryTime = 0f;

        StopDiggerLoop();
        StopMusicImmediately();
    }


    private void EndRunAudioState()
    {
        previousWardenThreatActive = false;
        wardenThreatActive = false;

        ClearAttackSubscriptions();

        StopDiggerLoop();
        StopMusicImmediately();
    }


    private void BaselineCounters()
    {
        if (runStatsManager == null)
        {
            counterBaselinesInitialised = false;
            return;
        }

        previousCoinsCollected =
            runStatsManager.CoinsCollected;

        previousPulseChargesUsed =
            runStatsManager.PulseChargesUsed;

        previousShaperChargesUsed =
            runStatsManager.ShaperChargesUsed;

        previousPuzzlesCompleted =
            runStatsManager.PuzzlesCompleted;

        counterBaselinesInitialised = true;
    }


    private void ObserveFloorGeneration()
    {
        if (dungeonGenerator == null)
            return;

        int currentVersion =
            dungeonGenerator.GenerationVersion;

        if (currentVersion ==
            observedGenerationVersion)
        {
            return;
        }

        observedGenerationVersion =
            currentVersion;

        keyBaselineInitialised = false;

        hatchOpenSoundPlayed = false;

        hatchController =
            FindObjectOfType<ExitHatchController>();

        previousHatchOpen =
            hatchController != null &&
            hatchController.IsOpen;

        ClearAttackSubscriptions();

        nextActorDiscoveryTime = 0f;
    }


    private void UpdateCounterSounds()
    {
        if (runStatsManager == null)
        {
            counterBaselinesInitialised = false;
            return;
        }

        if (!counterBaselinesInitialised)
        {
            BaselineCounters();
            return;
        }

        int currentCoins =
            runStatsManager.CoinsCollected;

        if (currentCoins >
            previousCoinsCollected)
        {
            PlaySound(
                coinPickupSound
            );
        }

        previousCoinsCollected =
            currentCoins;


        int currentPulseUses =
            runStatsManager.PulseChargesUsed;

        if (currentPulseUses >
            previousPulseChargesUsed)
        {
            PlaySound(
                pulseUseSound
            );
        }

        previousPulseChargesUsed =
            currentPulseUses;


        int currentShaperUses =
            runStatsManager.ShaperChargesUsed;

        if (currentShaperUses >
            previousShaperChargesUsed)
        {
            StartDiggerLoop();
        }

        previousShaperChargesUsed =
            currentShaperUses;


        int currentPuzzlesCompleted =
            runStatsManager.PuzzlesCompleted;

        if (currentPuzzlesCompleted >
            previousPuzzlesCompleted)
        {
            PlaySound(
                puzzleCompleteSound
            );
        }

        previousPuzzlesCompleted =
            currentPuzzlesCompleted;
    }


    private void StartDiggerLoop()
    {
        if (diggerUseSound == null ||
            diggerLoopSource == null)
        {
            return;
        }

        diggerLoopSource.Stop();
        diggerLoopSource.clip =
            diggerUseSound;
        diggerLoopSource.loop =
            true;
        diggerLoopSource.volume =
            Mathf.Clamp01(
                soundEffectVolume
            );
        diggerLoopSource.Play();
    }


    private void UpdateDiggerLoop()
    {
        if (diggerLoopSource == null ||
            !diggerLoopSource.isPlaying)
        {
            return;
        }

        bool playerAlive =
            playerController == null ||
            playerController.IsAlive;

        bool playerDigging =
            terrainModifier != null &&
            terrainModifier.IsModifyingTerrain;

        if (!playerAlive ||
            !playerDigging)
        {
            StopDiggerLoop();
        }
    }


    private void StopDiggerLoop()
    {
        if (diggerLoopSource == null)
            return;

        diggerLoopSource.Stop();
        diggerLoopSource.clip = null;
    }


    private void UpdateUtilityPickupSound()
    {
        bool utilityCollected =
            false;


        if (pulseController != null)
        {
            int currentPulseInventory =
                pulseController.RemainingCharges;

            if (!pulseInventoryBaselineInitialised)
            {
                previousPulseInventory =
                    currentPulseInventory;

                pulseInventoryBaselineInitialised =
                    true;
            }
            else
            {
                if (currentPulseInventory >
                    previousPulseInventory)
                {
                    utilityCollected =
                        true;
                }

                previousPulseInventory =
                    currentPulseInventory;
            }
        }


        if (shaperController != null)
        {
            int currentShaperInventory =
                shaperController.RemainingCharges;

            if (!shaperInventoryBaselineInitialised)
            {
                previousShaperInventory =
                    currentShaperInventory;

                shaperInventoryBaselineInitialised =
                    true;
            }
            else
            {
                if (currentShaperInventory >
                    previousShaperInventory)
                {
                    utilityCollected =
                        true;
                }

                previousShaperInventory =
                    currentShaperInventory;
            }
        }


        if (Time.unscaledTime <
            utilityPickupGraceUntil)
        {
            return;
        }


        if (utilityCollected)
        {
            PlaySound(
                utilityPickupSound
            );
        }
    }


    private void UpdateKeyPickupSound()
    {
        if (dungeonGenerator == null ||
            dungeonGenerator.ObjectiveManager ==
                null)
        {
            keyBaselineInitialised =
                false;

            return;
        }

        int currentKeys =
            dungeonGenerator.ObjectiveManager
                .CollectedSigils;

        if (!keyBaselineInitialised)
        {
            previousKeysCollectedThisFloor =
                currentKeys;

            keyBaselineInitialised =
                true;

            return;
        }

        if (currentKeys >
            previousKeysCollectedThisFloor)
        {
            PlaySound(
                keyPickupSound
            );
        }

        previousKeysCollectedThisFloor =
            currentKeys;
    }


    private void UpdateLeverSound()
    {
        if (puzzleManager == null ||
            !puzzleManager.HasInteractionMessage ||
            !puzzleManager.CurrentInteractionUsesKeycap)
        {
            return;
        }

        string keyLabel =
            puzzleManager.CurrentInteractionKeyLabel;

        if (string.IsNullOrEmpty(
                keyLabel))
        {
            return;
        }

        KeyCode key;

        if (!Enum.TryParse(
                keyLabel,
                true,
                out key))
        {
            return;
        }

        if (Input.GetKeyDown(
                key))
        {
            PlaySound(
                leverSound
            );
        }
    }


    private void UpdateHatchSound()
    {
        if (hatchController == null)
        {
            hatchController =
                FindObjectOfType<ExitHatchController>();

            if (hatchController == null)
            {
                return;
            }
        }

        bool hatchOpeningCondition =
            hatchController
                .IsObjectiveComplete &&
            hatchController
                .IsPlayerInRange;

        bool hatchJustOpened =
            hatchController.IsOpen &&
            !previousHatchOpen;

        if (!hatchOpenSoundPlayed &&
            (hatchOpeningCondition ||
             hatchJustOpened))
        {
            hatchOpenSoundPlayed =
                true;

            PlaySound(
                hatchOpenSound
            );
        }

        previousHatchOpen =
            hatchController.IsOpen;
    }


    private void UpdateWardenThreatState()
    {
        bool playerAlive =
            playerController == null ||
            playerController.IsAlive;

        bool currentThreat =
            playerAlive &&
            wardenManager != null &&
            (
                wardenManager.IsWardenArriving ||
                wardenManager.IsPhysicalWardenPresent
            );

        if (currentThreat &&
            !previousWardenThreatActive)
        {
            PlaySound(
                wardenArrivalSound
            );
        }

        wardenThreatActive =
            currentThreat;

        previousWardenThreatActive =
            currentThreat;
    }


    private void UpdatePlayerLifeState()
    {
        if (playerController == null ||
            playerController.IsAlive ||
            playerDeathPlayedThisRun)
        {
            return;
        }

        PlayPlayerDeath();
    }


    private void EnsurePlayerSubscription()
    {
        if (playerController ==
            subscribedPlayerController)
        {
            return;
        }

        RemovePlayerSubscription();

        if (playerController == null)
            return;

        subscribedPlayerController =
            playerController;

        subscribedPlayerController.Damaged +=
            HandlePlayerDamaged;
    }


    private void RemovePlayerSubscription()
    {
        if (subscribedPlayerController != null)
        {
            subscribedPlayerController.Damaged -=
                HandlePlayerDamaged;
        }

        subscribedPlayerController =
            null;
    }


    private void HandlePlayerDamaged(
        int damage)
    {
        if (damage <= 0)
            return;

        if (playerController != null &&
            !playerController.IsAlive)
        {
            PlayPlayerDeath();
            return;
        }

        PlaySound(
            playerHurtSound
        );
    }


    private void PlayPlayerDeath()
    {
        if (playerDeathPlayedThisRun)
            return;

        playerDeathPlayedThisRun =
            true;

        StopDiggerLoop();
        StopMusicImmediately();

        PlaySound(
            playerDeathSound
        );
    }


    private void DiscoverAttackSources()
    {
        EnemyController[] enemies =
            FindObjectsOfType<EnemyController>();

        for (int i = 0;
             i < enemies.Length;
             i++)
        {
            EnemyController enemy =
                enemies[i];

            if (enemy == null)
                continue;

            int id =
                enemy.GetInstanceID();

            if (subscribedEnemies.ContainsKey(
                    id))
            {
                continue;
            }

            EnemyController capturedEnemy =
                enemy;

            Action handler =
                () =>
                    HandleEnemyAttack(
                        capturedEnemy
                    );

            enemy.AttackPerformed +=
                handler;

            subscribedEnemies[id] =
                enemy;

            enemyAttackHandlers[id] =
                handler;
        }


        WardenController[] wardens =
            FindObjectsOfType<WardenController>();

        for (int i = 0;
             i < wardens.Length;
             i++)
        {
            WardenController warden =
                wardens[i];

            if (warden == null)
                continue;

            int id =
                warden.GetInstanceID();

            if (subscribedWardens.ContainsKey(
                    id))
            {
                continue;
            }

            WardenController capturedWarden =
                warden;

            Action handler =
                () =>
                    HandleWardenAttack(
                        capturedWarden
                    );

            warden.AttackPerformed +=
                handler;

            subscribedWardens[id] =
                warden;

            wardenAttackHandlers[id] =
                handler;
        }
    }


    private void HandleEnemyAttack(
        EnemyController enemy)
    {
        if (enemy == null)
            return;

        ActorSpriteAnimator animator =
            enemy.GetComponent<ActorSpriteAnimator>();

        string setName =
            animator != null
                ? animator.ActiveSetName
                : string.Empty;

        AudioClip attackClip =
            enemy1AttackSound;

        if (!string.IsNullOrEmpty(
                setName))
        {
            if (setName.IndexOf(
                    "Enemy2",
                    StringComparison.OrdinalIgnoreCase) >=
                0)
            {
                attackClip =
                    enemy2AttackSound;
            }
            else if (setName.IndexOf(
                         "Enemy3",
                         StringComparison.OrdinalIgnoreCase) >=
                     0)
            {
                attackClip =
                    enemy3AttackSound;
            }
        }

        PlaySound(
            attackClip
        );
    }


    private void HandleWardenAttack(
        WardenController warden)
    {
        if (warden == null)
            return;

        PlaySound(
            wardenAttackSound
        );
    }


    private void ClearAttackSubscriptions()
    {
        foreach (KeyValuePair<int, EnemyController>
                 pair in subscribedEnemies)
        {
            EnemyController enemy =
                pair.Value;

            Action handler;

            if (enemy != null &&
                enemyAttackHandlers.TryGetValue(
                    pair.Key,
                    out handler))
            {
                enemy.AttackPerformed -=
                    handler;
            }
        }

        subscribedEnemies.Clear();
        enemyAttackHandlers.Clear();


        foreach (KeyValuePair<int, WardenController>
                 pair in subscribedWardens)
        {
            WardenController warden =
                pair.Value;

            Action handler;

            if (warden != null &&
                wardenAttackHandlers.TryGetValue(
                    pair.Key,
                    out handler))
            {
                warden.AttackPerformed -=
                    handler;
            }
        }

        subscribedWardens.Clear();
        wardenAttackHandlers.Clear();
    }


    private void UpdateMusic()
    {
        bool shouldPlayMusic =
            runStatsManager != null &&
            runStatsManager.RunActive &&
            (
                playerController == null ||
                playerController.IsAlive
            ) &&
            !playerDeathPlayedThisRun;

        float backgroundTarget =
            shouldPlayMusic &&
            !wardenThreatActive
                ? musicVolume
                : 0f;

        float wardenTarget =
            shouldPlayMusic &&
            wardenThreatActive
                ? musicVolume
                : 0f;

        UpdateMusicSource(
            backgroundMusicSource,
            backgroundMusic,
            backgroundTarget
        );

        UpdateMusicSource(
            wardenMusicSource,
            wardenMusic,
            wardenTarget
        );
    }


    private void UpdateMusicSource(
        AudioSource source,
        AudioClip clip,
        float targetVolume)
    {
        if (source == null)
            return;

        if (clip == null)
        {
            source.Stop();
            source.clip = null;
            source.volume = 0f;
            return;
        }

        if (source.clip !=
            clip)
        {
            source.Stop();
            source.clip =
                clip;
            source.volume =
                0f;
        }

        if (targetVolume >
                0f &&
            !source.isPlaying)
        {
            source.Play();
        }

        float duration =
            Mathf.Max(
                0f,
                musicCrossfadeDuration
            );

        if (duration <=
            0.001f)
        {
            source.volume =
                targetVolume;
        }
        else
        {
            float maximumChange =
                Mathf.Max(
                    0.01f,
                    musicVolume
                ) *
                Time.unscaledDeltaTime /
                duration;

            source.volume =
                Mathf.MoveTowards(
                    source.volume,
                    targetVolume,
                    maximumChange
                );
        }

        if (targetVolume <=
                0f &&
            source.volume <=
                0.001f &&
            source.isPlaying)
        {
            source.Stop();
        }
    }


    private void StopMusicImmediately()
    {
        if (backgroundMusicSource != null)
        {
            backgroundMusicSource.Stop();
            backgroundMusicSource.volume = 0f;
        }

        if (wardenMusicSource != null)
        {
            wardenMusicSource.Stop();
            wardenMusicSource.volume = 0f;
        }
    }


    private void PlaySound(
        AudioClip clip)
    {
        if (clip == null ||
            soundEffectSource == null)
        {
            return;
        }

        soundEffectSource.PlayOneShot(
            clip,
            Mathf.Clamp01(
                soundEffectVolume
            )
        );
    }
}
