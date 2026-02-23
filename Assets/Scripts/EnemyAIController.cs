using UnityEngine;
using UnityEngine.AI;

public class EnemyAIController : MonoBehaviour
{
    public Transform target;

    [Header("Distances")]
    public float chaseDistance = 12f;
    public float attackDistance = 8f;
    public float loseDistance = 18f;

    [Header("Roam")]
    public float roamRadius = 10f;
    public float roamWaitMin = 0.5f;
    public float roamWaitMax = 2.0f;

    [Header("Chase (more direct)")]
    public float chaseRepathInterval = 0.35f;
    public float chaseStrafeStep = 2.5f;

    [Header("Attack (more strafe/orbit)")]
    public float attackRepathInterval = 0.2f;
    public float orbitRadius = 7f;
    public float orbitJitter = 1.5f;
    public float attackStrafeStep = 5f;

    [Header("Line of Sight (LOS)")]
    public Transform eye;                   // Enemy/Eye
    public float targetAimHeight = 1.0f;    // 瞄玩家身体的高度
    public float losCheckInterval = 0.1f;   // 视线检测频率
    public bool hasLOS = false;             // 是否看得见玩家（无遮挡）
    public Vector3 lastSeenPos;             // 最后一次看到玩家的位置

    private NavMeshAgent agent;

    enum State { Roam, Chase, Attack }
    private State state = State.Roam;

    private Vector3 roamCenter;
    private float roamWaitTimer = 0f;

    private float nextRepathTime = 0f;
    private int strafeDir = 1;
    private float nextSwitchTime = 0f;

    private float nextLosCheckTime = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        roamCenter = transform.position;

        if (eye == null)
        {
            Transform t = transform.Find("Eye");
            eye = (t != null) ? t : transform;
        }

        lastSeenPos = transform.position;
    }

    void Update()
    {
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);

        // ===== LOS 检测（固定频率）=====
        if (Time.time >= nextLosCheckTime)
        {
            UpdateLOS();
            nextLosCheckTime = Time.time + losCheckInterval;
        }

        // ===== 状态切换逻辑 =====
        if (dist > loseDistance)
            SetState(State.Roam);
        else if (dist <= attackDistance)
            SetState(State.Attack);
        else if (dist <= chaseDistance)
            SetState(State.Chase);
        else
            SetState(State.Roam);

        // ===== 执行当前状态 =====
        switch (state)
        {
            case State.Roam:   TickRoam(); break;
            case State.Chase:  TickChase(); break;
            case State.Attack: TickAttack(); break;
        }
    }

    void UpdateLOS()
    {
        Vector3 origin = eye.position;
        Vector3 aimPoint = target.position + Vector3.up * targetAimHeight;
        Vector3 dir = aimPoint - origin;

        // Debug 线：绿色=看见，红色=被挡
        Color c = Color.red;

        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Player"))
            {
                hasLOS = true;
                lastSeenPos = target.position;
                c = Color.green;
            }
            else
            {
                hasLOS = false;
            }
        }
        else
        {
            // 没打到任何东西：一般不太会发生，但当作看不见
            hasLOS = false;
        }

        Debug.DrawLine(origin, aimPoint, c, losCheckInterval);
    }

    void SetState(State newState)
    {
        if (state == newState) return;
        state = newState;
        nextRepathTime = 0f;

        if (state == State.Roam)
        {
            roamWaitTimer = 0f;
        }
    }

    // ---------- Roam ----------
    void TickRoam()
    {
        if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            roamWaitTimer -= Time.deltaTime;
            if (roamWaitTimer <= 0f)
            {
                Vector3 p = RandomNavPoint(roamCenter, roamRadius);
                agent.SetDestination(p);
                roamWaitTimer = Random.Range(roamWaitMin, roamWaitMax);
            }
        }
    }

    // ---------- Chase ----------
    void TickChase()
    {
        if (Time.time < nextRepathTime) return;

        Vector3 toEnemy = (transform.position - target.position);
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude < 0.01f) toEnemy = Vector3.forward;

        Vector3 radial = toEnemy.normalized;
        Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;

        float desiredR = Mathf.Max(attackDistance - 1f, 2.5f);

        Vector3 rawPoint = target.position + radial * desiredR + tangent * (chaseStrafeStep * strafeDir);

        if (NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(target.position);

        if (Time.time >= nextSwitchTime)
        {
            strafeDir *= -1;
            nextSwitchTime = Time.time + Random.Range(0.6f, 1.4f);
        }

        nextRepathTime = Time.time + chaseRepathInterval;
    }

    // ---------- Attack (movement only for now) ----------
    void TickAttack()
    {
        if (Time.time >= nextSwitchTime)
        {
            strafeDir *= -1;
            nextSwitchTime = Time.time + Random.Range(0.6f, 1.4f);
        }

        if (Time.time < nextRepathTime) return;

        Vector3 toEnemy = (transform.position - target.position);
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude < 0.01f) toEnemy = Vector3.forward;

        Vector3 radial = toEnemy.normalized;
        Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized * strafeDir;

        float desiredR = orbitRadius + Random.Range(-orbitJitter, orbitJitter);

        Vector3 rawPoint = target.position + radial * desiredR + tangent * attackStrafeStep;

        if (NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(target.position + radial * desiredR);

        nextRepathTime = Time.time + attackRepathInterval;
    }

    Vector3 RandomNavPoint(Vector3 center, float radius)
    {
        Vector2 rnd = Random.insideUnitCircle * radius;
        Vector3 raw = center + new Vector3(rnd.x, 0f, rnd.y);

        if (NavMesh.SamplePosition(raw, out NavMeshHit hit, radius, NavMesh.AllAreas))
            return hit.position;

        return center;
    }
}