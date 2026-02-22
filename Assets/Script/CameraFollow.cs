using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;              // Drag Player here (required)

    [Header("Offset (in local rotated space)")]
    [SerializeField] private Vector3 pivotOffset = Vector3.zero;     // Usually (0,0,0) or slight raise
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 12f, -8f); // height + back distance

    [Header("Smoothing")]
    [SerializeField, Range(0f, 1f)] private float positionSmoothTime = 0.12f;
    [SerializeField, Range(0f, 1f)] private float rotationSmoothTime = 0.10f;

    [Header("Pitch Limits (when orbiting)")]
    [SerializeField] private bool limitPitch = true;
    [SerializeField, Range(-89f, 89f)] private float minPitch = -65f;
    [SerializeField, Range(-89f, 89f)] private float maxPitch = 75f;

    [Header("Mouse Orbit (drag to rotate view)")]
    [SerializeField] private bool enableOrbit = true;
    [SerializeField] private float orbitSensitivity = 2.5f;
    [SerializeField, Range(0f, 1f)] private float orbitDamping = 0.12f;

    // Runtime variables
    private Vector3 velocity             = Vector3.zero;
    private float   currentYaw           = 0f;
    private float   currentPitch         = 0f;
    private Vector2 orbitInputSmoothed   = Vector2.zero;

    void Reset()
    {
        // Sensible defaults when adding component
        cameraOffset = new Vector3(0f, 12f, -8f);
        positionSmoothTime = 0.12f;
        rotationSmoothTime = 0.10f;
        orbitSensitivity   = 2.5f;
    }

    void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (target == null)
        {
            Debug.LogWarning($"{nameof(TopDownCameraFollow)}: No target assigned and no Player tagged object found.");
            enabled = false;
            return;
        }

        // Initialize camera angles from initial offset
        var flatOffset = new Vector3(cameraOffset.x, 0f, cameraOffset.z);
        currentYaw   = Mathf.Atan2(flatOffset.x, flatOffset.z)   * Mathf.Rad2Deg;
        currentPitch = -Mathf.Asin(cameraOffset.y / cameraOffset.magnitude) * Mathf.Rad2Deg;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // ───────────────────────────────────────
        // 1. Optional mouse orbit input
        // ───────────────────────────────────────
        if (enableOrbit && Time.timeScale > 0)
        {
            Vector2 mouseDelta = new Vector2(
                Input.GetAxis("Mouse X"),
                -Input.GetAxis("Mouse Y")   // invert Y for natural feel
            ) * orbitSensitivity;

            orbitInputSmoothed = Vector2.Lerp(
                orbitInputSmoothed,
                mouseDelta,
                1f - orbitDamping
            );

            currentYaw   += orbitInputSmoothed.x * Time.deltaTime;
            currentPitch += orbitInputSmoothed.y * Time.deltaTime;

            if (limitPitch)
            {
                currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
            }

            // Optional: wrap yaw (360°)
            currentYaw = Mathf.Repeat(currentYaw, 360f);
        }

        // ───────────────────────────────────────
        // 2. Calculate desired camera position & rotation
        // ───────────────────────────────────────
        Quaternion orbitRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3 desiredPosition = target.position + pivotOffset + orbitRotation * cameraOffset;

        // Smooth position
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            positionSmoothTime
        );

        // ───────────────────────────────────────
        // 3. ALWAYS look exactly at target + pivotOffset
        // ───────────────────────────────────────
        Vector3 directionToTarget = (target.position + pivotOffset) - transform.position;

        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            Quaternion targetLookRotation = Quaternion.LookRotation(directionToTarget, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetLookRotation,
                1f - Mathf.Pow(1f - rotationSmoothTime, Time.deltaTime * 60f)   // framerate independent-ish
            );
        }
    }

    // Public methods (useful for reset / cutscenes / zoom)
    public void SetYaw(float yaw)           => currentYaw = yaw;
    public void SetPitch(float pitch)       => currentPitch = limitPitch ? Mathf.Clamp(pitch, minPitch, maxPitch) : pitch;
    public void SetOrbitAngles(float yaw, float pitch)
    {
        currentYaw   = yaw;
        currentPitch = limitPitch ? Mathf.Clamp(pitch, minPitch, maxPitch) : pitch;
    }

    public void ResetToDefaultAngles()
    {
        var flat = new Vector3(cameraOffset.x, 0f, cameraOffset.z);
        currentYaw   = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        currentPitch = -Mathf.Asin(cameraOffset.y / cameraOffset.magnitude) * Mathf.Rad2Deg;
    }
}