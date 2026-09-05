using UnityEngine;

/// <summary>
/// Applies the correct ActorSpriteAnimator visual set to the existing player,
/// procedurally generated normal enemies and dynamically spawned Warden.
///
/// This avoids rewriting stable PlayerController, EnemyController,
/// DungeonContentGenerator or Warden AI code purely for visual presentation.
///
/// Normal enemy visual variants are selected deterministically from the floor
/// seed and the enemy's initial spawn cell.
/// </summary>
public class ActorSpriteVisualManager : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [SerializeField]
    private PlayerController playerController;


    [Header("Player - Character2")]

    [SerializeField]
    private ActorSpriteAnimator.ActorSpriteSet playerSpriteSet =
        new ActorSpriteAnimator.ActorSpriteSet();


    [Header("Normal Enemy - Enemy1")]

    [SerializeField]
    private ActorSpriteAnimator.ActorSpriteSet enemy1SpriteSet =
        new ActorSpriteAnimator.ActorSpriteSet();


    [Header("Normal Enemy - Enemy2")]

    [SerializeField]
    private ActorSpriteAnimator.ActorSpriteSet enemy2SpriteSet =
        new ActorSpriteAnimator.ActorSpriteSet();


    [Header("Normal Enemy - Enemy3")]

    [SerializeField]
    private ActorSpriteAnimator.ActorSpriteSet enemy3SpriteSet =
        new ActorSpriteAnimator.ActorSpriteSet();


    [Header("Warden - Enemy4")]

    [SerializeField]
    private ActorSpriteAnimator.ActorSpriteSet wardenSpriteSet =
        new ActorSpriteAnimator.ActorSpriteSet();


    [Header("Runtime Discovery")]

    [Tooltip(
        "Normal enemies and the Warden are created at runtime, so this manager " +
        "periodically looks for newly spawned actors."
    )]
    [Min(0.05f)]
    [SerializeField]
    private float discoveryInterval =
        0.20f;


    private float nextDiscoveryTime;


    private void Reset()
    {
        playerSpriteSet.setName =
            "Character2";

        enemy1SpriteSet.setName =
            "Enemy1";

        enemy2SpriteSet.setName =
            "Enemy2";

        enemy3SpriteSet.setName =
            "Enemy3";

        wardenSpriteSet.setName =
            "Enemy4 - Warden";
    }


    private void Awake()
    {
        /*
         * Also set names at runtime in case this component was added before
         * Reset populated its defaults in the Inspector.
         */
        EnsureSetName(
            playerSpriteSet,
            "Character2"
        );

        EnsureSetName(
            enemy1SpriteSet,
            "Enemy1"
        );

        EnsureSetName(
            enemy2SpriteSet,
            "Enemy2"
        );

        EnsureSetName(
            enemy3SpriteSet,
            "Enemy3"
        );

        EnsureSetName(
            wardenSpriteSet,
            "Enemy4 - Warden"
        );
    }


    private void Update()
    {
        if (Time.unscaledTime <
            nextDiscoveryTime)
        {
            return;
        }


        nextDiscoveryTime =
            Time.unscaledTime +
            discoveryInterval;


        ApplyPlayerVisual();

        ApplyNormalEnemyVisuals();

        ApplyWardenVisual();
    }


    private void ApplyPlayerVisual()
    {
        if (playerController == null)
        {
            playerController =
                FindObjectOfType<PlayerController>();
        }


        if (playerController == null ||
            playerSpriteSet == null ||
            playerSpriteSet.spriteSheet == null)
        {
            return;
        }


        EnsureAnimator(
            playerController.gameObject,
            playerSpriteSet
        );
    }


    private void ApplyNormalEnemyVisuals()
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


            ActorSpriteAnimator existing =
                enemy.GetComponent<ActorSpriteAnimator>();


            /*
             * Once an enemy has been assigned Enemy1/2/3, keep that appearance
             * for its lifetime even after it moves away from the spawn cell.
             */
            if (existing != null &&
                existing.IsConfigured)
            {
                continue;
            }


            ActorSpriteAnimator.ActorSpriteSet selected =
                ChooseNormalEnemySet(
                    enemy
                );


            if (selected == null ||
                selected.spriteSheet == null)
            {
                continue;
            }


            EnsureAnimator(
                enemy.gameObject,
                selected
            );
        }
    }


    private ActorSpriteAnimator.ActorSpriteSet ChooseNormalEnemySet(
        EnemyController enemy)
    {
        ActorSpriteAnimator.ActorSpriteSet[] available =
        {
            enemy1SpriteSet,
            enemy2SpriteSet,
            enemy3SpriteSet
        };


        int x =
            Mathf.RoundToInt(
                enemy.transform.position.x
            );


        int y =
            Mathf.RoundToInt(
                enemy.transform.position.y
            );


        int seed =
            dungeonGenerator != null
                ? dungeonGenerator.CurrentSeed
                : 0;


        int hash =
            unchecked(
                seed * 73856093 ^
                x * 19349663 ^
                y * 83492791
            );


        int index =
            (hash &
             0x7fffffff) %
            available.Length;


        /*
         * If one sheet was not assigned, rotate through the other two before
         * giving up.
         */
        for (int offset = 0;
             offset < available.Length;
             offset++)
        {
            ActorSpriteAnimator.ActorSpriteSet candidate =
                available[
                    (index + offset) %
                    available.Length
                ];


            if (candidate != null &&
                candidate.spriteSheet != null)
            {
                return candidate;
            }
        }


        return null;
    }


    private void ApplyWardenVisual()
    {
        if (wardenSpriteSet == null ||
            wardenSpriteSet.spriteSheet == null)
        {
            return;
        }


        WardenController[] wardens =
            FindObjectsOfType<WardenController>();


        for (int i = 0;
             i < wardens.Length;
             i++)
        {
            if (wardens[i] == null)
                continue;


            EnsureAnimator(
                wardens[i].gameObject,
                wardenSpriteSet
            );
        }
    }


    private void EnsureAnimator(
        GameObject actorObject,
        ActorSpriteAnimator.ActorSpriteSet spriteSet)
    {
        if (actorObject == null ||
            spriteSet == null ||
            spriteSet.spriteSheet == null)
        {
            return;
        }


        ActorSpriteAnimator animator =
            actorObject.GetComponent<ActorSpriteAnimator>();


        if (animator == null)
        {
            animator =
                actorObject.AddComponent<ActorSpriteAnimator>();
        }


        if (!animator.IsConfigured)
        {
            animator.Initialise(
                spriteSet
            );
        }
    }


    private void EnsureSetName(
        ActorSpriteAnimator.ActorSpriteSet spriteSet,
        string fallbackName)
    {
        if (spriteSet == null)
            return;


        if (string.IsNullOrEmpty(
                spriteSet.setName))
        {
            spriteSet.setName =
                fallbackName;
        }
    }
}
