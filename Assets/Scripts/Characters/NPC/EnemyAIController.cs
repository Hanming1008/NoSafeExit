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
    
    public GameObject muzzleFlashVfxPrefab;
    public float muzzleFlashLifeTime = 1.5f;
    public bool useProjectileVfx;
    public GameObject projectileVfxPrefab;
    

    [Header("Shell Ejection")]
    public GameObject shellPrefab;
    public Transform shellEjectPoint;
    public float shellEjectForce = 2.3f;
    public float shellEjectUpForce = 1.4f;
    public float shellTorque = 8.0f;
    public float shellLifeTime = 2.5f;
    public float shellScale = 1.1f;
    public float shellDirectionJitter = 0.2f;
    public float shellSpawnRightOffset = 0.02f;
    public float shellSpawnUpOffset = 0.025f;
    public float shellSpawnBackOffset = 0.34f;
public GameObject impactVfxPrefab;
public float aimTurnSpeed = 15f;        // 敌人转向速度（让枪口对准）

    [Header("Gunshot Alert (Hearing)")]
    public float gunshotHearDistance = 25f; // 听枪声距离（建议 > chaseDistance）
    public float alertDuration = 6f;        // 听到枪声后警戒多久（秒）
    

    [Header("NavMesh Grounding")]
    [Min(0f)] public float navMeshFootClearance = 0.05f;
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
        ConfigureAgentGroundOffset();
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

    
    private void ConfigureAgentGroundOffset()
    {
        if (agent == null) return;

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null || capsule.direction != 1) return;

        // Keep feet slightly above navmesh to reduce clipping on uneven triangles.
        agent.baseOffset = capsule.center.y + (capsule.height * 0.5f) + navMeshFootClearance;
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
            projectile.Initialize(gameObject, useProjectileVfx ? projectileVfxPrefab : null, impactVfxPrefab);

        
        SpawnShell(rot);
SpawnMuzzleFlash(rot);
        nextFireTime = Time.time + 1f / fireRate;
    }

    // ✅ 改动点：进入 Roam 时，把巡逻中心更新为“当前位置”，不回出生点
    
    private void SpawnMuzzleFlash(Quaternion rotation)
    {
        if (muzzleFlashVfxPrefab == null || muzzle == null) return;

        GameObject muzzleFx = Instantiate(muzzleFlashVfxPrefab, muzzle.position, rotation);
        if (muzzleFlashLifeTime > 0f)
            Destroy(muzzleFx, muzzleFlashLifeTime);
    }

    private void SpawnShell(Quaternion shotRotation)
    {
        if (shellPrefab == null || muzzle == null) return;

        Vector3 spawnPos;
        Quaternion spawnRot;
        Vector3 ejectDir;

        if (shellEjectPoint != null)
        {
            spawnPos = shellEjectPoint.position;
            spawnRot = shellEjectPoint.rotation;
            ejectDir = shellEjectPoint.right;
        }
        else
        {
            spawnPos = muzzle.position
                + muzzle.right * shellSpawnRightOffset
                + muzzle.up * shellSpawnUpOffset
                - muzzle.forward * shellSpawnBackOffset;
            spawnRot = shotRotation * Quaternion.Euler(0f, 90f, 0f);
            ejectDir = muzzle.right;
        }

        GameObject shell = Instantiate(shellPrefab, spawnPos, spawnRot);
        if (shellScale > 0f)
            shell.transform.localScale *= shellScale;

        Rigidbody shellRb = EnsureShellPhysics(shell);
        IgnoreShellOwnerCollisions(shell);

        Vector3 randomizedDir = (ejectDir + Random.insideUnitSphere * shellDirectionJitter).normalized;
        Vector3 impulse = randomizedDir * shellEjectForce + Vector3.up * shellEjectUpForce - muzzle.forward * (shellEjectForce * 0.2f);
        shellRb.linearVelocity = Vector3.zero;
        shellRb.angularVelocity = Vector3.zero;
        shellRb.AddForce(impulse, ForceMode.VelocityChange);
        shellRb.AddTorque(Random.onUnitSphere * shellTorque, ForceMode.VelocityChange);

        if (shellLifeTime > 0f)
            Destroy(shell, shellLifeTime);
    }

    private Rigidbody EnsureShellPhysics(GameObject shell)
    {
        Rigidbody shellRb = shell.GetComponent<Rigidbody>();
        if (shellRb == null)
            shellRb = shell.AddComponent<Rigidbody>();

        if (shell.GetComponent<Collider>() == null)
        {
            CapsuleCollider collider = shell.AddComponent<CapsuleCollider>();
            collider.radius = 0.035f;
            collider.height = 0.12f;
            collider.direction = 2;
        }

        shellRb.isKinematic = false;
        shellRb.useGravity = true;
        shellRb.mass = 0.08f;
        shellRb.linearDamping = 0.05f;
        shellRb.angularDamping = 0.05f;
        shellRb.interpolation = RigidbodyInterpolation.Interpolate;
        shellRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        shellRb.constraints = RigidbodyConstraints.None;
        return shellRb;
    }

    private void IgnoreShellOwnerCollisions(GameObject shell)
    {
        Collider[] shellColliders = shell.GetComponentsInChildren<Collider>(true);
        if (shellColliders == null || shellColliders.Length == 0) return;

        Collider[] ownerColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < shellColliders.Length; i++)
        {
            Collider shellCollider = shellColliders[i];
            if (shellCollider == null) continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider ownerCollider = ownerColliders[j];
                if (ownerCollider == null) continue;
                Physics.IgnoreCollision(shellCollider, ownerCollider, true);
            }
        }
    }
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
