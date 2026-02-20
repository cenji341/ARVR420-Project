using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AnemyAI : MonoBehaviour
{
    public enum Difficulty { Easy, Normal, Hard }

    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform eyePoint;

    [Header("Roaming (Idle Movement)")]
    [SerializeField] private bool roamWhenNoTarget = true;
    [SerializeField] private float roamRadius = 12f;
    [SerializeField] private float roamPointTolerance = 1.2f;
    [SerializeField] private Vector2 roamWaitRange = new Vector2(0.5f, 2.5f);
    [SerializeField] private bool roamAroundSpawn = true;
    [SerializeField] private float navMeshSampleDistance = 3f;
    [SerializeField] private int roamPickAttempts = 20;

    [Header("Turn Then Move")]
    [Tooltip("If angle to the desired direction is bigger than this, the agent will turn in place first.")]
    [SerializeField] private float turnInPlaceAngle = 25f;
    [Tooltip("If true, the AI also rotates a bit while moving (keeps it aligned).")]
    [SerializeField] private bool rotateWhileMoving = true;

    [Header("Combat")]
    [SerializeField] private float detectionRange = 18f;
    [SerializeField] private float shootingRange = 11f;
    [SerializeField] private float fireRate = 1.2f; // shots per second
    [SerializeField] private float turnSpeed = 8f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Shooting Rules")]
    [Tooltip("Only fire if facing the player within this angle.")]
    [SerializeField] private float fireFacingAngle = 10f;

    [Header("Animation Parameters")]
    [SerializeField] private string walkBoolName = "IsWalking";
    [SerializeField] private string shootTriggerName = "Shoot";
    [SerializeField] private string speedFloatName = "Speed";

    [Header("Difficulty")]
    [SerializeField] private Difficulty difficulty = Difficulty.Normal;

    private NavMeshAgent agent;
    private float nextShotTime;

    private float currentDetectionRange;
    private float currentShootingRange;
    private float currentFireRate;

    private Vector3 roamCenter;
    private float nextRoamPickTime;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false; // IMPORTANT: we rotate manually so we can "turn then move"

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (eyePoint == null)
            eyePoint = transform;

        ApplyDifficultySettings();
    }

    private void Start()
    {
        roamCenter = transform.position;
        TryPickNewRoamDestination(force: true);
    }

    private void Update()
    {
        // No target? roam
        if (target == null)
        {
            Roam();
            UpdateAnimator();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        bool canSeeTarget = distanceToTarget <= currentDetectionRange && HasLineOfSight();

        // Lost sight? roam
        if (!canSeeTarget)
        {
            Roam();
            UpdateAnimator();
            return;
        }

        // See target: chase or attack
        if (distanceToTarget > currentShootingRange)
            ChaseTarget();
        else
            AttackTarget(distanceToTarget);

        UpdateAnimator();
    }

    public void SetDifficulty(Difficulty newDifficulty)
    {
        difficulty = newDifficulty;
        ApplyDifficultySettings();
    }

    private void ApplyDifficultySettings()
    {
        switch (difficulty)
        {
            case Difficulty.Easy:
                agent.speed = 1.0f;
                currentDetectionRange = detectionRange * 0.8f;
                currentShootingRange = shootingRange * 0.9f;
                currentFireRate = fireRate * 0.75f;
                break;

            case Difficulty.Hard:
                agent.speed = 1.5f;
                currentDetectionRange = detectionRange * 1.25f;
                currentShootingRange = shootingRange * 1.1f;
                currentFireRate = fireRate * 1.25f;
                break;

            default:
                agent.speed = 1.25f;
                currentDetectionRange = detectionRange;
                currentShootingRange = shootingRange;
                currentFireRate = fireRate;
                break;
        }
    }

    // -------------------------
    // Roaming
    // -------------------------
    private void Roam()
    {
        if (!roamWhenNoTarget)
        {
            agent.isStopped = false;
            agent.ResetPath();
            return;
        }

        bool hasArrived =
            !agent.pathPending &&
            agent.hasPath &&
            agent.remainingDistance <= Mathf.Max(roamPointTolerance, agent.stoppingDistance + 0.05f);

        bool badPath =
            !agent.pathPending &&
            (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete);

        // If we reached the point, stop and "wait" until it's time to pick another
        if (hasArrived && Time.time < nextRoamPickTime)
        {
            agent.isStopped = true;
            return;
        }

        // Pick new point when arrived (and wait expired) or path is bad
        if ((hasArrived || badPath) && Time.time >= nextRoamPickTime)
        {
            TryPickNewRoamDestination(force: false);
        }

        // While moving along a path, turn first then move
        if (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete)
        {
            Vector3 moveDir = agent.steeringTarget - transform.position;
            TurnThenMove(moveDir);
        }
        else
        {
            agent.isStopped = false; // allow path to compute if pending
        }
    }

    private void TryPickNewRoamDestination(bool force)
    {
        if (!force && Time.time < nextRoamPickTime)
            return;

        Vector3 center = roamAroundSpawn ? roamCenter : transform.position;

        for (int i = 0; i < roamPickAttempts; i++)
        {
            Vector3 randomOffset = Random.insideUnitSphere * roamRadius;
            randomOffset.y = 0f;

            Vector3 candidate = center + randomOffset;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);

                float wait = Random.Range(roamWaitRange.x, roamWaitRange.y);
                nextRoamPickTime = Time.time + Mathf.Max(0f, wait);
                return;
            }
        }

        nextRoamPickTime = Time.time + 0.5f;
    }

    // -------------------------
    // Chase / Attack
    // -------------------------
    private void ChaseTarget()
    {
        agent.SetDestination(target.position);

        // Use steering target (next corner) so the character faces the actual path direction
        Vector3 moveDir = agent.steeringTarget - transform.position;
        if (moveDir.sqrMagnitude < 0.01f)
            moveDir = target.position - transform.position;

        TurnThenMove(moveDir);
    }

    private void AttackTarget(float distanceToTarget)
    {
        agent.isStopped = true;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        // Always face player when close enough
        RotateTowards(toTarget);

        // Only shoot if:
        // - close enough (we are in AttackTarget so yes)
        // - line of sight still true
        // - facing within fireFacingAngle
        float angle = Vector3.Angle(transform.forward, toTarget.normalized);
        bool facingEnough = angle <= fireFacingAngle;

        if (facingEnough && Time.time >= nextShotTime)
        {
            nextShotTime = Time.time + (1f / Mathf.Max(currentFireRate, 0.01f));

            if (animator != null && !string.IsNullOrWhiteSpace(shootTriggerName))
                animator.SetTrigger(shootTriggerName);

            // Hook your projectile / damage logic here.
            Debug.DrawLine(eyePoint.position, target.position + Vector3.up * 1.2f, Color.red, 0.2f);
        }
    }

    // -------------------------
    // Turn-then-move helpers
    // -------------------------
    private void TurnThenMove(Vector3 desiredDirection)
    {
        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            agent.isStopped = true;
            return;
        }

        Vector3 desiredForward = desiredDirection.normalized;
        float angle = Vector3.Angle(transform.forward, desiredForward);

        if (angle > turnInPlaceAngle)
        {
            // Turn in place first
            agent.isStopped = true;
            RotateTowards(desiredForward);
        }
        else
        {
            // Now we can move
            agent.isStopped = false;

            if (rotateWhileMoving)
                RotateTowards(desiredForward);
        }
    }

    private void RotateTowards(Vector3 desiredForward)
    {
        if (desiredForward.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(desiredForward, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
    }

    // -------------------------
    // Vision / Animation / Gizmos
    // -------------------------
    private bool HasLineOfSight()
    {
        Vector3 origin = eyePoint.position;
        Vector3 destination = target.position + Vector3.up * 1.2f;
        Vector3 direction = destination - origin;

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == target || hit.transform.IsChildOf(target))
                return true;

            if (((1 << hit.collider.gameObject.layer) & obstacleMask) != 0)
                return false;

            return false;
        }

        return false;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        bool isMoving = agent.velocity.sqrMagnitude > 0.01f && !agent.isStopped;

        if (!string.IsNullOrWhiteSpace(walkBoolName))
            animator.SetBool(walkBoolName, isMoving);

        if (!string.IsNullOrWhiteSpace(speedFloatName))
            animator.SetFloat(speedFloatName, agent.velocity.magnitude);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, shootingRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Application.isPlaying ? roamCenter : transform.position, roamRadius);
    }
}
