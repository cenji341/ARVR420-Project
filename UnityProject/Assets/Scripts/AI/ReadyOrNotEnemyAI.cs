using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ReadyOrNotEnemyAI : MonoBehaviour
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

    [Header("Navigation")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolPointTolerance = 1.2f;

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
    private int patrolIndex;
    private float nextShotTime;

    private float currentDetectionRange;
    private float currentShootingRange;
    private float currentFireRate;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (eyePoint == null)
        {
            eyePoint = transform;
        }

        ApplyDifficultySettings();
    }

    private void Start()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            agent.SetDestination(patrolPoints[patrolIndex].position);
        }
    }

    private void Update()
    {
        if (target == null)
        {
            Patrol();
            UpdateAnimator();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        bool canSeeTarget = distanceToTarget <= currentDetectionRange && HasLineOfSight();

        if (!canSeeTarget)
        {
            Patrol();
            UpdateAnimator();
            return;
        }

        if (distanceToTarget > currentShootingRange)
        {
            ChaseTarget();
        }
        else
        {
            AttackTarget();
        }

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
                agent.speed = 2.8f;
                currentDetectionRange = detectionRange * 0.8f;
                currentShootingRange = shootingRange * 0.9f;
                currentFireRate = fireRate * 0.75f;
                break;

            case Difficulty.Hard:
                agent.speed = 4.2f;
                currentDetectionRange = detectionRange * 1.25f;
                currentShootingRange = shootingRange * 1.1f;
                currentFireRate = fireRate * 1.5f;
                break;

            default:
                agent.speed = 3.5f;
                currentDetectionRange = detectionRange;
                currentShootingRange = shootingRange;
                currentFireRate = fireRate;
                break;
        }
    }

    private void Patrol()
    {
        agent.isStopped = false;

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            agent.ResetPath();
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= patrolPointTolerance)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            agent.SetDestination(patrolPoints[patrolIndex].position);
        }
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
            {
                animator.SetTrigger(shootTriggerName);
            }

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
            {
                return true;
            }

            if (((1 << hit.collider.gameObject.layer) & obstacleMask) != 0)
            {
                return false;
            }

            return false;
        }

        return false;
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        bool isMoving = agent.velocity.sqrMagnitude > 0.01f && !agent.isStopped;

        if (!string.IsNullOrWhiteSpace(walkBoolName))
        {
            animator.SetBool(walkBoolName, isMoving);
        }

        if (!string.IsNullOrWhiteSpace(speedFloatName))
        {
            animator.SetFloat(speedFloatName, agent.velocity.magnitude);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, shootingRange);
    }
}
