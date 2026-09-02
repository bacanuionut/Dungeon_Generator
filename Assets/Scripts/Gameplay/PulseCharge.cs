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

    private float wardenStunDuration;

    private Material chargeMaterial;


    public void Initialise(
        DungeonGenerator generator,
        float fuse,
        float radius,
        float normalStunDuration,
        float wardenDuration)
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
                normalStunDuration
            );


        wardenStunDuration =
            Mathf.Max(
                0.1f,
                wardenDuration
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

    /// <summary>
    /// Checks whether a dungeon cell is inside the Pulse radius and has a
    /// clear line of effect from the charge.
    ///
    /// Using the dungeon LOS rules means solid terrain blocks the Pulse
    /// for both normal enemies and the Warden.
    /// </summary>
    private bool CanPulseReachCell(
        Vector2Int targetCell)
    {
        if (dungeonGenerator == null ||
            dungeonGenerator.Grid == null)
        {
            return false;
        }


        Vector2Int pulseCell =
            new Vector2Int(
                Mathf.FloorToInt(
                    transform.position.x
                ),
                Mathf.FloorToInt(
                    transform.position.y
                )
            );


        Vector2 pulseCentre =
            new Vector2(
                pulseCell.x + 0.5f,
                pulseCell.y + 0.5f
            );


        Vector2 targetCentre =
            new Vector2(
                targetCell.x + 0.5f,
                targetCell.y + 0.5f
            );


        float distance =
            Vector2.Distance(
                pulseCentre,
                targetCentre
            );


        if (distance >
            blastRadius)
        {
            return false;
        }


        return DungeonVisibilityUtility.HasLineOfSight(
            dungeonGenerator.Grid,
            pulseCell,
            targetCell
        );
    }

    private void Detonate()
    {
        int normalEnemiesStunned =
            0;


        int wardensStunned =
            0;


        // ============================================================
        // NORMAL ENEMIES
        // ============================================================

        EnemyController[] enemies =
            FindObjectsOfType<EnemyController>();


        foreach (EnemyController enemy in
                 enemies)
        {
            if (enemy == null)
                continue;


            if (!CanPulseReachCell(
                    enemy.GridPosition))
            {
                continue;
            }


            enemy.ApplyStun(
                normalEnemyStunDuration
            );


            normalEnemiesStunned++;
        }


        // ============================================================
        // WARDEN
        // ============================================================

        WardenController[] wardens =
            FindObjectsOfType<WardenController>();


        foreach (WardenController warden in
                 wardens)
        {
            if (warden == null)
                continue;


            if (!CanPulseReachCell(
                    warden.GridPosition))
            {
                continue;
            }


            warden.ApplyStun(
                wardenStunDuration
            );


            wardensStunned++;
        }


        UnityEngine.Debug.Log(
            "========== PULSE DETONATED ==========\n" +
            $"Position: {transform.position}\n" +
            $"Radius: {blastRadius:0.0}\n" +
            $"Normal enemies stunned: {normalEnemiesStunned}\n" +
            $"Wardens stunned: {wardensStunned}\n" +
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