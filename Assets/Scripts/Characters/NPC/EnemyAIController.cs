using UnityEngine;
using UnityEngine.AI;

public class EnemyAIController : MonoBehaviour
{
    public Transform target;

    [Header("Distances")]
    public float chaseDistance = 12f;   // 警戒/发现
    public float attackDistance = 8f;   // 近距离走位更明显
    public float loseDistance = 18f;    // 丢失（回巡逻）

    [Header("Roam")]
    public float roamRadius = 10f;
    public float roamWaitMin = 0.5f;
    public float roamWaitMax = 2.0f;

    [Header("Chase Movement")]
    public float chaseRepathInterval = 0.35f;
    public float chaseStrafeStep = 2.5f;

    [Header("Attack Movement (no retreat)")]
    public float attackRepathInterval = 0.2f;
    public float orbitRadius = 7f;         // 交战理想半径（越大越绕圈）
    public float orbitJitter = 1.5f;
    public float attackStrafeStep = 5f;    // 横移幅度（越大越灵活）

    [Header("Line of Sight (LOS)")]
    public Transform eye;                   // Enemy/Eye
    public float targetAimHeight = 1.0f;    // 瞄玩家身体高度
    public float losCheckInterval = 0.1f;   // 视线检测频率
    public bool hasLOS = false;
    public Vector3 lastSeenPos;

    [Header("Shooting")]
    public Transform muzzle;                // Enemy/Muzzle
    public GameObject bulletPrefab;         // 直接拖你现有的 Bullet.prefab
    public float fireRate = 4f;             // 每秒几发
    public float aimTurnSpeed = 15f;        // 敌人转向速度（让枪口对准）

    [Header("Gunshot Alert (Hearing)")]
    public float gunshotHearDistance = 25f; // 听枪声距离（建议 > chaseDistance）
    public float alertDuration = 6f;        // 听到枪声后警戒多久（秒）
    private float alertedUntil = 0f;        // 警戒截止时间

    private float nextFireTime = 0f;
    private NavMeshAgent agent;

    enum State { Roam, Chase, Attack }
    private State state = State.Roam;

    private Vector3 roamCenter;
    private float roamWaitTimer = 0f;

    private float nextRepathTime = 0f;
    private int strafeDir = 1;
    private float nextSwitchTime = 0f;

    private float nextLosCheckTime = 0f;

    private bool CanUseAgent()
    {
        return agent != null && agent.enabled && agent.isActiveAndEnabled && agent.isOnNavMesh;
    }

    void OnEnable()
    {
        GunshotSystem.OnGunshot += OnGunshotHeard;
    }

    void OnDisable()
    {
        GunshotSystem.OnGunshot -= OnGunshotHeard;
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        roamCenter = transform.position;

        if (eye == null)
        {
            Transform t = transform.Find("Eye");
            eye = (t != null) ? t : transform;
        }

        if (muzzle == null)
        {
            Transform t = transform.Find("Muzzle");
            muzzle = (t != null) ? t : transform;
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

        // ===== 枪声警戒：警戒期间扩大“有效警戒距离” =====
        bool alerted = Time.time < alertedUntil;
        float effectiveChase = alerted ? gunshotHearDistance : chaseDistance;
        float effectiveLose = alerted ? Mathf.Max(loseDistance, gunshotHearDistance + 4f) : loseDistance;

        // ===== 掩体逻辑：没视线但在“有效警戒范围内” -> 当作知道你位置（找你）=====
        if (!hasLOS && dist <= effectiveChase)
            lastSeenPos = target.position;

        // ===== 状态切换 =====
        if (dist > effectiveLose)
            SetState(State.Roam);
        else if (dist <= attackDistance)
            SetState(State.Attack);
        else if (dist <= effectiveChase)
            SetState(State.Chase);
        else
            SetState(State.Roam);

        // ===== 执行状态 =====
        switch (state)
        {
            case State.Roam:   TickRoam(); break;
            case State.Chase:  TickChase(dist); break;
            case State.Attack: TickAttack(dist); break;
        }

        // ===== 两个战斗状态都要射击，但必须看得见（LOS）=====
        if (state == State.Chase || state == State.Attack)
        {
            AimAtTarget();
            if (hasLOS) TryShoot();
        }
    }

    void OnGunshotHeard(Vector3 pos, float radius)
    {
        float d = Vector3.Distance(transform.position, pos);

        // 只响应“传播半径”内，并且不超过自身听觉上限
        if (d > radius) return;
        if (d > gunshotHearDistance) return;

        alertedUntil = Time.time + alertDuration;

        // 听到枪声：更新最后已知位置为枪声位置（会靠近找）
        lastSeenPos = pos;

        // 进入追击（保持战斗态）
        SetState(State.Chase);
    }

    void UpdateLOS()
    {
        Vector3 origin = eye.position;
        Vector3 aimPoint = target.position + Vector3.up * targetAimHeight;
        Vector3 dir = aimPoint - origin;

        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Player"))
            {
                hasLOS = true;
                lastSeenPos = target.position;
            }
            else
            {
                hasLOS = false;
            }
        }
        else
        {
            hasLOS = false;
        }
    }

    void AimAtTarget()
    {
        Vector3 aimPoint = target.position + Vector3.up * targetAimHeight;
        Vector3 dir = aimPoint - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, aimTurnSpeed * Time.deltaTime);
    }

    void TryShoot()
    {
        if (bulletPrefab == null || muzzle == null) return;
        if (Time.time < nextFireTime) return;

        Vector3 aimPoint = target.position + Vector3.up * targetAimHeight;
        Vector3 aimDir = (aimPoint - muzzle.position).normalized;
        Quaternion rot = Quaternion.LookRotation(aimDir, Vector3.up);

        GameObject bullet = Instantiate(bulletPrefab, muzzle.position, rot);
        BulletProjectile projectile = bullet.GetComponent<BulletProjectile>();
        if (projectile != null)
            projectile.Initialize(gameObject);
        nextFireTime = Time.time + 1f / fireRate;
    }

    // ✅ 改动点：进入 Roam 时，把巡逻中心更新为“当前位置”，不回出生点
    void SetState(State newState)
    {
        if (state == newState) return;

        State oldState = state;
        state = newState;
        nextRepathTime = 0f;

        if (state == State.Roam)
        {
            roamWaitTimer = 0f;

            // 从战斗/追击切回巡逻：就在原地附近巡逻
            if (oldState != State.Roam)
            {
                roamCenter = transform.position;
                if (CanUseAgent())
                    agent.ResetPath();
            }
        }
    }

    // ---------- Roam ----------
    void TickRoam()
    {
        if (!CanUseAgent()) return;

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
    void TickChase(float distToPlayer)
    {
        if (!CanUseAgent()) return;

        // 掩体后：不横移，逼近 lastSeenPos
        if (!hasLOS)
        {
            agent.SetDestination(lastSeenPos);
            nextRepathTime = Time.time + chaseRepathInterval;
            return;
        }

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

    // ---------- Attack ----------
    void TickAttack(float distToPlayer)
    {
        if (!CanUseAgent()) return;

        // 掩体后：逼近 lastSeenPos
        if (!hasLOS)
        {
            agent.SetDestination(lastSeenPos);
            nextRepathTime = Time.time + attackRepathInterval;
            return;
        }

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

        // 不后退：desiredR 不大于当前距离
        float desiredR = orbitRadius + Random.Range(-orbitJitter, orbitJitter);
        desiredR = Mathf.Min(desiredR, distToPlayer);

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
