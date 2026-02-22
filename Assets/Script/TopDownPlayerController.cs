using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class TopDownPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.7f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float deceleration = 25f;

    [Header("Smoothing")]
    [SerializeField, Range(0f, 1f)] private float inputSmoothing = 0.15f;

    [Header("Attack Keys")]
    [SerializeField] private KeyCode tailAttackKey = KeyCode.Space;
    [SerializeField] private KeyCode shootKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode flyShootKey = KeyCode.Mouse1;

    [Header("Projectiles")]
    [SerializeField] private GameObject shootProjectilePrefab;
    [SerializeField] private Transform shootFirePoint;
    [SerializeField] private float shootProjectileSpeed = 25f;
    [SerializeField] private float shootFireRate = 10f;

    [SerializeField] private GameObject flyShootProjectilePrefab;
    [SerializeField] private Transform flyShootFirePoint;
    [SerializeField] private float flyShootProjectileSpeed = 30f;
    [SerializeField] private float flyShootFireRate = 8f;

    [Header("Shooting Delay")]
    [SerializeField, Tooltip("Delay in seconds before the first shot when holding the key")]
    private float initialShootDelay = 1f;

    [Header("Tail Attack Collider")]
    [SerializeField] private Collider tailAttackCollider;           // ← Assign in Inspector (e.g. Box/Sphere/Capsule)
    [SerializeField] private float tailAttackColliderDuration = 2f; // How long the collider stays active

    [Header("Aiming")]
    [SerializeField] private LayerMask groundAimLayers = -1;
    [SerializeField] private float aimRotationSpeed = 720f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("AI Control")]
    [SerializeField] private bool isAI = false;
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private float optimalShootDistance = 8f;
    [SerializeField] private float minShootDistance = 6f;
    [SerializeField] private float tailAttackRange = 2.5f;
    [SerializeField] private float stopDistance = 7f;
    [SerializeField] private float aiReactionTime = 0.3f;
    [SerializeField, Range(0f, 1f)] private float aiShootChance = 0.7f;

    // Animator hashes
    private static readonly int IsWalkingHash    = Animator.StringToHash("IsWalk");
    private static readonly int IsTailAttackHash = Animator.StringToHash("IsTailAttack");
    private static readonly int IsShootHash      = Animator.StringToHash("IsShoot");
    private static readonly int IsFlyShootHash   = Animator.StringToHash("IsFlyShoot");

    // Components
    private Rigidbody rb;
    private Camera mainCamera;

    // State
    private Vector3 moveDirection;
    private Vector2 smoothedInput;
    private bool isSprinting;
    private bool wasWalkingLastFrame;

    // Firing
    private float nextShootFireTime;
    private float nextFlyShootFireTime;
    private float shootHoldStartTime;
    private float flyShootHoldStartTime;
    private bool isShootKeyHeld;
    private bool isFlyShootKeyHeld;

    // Tail attack timing
    private float tailAttackDisableTime = -1f;

    // AI states
    private bool aiWantsTailAttack;
    private bool aiWantsShoot;
    private bool aiWantsFlyShoot;
    private float lastAIDecisionTime;
    private bool aiShouldMove;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mainCamera = Camera.main;

        if (animator == null)
        {
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (shootFirePoint == null) shootFirePoint = transform;
        if (flyShootFirePoint == null) flyShootFirePoint = transform;

        // Important: start with collider disabled
        if (tailAttackCollider != null)
        {
            tailAttackCollider.enabled = false;
        }
        else
        {
            Debug.LogWarning("Tail attack collider is not assigned!", this);
        }

        ResetAttackBools();
        ResetShootingTimers();
        lastAIDecisionTime = -aiReactionTime;
    }

    void Update()
    {
        if (targetPlayer == null && isAI)
        {
            Debug.LogWarning("AI player has no targetPlayer assigned!", this);
            return;
        }

        // ── AI Decision Making ─────────────────────────────────────────────────
        if (isAI)
        {
            UpdateAIDecisions();
        }

        // ── Movement (camera-relative) ────────────────────────────────────────
        Vector2 input;
        bool sprinting;

        if (!isAI)
        {
            input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            sprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
        else
        {
            if (aiShouldMove)
            {
                Vector3 dirToTarget = (targetPlayer.position - transform.position);
                dirToTarget.y = 0f;

                float distToTarget = dirToTarget.magnitude;
                if (distToTarget > stopDistance)
                {
                    dirToTarget.Normalize();
                }
                else
                {
                    dirToTarget = Vector3.zero;
                }

                Vector3 camForward = mainCamera.transform.forward;
                Vector3 camRight   = mainCamera.transform.right;
                camForward.y = camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                float inputX = Vector3.Dot(dirToTarget, camRight);
                float inputY = Vector3.Dot(dirToTarget, camForward);
                input = new Vector2(inputX, inputY).normalized;
            }
            else
            {
                input = Vector2.zero;
            }

            sprinting = aiShouldMove;
        }

        isSprinting = sprinting;
        smoothedInput = Vector2.Lerp(smoothedInput, input, 1f - inputSmoothing);

        Vector3 camForwardNorm = mainCamera.transform.forward;
        Vector3 camRightNorm   = mainCamera.transform.right;
        camForwardNorm.y = camRightNorm.y = 0f;
        camForwardNorm.Normalize();
        camRightNorm.Normalize();
        moveDirection = (camRightNorm * smoothedInput.x + camForwardNorm * smoothedInput.y).normalized;

        // ── Animation bools ────────────────────────────────────────────────────
        if (animator != null)
        {
            if (!isAI)
            {
                // Player: press → start animation & collider
                if (Input.GetKeyDown(tailAttackKey))
                {
                    animator.SetBool(IsTailAttackHash, true);
                    EnableTailCollider();
                }
                if (Input.GetKeyUp(tailAttackKey))
                {
                    animator.SetBool(IsTailAttackHash, false);
                    // Note: we don't disable here — we use timer
                }

                if (Input.GetKeyDown(shootKey))     animator.SetBool(IsShootHash, true);
                if (Input.GetKeyUp(shootKey))        animator.SetBool(IsShootHash, false);

                if (Input.GetKeyDown(flyShootKey))   animator.SetBool(IsFlyShootHash, true);
                if (Input.GetKeyUp(flyShootKey))     animator.SetBool(IsFlyShootHash, false);
            }
            // AI attack bools are set in UpdateAIDecisions
        }

        // ── Handle ranged shooting ─────────────────────────────────────────────
        if (!isAI)
        {
            HandleShootInput(shootKey, ref isShootKeyHeld, ref shootHoldStartTime,
                             shootProjectilePrefab, shootFirePoint, shootProjectileSpeed,
                             shootFireRate, ref nextShootFireTime);

            HandleShootInput(flyShootKey, ref isFlyShootKeyHeld, ref flyShootHoldStartTime,
                             flyShootProjectilePrefab, flyShootFirePoint, flyShootProjectileSpeed,
                             flyShootFireRate, ref nextFlyShootFireTime);
        }
        else
        {
            HandleAIShootInput(aiWantsShoot, ref isShootKeyHeld, ref shootHoldStartTime,
                               shootProjectilePrefab, shootFirePoint, shootProjectileSpeed,
                               shootFireRate, ref nextShootFireTime);

            HandleAIShootInput(aiWantsFlyShoot, ref isFlyShootKeyHeld, ref flyShootHoldStartTime,
                               flyShootProjectilePrefab, flyShootFirePoint, flyShootProjectileSpeed,
                               flyShootFireRate, ref nextFlyShootFireTime);
        }

        // ── Tail attack collider timer ─────────────────────────────────────────
        if (tailAttackCollider != null && tailAttackDisableTime > 0 && Time.time >= tailAttackDisableTime)
        {
            tailAttackCollider.enabled = false;
            tailAttackDisableTime = -1f;
        }

        // ── Facing direction ───────────────────────────────────────────────────
        Vector3 aimDirection;
        if (isAI)
        {
            Vector3 dirToTarget = (targetPlayer.position - transform.position);
            dirToTarget.y = 0f;
            aimDirection = dirToTarget.normalized;
        }
        else
        {
            Vector3 playerAimPos = GetAimPoint(transform.position.y);
            aimDirection = (playerAimPos - transform.position).normalized;
        }

        if (aimDirection.sqrMagnitude > 0.01f)
        {
            aimDirection.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(aimDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, aimRotationSpeed * Time.deltaTime);
        }

        // ── Walking animation ──────────────────────────────────────────────────
        bool isWalkingNow = smoothedInput.sqrMagnitude > 0.01f;
        if (animator != null && isWalkingNow != wasWalkingLastFrame)
        {
            animator.SetBool(IsWalkingHash, isWalkingNow);
        }
        wasWalkingLastFrame = isWalkingNow;
    }

    private void EnableTailCollider()
    {
        if (tailAttackCollider != null)
        {
            tailAttackCollider.enabled = true;
            tailAttackDisableTime = Time.time + tailAttackColliderDuration;
        }
    }

    private void UpdateAIDecisions()
    {
        float distToTarget = Vector3.Distance(transform.position, targetPlayer.position);
        float timeSinceLastDecision = Time.time - lastAIDecisionTime;

        if (timeSinceLastDecision >= aiReactionTime)
        {
            lastAIDecisionTime = Time.time;

            bool wantTailAttack = distToTarget <= tailAttackRange;
            bool inShootRange   = distToTarget <= optimalShootDistance && distToTarget >= minShootDistance;

            bool wantShoot    = false;
            bool wantFlyShoot = false;

            if (inShootRange)
            {
                if (Random.value < aiShootChance)
                    wantShoot = true;
                else
                    wantFlyShoot = true;
            }

            aiShouldMove = distToTarget > stopDistance;

            // For AI we enable collider when tail attack starts
            if (wantTailAttack && !aiWantsTailAttack)
            {
                EnableTailCollider();
            }

            UpdateAIAttackState(IsTailAttackHash, wantTailAttack, ref aiWantsTailAttack);
            UpdateAIAttackState(IsShootHash,     wantShoot,    ref aiWantsShoot);
            UpdateAIAttackState(IsFlyShootHash,  wantFlyShoot, ref aiWantsFlyShoot);

            if (animator != null)
            {
                animator.SetBool(IsTailAttackHash, aiWantsTailAttack);
                animator.SetBool(IsShootHash,      aiWantsShoot);
                animator.SetBool(IsFlyShootHash,   aiWantsFlyShoot);
            }
        }
    }

    private void UpdateAIAttackState(int animHash, bool wantAttack, ref bool currentState)
    {
        if (wantAttack && !currentState) currentState = true;
        else if (!wantAttack && currentState) currentState = false;
    }

    // ── The rest of the methods remain unchanged ──────────────────────────────

    private void HandleShootInput(KeyCode key, ref bool isHeld, ref float holdStartTime,
                                  GameObject prefab, Transform firePoint, float speed,
                                  float fireRate, ref float nextFireTime)
    {
        if (Input.GetKeyDown(key))
        {
            isHeld = true;
            holdStartTime = Time.time;
        }

        if (Input.GetKeyUp(key))
        {
            isHeld = false;
        }

        if (isHeld && prefab != null && firePoint != null)
        {
            float timeHeld = Time.time - holdStartTime;
            bool delayPassed = timeHeld >= initialShootDelay;

            if (delayPassed && Time.time >= nextFireTime)
            {
                FireProjectile(prefab, firePoint, speed);
                nextFireTime = Time.time + 1f / fireRate;
            }
        }
    }

    private void HandleAIShootInput(bool wantsToShoot, ref bool isHeld, ref float holdStartTime,
                                    GameObject prefab, Transform firePoint, float speed,
                                    float fireRate, ref float nextFireTime)
    {
        if (wantsToShoot && !isHeld)
        {
            isHeld = true;
            holdStartTime = Time.time;
        }
        else if (!wantsToShoot && isHeld)
        {
            isHeld = false;
        }

        if (isHeld && prefab != null && firePoint != null)
        {
            float timeHeld = Time.time - holdStartTime;
            bool delayPassed = timeHeld >= initialShootDelay;

            if (delayPassed && Time.time >= nextFireTime)
            {
                FireProjectile(prefab, firePoint, speed);
                nextFireTime = Time.time + 1f / fireRate;
            }
        }
    }

    void FixedUpdate()
    {
        float currentSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
        Vector3 targetVelocity = moveDirection * currentSpeed;
        Vector3 velocityDiff = targetVelocity - rb.linearVelocity;
        velocityDiff.y = 0f;
        float accel = moveDirection.sqrMagnitude > 0.01f ? acceleration : deceleration;
        Vector3 force = velocityDiff * accel * Time.fixedDeltaTime;
        if (force.magnitude > velocityDiff.magnitude) force = velocityDiff;
        rb.AddForce(force, ForceMode.VelocityChange);
    }

    private Vector3 GetAimPoint(float height)
    {
        if (isAI && targetPlayer != null)
        {
            return new Vector3(targetPlayer.position.x, height, targetPlayer.position.z);
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundAimLayers))
        {
            return new Vector3(hit.point.x, height, hit.point.z);
        }

        Plane aimPlane = new Plane(Vector3.up, height);
        if (aimPlane.Raycast(ray, out float distance))
        {
            Vector3 point = ray.GetPoint(distance);
            return new Vector3(point.x, height, point.z);
        }

        return transform.position;
    }

    private void FireProjectile(GameObject prefab, Transform firePoint, float speed)
    {
        Vector3 fireAimPos;
        if (isAI && targetPlayer != null)
        {
            fireAimPos = new Vector3(targetPlayer.position.x, firePoint.position.y, targetPlayer.position.z);
        }
        else
        {
            fireAimPos = GetAimPoint(firePoint.position.y);
        }

        Vector3 dir = (fireAimPos - firePoint.position).normalized;

        if (dir.sqrMagnitude > 0.01f)
        {
            dir.Normalize();
            GameObject proj = Instantiate(prefab, firePoint.position, Quaternion.LookRotation(dir, Vector3.up));
            Rigidbody projRb = proj.GetComponent<Rigidbody>();
            if (projRb != null)
            {
                projRb.linearVelocity = dir * speed;
            }
        }
    }

    private void ResetAttackBools()
    {
        if (animator != null)
        {
            animator.SetBool(IsTailAttackHash, false);
            animator.SetBool(IsShootHash, false);
            animator.SetBool(IsFlyShootHash, false);
        }

        aiWantsTailAttack = false;
        aiWantsShoot = false;
        aiWantsFlyShoot = false;
        aiShouldMove = true;
    }

    private void ResetShootingTimers()
    {
        nextShootFireTime = nextFlyShootFireTime = 0f;
        shootHoldStartTime = flyShootHoldStartTime = 0f;
        isShootKeyHeld = isFlyShootKeyHeld = false;
    }

    void OnDrawGizmosSelected()
    {
        if (isAI && targetPlayer != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(targetPlayer.position, tailAttackRange);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPlayer.position, optimalShootDistance);
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(targetPlayer.position, stopDistance);
        }

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, moveDirection * 2.5f);
        }

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 2f);
        }

        Vector3 aimPos = GetAimPoint(transform.position.y);
        Vector3 aimDir = (aimPos - transform.position).normalized;
        if (aimDir.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, aimDir * 3f);
            Gizmos.DrawWireSphere(aimPos, 0.3f);
        }
    }
}