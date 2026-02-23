using UnityEngine;
using UnityEngine.AI;

public class EnemyChase : MonoBehaviour
{
    public Transform target;

    [Header("Distances")]
    public float chaseDistance = 12f;
    public float loseDistance = 18f;

    [Header("Strafe / Orbit")]
    public float orbitRadius = 6f;          // 想与玩家保持的距离（越大越横移）
    public float orbitJitter = 1.5f;        // 距离抖动（更不规律）
    public float strafeStep = 3.5f;         // 每次横移目标点离玩家的切线距离（越大越灵活）
    public float repathInterval = 0.25f;    // 重新选目标点频率（越小越灵活）
    public float stopDistance = 1.8f;

    private NavMeshAgent agent;
    private float nextRepathTime = 0f;
    private int strafeDir = 1;
    private float nextSwitchTime = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = stopDistance;
    }

    void Update()
    {
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);

        if (dist > chaseDistance || dist > loseDistance)
        {
            agent.ResetPath();
            return;
        }

        // 每隔一段时间换一次左右方向（来回横移）
        if (Time.time >= nextSwitchTime)
        {
            strafeDir *= -1;
            nextSwitchTime = Time.time + Random.Range(0.6f, 1.4f);
        }

        if (Time.time >= nextRepathTime)
        {
            Vector3 desired = GetOrbitPoint();
            agent.SetDestination(desired);
            nextRepathTime = Time.time + repathInterval;
        }
    }

    Vector3 GetOrbitPoint()
    {
        Vector3 toEnemy = (transform.position - target.position);
        toEnemy.y = 0f;

        if (toEnemy.sqrMagnitude < 0.01f)
            toEnemy = Vector3.forward;

        Vector3 radial = toEnemy.normalized;

        // 切线方向：左右绕圈
        Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized * strafeDir;

        // 理想距离 + 少量抖动
        float desiredR = orbitRadius + Random.Range(-orbitJitter, orbitJitter);

        // 玩家周围的“绕圈+横移”点
        Vector3 rawPoint = target.position + radial * desiredR + tangent * strafeStep;

        // 投射到 NavMesh 上
        if (NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            return hit.position;

        // 兜底：稍微靠近玩家
        return target.position + radial * desiredR;
    }
}