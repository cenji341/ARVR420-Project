using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AnemyAI : MonoBehaviour
{
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard
    }

    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform eyePoint;

    [Header("Roaming (Idle Movement)")]
    [SerializeField] private bool roamWhenNoTarget = true;
    [SerializeField] private float roamRadius = 12f;
    [SerializeField] private float roamPointTolerance = 1.2f;
    [SerializeField] private Vector2 roamWaitRange = new Vector2(0.5f, 2.5f);
    [Tooltip("If true, roams around where the enemy spawned. If false, roams around its current position.")]
    [SerializeField] private bool roamAroundSpawn = true;
    [Tooltip("How far from a random point we search to snap onto the NavMesh.")]
    [SerializeField] private float navMeshSampleDistance = 3f;
    [Tooltip("How many attempts to find a valid roam point each pick.")]
    [SerializeField] private int roamPickAttempts = 20;

    [Header("Combat")]
    [SerializeField] private float detectionRange = 18f;
    [SerializeField] private float shootingRange = 11f;
    [SerializeField] private float fireRate = 1.2f;
    [SerializeField] private float turnSpeed = 8f;
    [SerializeField] private LayerMask obstacleMask;

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
        if (target == null)
        {
            Roam();
            UpdateAnimator();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        bool canSeeTarget = distanceToTarget <= currentDetectionRange && HasLineOfSight();

        if (!canSeeTarget)
        {
            Roam();
            UpdateAnimator();
            return;
        }

        if (distanceToTarget > currentShootingRange)
            ChaseTarget();
        else
            AttackTarget();

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

    private void Roam()
    {
        if (!roamWhenNoTarget)
        {
            agent.isStopped = false;
            agent.ResetPath();
            return;
        }

        agent.isStopped = false;

        // If we arrived (or have no path), pick a new random destination after a short wait.
        bool arrived =
            !agent.pathPending &&
            agent.hasPath &&
            agent.remainingDistance <= Mathf.Max(roamPointTolerance, agent.stoppingDistance + 0.05f);

        bool noValidPath =
            !agent.pathPending &&
            (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete);

        if ((arrived || noValidPath) && Time.time >= nextRoamPickTime)
        {
            TryPickNewRoamDestination(force: false);
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

        // If we failed to find a point, try again soon.
        nextRoamPickTime = Time.time + 0.5f;
    }

    private void ChaseTarget()
    {
        agent.isStopped = false;
        agent.SetDestination(target.position);
    }

    private void AttackTarget()
    {
        agent.isStopped = true;

        Vector3 lookDirection = target.position - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        if (Time.time >= nextShotTime)
        {
            nextShotTime = Time.time + (1f / Mathf.Max(currentFireRate, 0.01f));

            if (animator != null && !string.IsNullOrWhiteSpace(shootTriggerName))
                animator.SetTrigger(shootTriggerName);

            // Hook your damage / projectile logic here.
            Debug.DrawLine(eyePoint.position, target.position, Color.red, 0.2f);
        }
    }

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
