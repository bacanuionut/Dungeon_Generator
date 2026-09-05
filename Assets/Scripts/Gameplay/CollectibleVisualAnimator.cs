using UnityEngine;

/// <summary>
/// Lightweight visual animation for gameplay collectibles.
///
/// This component deliberately moves only the supplied visual Transform.
/// The gameplay object's grid position remains unchanged, so collection,
/// objective and navigation logic stay authoritative.
///
/// Suitable for keys, coins, Pulse pickups, Shaper pickups and hearts.
/// </summary>
public class CollectibleVisualAnimator : MonoBehaviour
{
    [Header("Visual Target")]
    [SerializeField] private Transform visualTarget;

    [Header("Bobbing")]
    [SerializeField] private bool enableBobbing = true;
    [Min(0f)]
    [SerializeField] private float bobHeight = 0.12f;
    [Min(0.05f)]
    [SerializeField] private float bobCyclesPerSecond = 1.6f;

    [Header("Scale Pulse")]
    [SerializeField] private bool enableScalePulse = true;
    [Range(0f, 0.25f)]
    [SerializeField] private float scalePulseAmount = 0.05f;
    [Min(0.05f)]
    [SerializeField] private float scalePulseCyclesPerSecond = 1.2f;

    [Header("Optional Rotation")]
    [SerializeField] private float rotationDegreesPerSecond = 0f;

    private Vector3 restingLocalPosition;
    private Vector3 restingLocalScale;
    private float phaseOffset;
    private bool initialised;

    private void Awake()
    {
        InitialiseIfNeeded();
    }

    private void OnEnable()
    {
        InitialiseIfNeeded();
    }

    private void InitialiseIfNeeded()
    {
        if (initialised)
            return;

        if (visualTarget == null)
        {
            visualTarget = transform;
        }

        restingLocalPosition = visualTarget.localPosition;
        restingLocalScale = visualTarget.localScale;

        Vector3 position = transform.position;

        int hash = unchecked(
            Mathf.RoundToInt(position.x * 100f) * 73856093 ^
            Mathf.RoundToInt(position.y * 100f) * 19349663 ^
            gameObject.name.GetHashCode()
        );

        phaseOffset =
            Mathf.Abs(hash % 1000) /
            1000f *
            Mathf.PI *
            2f;

        initialised = true;
    }

    private void Update()
    {
        if (!initialised || visualTarget == null)
            return;

        float time = Time.time;

        Vector3 localPosition = restingLocalPosition;

        if (enableBobbing && bobHeight > 0f)
        {
            float bob =
                Mathf.Sin(
                    time *
                    bobCyclesPerSecond *
                    Mathf.PI *
                    2f +
                    phaseOffset
                ) *
                bobHeight;

            localPosition.y += bob;
        }

        visualTarget.localPosition = localPosition;

        float scaleMultiplier = 1f;

        if (enableScalePulse && scalePulseAmount > 0f)
        {
            float pulse =
                Mathf.Sin(
                    time *
                    scalePulseCyclesPerSecond *
                    Mathf.PI *
                    2f +
                    phaseOffset
                );

            scaleMultiplier += pulse * scalePulseAmount;
        }

        visualTarget.localScale =
            restingLocalScale *
            scaleMultiplier;

        if (Mathf.Abs(rotationDegreesPerSecond) > 0.001f)
        {
            visualTarget.Rotate(
                0f,
                0f,
                rotationDegreesPerSecond *
                Time.deltaTime
            );
        }
    }

    public void SetVisualTarget(Transform target)
    {
        visualTarget =
            target != null
                ? target
                : transform;

        restingLocalPosition = visualTarget.localPosition;
        restingLocalScale = visualTarget.localScale;
        initialised = true;
    }

    public void ConfigureAsKey()
    {
        enableBobbing = true;
        bobHeight = 0.16f;
        bobCyclesPerSecond = 1.45f;

        enableScalePulse = true;
        scalePulseAmount = 0.045f;
        scalePulseCyclesPerSecond = 1.10f;

        rotationDegreesPerSecond = 0f;
    }

    public void ConfigureAsPickup()
    {
        enableBobbing = true;
        bobHeight = 0.10f;
        bobCyclesPerSecond = 1.75f;

        enableScalePulse = true;
        scalePulseAmount = 0.035f;
        scalePulseCyclesPerSecond = 1.30f;

        rotationDegreesPerSecond = 0f;
    }

    public void ConfigureAsCoin()
    {
        enableBobbing = true;
        bobHeight = 0.08f;
        bobCyclesPerSecond = 2.0f;

        enableScalePulse = true;
        scalePulseAmount = 0.025f;
        scalePulseCyclesPerSecond = 1.50f;

        rotationDegreesPerSecond = 0f;
    }
}
