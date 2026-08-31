using System.Collections;
using UnityEngine;

/// <summary>
/// One deployed Pulse Charge.
///
/// After a short fuse it creates an area pulse which temporarily
/// disables all enemies in range that are not protected by walls.
/// </summary>
public class PulseCharge : MonoBehaviour
{
    private DungeonGenerator dungeonGenerator;

    private float fuseDuration;

    private float blastRadius;

    private float normalEnemyStunDuration;


    private Material chargeMaterial;


    public void Initialise(
        DungeonGenerator generator,
        float fuse,
        float radius,
        float stunDuration)
    {
        dungeonGenerator =
            generator;

        fuseDuration =
            Mathf.Max(
                0.1f,
                fuse
            );

        blastRadius =
            Mathf.Max(
                0.5f,
                radius
            );

        normalEnemyStunDuration =
            Mathf.Max(
                0.1f,
                stunDuration
            );


        CreateVisual();


        StartCoroutine(
            DetonationSequence()
        );
    }


    private void CreateVisual()
    {
        transform.localScale =
            new Vector3(
                0.35f,
                0.35f,
                1f
            );


        Renderer renderer =
            GetComponent<Renderer>();


        if (renderer == null)
            return;


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
            return;


        chargeMaterial =
            new Material(
                shader
            );


        chargeMaterial.color =
            Color.white;


        renderer.sharedMaterial =
            chargeMaterial;
    }


    private IEnumerator DetonationSequence()
    {
        yield return new WaitForSeconds(
            fuseDuration
        );


        Detonate();


        Destroy(
            gameObject
        );
    }


    private void Detonate()
    {
        EnemyController[] enemies =
            FindObjectsOfType<EnemyController>();


        int enemiesStunned = 0;


        foreach (EnemyController enemy in
                 enemies)
        {
            if (enemy == null)
                continue;


            Vector2 enemyPosition =
                new Vector2(
                    enemy.transform.position.x,
                    enemy.transform.position.y
                );


            Vector2 pulsePosition =
                new Vector2(
                    transform.position.x,
                    transform.position.y
                );


            float distance =
                Vector2.Distance(
                    pulsePosition,
                    enemyPosition
                );


            if (distance >
                blastRadius)
            {
                continue;
            }


            /*
             * Walls block the Pulse.
             *
             * Convert world-space positions back to their dungeon
             * grid cells and reuse the same LOS rules already used
             * for perception.
             */
            Vector2Int pulseCell =
                new Vector2Int(
                    Mathf.FloorToInt(
                        transform.position.x
                    ),
                    Mathf.FloorToInt(
                        transform.position.y
                    )
                );


            if (!DungeonVisibilityUtility.HasLineOfSight(
                    dungeonGenerator.Grid,
                    pulseCell,
                    enemy.GridPosition))
            {
                continue;
            }


            enemy.ApplyStun(
                normalEnemyStunDuration
            );


            enemiesStunned++;
        }


        UnityEngine.Debug.Log(
            "========== PULSE DETONATED ==========\n" +
            $"Position: {transform.position}\n" +
            $"Radius: {blastRadius:0.0}\n" +
            $"Enemies stunned: {enemiesStunned}\n" +
            "====================================="
        );


        CreatePulseFlash();
    }


    private void CreatePulseFlash()
    {
        GameObject flash =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );


        flash.name =
            "Pulse Flash";


        flash.transform.position =
            new Vector3(
                transform.position.x,
                transform.position.y,
                -2.5f
            );


        flash.transform.localScale =
            new Vector3(
                blastRadius * 2f,
                blastRadius * 2f,
                1f
            );


        Collider collider =
            flash.GetComponent<Collider>();


        if (collider != null)
        {
            Destroy(
                collider
            );
        }


        Renderer renderer =
            flash.GetComponent<Renderer>();


        if (renderer != null)
        {
            Shader shader =
                Shader.Find(
                    "Unlit/Color"
                );


            if (shader != null)
            {
                Material flashMaterial =
                    new Material(
                        shader
                    );


                flashMaterial.color =
                    new Color(
                        0.75f,
                        0.9f,
                        1f,
                        0.28f
                    );


                renderer.material =
                    flashMaterial;
            }
        }


        Destroy(
            flash,
            0.12f
        );
    }


    private void OnDestroy()
    {
        if (chargeMaterial != null)
        {
            Destroy(
                chargeMaterial
            );
        }
    }
}